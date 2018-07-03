using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

//-----------------------------------------------------------------------------
[UpdateInGroup(typeof(ProcessGroup))]
public class AgentSpawningSystem : ComponentSystem
{
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Pathfinding/Create Test Agents")]
    static void Spawn()
    {
        var agentSpawnSystem = World.Active.GetOrCreateManager<AgentSpawningSystem>();
        agentSpawnSystem.m_SpawnDebugAgentsOnNextFrame = true;
    }
#endif

    [Inject] private Agent.SpawnGroup m_AgentData;
    [Inject] private ECSInput.InputDataGroup m_InputData;
    
    private Rect m_DistributionRect;
    private int m_DistHeight;
    private int m_DistWidth;
    private float m_RadiusSquared;
    private NativeArray<float3> m_Grid;
    private NativeList<float3> m_ActiveSamples;
    
    private bool m_SpawnDebugAgentsOnNextFrame = false;
	private bool m_DemoSpawn = false;
	private float m_DemoSpawnRate = 50;
	private float m_DemoSpawnRateAccel = 20;
	private float m_DemoSpawnRateAccelAccel = 20;
	
    //-----------------------------------------------------------------------------
	protected override void OnUpdate()
    {
        if (m_DemoSpawn)
		{
			var ws = Main.ActiveInitParams.m_grid.worldSize;
			var spawnThisFrame = (int)(m_DemoSpawnRate * Time.deltaTime);
			Spawn(new float3(Random.Range(30, ws.x - 60) - ws.x * .5f, 0, ws.y - ws.y * .5f - 50), 50, spawnThisFrame, false);
			m_DemoSpawnRate += m_DemoSpawnRateAccel * Time.deltaTime;
			m_DemoSpawnRateAccel += m_DemoSpawnRateAccelAccel * Time.deltaTime;
		} 

		if (m_SpawnDebugAgentsOnNextFrame)
        {
            Spawn(0, 500, 10000, true);
            m_SpawnDebugAgentsOnNextFrame = false;
        }
        else
        {
            if (m_InputData.Buttons[0].Values["SpawnAgents"].Status != ECSInput.InputButtons.PRESSED)
                return;
            if (!Physics.Raycast(Camera.main.ScreenPointToRay(m_InputData.MousePos[0].Value), out RaycastHit hit, Mathf.Infinity))
                return;
            
            var spawnData = Main.ActiveSpawnParams;
            Spawn(hit.point, spawnData.AgentSpawnRadius, spawnData.AgentDistNumPerClick, true);
        }
    }

    //-----------------------------------------------------------------------------
    private void Spawn(float3 point, float radius, int count, bool check)
    {
        point.y = 0;
        var spawnData = Main.ActiveSpawnParams;
        m_DistHeight = (int)math.floor(radius / spawnData.AgentDistCellSize);
        m_DistWidth = (int)math.floor(radius / spawnData.AgentDistCellSize);
		var spawnPoint = point - new float3(radius, 0, radius) * .5f;
		if (!check)
		{
			for (int index = 0; index < count; index++)
			{
				var randomPoint = new float3(Random.insideUnitSphere);
				randomPoint.y = 0;
				var p = spawnPoint + randomPoint * radius;
				CreateAgent(spawnData, p);
			}
			return;
		}

		m_ActiveSamples = new NativeList<float3>(Allocator.Temp);
		m_Grid = new NativeArray<float3>(m_DistWidth * m_DistHeight, Allocator.Temp);
        InitSampler(radius);
        
        for (int index = 0; index < count; index++)
        {
            float3? pos = Sample(spawnData, spawnPoint);
            if (pos != null && math.length(pos.Value - point) < radius * .5)
                CreateAgent(spawnData, pos.Value);
        }

        m_Grid.Dispose();
        m_ActiveSamples.Dispose();
    }

    //-----------------------------------------------------------------------------
    private void CreateAgent(AgentSpawnData spawnData, float3 _pos)
    {
        Archetypes.CreateAgent(PostUpdateCommands, _pos, spawnData.AgentMesh, spawnData.AgentMaterial, m_AgentData.GridSettings[0]);
    }

