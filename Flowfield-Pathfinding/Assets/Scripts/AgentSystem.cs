using System.Collections.Generic;
using Samples.Common;
using Unity.Burst;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics.Experimental;
using Unity.Transforms;

[System.Serializable]
public struct AgentData : IComponentData
{
	public float3 velocity;
}

public class AgentSystem : JobComponentSystem
{
	struct Data
	{
		[ReadOnly] public SharedComponentDataArray<GridSettings> Grid;
		[ReadOnly] public SharedComponentDataArray<FlowField.Data> Field;
		public ComponentDataArray<AgentData> Agents;
		public ComponentDataArray<Position> Positions;
		public EntityArray Entity;
		public int Length;
	}
	
	struct PrevCells
	{
		public NativeMultiHashMap<int, int> hashMap;
		public NativeArray<int>             cellIndices;
		public NativeArray<Heading>         cellAlignment;
		public NativeArray<Position>        cellSeparation;
		public NativeArray<int>             cellCount;
	}
	
	List<PrevCells> m_PrevCells   = new List<PrevCells>();
	[Inject] Data m_Data;
	[Inject] EndFrameBarrier m_Barrier;

	//-----------------------------------------------------------------------------
	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		
		return new Job()
		{
			Agents = m_Data.Agents,
			Field = m_Data.Field[0].Value,
			TimeDelta = Time.deltaTime,
			Positions = m_Data.Positions,
			MaxSpeed = InitializationData.Instance.m_unitMaxSpeed,
			MaxForce = InitializationData.Instance.m_unitMaxForce,
			Grid = m_Data.Grid[0]
		}.Schedule(m_Data.Length, 64, inputDeps);
	}

	//-----------------------------------------------------------------------------
	[BurstCompile]
	struct HashPositions : IJobParallelFor
	{
		[ReadOnly] public ComponentDataArray<Position> positions;
		public NativeMultiHashMap<int, int>.Concurrent hashMap;
		public float cellRadius;

		public void Execute(int index)
		{
			var hash = GridHash.Hash(positions[index].Value, cellRadius);
			hashMap.Add(hash, index);
		}
	}

	//-----------------------------------------------------------------------------
	[BurstCompile]
	struct MergeCells : IJobNativeMultiHashMapMergedSharedKeyIndices
	{
		public NativeArray<int>                 cellIndices;
		public NativeArray<Heading>             cellAlignment;
		public NativeArray<Position>            cellSeparation;
		public NativeArray<int>                 cellObstaclePositionIndex;
		public NativeArray<float>               cellObstacleDistance;
		public NativeArray<int>                 cellTargetPistionIndex;
		public NativeArray<int>                 cellCount;
		[ReadOnly] public NativeArray<Position> targetPositions;
		[ReadOnly] public NativeArray<Position> obstaclePositions;
		
		public void ExecuteFirst(int index)
		{
			cellIndices[index] = index;
		}

		public void ExecuteNext(int cellIndex, int index)
		{
			cellCount[cellIndex]      += 1;
			cellAlignment[cellIndex]  = new Heading { Value = cellAlignment[cellIndex].Value + cellAlignment[index].Value };
			cellSeparation[cellIndex] = new Position { Value = cellSeparation[cellIndex].Value + cellSeparation[index].Value };
			cellIndices[index]        = cellIndex;
		}
	}
	
	[BurstCompile]
	struct Steer : IJobParallelFor
	{
		[ReadOnly] public NativeArray<int> cellIndices;
		[ReadOnly] public GridSettings settings;
		[ReadOnly] public NativeArray<Heading> cellAlignment;
		[ReadOnly] public NativeArray<Position> cellSeparation;
		[ReadOnly] public NativeArray<int> cellCount;
		[ReadOnly] public ComponentDataArray<Position> positions;
		public float dt;
		public ComponentDataArray<Heading> headings;
		
		public void Execute(int index)
		{
			var forward                           = headings[index].Value;
			var position                          = positions[index].Value;
			var cellIndex                         = cellIndices[index];
			var neighborCount                     = cellCount[cellIndex];
			var alignment                         = cellAlignment[cellIndex].Value;
			var separation                        = cellSeparation[cellIndex].Value;
			
			var alignmentResult                   = settings.alignmentWeight * math_experimental.normalizeSafe((alignment/neighborCount)-forward);
			var separationResult                  = settings.separationWeight * math_experimental.normalizeSafe((position * neighborCount) - separation);
			var normalHeading                     = math_experimental.normalizeSafe(alignmentResult + separationResult);
			var nextHeading                       = math_experimental.normalizeSafe(forward + dt*(normalHeading-forward));
			headings[index]                       = new Heading {Value = nextHeading};
		}
	}
	
	
	//-----------------------------------------------------------------------------
	[BurstCompile]
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
			force.y = 0;
			//steering behavior goes here..
			
			
			
			var magnitude = math.length(force);
			if(magnitude > MaxForce)
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
