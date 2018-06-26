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
		public ComponentDataArray<TileCost> Cost;
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
			Grid = m_Data.Grid[0]
		}.Schedule(m_Data.Length, Stride, inputDeps));
	}

	[Unity.Burst.BurstCompile]
	struct Job : IJobParallelFor
	{
		[ReadOnly]
		public ComponentDataArray<TileCost> Cost;
		public NativeArray<RenderData> Render;
		public GridSettings Grid;

		public void Execute(int i)
		{
			var bi = GridUtilties.Grid2Index(Grid, new int2(i % Grid.cellCount.x, i / Grid.cellCount.x));

			Render[i] = new RenderData() { color = 1f - Cost[bi].value / 255f };
		}

	}

}