﻿using Agent;
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
[UpdateAfter(typeof(AgentSpawningSystem))]
//-----------------------------------------------------------------------------

public class AgentSystem : JobComponentSystem
{
	struct AgentData
	{
		[ReadOnly] public SharedComponentDataArray<GridSettings> GridSettings;
		[ReadOnly] public ComponentDataArray<Goal> Goals;
		public ComponentDataArray<Velocity> Velocities;
		public ComponentDataArray<Position> Positions;
        public ComponentDataArray<Rotation> Rotations;
        public EntityArray Entity;
		public readonly int Length;
	}

	struct PreviousJobData
	{
		public JobHandle jobHandle;
		public NativeArray<Position> positions;
		public NativeArray<Rotation> rotations;
		public NativeArray<Velocity> velocities;
		public NativeArray<Goal> goals;
		public NativeMultiHashMap<int, int> neighborHashMap;
		public bool isValid;
		public int length;
	}

	[Inject] private AgentData m_agents;
    [Inject, ReadOnly] private TileSystem m_tileSystem;
	private PreviousJobData m_PreviousJobData;

	//-----------------------------------------------------------------------------
    public int numAgents { get { return m_agents.Length; } }
	private NativeArray<float3> m_AllFlowFields;

	//-----------------------------------------------------------------------------
	[BurstCompile]
	struct HashPositionsWidthSavedHash : IJobParallelFor
	{
		[ReadOnly] public NativeArray<Position> Positions;
		[WriteOnly] public NativeMultiHashMap<int, int>.Concurrent HashMap;
		[WriteOnly] public NativeArray<int> HashedPositions;
		public float CellRadius;

		public void Execute(int index)
		{
			var hash = GridHash.Hash(Positions[index].Value, CellRadius);
			HashedPositions[index] = hash;
			HashMap.Add(hash, index);
		}
	}

	//-----------------------------------------------------------------------------
    protected override void OnCreateManager(int capacity)
    {
        base.OnCreateManager(capacity);
        m_AllFlowFields = new NativeArray<float3>(0, Allocator.Persistent);
    }

	//-----------------------------------------------------------------------------
    protected override void OnDestroyManager()
    {
        if (m_PreviousJobData.isValid)
        {
            m_PreviousJobData.jobHandle.Complete();
            m_PreviousJobData.goals.Dispose();
            m_PreviousJobData.positions.Dispose();
            m_PreviousJobData.rotations.Dispose();
            m_PreviousJobData.velocities.Dispose();
            m_PreviousJobData.neighborHashMap.Dispose();
        }
        m_AllFlowFields.Dispose();
    }

