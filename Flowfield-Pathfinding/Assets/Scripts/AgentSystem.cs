﻿using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

[System.Serializable]
public struct AgentData : IComponentData
{
	public float3 velocity;
}

[System.Serializable]
public struct FlowFieldData : ISharedComponentData
{
	public NativeArray<float3> value;
}


public class AgentSystem : JobComponentSystem
{
	struct Data
	{
		[ReadOnly]
		public SharedComponentDataArray<GridSettings> Grid;
		[ReadOnly]
		public SharedComponentDataArray<FlowFieldData> Field;
		public ComponentDataArray<AgentData> Agents;
		public ComponentDataArray<Position> Positions;
		public EntityArray Entity;
		public int Length;
	}
	[Inject] Data m_Data;
	[Inject] EndFrameBarrier m_Barrier;

	void SetFlowField(int index, FlowFieldData ff)
	{
		//.AddSharedComponent(m_Data.Entity[index], ff);


	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{

		return new Job()
		{
			Agents = m_Data.Agents,
			Field = m_Data.Field[0].value,
			TimeDelta = Time.deltaTime,
			Positions = m_Data.Positions,
			MaxSpeed = 10,
			MaxForce = 100,
			Grid = m_Data.Grid[0]
		}.Schedule(m_Data.Length, 64, inputDeps);
	}

	[Unity.Burst.BurstCompile]
	struct Job : IJobParallelFor
	{
		[ReadOnly]
		public NativeArray<float3> Field;
		public ComponentDataArray<AgentData> Agents;
		public ComponentDataArray<Position> Positions;
		public GridSettings Grid;
		public float TimeDelta;
		public float MaxForce;
		public float MaxSpeed;
		public void Execute(int i)
		{
			var pos = Positions[i];
			var agent = Agents[i];
			var force = new float3(0, 0, 0);

			var tileIndex = GridUtilties.WorldToIndex(Grid, pos.Value);
			force += Field[tileIndex];

			//force += new float3(1, 0, 1);
			//steering behavior goes here..
			var mag = math.length(force);
			if(mag > MaxForce)
				force = math.normalize(force) * MaxForce;

			agent.velocity += force * TimeDelta;
			var speed = math.length(agent.velocity);
			if (speed > MaxSpeed)
				agent.velocity = math.normalize(agent.velocity) * MaxSpeed;

			pos.Value += agent.velocity * TimeDelta;
			Agents[i] = agent;
			Positions[i] = pos;
		}
	}

}
