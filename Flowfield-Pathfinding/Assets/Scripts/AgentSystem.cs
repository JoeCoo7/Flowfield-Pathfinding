using System.Runtime.InteropServices.ComTypes;
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
public struct Velocity : IComponentData
{
	public float3 Value;
}

public class AgentSystem : JobComponentSystem
{
	struct AgentData
	{
		[ReadOnly] public SharedComponentDataArray<GridSettings> GridSettings;
		[ReadOnly] public SharedComponentDataArray<FlowField.Data> TargetFlowfield;
		public ComponentDataArray<Velocity> Velocities;
		public ComponentDataArray<Position> Positions;
		public EntityArray Entity;
		public int Length;
	}
	
	[Inject] AgentData m_agents;
	[Inject] EndFrameBarrier m_Barrier;
	private NativeMultiHashMap<int, int> m_hashMap;

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


	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		if (m_hashMap.IsCreated)
			m_hashMap.Dispose();
		
		var settings = m_agents.GridSettings[0];
		var positions = m_agents.Positions;
		var velocities = m_agents.Velocities;
		var agentCount = positions.Length;
		var cellIndices = new NativeArray<int>(agentCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
		var cellAlignment             = new NativeArray<Velocity>(agentCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
		var cellSeparation            = new NativeArray<Position>(agentCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
		var cellCount                 = new NativeArray<int>(agentCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
		m_hashMap = new NativeMultiHashMap<int,int>(agentCount,Allocator.TempJob);

		
		var hashPositionsJob = new HashPositions
		{
			positions      = positions,
			hashMap        = m_hashMap,
			cellRadius     = 3
		};
		var hashPositionsJobHandle = hashPositionsJob.Schedule(agentCount, 64, inputDeps);

		var initialCellAlignmentJob = new CopyComponentData<Velocity>
		{
			Source  = velocities,
			Results = cellAlignment
		};
		var initialCellAlignmentJobHandle = initialCellAlignmentJob.Schedule(agentCount, 64, inputDeps);
		
		var initialCellSeparationJob = new CopyComponentData<Position>
		{
			Source  = positions,
			Results = cellSeparation
		};
		var initialCellSeparationJobHandle = initialCellSeparationJob.Schedule(agentCount, 64, inputDeps);
		
		var initialCellCountJob = new MemsetNativeArray<int>
		{
			Source = cellCount,
			Value  = 1
		};
		var initialCellCountJobHandle = initialCellCountJob.Schedule(agentCount, 64, inputDeps);
		var initialCellBarrierJobHandle = JobHandle.CombineDependencies(initialCellAlignmentJobHandle, initialCellSeparationJobHandle, initialCellCountJobHandle);
		var mergeCellsBarrierJobHandle = JobHandle.CombineDependencies(hashPositionsJobHandle, initialCellBarrierJobHandle);

		var mergeCellsJob = new MergeCells
		{
			cellIndices               = cellIndices,
			cellAlignment             = cellAlignment,
			cellSeparation            = cellSeparation,
			cellCount                 = cellCount,
		};
		var mergeCellsJobHandle = mergeCellsJob.Schedule(m_hashMap,64,mergeCellsBarrierJobHandle);

		var steerJob = new Steer
		{
			cellIndices               = cellIndices,
			settings                  = settings,
			cellAlignment             = cellAlignment,
			cellSeparation            = cellSeparation,
			cellCount                 = cellCount,
			deltaTime                        = Time.deltaTime,
			positions                 = positions,
			velocities                 = velocities,
			targetFlowfield = m_agents.TargetFlowfield[0].Value,
			terrainFlowfield = InitializationData.m_initialFlow,
			maxSpeed = InitializationData.Instance.m_unitMaxSpeed
		};

		var speedJob = new PositionJob
		{
			Velocity = velocities,
			Positions = positions,
			TimeDelta = Time.deltaTime,
		};
		
		var steerJobHandle = steerJob.Schedule(agentCount, 64, mergeCellsJobHandle);
		var speedJobHandel = speedJob.Schedule(agentCount, 64, steerJobHandle);
		
		return speedJobHandel;
	}
	
	protected override void OnStopRunning()
	{
		m_hashMap.Dispose();
	}
	
	//-----------------------------------------------------------------------------
	[BurstCompile]
	struct MergeCells : IJobNativeMultiHashMapMergedSharedKeyIndices
	{
		public NativeArray<int> cellIndices;
		public NativeArray<Velocity> cellAlignment;
		public NativeArray<Position> cellSeparation;
		public NativeArray<int> cellCount;
		
		void NearestPosition(NativeArray<Position> neighbours, float3 agentPosition, out int nearestNeighbourIndex, out float nearestDistance )
		{
			nearestNeighbourIndex = 0;
			nearestDistance      = math.lengthSquared(agentPosition-neighbours[0].Value);
			for (int i = 1; i < neighbours.Length; i++)
			{
				var targetPosition = neighbours[i].Value;
				var distance       = math.lengthSquared(agentPosition-targetPosition);
				var nearest        = distance < nearestDistance;
				nearestDistance      = math.select(nearestDistance, distance, nearest);
				nearestNeighbourIndex = math.select(nearestNeighbourIndex, i, nearest);
			}
			nearestDistance = math.sqrt(nearestDistance);
		}
		
		public void ExecuteFirst(int index)
		{
			cellIndices[index] = index;
		}

		public void ExecuteNext(int cellIndex, int index)
		{
			cellCount[cellIndex]      += 1;
			cellAlignment[cellIndex]  = new Velocity { Value = cellAlignment[cellIndex].Value + cellAlignment[index].Value };
			cellSeparation[cellIndex] = new Position { Value = cellSeparation[cellIndex].Value + cellSeparation[index].Value };
			cellIndices[index]        = cellIndex;
		}
	}

	
	//-----------------------------------------------------------------------------
	[BurstCompile]
	struct Steer : IJobParallelFor
	{
		[ReadOnly] public GridSettings settings;
		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<int> cellIndices;
		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<Velocity> cellAlignment;
		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<Position> cellSeparation;
		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<int> cellCount;
		[ReadOnly] public NativeArray<float3> targetFlowfield;
		[ReadOnly] public NativeArray<float3> terrainFlowfield;
		[ReadOnly] public ComponentDataArray<Position> positions;
		[ReadOnly] public float maxSpeed;
		
		public float deltaTime;
		public ComponentDataArray<Velocity> velocities;
		
		
		public void Execute(int index)
		{
			var velocity = velocities[index].Value;
			var position = positions[index].Value;
			var cellIndex = cellIndices[index];
			var neighborCount = cellCount[cellIndex];
			var alignment = cellAlignment[cellIndex].Value;
			var separation = cellSeparation[cellIndex].Value;

			var alignmentResult = 0.5f * math_experimental.normalizeSafe((alignment/neighborCount)- velocity);
			var separationResult = 0.5f * math_experimental.normalizeSafe((position * neighborCount) - separation);
			//var gridIndex = GridUtilties.WorldToIndex(settings, position);
			var terrainFlowFieldResult = 0; //1 * terrainFlowfield[gridIndex];
			var targetFlowFieldResult = 0; //1 * targetFlowfield[gridIndex];
			var normalVelocity = math_experimental.normalizeSafe(alignmentResult + separationResult + terrainFlowFieldResult + targetFlowFieldResult);
			var nextVelocity = math_experimental.normalizeSafe(velocity + deltaTime*(normalVelocity-velocity));
			nextVelocity = new float3(nextVelocity.x, 0, nextVelocity.z);

			var speed = math.length(nextVelocity);
			if (speed > maxSpeed)
				nextVelocity = math.normalize(nextVelocity) * maxSpeed;

			velocities[index] = new Velocity {Value = nextVelocity};
		}
	}
	
	
	
	//-----------------------------------------------------------------------------
	[BurstCompile]
	struct PositionJob : IJobParallelFor
	{
		[ReadOnly] public ComponentDataArray<Velocity> Velocity;
		[ReadOnly]public float TimeDelta;
		public ComponentDataArray<Position> Positions;
		public void Execute(int i)
		{
			var pos = Positions[i];
			pos.Value += Velocity[i].Value * TimeDelta;
			Positions[i] = pos;
		}
	}

}
