using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

[System.Serializable]
public struct RenderData : IComponentData
{
	public float3 color;
}

public class RenderSystem : JobComponentSystem
{
	struct Data
	{
		[ReadOnly]
		public SharedComponentDataArray<GridSettings> Grid;
		[ReadOnly]
		public ComponentDataArray<Tile.Cost> Cost;
		public EntityArray Entity;
		public int Length;
	}

	[Inject] Data m_Data;

	struct Data2
	{
		[ReadOnly]
		public SharedComponentDataArray<DebugHeatmap.Component> Heat;
	}
	[Inject]
	Data2 m_Data2;


	public JobHandle lastJob;
	public NativeArray<RenderData> Render;
	NativeArray<int> EmptyHeatMap;
	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		if (!EmptyHeatMap.IsCreated)
			EmptyHeatMap = new NativeArray<int>(m_Data.Length, Allocator.Persistent);

		var heat = (m_Data2.Heat[0].Value.IsCreated ? m_Data2.Heat[0].Value : EmptyHeatMap).ToArray();

		return (lastJob = new Job()
		{
			Cost = m_Data.Cost,
			Render = Render,
			Heat = m_Data2.Heat[0].Value.IsCreated ? m_Data2.Heat[0].Value : EmptyHeatMap,
			HeatAlpha = (m_Data2.Heat[0].Value.IsCreated ? math.clamp(1 - (Time.realtimeSinceStartup - m_Data2.Heat[0].Time), 0, 1) : 0),
			Flow = InitializationData.m_initialFlow,
			Grid = m_Data.Grid[0],
			HeatScale = 1f / m_Data.Grid[0].cellCount.x
		}.Schedule(m_Data.Length, 64, inputDeps));
	}

	[Unity.Burst.BurstCompile]
	struct Job : IJobParallelFor
	{
		[ReadOnly]
		public ComponentDataArray<Tile.Cost> Cost;
		public NativeArray<RenderData> Render;
		[ReadOnly]
		public NativeArray<float3> Flow;
		public GridSettings Grid;
		[ReadOnly]
		public NativeArray<int> Heat;
		public float HeatAlpha;
		public float HeatScale;
		public void Execute(int i)
		{
			var bi = GridUtilties.Grid2Index(Grid, new int2(i % Grid.cellCount.x, i / Grid.cellCount.x));
			float3 flowColor = Flow[bi];
			if (Cost[bi].Value == 255)
			{
				flowColor = new float3(0, 0, 0);
			}
			else if(HeatAlpha > 0)
			{
				var h = Heat[bi] * HeatScale;
				if (h < 1)
					flowColor += (1 - h) * (1 - h) * HeatAlpha;
			}
			Render[i] = new RenderData() { color = flowColor };
		}

	}

}