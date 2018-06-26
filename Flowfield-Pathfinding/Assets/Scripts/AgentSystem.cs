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
        public ComponentDataArray<Rotation> Rotations;
        public EntityArray Entity;
		public int Length;
	}
	
	[Inject] AgentData m_agents;
	[Inject] EndFrameBarrier m_Barrier;
	private NativeMultiHashMap<int, int> m_hashMap;
	private NativeMultiHashMap<int, int> m_neighborHashMap;

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
		if (m_neighborHashMap.IsCreated)
			m_neighborHashMap.Dispose();

		var settings = m_agents.GridSettings[0];
		var positions = m_agents.Positions;
        var rotations = m_agents.Rotations;
		var velocities = m_agents.Velocities;
		var agentCount = positions.Length;
		var cellIndices = new NativeArray<int>(agentCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
		var cellAlignment             = new NativeArray<Velocity>(agentCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
		var cellSeparation            = new NativeArray<Position>(agentCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
		var cellCount = new NativeArray<int>(agentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
		var nearestNeighbor = new NativeArray<int>(agentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
		m_hashMap = new NativeMultiHashMap<int, int>(agentCount, Allocator.TempJob);
		m_neighborHashMap = new NativeMultiHashMap<int, int>(agentCount, Allocator.TempJob);
		var cellNeighborIndices = new NativeArray<int>(agentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

		var cellSize = 10;
		var neighborCellSize = 2;

		var hashPositionsJob = new HashPositions
		{
			positions      = positions,
			hashMap        = m_hashMap,
			cellRadius     = cellSize
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

		var hashNeighborPositionsJob = new HashPositions
		{
			positions = positions,
			hashMap = m_neighborHashMap,
			cellRadius = neighborCellSize
		};
		var hashNeighborPositionsJobHandle = hashNeighborPositionsJob.Schedule(agentCount, 64, inputDeps);

		var mergeNeighborCellsJob = new MergeNeighborCells
		{
			cellIndices = cellNeighborIndices, 
		};

		var mergeNeighborCellsJobHandle = mergeNeighborCellsJob.Schedule(m_neighborHashMap, 64, hashNeighborPositionsJobHandle);

		var closestNeighborJob = new FindClosestNeighbor
		{
			cellHash = m_neighborHashMap,
			cellIndices = cellNeighborIndices,
			positions = positions,
			closestNeighbor = nearestNeighbor,
			cellRadius = neighborCellSize
		};

		var closestNeighborJobHandle = closestNeighborJob.Schedule(agentCount, 64, mergeNeighborCellsJobHandle);

		var steerJob = new Steer
		{
			cellIndices = cellIndices,
			settings = settings,
			cellAlignment = cellAlignment,
			cellCohesion = cellSeparation,
			cellCount = cellCount,
			deltaTime = Time.deltaTime,
			closestNeighbor = nearestNeighbor,
			positions = positions,
			velocities = velocities,
			targetFlowfield = m_agents.TargetFlowfield[0].Value,
			terrainFlowfield = InitializationData.m_initialFlow,
			maxSpeed = InitializationData.Instance.m_unitMaxSpeed
		};

        var speedJob = new PositionRotationJob
        {
            Velocity = velocities,
            Positions = positions,
            Rotations = rotations,
			TimeDelta = Time.deltaTime,
		};

		var combinedJobHandle = JobHandle.CombineDependencies(mergeCellsJobHandle, closestNeighborJobHandle);
		var steerJobHandle = steerJob.Schedule(agentCount, 64, combinedJobHandle);
		var speedJobHandel = speedJob.Schedule(agentCount, 64, steerJobHandle);
		
		return speedJobHandel;
	}
	
	protected override void OnStopRunning()
	{
		m_hashMap.Dispose();
		m_neighborHashMap.Dispose();
	}

	//-----------------------------------------------------------------------------
	[BurstCompile]
	struct MergeCells : IJobNativeMultiHashMapMergedSharedKeyIndices
	{
		public NativeArray<int> cellIndices;
		public NativeArray<Velocity> cellAlignment;
		public NativeArray<Position> cellSeparation;
		public NativeArray<int> cellCount;

		public void ExecuteFirst(int index)
		{
			cellIndices[index] = index;
		}

		public void ExecuteNext(int cellIndex, int index)
		{
			cellCount[cellIndex] += 1;
			cellAlignment[cellIndex] = new Velocity { Value = cellAlignment[cellIndex].Value + cellAlignment[index].Value };
			cellSeparation[cellIndex] = new Position { Value = cellSeparation[cellIndex].Value + cellSeparation[index].Value };
			cellIndices[index] = cellIndex;
		}
	}

	//-----------------------------------------------------------------------------
	[BurstCompile]
	struct MergeNeighborCells : IJobNativeMultiHashMapMergedSharedKeyIndices
	{
		 public NativeArray<int> cellIndices;

		public void ExecuteFirst(int index)
		{
			cellIndices[index] = index;
		}

		public void ExecuteNext(int cellIndex, int index)
		{
			cellIndices[index] = cellIndex;
		}
	}

	[BurstCompile]
	struct FindClosestNeighbor : IJobParallelFor
	{
		[DeallocateOnJobCompletion] [ReadOnly]public NativeArray<int> cellIndices;
		[ReadOnly] public ComponentDataArray<Position> positions;
		[ReadOnly] public NativeMultiHashMap<int, int> cellHash;
		public NativeArray<int> closestNeighbor;
		public float cellRadius;
		public void Execute(int index)
		{
			var myPosition = positions[index].Value;
			var closestDistance = float.MaxValue;
			var closestIndex = -1;
			var hash = GridHash.Hash(myPosition, cellRadius);

			if (cellHash.TryGetFirstValue(hash, out int item, out NativeMultiHashMapIterator<int> it))
			{
				do
				{
					if (item != index)
					{
						var neighborPosition = positions[item].Value;
						var vecToNeighbor = neighborPosition - myPosition;
						var neighborDistance = math.lengthSquared(vecToNeighbor);
						if (neighborDistance < closestDistance)
						{
							closestIndex = item;
							closestDistance = neighborDistance;
							if (closestDistance < 1)
								break;
						}
					}

				} while (cellHash.TryGetNextValue(out item, ref it));
			}
			closestNeighbor[index] = closestIndex;
		}
	}
	
	//-----------------------------------------------------------------------------
	[BurstCompile]
	struct Steer : IJobParallelFor
	{
		[ReadOnly] public GridSettings settings;
		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<int> cellIndices;
		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<Velocity> cellAlignment;
		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<Position> cellCohesion;
		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<int> cellCount;
		[ReadOnly] public NativeArray<float3> targetFlowfield;
		[ReadOnly] public NativeArray<float3> terrainFlowfield;
		[ReadOnly] public ComponentDataArray<Position> positions;
		[ReadOnly] public float maxSpeed;
		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<int> closestNeighbor;

		public float deltaTime;
		public ComponentDataArray<Velocity> velocities;


		public void Execute(int index)
		{
			var velocity = velocities[index].Value;
			var position = positions[index].Value;
			var cellIndex = cellIndices[index];
			var neighborCount = cellCount[cellIndex];
			var alignment = cellAlignment[cellIndex].Value;
			var cohesion = cellCohesion[cellIndex].Value;

			var alignmentResult = 1 * math_experimental.normalizeSafe((alignment / neighborCount) - velocity);
			var cohesionResult = 1 * math_experimental.normalizeSafe((position * neighborCount) - cohesion);
			var separationResult = new float3(0, 0, 0);
			if (closestNeighbor[index] >= 0)
			{
				var np = positions[closestNeighbor[index]].Value;
				var nVec = position - np;
				var nDist2 = math.lengthSquared(nVec);
				if (nDist2 < 4)
				{
					var nDist = math.sqrt(nDist2);
					var force = 1f - nDist * .5f;
					separationResult = math.normalize(nVec) * force * 5;
				}
			}
			var gridIndex = GridUtilties.WorldToIndex(settings, position);
			var terrainFlowFieldResult = terrainFlowfield[gridIndex];
			terrainFlowFieldResult.y = 0;
			terrainFlowFieldResult *= terrainFlowFieldResult * terrainFlowFieldResult;
			var targetFlowFieldResult = targetFlowfield[gridIndex];
			targetFlowFieldResult.y = 0;
			var normalVelocity = math_experimental.normalizeSafe( alignmentResult + cohesionResult);
			var nextVelocity = math_experimental.normalizeSafe(velocity + deltaTime*(normalVelocity-velocity)) + separationResult + terrainFlowFieldResult + targetFlowFieldResult;
			nextVelocity = new float3(nextVelocity.x, 0, nextVelocity.z);

			var speed = math.length(nextVelocity);
			if (speed > maxSpeed)
				nextVelocity = math.normalize(nextVelocity) * maxSpeed;

			velocities[index] = new Velocity { Value = nextVelocity};
		}
	}
	
	
	
	//-----------------------------------------------------------------------------
	[BurstCompile]
	struct PositionRotationJob : IJobParallelFor
	{
		[ReadOnly] public ComponentDataArray<Velocity> Velocity;
		[ReadOnly]public float TimeDelta;
		public ComponentDataArray<Position> Positions;
        public ComponentDataArray<Rotation> Rotations;

        public void Execute(int i)
		{
			var pos = Positions[i];
			pos.Value += Velocity[i].Value * TimeDelta;
			Positions[i] = pos;
            var rot = Rotations[i];
            rot.Value = math.lookRotationToQuaternion(Velocity[i].Value, new float3(0.0f, 1.0f, 0.0f));
            Rotations[i] = rot;
		}
	}

}
