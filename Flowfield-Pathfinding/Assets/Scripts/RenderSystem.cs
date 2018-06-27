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

[UpdateAfter(typeof(TileSystem))]
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

	public int renderingJobIndex = 0;
	public int displayJobIndex = 0;
	public JobHandle[] lastJobs = new JobHandle[2];
	public NativeArray<RenderData>[] Render = new NativeArray<RenderData>[2];
	NativeArray<int> EmptyHeatMap;

	protected override void OnStartRunning()
	{
		var grid = InitializationData.Instance.m_grid;
		EmptyHeatMap = new NativeArray<int>(grid.cellCount.x * grid.cellCount.y, Allocator.Persistent);
		Render[0] = new NativeArray<RenderData>(grid.cellCount.x * grid.cellCount.y, Allocator.Persistent);
		Render[1] = new NativeArray<RenderData>(grid.cellCount.x * grid.cellCount.y, Allocator.Persistent);
	}

	protected override void OnStopRunning()
	{
		lastJobs[0].Complete();
		lastJobs[1].Complete();
		EmptyHeatMap.Dispose();
		Render[0].Dispose();
		Render[1].Dispose();
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		displayJobIndex = renderingJobIndex;
		renderingJobIndex++;
		if (renderingJobIndex > 1)
			renderingJobIndex = 0;

		return lastJobs[renderingJobIndex] = new Job()
		{
			Cost = m_Data.Cost,
			Render = Render[renderingJobIndex],
			Heat = m_Data2.Heat[0].Value.IsCreated ? m_Data2.Heat[0].Value : EmptyHeatMap,
			HeatAlpha = 1,//(m_Data2.Heat[0].Value.IsCreated ? math.clamp(1 - (Time.realtimeSinceStartup - m_Data2.Heat[0].Time), 0, 1) : 0),
			Flow = InitializationData.m_initialFlow,
			Grid = m_Data.Grid[0],
			HeatScale = 1f / m_Data.Grid[0].cellCount.x
		}.Schedule(m_Data.Length, 64, inputDeps);
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
					flowColor += (1 - h) * (1 - h) * (1 - h) * HeatAlpha;
			}
			Render[i] = new RenderData() { color = flowColor };
		}

	}

}