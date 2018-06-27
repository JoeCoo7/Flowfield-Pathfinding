﻿using RSGLib.ECS;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

//-----------------------------------------------------------------------------
[UpdateInGroup(typeof(ProcessGroup))]
public class AgentSpawingSystem : ComponentSystem
{
#if UNITY_EDITOR
	[UnityEditor.MenuItem("Pathfinding/Create Test Agents")]
	static void Spawn()
	{
		var agentSpawnSystem = World.Active.GetOrCreateManager<AgentSpawingSystem>();
		agentSpawnSystem.spawnDebugAgentsOnNextFrame = true;
	}
#endif


	struct AgentData
	{
		[ReadOnly] public SharedComponentDataArray<GridSettings> GridSettings;
        [ReadOnly] public ComponentDataArray<Tile.Cost> TileCost;
	}
	
	[Inject] private AgentData m_agentData;
	[Inject] private ECSInput.InputDataGroup m_inputData;
	private Rect m_DistributionRect;
	private int m_DistHeight;
	private int m_DistWidth;
	private float m_RadiusSquared; 
	private NativeArray<float3> m_Grid;
	private NativeList<float3> m_activeSamples;
	static FlowField.Data m_flowField;
	bool spawnDebugAgentsOnNextFrame = false;

	//-----------------------------------------------------------------------------
	protected override void OnUpdate()
	{
		var spawnData = Main.ActiveSpawnParams;
		if (spawnDebugAgentsOnNextFrame)
		{
			Spawn(0, 500, 10000);
			spawnDebugAgentsOnNextFrame = false;
		}
		else
		{
			if (m_inputData.Buttons[0].Values["SpawnAgents"].Status != ECSInput.InputButtons.PRESSED) return;
			if (!Physics.Raycast(Camera.main.ScreenPointToRay(m_inputData.MousePos[0].Value), out RaycastHit hit, Mathf.Infinity)) return;
			Spawn(hit.point, spawnData.AgentSpawnRadius, spawnData.AgentDistNumPerClick);
		}
	}

	void Spawn(float3 point, float radius, int count)
	{
		Debug.LogFormat("SPAWN({0}, {1}, {2})", point, radius, count);

		var spawnData = Main.ActiveSpawnParams;
		m_activeSamples = new NativeList<float3>(Allocator.Temp);
		m_DistHeight = (int)math.floor(radius / spawnData.AgentDistCellSize);
		m_DistWidth = (int)math.floor(radius / spawnData.AgentDistCellSize);
		m_Grid = new NativeArray<float3>(m_DistWidth * m_DistHeight, Allocator.Temp);
		InitSampler(spawnData, radius);
		var spawnPoint = point - new float3(radius, 0, radius) * .5f;
		for (int index = 0; index < count; index++)
		{
			float3? pos = Sample(spawnData, spawnPoint);
			if (pos != null && math.length(pos.Value - point) < radius * .5)
				CreateAgent(spawnData, pos.Value);
		}

		m_Grid.Dispose();
		m_activeSamples.Dispose();
	}

	//-----------------------------------------------------------------------------
	private void CreateAgent(AgentSpawnData spawnData, float3 _pos)
	{
		if (!m_flowField.Value.IsCreated)
            m_flowField = new FlowField.Data() { Value = InitializationData.m_initialFlow };

        Manager.Archetype.CreateAgent(PostUpdateCommands, _pos, spawnData.AgentMesh, spawnData.AgentMaterial, m_agentData.GridSettings[0], m_flowField);
	}

	//-----------------------------------------------------------------------------
	public void InitSampler(AgentSpawnData spawnData, float radius)
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
		while (m_activeSamples.Length > 0)
		{
			// Pick a random active sample
			int i = (int) Random.value * m_activeSamples.Length;
			float3 sample = m_activeSamples[i];

			// Try random candidates between [radius, 2 * radius] from that sample.
			for (int j = 0; j < spawnData.AgentDistMaxTries; ++j)
			{
				float angle = 2 * Mathf.PI * Random.value;
				// See: http://stackoverflow.com/questions/9048095/create-random-number-within-an-annulus/9048443#9048443
				float randomNumber = Mathf.Sqrt(Random.value * 3 * m_RadiusSquared + m_RadiusSquared);
				var candidate = sample + randomNumber * new float3(math.cos(angle), 0, math.sin(angle));

				// Accept candidates if it's inside the rect and farther than 2 * radius to any existing sample.
				if (m_DistributionRect.Contains(new float2(candidate.x, candidate.z)) && IsFarEnough(candidate, spawnData.AgentDistCellSize))
				{
					var agentPos = new float3(candidate.x + _hit.x, 0, candidate.z + _hit.z);
					var gridIndex = GridUtilties.WorldToIndex(m_agentData.GridSettings[0], agentPos);
					if (gridIndex < 0 || m_agentData.TileCost[gridIndex].Value > spawnData.AgentDistSpawningThreshold)
						continue;
					
					AddSample(candidate, spawnData.AgentDistCellSize);
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
