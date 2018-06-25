using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

[System.Serializable]
public struct AgentData : IComponentData
{
	public float2 position;
	public float2 velocity;
}

public class AgentSystem : JobComponentSystem
{
	struct Data
	{
		[ReadOnly]
		public SharedComponentDataArray<GridSettings> Grid;
		[ReadOnly]
		public ComponentDataArray<TileDirection> Field;
		public ComponentDataArray<AgentData> Agents;
		public EntityArray Entity;
		public int Length;
	}
	[Inject] Data m_Data;

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		return new Job()
		{
			Agents = m_Data.Agents,
			Field = m_Data.Field,
			Grid = m_Data.Grid[0]
		}.Schedule(m_Data.Length, 64, inputDeps);
	}

	[Unity.Burst.BurstCompile]
	struct Job : IJobParallelFor
	{
		[ReadOnly]
		public ComponentDataArray<TileDirection> Field;
		public ComponentDataArray<AgentData> Agents;
		public GridSettings Grid;
		public void Execute(int i)
		{
		}
	}

}