	//-----------------------------------------------------------------------------
    private void CopyFlowField()
    {
        var cache = m_tileSystem.CachedFlowFields;
        if (cache.IsCreated && m_AllFlowFields.Length != cache.Length)
        {
            m_AllFlowFields.Dispose();
            m_AllFlowFields = new NativeArray<float3>(cache.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        if (cache.IsCreated)
            m_AllFlowFields.CopyFrom(cache);
    }

	//-----------------------------------------------------------------------------
    [BurstCompile]
    struct CopyPreviousResultsToAgentsJob : IJobParallelFor
    {
        [ReadOnly, DeallocateOnJobCompletion] public NativeArray<Position> positions;
        [ReadOnly, DeallocateOnJobCompletion] public NativeArray<Rotation> rotations;
        [ReadOnly, DeallocateOnJobCompletion] public NativeArray<Velocity> velocities;
        [WriteOnly] public ComponentDataArray<Position> outputPositions;
        [WriteOnly] public ComponentDataArray<Rotation> outputRotations;
        [WriteOnly] public ComponentDataArray<Velocity> outputVelocities;

        public void Execute(int index)
        {
            outputPositions[index] = new Position { Value = positions[index].Value };
            outputRotations[index] = new Rotation { Value = rotations[index].Value };
            outputVelocities[index] = new Velocity { Value = velocities[index].Value };
        }
    }

	//-----------------------------------------------------------------------------
    protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		var settings = m_agents.GridSettings[0];
        var agentCount = m_agents.Positions.Length;

        // process the previous frame
        if (m_PreviousJobData.isValid && m_PreviousJobData.jobHandle.IsCompleted)
        {
            m_PreviousJobData.jobHandle.Complete();

            // copy data back to the components
            var copyPrevJobHandle = new CopyPreviousResultsToAgentsJob
            {
                positions = m_PreviousJobData.positions,
                rotations = m_PreviousJobData.rotations,
                velocities = m_PreviousJobData.velocities,

                outputPositions = m_agents.Positions,
                outputRotations = m_agents.Rotations,
                outputVelocities = m_agents.Velocities
            }.Schedule(m_PreviousJobData.length, 64, inputDeps);

            copyPrevJobHandle.Complete();
            m_PreviousJobData.goals.Dispose();
            m_PreviousJobData.neighborHashMap.Dispose();
        }
        else if (m_PreviousJobData.isValid && !m_PreviousJobData.jobHandle.IsCompleted)
        {
            return inputDeps;
        }

        var positions = new NativeArray<Position>(agentCount, Allocator.TempJob);
        var copyPositions = new CopyComponentData<Position>
        {
            Source = m_agents.Positions,
            Results = positions
        }.Schedule(agentCount, 64, inputDeps);

        var rotations = new NativeArray<Rotation>(agentCount, Allocator.TempJob);
        var copyRotation = new CopyComponentData<Rotation>
        {
            Source = m_agents.Rotations,
            Results = rotations
        }.Schedule(agentCount, 64, inputDeps);

        var velocities = new NativeArray<Velocity>(agentCount, Allocator.TempJob);
        var copyVelocities = new CopyComponentData<Velocity>
        {
            Source = m_agents.Velocities,
            Results = velocities
        }.Schedule(agentCount, 64, inputDeps);

        var goals = new NativeArray<Goal>(agentCount, Allocator.TempJob);
        var copyGoals = new CopyComponentData<Goal>
        {
            Source = m_agents.Goals,
            Results = goals
        }.Schedule(agentCount, 64, inputDeps);

        var copyJobs = JobHandle.CombineDependencies(JobHandle.CombineDependencies(copyPositions, copyRotation, copyVelocities), copyGoals);
        CopyFlowField();

        var neighborHashMap = new NativeMultiHashMap<int, int>(agentCount, Allocator.TempJob);
		var vecFromNearestNeighbor = new NativeArray<float3>(agentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
		var cellNeighborIndices = new NativeArray<int>(agentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
		var neighborHashes = new NativeArray<int>(agentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
		var avgNeighborPositions = new NativeArray<float3>(agentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
		var avgNeighborVelocities = new NativeArray<float3>(agentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
		var steerParams = Main.ActiveSteeringParams;
		var neighborCellSize = steerParams.NeighbourHashCellSize;

		var hashNeighborPositionsJob = new HashPositionsWidthSavedHash
		{ 
			Positions = positions,
			HashMap = neighborHashMap,
			CellRadius = neighborCellSize,
			HashedPositions = neighborHashes
		};
		var hashNeighborPositionsJobHandle = hashNeighborPositionsJob.Schedule(agentCount, 64, copyJobs);

		var mergeNeighborCellsJob = new MergeNeighborCells
		{ 
			cellIndices = cellNeighborIndices, 
		};

		var mergeNeighborCellsJobHandle = mergeNeighborCellsJob.Schedule(neighborHashMap, 64, hashNeighborPositionsJobHandle);

		var closestNeighborJob = new FindClosestNeighbor
		{
			cellHash = neighborHashMap,
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
			terrainFlowfield = Main.TerrainFlow,
            goals = goals,
            flowFields = m_AllFlowFields,
            flowFieldLength = m_AllFlowFields.Length / TileSystem.k_MaxNumFlowFields,
			steerParams = steerParams
		};

        var speedJob = new PositionRotationJob
        { 
            Velocity = velocities,
            Positions = positions,
            Rotations = rotations,
			TimeDelta = Time.deltaTime,
			steerParams = steerParams,
			grid = settings,
			Heights = Main.TerrainHeight,
	        Normals = Main.TerrainNormals
		};

		var steerJobHandle = steerJob.Schedule(agentCount, 64, closestNeighborJobHandle);
		var speedJobHandel = speedJob.Schedule(agentCount, 64, steerJobHandle);

        m_PreviousJobData = new PreviousJobData
        {
            jobHandle = speedJobHandel,
            isValid = true,
            positions = positions,
            rotations = rotations,
            velocities = velocities,
            goals = goals,
            neighborHashMap = neighborHashMap,
            length = agentCount,
        };

        return copyJobs;
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

	//-----------------------------------------------------------------------------
	[BurstCompile]
	struct FindClosestNeighbor : IJobParallelFor
	{
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> cellHashes;
		[ReadOnly] public NativeArray<Position> positions;
		[ReadOnly] public NativeArray<Velocity> velocities;
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
			float3 closestVecFromNeighbor = float.MaxValue;
			var hash = hashes[index];
			var totalPosition = myPosition;
			var totalVelocity = myVelocity;
			var foundCount = 1;
			var checkedCount = 0;
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
							foundCount++;
						}
					}
					checkedCount++;
					if (checkedCount > steerParams.MaxNeighborChecks && foundCount > 1)
						break;
				} while (cellHash.TryGetNextValue(out item, ref it));
			}
			closestNeighbor[index] = closestVecFromNeighbor;
			avgNeighborPositions[index] = totalPosition / foundCount;
			avgNeighborVelocities[index] = totalVelocity / foundCount;
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

        [ReadOnly] public NativeArray<Goal> goals;
		[ReadOnly] public NativeArray<float3> terrainFlowfield;
		[ReadOnly] public NativeArray<Position> positions;
        [ReadOnly] public NativeArray<float3> flowFields;
        public int flowFieldLength;
		public float deltaTime;
		public NativeArray<Velocity> velocities;

		//-----------------------------------------------------------------------------
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

		//-----------------------------------------------------------------------------
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

		//-----------------------------------------------------------------------------
		float3 Separation(int index)
		{
			var nVec = vecFromNearestNeighbor[index];
			var nDist = math.length(nVec);
			var diff =  steerParams.SeparationRadius - nDist;
			if (diff < 0)
				return 0;
			var strength = diff / steerParams.SeparationRadius;
			return steerParams.SeparationWeight * (nVec / nDist) * strength * strength;
		}

		//-----------------------------------------------------------------------------
		float3 FlowField(float3 velocity, float3 fieldVal, float weight)
		{
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

		//-----------------------------------------------------------------------------
		float3 Velocity(float3 velocity, float3 forceDirection)
		{
			var desiredVelocity = forceDirection * steerParams.MaxSpeed;
			var accelForce = (desiredVelocity - velocity) * math.min(deltaTime * steerParams.Acceleration, 1);
			var nextVelocity = velocity + accelForce;
			nextVelocity.y = 0;

			var speed = math.length(nextVelocity);
			if (speed > steerParams.MaxSpeed)
				nextVelocity = math.normalize(nextVelocity) * steerParams.MaxSpeed;

			return nextVelocity - nextVelocity * deltaTime * steerParams.Drag;
		}

		//-----------------------------------------------------------------------------
		public void Execute(int index)
		{
			var velocity = velocities[index].Value;
			var position = positions[index].Value;
            var goal = goals[index].Current;

            var targetFlowFieldContribution = new float3(0, 0, 0);
            var terrainFlowFieldContribution = new float3(0, 0, 0);

            // This is what we want to do, but the targetFlowField is marked as [WriteOnly],
            // which feels like a bug in the JobSystem
            var gridIndex = GridUtilties.WorldToIndex(settings, position);
            if (gridIndex != -1)
            {
                terrainFlowFieldContribution = FlowField(velocity, terrainFlowfield[gridIndex], steerParams.TerrainFieldWeight);
                if (goal != TileSystem.k_InvalidHandle && flowFields.Length > 0)
                {
                    var flowFieldValue = flowFields[flowFieldLength * goal + gridIndex];
                    targetFlowFieldContribution =
                        FlowField(velocity, flowFieldValue, steerParams.TargetFieldWeight);
                }
            }

            var normalizedForces = math_experimental.normalizeSafe
			(
				Alignment(index, velocity) +
				Cohesion(index, position) +
				Separation(index) +
                terrainFlowFieldContribution +
				targetFlowFieldContribution
			);

			var newVelocity = Velocity(velocity, normalizedForces);
			velocities[index] = new Velocity { Value = newVelocity};
		}
	}

	//-----------------------------------------------------------------------------
	[BurstCompile]
	struct PositionRotationJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<Velocity> Velocity;
		[ReadOnly] public float TimeDelta;
		public NativeArray<Position> Positions;
		public NativeArray<Rotation> Rotations;
		[ReadOnly] public NativeArray<float> Heights;
		[ReadOnly] public NativeArray<float3> Normals;
		public AgentSteerParams steerParams;
		public GridSettings grid;
		public void Execute(int index)
		{
			var pos = Positions[index].Value;
			var rot = Rotations[index].Value;
			var vel = Velocity[index].Value;
			var speed = math.length(vel);
			pos += vel * TimeDelta;

			var gridIndex = GridUtilties.WorldToIndex(grid, pos);
			var terrainHeight = (gridIndex < 0 ? pos.y : Heights[gridIndex]);
			var currUp = math.up(rot);
			var normal = gridIndex < 0 ? currUp : Normals[gridIndex];

			var targetHeight = terrainHeight + 3;
			pos.y = pos.y + (targetHeight - pos.y) * math.min(TimeDelta * (speed + 1), 1);
			if (pos.z < -grid.worldSize.y * .5f)
				pos.z = grid.worldSize.y - grid.worldSize.y * .5f - 50;

			var currDir = math.forward(rot);
			var normalDiff = normal - currUp;
			var newUp = math.normalize(currUp + normalDiff * math.min(TimeDelta * 10, 1));
			var newDir = currDir;
			if (speed > .1f)
			{
				var speedPer = speed / steerParams.MaxSpeed;
				var desiredDir = math.normalize(vel);
				var dirDiff = desiredDir - currDir;
				newDir = math.normalize(currDir + dirDiff * math.min(TimeDelta * steerParams.RotationSpeed * (.5f + speedPer * .5f), 1));
			}
			rot = math.lookRotationToQuaternion(newDir, newUp);
			Positions[index] = new Position(pos);
			Rotations[index] = new Rotation(rot);
		}
	}

	//-----------------------------------------------------------------------------
	[BurstCompile]
	struct PositionRotationJobBroke : IJobParallelFor
	{
		[ReadOnly] public ComponentDataArray<Velocity> Velocity;
		[ReadOnly] public float TimeDelta;
		public ComponentDataArray<Position> Positions;
		public ComponentDataArray<Rotation> Rotations;
		[ReadOnly] public NativeArray<float> Heights;
		[ReadOnly] public NativeArray<float3> Normals;
		public AgentSteerParams steerParams;
		public GridSettings grid;

		public static float CalculateAngle(float _x1, float _y1, float _x2, float _y2)
		{
			return math.atan2((_y2 - _y1), (_x2 - _x1));
		}

		public static float AngleBetween(float2 _vector1, float2 _vector2)
		{
			float2 diff = _vector2 - _vector1;
			float sign = (_vector2.y < _vector1.y) ? -1.0f : 1.0f;
			return CalculateAngle(1, 0, diff.x, diff.y) * sign;
		}


		public void Execute(int i)
		{
			var pos = Positions[i].Value;
			var rot = Rotations[i].Value;
			var vel = Velocity[i].Value;
			var speed = math.length(vel);
			pos += vel * TimeDelta;

			var index = GridUtilties.WorldToIndex(grid, pos);
			var terrainHeight = (index < 0 ? pos.y : Heights[index]);
			var currUp = math.up(rot);
			var normal = index < 0 ? currUp : Normals[index];

//			var coord = GridUtilties.World2Grid(grid, pos);
//			var c = coord * grid.cellSize - grid.worldSize * .5f - grid.cellSize * .5f;
//			var center = new float3(c.x, 0, c.y);
//			pos.y = 0;
//			var vecFromGridCenter = pos - center;
//			var nVec = math.normalize(vecFromGridCenter);
//			var angleX = math.atan2(-normal.y, normal.x - nVec.x);
//			var tanX = math.tan(angleX);
//			var ox = vecFromGridCenter.x * tanX;
//			var angleZ = math.atan2(-normal.y, normal.z - nVec.z);
//			var tanZ = math.tan(angleZ);
//			var oz = vecFromGridCenter.z * tanZ;
//			var o = (ox + oz) * .5f;
//			pos.y = terrainHeight + -o;
//			var worldCellPos = coord * grid.cellSize - grid.worldSize * .5f;
//			var adjacentLength = new float2(pos.x, pos.z) - worldCellPos;
//			var angleX = AngleBetween(math.normalize(new float2(adjacentLength.x, 0)), math.normalize(new float2(normal.x, normal.y)));
//			var offsetX = adjacentLength.x * math.tan(angleX);
//			var angleZ = AngleBetween(math.normalize(new float2(adjacentLength.y, 0)), math.normalize(new float2(normal.z, normal.y)));
//			var offsetZ = adjacentLength.y * math.tan(angleZ);
//			var o = (offsetX + offsetZ) * .5f;
//			pos.y = terrainHeight - o;

			pos.y = pos.y + (terrainHeight - pos.y) * math.min(TimeDelta * (speed + 20) * 2, 1) + 5;
			var currDir = math.forward(rot);
			var normalDiff = normal - currUp;
			var newUp = math.normalize(currUp + normalDiff * math.min(TimeDelta * 10, 1));
			var newDir = currDir;
			if (speed > .1f)
			{
				var speedPer = speed / steerParams.MaxSpeed;
				var desiredDir = math.normalize(vel);
				var dirDiff = desiredDir - currDir;
				newDir = math.normalize(currDir + dirDiff * math.min(TimeDelta * steerParams.RotationSpeed * (.5f + speedPer * .5f), 1));
			}
			rot = math.lookRotationToQuaternion(newDir, newUp);
			Positions[i] = new Position(pos);
			Rotations[i] = new Rotation(rot);
		}
	}

}
