using RSGLib;
using RSGLib.ECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

//-----------------------------------------------------------------------------
public class AgentSpawingSystem : ComponentSystem
{
	struct Data
	{
		[ReadOnly]
		public SharedComponentDataArray<GridSettings> GridSettings;
		public ComponentDataArray<TileCost> TileCost;
	}

	[Inject] Data m_Data;
	private Rect m_DistributionRect;
	private int m_DistHeight;
	private int m_DistWidth;
	private float m_RadiusSquared; // radius squared
	private NativeArray<float3> m_Grid;
	private NativeList<float3> m_activeSamples;
	static FlowField.Data m_flowField;

	//-----------------------------------------------------------------------------
	protected override void OnUpdate()
	{
		if (Input.GetMouseButtonDown(StandardInput.RIGHT_MOUSE_BUTTON))
		{
			if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit2))
			{
				var grid = InitializationData.Instance.m_grid;
				var uv = new float2(hit2.textureCoord.x,hit2.textureCoord.y);
				var pos = uv * grid.worldSize - grid.worldSize * .5f;
				var coord = GridUtilties.World2Grid(grid, new float3(pos.x, 0, pos.y));
				Debug.LogFormat("UV {0}, POS {1}, COORD: {2}", uv, pos, coord);
			}

		}

		if (!Input.GetMouseButtonDown(StandardInput.LEFT_MOUSE_BUTTON)) return;
		if (!Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, Mathf.Infinity)) return;
		var initData = InitializationData.Instance;

		m_activeSamples = new NativeList<float3>(Allocator.Temp);
		m_DistHeight = (int)math.floor(initData.m_unitDistSize.x / initData.m_unitDistCellSize);
		m_DistWidth = (int)math.floor(initData.m_unitDistSize.y / initData.m_unitDistCellSize);
		m_Grid = new NativeArray<float3>(m_DistWidth * m_DistHeight, Allocator.Temp);

		for (int index = 0; index < initData.m_unitDistNumPerClick; index++)
		{
			InitSampler();
			float3? pos = Sample(hit.point);
			if (pos != null)
				CreateAgent(pos.Value);
		}

		m_Grid.Dispose();
		m_activeSamples.Dispose();
	}


	//-----------------------------------------------------------------------------
	private void CreateAgent(float3 _pos)
	{
		if (!m_flowField.Value.IsCreated)
            m_flowField = new FlowField.Data() { Value = GridUtilties.m_initialFlow };

        Manager.Archetype.CreateAgent(PostUpdateCommands, _pos, InitializationData.Instance.AgentMesh, InitializationData.Instance.AgentMaterial, m_Data.GridSettings[0], m_flowField);
	}

	//-----------------------------------------------------------------------------
	public void InitSampler()
	{
		var initData = InitializationData.Instance;
		m_DistributionRect = new Rect(0, 0, initData.m_unitDistSize.x, initData.m_unitDistSize.y);
		m_RadiusSquared = initData.m_cellSize * initData.m_cellSize;
	}

	//-----------------------------------------------------------------------------
	public float3? Sample(float3 _hit)
	{
		// First sample is choosen randomly
		var initData = InitializationData.Instance;
		AddSample(new float3(Random.value * m_DistributionRect.width, 0, Random.value * m_DistributionRect.height), initData.m_unitDistCellSize);
		while (m_activeSamples.Length > 0)
		{
			// Pick a random active sample
			int i = (int) Random.value * m_activeSamples.Length;
			float3 sample = m_activeSamples[i];

			// Try random candidates between [radius, 2 * radius] from that sample.
			for (int j = 0; j < initData.m_unitDistMaxTries; ++j)
			{
				float angle = 2 * Mathf.PI * Random.value;
				// See: http://stackoverflow.com/questions/9048095/create-random-number-within-an-annulus/9048443#9048443
				float randomNumber = Mathf.Sqrt(Random.value * 3 * m_RadiusSquared + m_RadiusSquared);
				var candidate = sample + randomNumber * new float3(math.cos(angle), 0, math.sin(angle));

				// Accept candidates if it's inside the rect and farther than 2 * radius to any existing sample.
				if (m_DistributionRect.Contains(new float2(candidate.x, candidate.z)) && IsFarEnough(candidate, initData.m_unitDistCellSize))
				{
					var agentPos = new float3(candidate.x + _hit.x, 0, candidate.z + _hit.z);
					var gridIndex = GridUtilties.WorldToIndex(m_Data.GridSettings[0], agentPos);
					if (m_Data.TileCost[gridIndex].value < 255)
						continue;
					
					AddSample(candidate, initData.m_unitDistCellSize);
					return agentPos;
				}
			}

			// If we couldn't find a valid candidate after k attempts, remove this sample from the active samples queue
			m_activeSamples[i] = m_activeSamples[m_activeSamples.Length - 1];
			m_activeSamples.RemoveAtSwapBack(m_activeSamples.Length - 1);
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
				if (cell.Equals(MathExt.Float3Zero()))
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
		m_activeSamples.Add(sample);
		var x = (int)(sample.x / cellSize);
		var z = (int)(sample.z / cellSize);
		var index = z * m_DistWidth + x;
		m_Grid[index] = sample;
	}
}
