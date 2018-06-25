using RSGLib;
using RSGLib.ECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Random = UnityEngine.Random;

//-----------------------------------------------------------------------------
public class AgentSpawingSystem : ComponentSystem
{
	public static EntityArchetype s_AgentType;

	struct Data
	{
		[ReadOnly]
		public SharedComponentDataArray<GridSettings> Grid;
	}

	[Inject] Data m_Data;
	private Rect m_DistributionRect;
	private int m_DistHeight;
	private int m_DistWidth;
	private float m_RadiusSquared; // radius squared
	private NativeArray<float2> m_Grid;
	private NativeList<float2> m_activeSamples;

	//-----------------------------------------------------------------------------
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	static void Initialize()
	{
		var entityManager = World.Active.GetOrCreateManager<EntityManager>();
		s_AgentType = entityManager.CreateArchetype(typeof(GridSettings), typeof(Position), typeof(Rotation), typeof(TransformMatrix), typeof(MeshInstanceRenderer), typeof(TileDirection), typeof(AgentData));
	}

	//-----------------------------------------------------------------------------
	protected override void OnUpdate()
	{
		if (!Input.GetMouseButtonDown(StandardInput.LEFT_MOUSE_BUTTON)) return;
		if (!Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, Mathf.Infinity)) return;
		var initData = InitializationData.Instance;
		
		m_activeSamples = new NativeList<float2>(Allocator.Temp);
		m_DistHeight = (int)math.floor(initData.m_unitDistSize.x / initData.m_unitDistCellSize);
		m_DistWidth = (int)math.floor(initData.m_unitDistSize.y / initData.m_unitDistCellSize);
		m_Grid = new NativeArray<float2>(m_DistWidth * m_DistHeight, Allocator.Temp);
		
		for (int index = 0; index < initData.m_unitDistNumPerClick; index++)
		{
			InitSampler();
			float2? pos = Sample();
			if (pos != null)
				CreateAgent(new Vector3(pos.Value.x + hit.point.x, 0, pos.Value.y + hit.point.z));
		}
		
		m_Grid.Dispose();
		m_activeSamples.Dispose();
	}

	//-----------------------------------------------------------------------------
	private void CreateAgent(float3 _pos)
	{
		PostUpdateCommands.CreateEntity(s_AgentType);
		PostUpdateCommands.SetComponent(new Position() {Value = _pos});
		PostUpdateCommands.SetSharedComponent(m_Data.Grid[0]);
		PostUpdateCommands.SetComponent(new Position() { Value = _pos});
		PostUpdateCommands.SetComponent(new AgentData() { velocity = new float3(0, 0, 0) });
		PostUpdateCommands.SetComponent(new Rotation());
		PostUpdateCommands.SetComponent(new TransformMatrix());
		PostUpdateCommands.SetSharedComponent(new MeshInstanceRenderer()
		{
			mesh = InitializationData.Instance.AgentMesh,
			material = InitializationData.Instance.AgentMaterial
		});
	}

	//-----------------------------------------------------------------------------
	public void InitSampler()
	{
		var initData = InitializationData.Instance;
		m_DistributionRect = new Rect(0, 0, initData.m_unitDistSize.x, initData.m_unitDistSize.y);
		m_RadiusSquared = initData.m_cellSize * initData.m_cellSize;
	}

	//-----------------------------------------------------------------------------
	public float2? Sample()
	{
		// First sample is choosen randomly
		var initData = InitializationData.Instance;
		AddSample(new float2(Random.value * m_DistributionRect.width, Random.value * m_DistributionRect.height), initData.m_unitDistCellSize);
		while (m_activeSamples.Length > 0)
		{
			// Pick a random active sample
			int i = (int) Random.value * m_activeSamples.Length;
			float2 sample = m_activeSamples[i];

			// Try random candidates between [radius, 2 * radius] from that sample.
			for (int j = 0; j < initData.m_unitDistMaxTries; ++j)
			{

				float angle = 2 * Mathf.PI * Random.value;
				// See: http://stackoverflow.com/questions/9048095/create-random-number-within-an-annulus/9048443#9048443
				float randomNumber = Mathf.Sqrt(Random.value * 3 * m_RadiusSquared + m_RadiusSquared);
				float2 candidate = sample + randomNumber * new float2(math.cos(angle), math.sin(angle));

				// Accept candidates if it's inside the rect and farther than 2 * radius to any existing sample.
				if (m_DistributionRect.Contains(candidate) && IsFarEnough(candidate, initData.m_unitDistCellSize))
					return AddSample(candidate, initData.m_unitDistCellSize);
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
	private bool IsFarEnough(float2 sample, float cellSize)
	{
		var posX = (int)(sample.x / cellSize);
		var posY = (int)(sample.y / cellSize);
		int xmin = Mathf.Max(posX - 2, 0);
		int ymin = Mathf.Max(posY - 2, 0);
		int xmax = Mathf.Min(posX + 2, m_DistWidth - 1);
		int ymax = Mathf.Min(posY + 2, m_DistHeight - 1);
		for (int y = ymin; y <= ymax; y++) 
		{
			for (int x = xmin; x <= xmax; x++) 
			{
				float2 cell = m_Grid[y * m_DistWidth + x];
				if (cell.Equals(MathExt.Float2Zero()))
					continue;
				
				float2 d = cell - sample;
				if (d.x * d.x + d.y * d.y < m_RadiusSquared) 
					return false;
			}
		}
		return true;
	}
	//-----------------------------------------------------------------------------
	private Vector2 AddSample(float2 sample, float cellSize)
	{
		m_activeSamples.Add(sample);
		var x = (int)(sample.x / cellSize);
		var y = (int)(sample.y / cellSize);
		m_Grid[y * m_DistWidth + x] = sample;
		return sample;
	}
}