    //-----------------------------------------------------------------------------
    private void InitSampler(float radius)
    {
        var initData = Main.Instance.m_InitData;
        m_DistributionRect = new Rect(0, 0, radius, radius);
        m_RadiusSquared = initData.m_cellSize * initData.m_cellSize;
    }

    //-----------------------------------------------------------------------------
    public float3? Sample(AgentSpawnData spawnData, float3 _hit)
    {
        // First sample is choosen randomly
        AddSample(new float3(Random.value * m_DistributionRect.width, 0, Random.value * m_DistributionRect.height), spawnData.AgentDistCellSize);
        while (m_ActiveSamples.Length > 0)
        {
            // Pick a random active sample
            int i = (int)Random.value * m_ActiveSamples.Length;
            if (i >= m_ActiveSamples.Length)
                continue;
                    
            float3 sample = m_ActiveSamples[i];

            // Try random candidates between [radius, 2 * radius] from that sample.
            for (int index = 0; index < spawnData.AgentDistMaxTries; ++index)
            {
                float angle = 2 * Mathf.PI * Random.value;
                // See: http://stackoverflow.com/questions/9048095/create-random-number-within-an-annulus/9048443#9048443
                float randomNumber = Mathf.Sqrt(Random.value * 3 * m_RadiusSquared + m_RadiusSquared);
                var candidate = sample + randomNumber * new float3(math.cos(angle), 0, math.sin(angle));

                // Accept candidates if it's inside the rect and farther than 2 * radius to any existing sample.
                if (m_DistributionRect.Contains(new float2(candidate.x, candidate.z)) && IsFarEnough(candidate, spawnData.AgentDistCellSize))
                {
                    var agentPos = new float3(candidate.x + _hit.x, 0, candidate.z + _hit.z);
                    var gridIndex = GridUtilties.WorldToIndex(m_AgentData.GridSettings[0], agentPos);
                    if (gridIndex < 0)// || m_agentData.TileCost[gridIndex].Value > spawnData.AgentDistSpawningThreshold)
                        continue;

                    candidate.y = Main.TerrainHeight[gridIndex];
                    AddSample(candidate, spawnData.AgentDistCellSize);
                    return agentPos;
                }
            }

            // If we couldn't find a valid candidate after k attempts, remove this sample from the active samples queue
            m_ActiveSamples[i] = m_ActiveSamples[m_ActiveSamples.Length - 1];
            m_ActiveSamples.RemoveAtSwapBack(m_ActiveSamples.Length - 1);
        }
        return null;
    }

    //-----------------------------------------------------------------------------
    // Note: we use the zero vector to denote an unfilled cell in the grid. This means that if we were
    // to randomly pick (0, 0) as a sample, it would be ignored for the purposes of proximity-testing
    // and we might end up with another sample too close from (0, 0). This is a very minor issue.
    private bool IsFarEnough(float3 sample, float cellSize)
    {
        var posX = (int)(sample.x / cellSize);
        var posZ = (int)(sample.z / cellSize);
        int xmin = Mathf.Max(posX - 2, 0);
        int ymin = Mathf.Max(posZ - 2, 0);
        int xmax = Mathf.Min(posX + 2, m_DistWidth - 1);
        int ymax = Mathf.Min(posZ + 2, m_DistHeight - 1);
        for (int y = ymin; y <= ymax; y++)
        {
            for (int x = xmin; x <= xmax; x++)
            {
                float3 cell = m_Grid[y * m_DistWidth + x];
                if (cell.Equals(new float3(0,0,0)))
                    continue;

                float3 d = cell - sample;
                if (d.x * d.x + d.z * d.z < m_RadiusSquared)
                    return false;
            }
        }
        return true;
    }
    //-----------------------------------------------------------------------------
    private void AddSample(float3 sample, float cellSize)
    {
        m_ActiveSamples.Add(sample);
        var x = (int)(sample.x / cellSize);
        var z = (int)(sample.z / cellSize);
        var index = z * m_DistWidth + x;
        if (index >= 0 && index < m_Grid.Length)
            m_Grid[index] = sample;
    }
}
