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
		lastJob.Complete();
		return (lastJob = new Job()
		{
			Cost = m_Data.Cost,
			Render = Render,
			Stride = Stride
		}.Schedule(m_Data.Length, Stride, inputDeps));
	}

	[Unity.Burst.BurstCompile]
	struct Job : IJobParallelFor
	{
		public int Stride;
		[ReadOnly]
		public ComponentDataArray<TileCost> Cost;
		public NativeArray<RenderData> Render;

		public void Execute(int i)
		{
			Render[i] = new RenderData() { color = Cost[i].cost / 255f };
		}

	}

}