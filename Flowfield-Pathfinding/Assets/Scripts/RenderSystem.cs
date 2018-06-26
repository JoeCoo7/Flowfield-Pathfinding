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
	public int Stride;
	public JobHandle lastJob;
	public NativeArray<RenderData> Render;
	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		return (lastJob = new Job()
		{
			Cost = m_Data.Cost,
			Render = Render,
			Flow = InitializationData.m_initialFlow,
			Grid = m_Data.Grid[0]
		}.Schedule(m_Data.Length, Stride, inputDeps));
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

		public void Execute(int i)
		{
			var bi = GridUtilties.Grid2Index(Grid, new int2(i % Grid.cellCount.x, i / Grid.cellCount.x));
			float3 flowColor = Flow[bi];
			if (Cost[bi].Value == 255)
				flowColor = new float3(0,0,0);
			Render[i] = new RenderData() { color = flowColor};
		}

	}

}