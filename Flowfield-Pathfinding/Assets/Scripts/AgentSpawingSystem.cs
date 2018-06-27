﻿using RSGLib;
using RSGLib.ECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

//-----------------------------------------------------------------------------
[UpdateInGroup(typeof(ProcessGroup))]
public class AgentSpawingSystem : ComponentSystem
{
	struct AgentData
	{
		[ReadOnly] public SharedComponentDataArray<GridSettings> GridSettings;
        [ReadOnly] public ComponentDataArray<Tile.Cost> TileCost;
	}

	[Inject] private AgentData m_agentData;
	private Rect m_DistributionRect;
	private int m_DistHeight;
	private int m_DistWidth;
	private float m_RadiusSquared; 
	private NativeArray<float3> m_Grid;
	private NativeList<float3> m_activeSamples;
	static FlowField.Data m_flowField;
	private AgentSpawnData m_SpawnData;
	private AgentSteerParams m_steerParams;

	protected override void OnCreateManager(int capacity)
	{
		base.OnCreateManager(capacity);
	}

	//-----------------------------------------------------------------------------
	protected override void OnUpdate()
	{
		if (!Input.GetMouseButton(StandardInput.LEFT_MOUSE_BUTTON)) return;
		if (!Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, Mathf.Infinity)) return;

		if (m_SpawnData == null)
		{
			m_SpawnData = AgentSpawnData.Instance;
			CreateAgentSteerData();
		}

		m_activeSamples = new NativeList<float3>(Allocator.Temp);
		m_DistHeight = (int)math.floor(m_SpawnData.AgentDistSize.x / m_SpawnData.AgentDistCellSize);
		m_DistWidth = (int)math.floor(m_SpawnData.AgentDistSize.y / m_SpawnData.AgentDistCellSize);
		m_Grid = new NativeArray<float3>(m_DistWidth * m_DistHeight, Allocator.Temp);

		for (int index = 0; index < m_SpawnData.AgentDistNumPerClick; index++)
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
            m_flowField = new FlowField.Data() { Value = InitializationData.m_initialFlow };

        Manager.Archetype.CreateAgent(PostUpdateCommands, _pos, m_SpawnData.AgentMesh, m_SpawnData.AgentMaterial, m_agentData.GridSettings[0], m_flowField, m_steerParams);
	}

	//-----------------------------------------------------------------------------
	public void InitSampler()
	{
		var initData = InitializationData.Instance;
		m_DistributionRect = new Rect(0, 0, m_SpawnData.AgentDistSize.x, m_SpawnData.AgentDistSize.y);
		m_RadiusSquared = initData.m_cellSize * initData.m_cellSize;
	}

	//-----------------------------------------------------------------------------
	public float3? Sample(float3 _hit)
	{
		// First sample is choosen randomly
		AddSample(new float3(Random.value * m_DistributionRect.width, 0, Random.value * m_DistributionRect.height), m_SpawnData.AgentDistCellSize);
		while (m_activeSamples.Length > 0)
		{
			// Pick a random active sample
			int i = (int) Random.value * m_activeSamples.Length;
			float3 sample = m_activeSamples[i];

			// Try random candidates between [radius, 2 * radius] from that sample.
			for (int j = 0; j < m_SpawnData.AgentDistMaxTries; ++j)
			{
				float angle = 2 * Mathf.PI * Random.value;
				// See: http://stackoverflow.com/questions/9048095/create-random-number-within-an-annulus/9048443#9048443
				float randomNumber = Mathf.Sqrt(Random.value * 3 * m_RadiusSquared + m_RadiusSquared);
				var candidate = sample + randomNumber * new float3(math.cos(angle), 0, math.sin(angle));

				// Accept candidates if it's inside the rect and farther than 2 * radius to any existing sample.
				if (m_DistributionRect.Contains(new float2(candidate.x, candidate.z)) && IsFarEnough(candidate, m_SpawnData.AgentDistCellSize))
				{
					var agentPos = new float3(candidate.x + _hit.x, 0, candidate.z + _hit.z);
					var gridIndex = GridUtilties.WorldToIndex(m_agentData.GridSettings[0], agentPos);
					if (m_agentData.TileCost[gridIndex].Value > m_SpawnData.AgentDistSpawningThreshold)
						continue;
					
					AddSample(candidate, m_SpawnData.AgentDistCellSize);
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
	
	//-----------------------------------------------------------------------------
	private void CreateAgentSteerData()
	{
		var steerData = AgentSteerData.Instance;
		m_steerParams = new AgentSteerParams
		{
			MaxSpeed = steerData.MaxSpeed,
			Acceleration = steerData.Acceleration,
			TerrainFieldWeight = steerData.TerrainFieldWeight,
			TargetFieldWeight = steerData.TargetFieldWeight,
			SeparationRadius = steerData.SeparationRadius,
			SeparationWeight = steerData.SeparationWeight,
			AlignmentWeight = steerData.AlignmentWeight,
			CohesionWeight = steerData.CohesionWeight,
			NeighbourHashCellSize = steerData.NeighbourHashCellSize,
			AlignmentHashCellSize = steerData.AlignmentHashCellSize,
		};
	}
}
