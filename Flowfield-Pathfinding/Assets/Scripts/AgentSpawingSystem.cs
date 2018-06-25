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
	private const int MAX_UNITS_PER_CLICK = 1000;
	private const float DIST_SIZEX = 100;
	private const float DIST_SIZEY = 100;
	private const int MAX_TRIES = 30; // Maximum number of attempts before marking a sample as inactive.
	private const float CELL_SIZE = 1f;

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
		s_AgentType = entityManager.CreateArchetype(typeof(Position), typeof(Rotation), typeof(TransformMatrix), typeof(MeshInstanceRenderer));
	}

	//-----------------------------------------------------------------------------
	protected override void OnUpdate()
	{
		if (!Input.GetMouseButtonDown(StandardInput.LEFT_MOUSE_BUTTON)) return;
		if (!Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, Mathf.Infinity)) return;
		
		m_activeSamples = new NativeList<float2>(Allocator.Temp);
		m_DistHeight = (int)math.floor(DIST_SIZEX / CELL_SIZE);
		m_DistWidth = (int)math.floor(DIST_SIZEY / CELL_SIZE);
		m_Grid = new NativeArray<float2>(m_DistWidth * m_DistHeight, Allocator.Temp);
		
		for (int index = 0; index < MAX_UNITS_PER_CLICK; index++)
		{
			InitSampler(new float2(hit.point.x, hit.point.z), DIST_SIZEX, DIST_SIZEY, CELL_SIZE);
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
		PostUpdateCommands.SetComponent(new Rotation());
		PostUpdateCommands.SetComponent(new TransformMatrix());
		PostUpdateCommands.SetSharedComponent(new MeshInstanceRenderer()
		{
			mesh = InitializationData.Instance.AgentMesh,
			material = InitializationData.Instance.AgentMaterial
		});
	}

	//-----------------------------------------------------------------------------
	public void InitSampler(float2 pos, float width, float height, float radius)
	{
		m_DistributionRect = new Rect(0, 0, width, height);
		m_RadiusSquared = radius * radius;
	}

	//-----------------------------------------------------------------------------
	public float2? Sample()
	{
		// First sample is choosen randomly
		AddSample(new float2(Random.value * m_DistributionRect.width, Random.value * m_DistributionRect.height));

		while (m_activeSamples.Length > 0)
		{
			// Pick a random active sample
			int i = (int) Random.value * m_activeSamples.Length;
			float2 sample = m_activeSamples[i];

			// Try `k` random candidates between [radius, 2 * radius] from that sample.
			for (int j = 0; j < MAX_TRIES; ++j)
			{

				float angle = 2 * Mathf.PI * Random.value;
				// See: http://stackoverflow.com/questions/9048095/create-random-number-within-an-annulus/9048443#9048443
				float randomNumber = Mathf.Sqrt(Random.value * 3 * m_RadiusSquared + m_RadiusSquared);
				float2 candidate = sample + randomNumber * new float2(math.cos(angle), math.sin(angle));

				// Accept candidates if it's inside the rect and farther than 2 * radius to any existing sample.
				if (m_DistributionRect.Contains(candidate) && IsFarEnough(candidate))
					return AddSample(candidate);
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
	private bool IsFarEnough(float2 sample)
	{
		GridPos pos = new GridPos(sample, CELL_SIZE);
		int xmin = Mathf.Max(pos.x - 2, 0);
		int ymin = Mathf.Max(pos.y - 2, 0);
		int xmax = Mathf.Min(pos.x + 2, m_DistWidth - 1);
		int ymax = Mathf.Min(pos.y + 2, m_DistHeight - 1);
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
	
	/// Adds the sample to the active samples queue and the grid before returning it
	private Vector2 AddSample(float2 sample)
	{
		m_activeSamples.Add(sample);
		GridPos pos = new GridPos(sample, CELL_SIZE);
		m_Grid[pos.y * m_DistWidth + pos.x] = sample;
		return sample;
	}

	/// Helper struct to calculate the x and y indices of a sample in the grid
	private struct GridPos
	{
		public int x;
		public int y;
		public GridPos(float2 sample, float cellSize)
		{
			x = (int)(sample.x / cellSize);
			y = (int)(sample.y / cellSize);
		}
	}	
	
}