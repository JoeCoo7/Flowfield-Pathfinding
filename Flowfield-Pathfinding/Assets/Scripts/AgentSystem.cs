using Samples.Common;
using Unity.Burst;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics.Experimental;
using Unity.Transforms;

public class AgentSystem : JobComponentSystem
{
	struct AgentData
	{
		[ReadOnly] public SharedComponentDataArray<GridSettings> GridSettings;
		[ReadOnly] public SharedComponentDataArray<FlowField.Data> TargetFlowfield;
//		[ReadOnly] public SharedComponentDataArray<AgentSteerParams> AgentSteerParams;
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
		[WriteOnly] public NativeMultiHashMap<int, int>.Concurrent hashMap;
		public float cellRadius;

		public void Execute(int index)
		{
			var pos = positions[index].Value;
			var hash = GridHash.Hash(pos, cellRadius);
			hashMap.Add(hash, index);
		}
	}
	[BurstCompile]
	struct HashPositionsWidthSavedHash : IJobParallelFor
	{
		[ReadOnly] public ComponentDataArray<Position> positions;
		[WriteOnly] public NativeMultiHashMap<int, int>.Concurrent hashMap;
		[WriteOnly] public NativeArray<int> HashedPositions;
		public float cellRadius;

		public void Execute(int index)
		{
			var hash = GridHash.Hash(positions[index].Value, cellRadius);
			HashedPositions[index] = hash;
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
//		var cellAlignment             = new NativeArray<Velocity>(agentCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
		//var cellSeparation            = new NativeArray<Position>(agentCount, Allocator.TempJob,NativeArrayOptions.UninitializedMemory);
		var cellCount = new NativeArray<int>(agentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
		var nearestNeighbor = new NativeArray<int>(agentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
		m_hashMap = new NativeMultiHashMap<int, int>(agentCount, Allocator.TempJob);
		m_neighborHashMap = new NativeMultiHashMap<int, int>(agentCount, Allocator.TempJob);
		var cellNeighborIndices = new NativeArray<int>(agentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
		var neighborHashes = new NativeArray<int>(agentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
		var avgNeighborPositions = new NativeArray<float3>(agentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
		var avgNeighborVelocities = new NativeArray<float3>(agentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
		var steerParams = AgentSteerData.Instance.m_Data;
		var cellSize = steerParams.AlignmentHashCellSize;
		var neighborCellSize = steerParams.NeighbourHashCellSize;

		var hashPositionsJob = new HashPositions
		{
			positions = positions,
			hashMap = m_hashMap,
			cellRadius = cellSize,
		};
		var hashPositionsJobHandle = hashPositionsJob.Schedule(agentCount, 64, inputDeps);
		
		/*
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
		*/

		var initialCellCountJob = new MemsetNativeArray<int>
		{
			Source = cellCount,
			Value  = 1
		};
		var initialCellCountJobHandle = initialCellCountJob.Schedule(agentCount, 64, inputDeps);
	//	var initialCellBarrierJobHandle = JobHandle.CombineDependencies(initialCellAlignmentJobHandle, initialCellSeparationJobHandle, initialCellCountJobHandle);
		var mergeCellsBarrierJobHandle = JobHandle.CombineDependencies(hashPositionsJobHandle, initialCellCountJobHandle);

		var mergeCellsJob = new MergeCells
		{
			cellIndices               = cellIndices,
		//	cellAlignment             = cellAlignment,
		//	cellSeparation            = cellSeparation,
			cellCount                 = cellCount,
		};
		var mergeCellsJobHandle = mergeCellsJob.Schedule(m_hashMap,64,mergeCellsBarrierJobHandle);

		var hashNeighborPositionsJob = new HashPositionsWidthSavedHash
		{
			positions = positions,
			hashMap = m_neighborHashMap,
			cellRadius = neighborCellSize,
			 HashedPositions = neighborHashes
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
			cellHashes = cellNeighborIndices,
			positions = positions,
			closestNeighbor = nearestNeighbor,
			cellRadius = neighborCellSize, 
			 hashes = neighborHashes,
			  steerParams = steerParams,
			avgNeighborPositions = avgNeighborPositions,
			avgNeighborVelocities = avgNeighborVelocities,
			 velocities = velocities

		};

		var closestNeighborJobHandle = closestNeighborJob.Schedule(agentCount, 64, mergeNeighborCellsJobHandle);

		var steerJob = new Steer
		{
			cellIndices = cellIndices,
			settings = settings,
			cellAlignment = avgNeighborVelocities,
			cellCohesion = avgNeighborPositions,
			cellCount = cellCount,
			deltaTime = Time.deltaTime,
			closestNeighbor = nearestNeighbor,
			positions = positions,
			velocities = velocities,
			targetFlowfield = m_agents.TargetFlowfield[0].Value,
			terrainFlowfield = InitializationData.m_initialFlow,
			 steerParams = steerParams
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
		[WriteOnly] public NativeArray<int> cellIndices;
//		public NativeArray<Velocity> cellAlignment;
//		public NativeArray<Position> cellSeparation;
		public NativeArray<int> cellCount;

		public void ExecuteFirst(int index)
		{
			cellIndices[index] = index;
		}

		public void ExecuteNext(int cellIndex, int index)
		{
			cellCount[cellIndex] += 1;
//			cellAlignment[cellIndex] = new Velocity { Value = cellAlignment[cellIndex].Value + cellAlignment[index].Value };
//			cellSeparation[cellIndex] = new Position { Value = cellSeparation[cellIndex].Value + cellSeparation[index].Value };
			cellIndices[index] = cellIndex;
		}
	}

	//-----------------------------------------------------------------------------
	[BurstCompile]
	struct MergeNeighborCells : IJobNativeMultiHashMapMergedSharedKeyIndices
	{
		[WriteOnly] public NativeArray<int> cellIndices;

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
		[DeallocateOnJobCompletion] [ReadOnly]public NativeArray<int> cellHashes;
		[ReadOnly] public ComponentDataArray<Position> positions;
		[ReadOnly] public ComponentDataArray<Velocity> velocities;
		[ReadOnly] public NativeMultiHashMap<int, int> cellHash;
		[WriteOnly] public NativeArray<int> closestNeighbor;
		[WriteOnly] public NativeArray<float3> avgNeighborPositions;
		[WriteOnly] public NativeArray<float3> avgNeighborVelocities;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> hashes;
		[ReadOnly] public AgentSteerParams steerParams;
		public float cellRadius;
		public void Execute(int index)
		{
			var myPosition = positions[index].Value;
			var myVelocity = velocities[index].Value;
			var closestDistance = float.MaxValue;
			var closestIndex = -1;
			var hash = hashes[index];
			var avgNeighborPosition = myPosition;
			var avgNeighborVelocity = myVelocity;
			var count = 1;
			if (cellHash.TryGetFirstValue(hash, out int item, out NativeMultiHashMapIterator<int> it))
			{
				do
				{
					if (item != index)
					{
						var neighborPosition = positions[item].Value;
						var vecToNeighbor = neighborPosition - myPosition;
						var neighborDistance = math.length(vecToNeighbor);
						if (neighborDistance < closestDistance)
						{
							closestIndex = item;
							closestDistance = neighborDistance;
						}
						if (neighborDistance < steerParams.AlignmentRadius)
						{
							avgNeighborVelocity += velocities[item].Value;
							avgNeighborPosition += neighborPosition;
							count++;
						}
					}

				} while (cellHash.TryGetNextValue(out item, ref it));
			}
			closestNeighbor[index] = closestIndex;
			avgNeighborPositions[index] = avgNeighborPosition / count;
			avgNeighborVelocities[index] = avgNeighborVelocity / count;
		}
	}
	
	//-----------------------------------------------------------------------------
	[BurstCompile]
	struct Steer : IJobParallelFor
	{
		[ReadOnly] public GridSettings settings;
		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<int> cellIndices;
		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<float3> cellAlignment;
		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<float3> cellCohesion;
		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<int> cellCount;
		[ReadOnly] public AgentSteerParams steerParams;

		[ReadOnly] public NativeArray<float3> targetFlowfield;
		[ReadOnly] public NativeArray<float3> terrainFlowfield;
		[ReadOnly] public ComponentDataArray<Position> positions;
		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<int> closestNeighbor;
		public float deltaTime;
		public ComponentDataArray<Velocity> velocities;
		public void Execute(int index)
		{
			var velocity = velocities[index].Value;
			var position = positions[index].Value;
			var cellIndex = cellIndices[index];
			var neighborCount = cellCount[cellIndex];
			var alignment = cellAlignment[cellIndex];
			var cohesion = cellCohesion[cellIndex];
			var alignmentResult = steerParams.AlignmentWeight * math_experimental.normalizeSafe(alignment - velocity);
			var cohesionResult = steerParams.CohesionWeight * math_experimental.normalizeSafe(cohesion - position);//math_experimental.normalizeSafe((position * neighborCount) - cohesion);
			var separationResult = new float3(0, 0, 0);
			if (closestNeighbor[index] >= 0)
			{
				var np = positions[closestNeighbor[index]].Value;
				var nVec = position - np;
				var nDist2 = math.lengthSquared(nVec);
				if (nDist2 < steerParams.SeparationRadius * steerParams.SeparationRadius)
				{
					var nDist = math.sqrt(nDist2);
					var force = 1f - nDist / steerParams.SeparationRadius;
					separationResult = math.normalize(nVec) * force * steerParams.SeparationWeight;
				}
			}

			var gridIndex = GridUtilties.WorldToIndex(settings, position);
			var terrainFlowFieldResult = terrainFlowfield[gridIndex] * steerParams.TerrainFieldWeight;
			terrainFlowFieldResult.y = 0;
			var targetFlowFieldResult = targetFlowfield[gridIndex] * steerParams.TargetFieldWeight;
			targetFlowFieldResult.y = 0;



			var desiredVelocity = math_experimental.normalizeSafe(alignmentResult + cohesionResult + separationResult + terrainFlowFieldResult + targetFlowFieldResult) * steerParams.MaxSpeed;
			var accelForce = (desiredVelocity - velocity) * math.min(deltaTime * steerParams.Acceleration, 1);
			var nextVelocity = velocity + accelForce;
			nextVelocity = new float3(nextVelocity.x, 0, nextVelocity.z);

			var speed = math.length(velocity);
			if (speed > steerParams.MaxSpeed)
				nextVelocity = math.normalize(nextVelocity) * steerParams.MaxSpeed;

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
