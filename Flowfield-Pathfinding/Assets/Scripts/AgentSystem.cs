using Samples.Common;
using Unity.Burst;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics.Experimental;
using Unity.Transforms;

[UpdateInGroup(typeof(ProcessGroup))]
[UpdateAfter(typeof(AgentSpawingSystem))]
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
	private NativeMultiHashMap<int, int> m_neighborHashMap;


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
		if (m_neighborHashMap.IsCreated)
			m_neighborHashMap.Dispose();

		var settings = m_agents.GridSettings[0];
		var positions = m_agents.Positions;
        var rotations = m_agents.Rotations;
		var velocities = m_agents.Velocities;
		var agentCount = positions.Length;

		m_neighborHashMap = new NativeMultiHashMap<int, int>(agentCount, Allocator.TempJob);
		var vecFromNearestNeighbor = new NativeArray<float3>(agentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
		var cellNeighborIndices = new NativeArray<int>(agentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
		var neighborHashes = new NativeArray<int>(agentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
		var avgNeighborPositions = new NativeArray<float3>(agentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
		var avgNeighborVelocities = new NativeArray<float3>(agentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
		var steerParams = Main.ActiveSteeringParams;
		var neighborCellSize = steerParams.NeighbourHashCellSize;

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
			closestNeighbor = vecFromNearestNeighbor,
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
			
			settings = settings,
			avgVelocities = avgNeighborVelocities,
			avgPositions = avgNeighborPositions,
			deltaTime = Time.deltaTime,
			vecFromNearestNeighbor = vecFromNearestNeighbor,
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
			steerParams = steerParams
		};

		var steerJobHandle = steerJob.Schedule(agentCount, 64, closestNeighborJobHandle);
		var speedJobHandel = speedJob.Schedule(agentCount, 64, steerJobHandle);
		
		return speedJobHandel;
	}
	
	protected override void OnStopRunning()
	{
		m_neighborHashMap.Dispose();
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
		[WriteOnly] public NativeArray<float3> closestNeighbor;
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
			float3 closestVecFromNeighbor = 0;
			var hash = hashes[index];
			var totalPosition = myPosition;
			var totalVelocity = myVelocity;
			var count = 1;
			if (cellHash.TryGetFirstValue(hash, out int item, out NativeMultiHashMapIterator<int> it))
			{
				do
				{
					if (item != index)
					{
						var neighborPosition = positions[item].Value;
						var vecFromNeighbor =  myPosition - neighborPosition;
						var neighborDistance = math.length(vecFromNeighbor);
						if (neighborDistance < closestDistance)
						{
							closestDistance = neighborDistance;
							closestVecFromNeighbor = vecFromNeighbor;
						}
						if (neighborDistance < steerParams.AlignmentRadius)
						{
							totalVelocity += velocities[item].Value;
							totalPosition += neighborPosition;
							count++;
						}
					}

				} while (cellHash.TryGetNextValue(out item, ref it));
			}
			closestNeighbor[index] = closestVecFromNeighbor;
			avgNeighborPositions[index] = totalPosition / count;
			avgNeighborVelocities[index] = totalVelocity / count;
		}
	}
	
	//-----------------------------------------------------------------------------
	[BurstCompile]
	struct Steer : IJobParallelFor
	{
		[ReadOnly] public GridSettings settings;
		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<float3> avgVelocities;
		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<float3> avgPositions;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float3> vecFromNearestNeighbor;
		[ReadOnly] public AgentSteerParams steerParams;

		[ReadOnly] public NativeArray<float3> targetFlowfield;
		[ReadOnly] public NativeArray<float3> terrainFlowfield;
		[ReadOnly] public ComponentDataArray<Position> positions;
		public float deltaTime;
		public ComponentDataArray<Velocity> velocities;

		float3 Cohesion(int index, float3 position)
		{
			var avgPosition = avgPositions[index];
			var vecToCenter = avgPosition - position;
			var distToCenter = math.length(vecToCenter);
			var distFromOuter = distToCenter - steerParams.AlignmentRadius * .5f;
			if (distFromOuter < 0)
				return 0;
			var strength = distFromOuter / (steerParams.AlignmentRadius * .5f);
			return steerParams.CohesionWeight * (vecToCenter / distToCenter) * (strength * strength);
		}

		float3 Alignment(int index, float3 velocity)
		{
			var avgVelocity = avgVelocities[index];
			var velDiff = avgVelocity - velocity;
			var diffLen = math.length(velDiff);
			if (diffLen < .1f)
				return 0;
			var strength = diffLen / steerParams.MaxSpeed;
			return steerParams.AlignmentWeight * (velDiff / diffLen) * strength * strength;
		}

		float3 Separation(int index, float3 position)
		{
			var nVec = vecFromNearestNeighbor[index];
			var nDist = math.length(nVec);
			var diff =  steerParams.SeparationRadius - nDist;
			if (diff < 0)
				return 0;
			var strength = diff / steerParams.SeparationRadius;
			return steerParams.SeparationWeight * (nVec / nDist) * strength * strength;
		}

		float3 FlowField(float3 position, float3 velocity, NativeArray<float3> field, float weight)
		{
			var gridIndex = GridUtilties.WorldToIndex(settings, position);
			if (gridIndex < 0)
				return 0;
			var fieldVal = field[gridIndex];
			fieldVal.y = 0;
			var fieldLen = math.length(fieldVal);
			if (fieldLen < .1f)
				return 0;
			var desiredVelocity = fieldVal * steerParams.MaxSpeed;
			var velDiff = desiredVelocity - velocity;
			var diffLen = math.length(velDiff);
			var strength = diffLen / steerParams.MaxSpeed;
			return weight * (velDiff / diffLen) * strength * strength;
		}

		float3 Velocity(float3 velocity, float3 forceDirection)
		{
			var desiredVelocity = forceDirection * steerParams.MaxSpeed;
			var accelForce = (desiredVelocity - velocity) * math.min(deltaTime * steerParams.Acceleration, 1);
			var nextVelocity = velocity + accelForce;
			nextVelocity.y = 0;

			var speed = math.length(nextVelocity);
			if (speed > steerParams.MaxSpeed)
			{
				speed = steerParams.MaxSpeed;
				nextVelocity = math.normalize(nextVelocity) * steerParams.MaxSpeed;
			}

			return nextVelocity - nextVelocity * deltaTime * steerParams.Drag;
		}

		public void Execute(int index)
		{
			var velocity = velocities[index].Value;
			var position = positions[index].Value;

			var normalizedForces = math_experimental.normalizeSafe
				(
				Alignment(index, velocity) +
				Cohesion(index, position) +
				Separation(index, position) +
				FlowField(position, velocity, terrainFlowfield, steerParams.TerrainFieldWeight) +
				FlowField(position, velocity, targetFlowfield, steerParams.TargetFieldWeight)
				);
			var newVelocity = Velocity(velocity, normalizedForces);

			velocities[index] = new Velocity { Value = newVelocity};
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
		public AgentSteerParams steerParams;
        public void Execute(int i)
		{
			var pos = Positions[i];
			pos.Value += Velocity[i].Value * TimeDelta;
			Positions[i] = pos;

            var rot = Rotations[i];
			var currDir = math.forward(Rotations[i].Value);
			var speed = math.length(Velocity[i].Value);
			if (speed > .1f)
			{
				var speedPer = speed / steerParams.MaxSpeed;
				var desiredDir = math.normalize(Velocity[i].Value);
				var dirDiff = desiredDir - currDir;
				var newDir = math.normalize(currDir + dirDiff * math.min(TimeDelta * steerParams.RotationSpeed * (.5f + speedPer * .5f), 1));
				rot.Value = math.lookRotationToQuaternion(newDir, new float3(0.0f, 1.0f, 0.0f));
				Rotations[i] = rot;
			}
		}
	}

}
