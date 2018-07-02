using Agent;
using Samples.Common;
using Tile;
using Unity.Burst;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics.Experimental;
using Unity.Transforms;
using Position = Unity.Transforms.Position;

[UpdateInGroup(typeof(ProcessGroup))]
[UpdateAfter(typeof(AgentSpawningSystem))]
//-----------------------------------------------------------------------------
public class AgentSystem : JobComponentSystem
{

	//-----------------------------------------------------------------------------
	struct PreviousJobData
	{
		public JobHandle JobHandle;
		public NativeArray<Position> Positions;
		public NativeArray<Rotation> Rotations;
		public NativeArray<Velocity> Velocities;
		public NativeArray<Goal> Goals;
		public NativeMultiHashMap<int, int> NeighborHashMap;
		public bool IsValid;
		public int Length;
	}

	public int NumAgents { get { return m_Agents.Length; } }
	
	[Inject] private AllDataGroup m_Agents;
    [Inject, ReadOnly] private TileSystem m_TileSystem;
	private PreviousJobData m_PreviousJobData;
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
        if (m_PreviousJobData.IsValid)
        {
            m_PreviousJobData.JobHandle.Complete();
            m_PreviousJobData.Goals.Dispose();
            m_PreviousJobData.Positions.Dispose();
            m_PreviousJobData.Rotations.Dispose();
            m_PreviousJobData.Velocities.Dispose();
            m_PreviousJobData.NeighborHashMap.Dispose();
        }
        m_AllFlowFields.Dispose();
    }

	//-----------------------------------------------------------------------------
    private void CopyFlowField()
    {
        var cache = m_TileSystem.CachedFlowFields;
        if (cache.IsCreated && m_AllFlowFields.Length != cache.Length)
        {
            m_AllFlowFields.Dispose();
            m_AllFlowFields = new NativeArray<float3>(cache.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        if (cache.IsCreated)
            m_AllFlowFields.CopyFrom(cache);
    }

	//-----------------------------------------------------------------------------
    protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		var settings = m_Agents.GridSettings[0];
        var agentCount = m_Agents.Positions.Length;

        // process the previous frame
        if (m_PreviousJobData.IsValid && m_PreviousJobData.JobHandle.IsCompleted)
        {
            m_PreviousJobData.JobHandle.Complete();

            // copy data back to the components
            var copyPrevJobHandle = new CopyPreviousResultsToAgentsJob
            {
                positions = m_PreviousJobData.Positions,
                rotations = m_PreviousJobData.Rotations,
                velocities = m_PreviousJobData.Velocities,

                outputPositions = m_Agents.Positions,
                outputRotations = m_Agents.Rotations,
                outputVelocities = m_Agents.Velocities
            }.Schedule(m_PreviousJobData.Length, 64, inputDeps);

            copyPrevJobHandle.Complete();
            m_PreviousJobData.Goals.Dispose();
            m_PreviousJobData.NeighborHashMap.Dispose();
        }
        else if (m_PreviousJobData.IsValid && !m_PreviousJobData.JobHandle.IsCompleted)
        {
            return inputDeps;
        }

        var positions = new NativeArray<Position>(agentCount, Allocator.TempJob);
        var copyPositions = new CopyComponentData<Position>
        {
            Source = m_Agents.Positions,
            Results = positions
        }.Schedule(agentCount, 64, inputDeps);

        var rotations = new NativeArray<Rotation>(agentCount, Allocator.TempJob);
        var copyRotation = new CopyComponentData<Rotation>
        {
            Source = m_Agents.Rotations,
            Results = rotations
        }.Schedule(agentCount, 64, inputDeps);

        var velocities = new NativeArray<Velocity>(agentCount, Allocator.TempJob);
        var copyVelocities = new CopyComponentData<Velocity>
        {
            Source = m_Agents.Velocities,
            Results = velocities
        }.Schedule(agentCount, 64, inputDeps);

        var goals = new NativeArray<Goal>(agentCount, Allocator.TempJob);
        var copyGoals = new CopyComponentData<Goal>
        {
            Source = m_Agents.Goals,
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
			CellHash = neighborHashMap,
			CellHashes = cellNeighborIndices,
			Positions = positions,
			ClosestNeighbor = vecFromNearestNeighbor,
			CellRadius = neighborCellSize, 
			Hashes = neighborHashes,
			SteerParams = steerParams,
			AvgNeighborPositions = avgNeighborPositions,
			AvgNeighborVelocities = avgNeighborVelocities,
			Velocities = velocities
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
            JobHandle = speedJobHandel,
            IsValid = true,
            Positions = positions,
            Rotations = rotations,
            Velocities = velocities,
            Goals = goals,
            NeighborHashMap = neighborHashMap,
            Length = agentCount,
        };

        return copyJobs;
	}

	//-----------------------------------------------------------------------------
	[BurstCompile]
	private struct CopyPreviousResultsToAgentsJob : IJobParallelFor
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
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> CellHashes;
		[ReadOnly] public NativeArray<Position> Positions;
		[ReadOnly] public NativeArray<Velocity> Velocities;
		[ReadOnly] public NativeMultiHashMap<int, int> CellHash;
		[WriteOnly] public NativeArray<float3> ClosestNeighbor;
		[WriteOnly] public NativeArray<float3> AvgNeighborPositions;
		[WriteOnly] public NativeArray<float3> AvgNeighborVelocities;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> Hashes;
		[ReadOnly] public AgentSteerParams SteerParams;
		public float CellRadius;
		public void Execute(int index)
		{
			var myPosition = Positions[index].Value;
			var myVelocity = Velocities[index].Value;
			var closestDistance = float.MaxValue;
			float3 closestVecFromNeighbor = float.MaxValue;
			var hash = Hashes[index];
			var totalPosition = myPosition;
			var totalVelocity = myVelocity;
			var foundCount = 1;
			var checkedCount = 0;
			
			if (CellHash.TryGetFirstValue(hash, out int item, out NativeMultiHashMapIterator<int> it))
			{
				do
				{
					if (item != index)
					{
						var neighborPosition = Positions[item].Value;
						var vecFromNeighbor =  myPosition - neighborPosition;
						var neighborDistance = math.length(vecFromNeighbor);
						if (neighborDistance < closestDistance)
						{
							closestDistance = neighborDistance;
							closestVecFromNeighbor = vecFromNeighbor;
						}
						if (neighborDistance < SteerParams.AlignmentRadius)
						{
							totalVelocity += Velocities[item].Value;
							totalPosition += neighborPosition;
							foundCount++;
						}
					}
					checkedCount++;
					if (checkedCount > SteerParams.MaxNeighborChecks && foundCount > 1)
						break;
				} while (CellHash.TryGetNextValue(out item, ref it));
			}
			ClosestNeighbor[index] = closestVecFromNeighbor;
			AvgNeighborPositions[index] = totalPosition / foundCount;
			AvgNeighborVelocities[index] = totalVelocity / foundCount;
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

}
