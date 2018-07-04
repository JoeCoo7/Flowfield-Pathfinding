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
		public NativeArray<TargetReached> TargetReached;
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
            m_PreviousJobData.Positions.Dispose();
            m_PreviousJobData.Rotations.Dispose();
            m_PreviousJobData.Velocities.Dispose();
	        m_PreviousJobData.Goals.Dispose();
	        m_PreviousJobData.TargetReached.Dispose();
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
                Positions = m_PreviousJobData.Positions,
                Rotations = m_PreviousJobData.Rotations,
                Velocities = m_PreviousJobData.Velocities,
	            TargetReached = m_PreviousJobData.TargetReached,

                OutputPositions = m_Agents.Positions,
                OutputRotations = m_Agents.Rotations,
                OutputVelocities = m_Agents.Velocities,
	            OutputTargetReached = m_Agents.TargetReached,
            }.Schedule(m_PreviousJobData.Length, 64, inputDeps);

            copyPrevJobHandle.Complete();
	        m_PreviousJobData.Goals.Dispose();
            m_PreviousJobData.NeighborHashMap.Dispose();

        }
        else if (m_PreviousJobData.IsValid && !m_PreviousJobData.JobHandle.IsCompleted)
            return inputDeps;

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

		var targetReached = new NativeArray<TargetReached>(agentCount, Allocator.TempJob);
		var copyTargetReached = new CopyComponentData<TargetReached>
		{
			Source = m_Agents.TargetReached,
			Results = targetReached
		}.Schedule(agentCount, 64, inputDeps);
		
		
        var copyJobs = JobHandle.CombineDependencies(JobHandle.CombineDependencies(copyPositions, copyRotation, copyVelocities), copyGoals, copyTargetReached);
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
			Settings = settings,
			AvgVelocities = avgNeighborVelocities,
			AvgPositions = avgNeighborPositions,
			DeltaTime = Time.deltaTime,
			VecFromNearestNeighbor = vecFromNearestNeighbor,
			Positions = positions,
			Velocities = velocities,
			TerrainFlowfield = Main.TerrainFlow,
            Goals = goals,
			TargetReached = targetReached,
            FlowFields = m_AllFlowFields,
            FlowFieldLength = m_AllFlowFields.Length / TileSystem.k_MaxNumFlowFields,
			SteerParams = steerParams
		};

        var speedJob = new PositionRotationJob
        { 
            Velocity = velocities,
            Positions = positions,
            Rotations = rotations,
			TimeDelta = Time.deltaTime,
			SteerParams = steerParams,
			GridSettings = settings,
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
            TargetReached = targetReached,
            NeighborHashMap = neighborHashMap,
            Length = agentCount,
        };

        return copyJobs;
	}

	//-----------------------------------------------------------------------------
	[BurstCompile]
	private struct CopyPreviousResultsToAgentsJob : IJobParallelFor
	{
		[ReadOnly, DeallocateOnJobCompletion] public NativeArray<Position> Positions;
		[ReadOnly, DeallocateOnJobCompletion] public NativeArray<Rotation> Rotations;
		[ReadOnly, DeallocateOnJobCompletion] public NativeArray<Velocity> Velocities;
		[ReadOnly, DeallocateOnJobCompletion] public NativeArray<TargetReached> TargetReached;
		[WriteOnly] public ComponentDataArray<Position> OutputPositions;
		[WriteOnly] public ComponentDataArray<Rotation> OutputRotations;
		[WriteOnly] public ComponentDataArray<Velocity> OutputVelocities;
		[WriteOnly] public ComponentDataArray<TargetReached> OutputTargetReached;

		public void Execute(int index)
		{
			OutputPositions[index] = new Position { Value = Positions[index].Value };
			OutputRotations[index] = new Rotation { Value = Rotations[index].Value };
			OutputVelocities[index] = new Velocity { Value = Velocities[index].Value };
			OutputTargetReached[index] = new TargetReached { Value = TargetReached[index].Value, CurrentGoal =  TargetReached[index].CurrentGoal};
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
		[ReadOnly] public GridSettings Settings;
		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<float3> AvgVelocities;
		[DeallocateOnJobCompletion][ReadOnly] public NativeArray<float3> AvgPositions;
		[DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float3> VecFromNearestNeighbor;
		[ReadOnly] public AgentSteerParams SteerParams;

		[ReadOnly] public NativeArray<float3> TerrainFlowfield;
		[ReadOnly] public NativeArray<Position> Positions;
        [ReadOnly] public NativeArray<float3> FlowFields;
		[ReadOnly] public int FlowFieldLength;
		[ReadOnly] public float DeltaTime;
		[ReadOnly] public NativeArray<Goal> Goals;
		
		public NativeArray<TargetReached> TargetReached;
		public NativeArray<Velocity> Velocities;

		//-----------------------------------------------------------------------------
		float3 Cohesion(int index, float3 position)
		{
			var avgPosition = AvgPositions[index];
			var vecToCenter = avgPosition - position;
			var distToCenter = math.length(vecToCenter);
			var distFromOuter = distToCenter - SteerParams.AlignmentRadius * .5f;
			if (distFromOuter < 0)
				return 0;
			var strength = distFromOuter / (SteerParams.AlignmentRadius * .5f);
			return SteerParams.CohesionWeight * (vecToCenter / distToCenter) * (strength * strength);
		}

		//-----------------------------------------------------------------------------
		float3 Alignment(int index, float3 velocity)
		{
			var avgVelocity = AvgVelocities[index];
			var velDiff = avgVelocity - velocity;
			var diffLen = math.length(velDiff);
			if (diffLen < .1f)
				return 0;
			var strength = diffLen / SteerParams.MaxSpeed;
			return SteerParams.AlignmentWeight * (velDiff / diffLen) * strength * strength;
		}

		//-----------------------------------------------------------------------------
		float3 Separation(int index)
		{
			var nVec = VecFromNearestNeighbor[index];
			var nDist = math.length(nVec);
			var diff =  SteerParams.SeparationRadius - nDist;
			if (diff < 0)
				return 0;
			var strength = diff / SteerParams.SeparationRadius;
			return SteerParams.SeparationWeight * (nVec / nDist) * strength * strength;
		}

		//-----------------------------------------------------------------------------
		float3 FlowField(float3 velocity, float3 fieldVal, float weight)
		{
			fieldVal.y = 0;
			var fieldLen = math.length(fieldVal);
			if (fieldLen < .1f)
				return 0;
			var desiredVelocity = fieldVal * SteerParams.MaxSpeed;
			var velDiff = desiredVelocity - velocity;
			var diffLen = math.length(velDiff);
			var strength = diffLen / SteerParams.MaxSpeed;
			return weight * (velDiff / diffLen) * strength * strength;
		}

		//-----------------------------------------------------------------------------
		float3 Velocity(float3 velocity, float3 forceDirection)
		{
			var desiredVelocity = forceDirection * SteerParams.MaxSpeed;
			var accelForce = (desiredVelocity - velocity) * math.min(DeltaTime * SteerParams.Acceleration, 1);
			var nextVelocity = velocity + accelForce;
			nextVelocity.y = 0;

			var speed = math.length(nextVelocity);
			if (speed > SteerParams.MaxSpeed)
				nextVelocity = math.normalize(nextVelocity) * SteerParams.MaxSpeed;

			return nextVelocity - nextVelocity * DeltaTime * SteerParams.Drag;
		}

		//-----------------------------------------------------------------------------
		public void Execute(int index)
		{
			var velocity = Velocities[index].Value;
			var position = Positions[index].Value;
			var goal = Goals[index];
			var currentGoal = goal.Current;

			if (TargetReached[index].CurrentGoal != currentGoal && currentGoal!=TileSystem.k_InvalidHandle)
				TargetReached[index] = new TargetReached {Value = 0, CurrentGoal = currentGoal};
				
            var targetFlowFieldContribution = new float3(0, 0, 0);
            var terrainFlowFieldContribution = new float3(0, 0, 0);

            // This is what we want to do, but the targetFlowField is marked as [WriteOnly],
            // which feels like a bug in the JobSystem
            var gridIndex = GridUtilties.WorldToIndex(Settings, position);
            if (gridIndex != -1)
            {
                terrainFlowFieldContribution = FlowField(velocity, TerrainFlowfield[gridIndex], SteerParams.TerrainFieldWeight);
                if (currentGoal != TileSystem.k_InvalidHandle && FlowFields.Length > 0 && TargetReached[index].Value == 0)
                {
	                var flowFieldValue = FlowFields[FlowFieldLength * currentGoal + gridIndex];
					if (IsCloseToTarget(goal, position))	                
		                TargetReached[index] = new TargetReached {Value = 1, CurrentGoal = currentGoal };
	                else
		                targetFlowFieldContribution = FlowField(velocity, flowFieldValue, SteerParams.TargetFieldWeight);
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
			Velocities[index] = new Velocity { Value = newVelocity};
		}

		//-----------------------------------------------------------------------------
		private bool IsCloseToTarget(Goal _goal, float3 _position)
		{
			var gridPos = GridUtilties.World2Grid(Settings, _position);
			return math.abs(gridPos.x - _goal.Position.x) <= _goal.Size && math.abs(gridPos.y - _goal.Position.y) <= _goal.Size;
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
		public AgentSteerParams SteerParams;
		public GridSettings GridSettings;
		
		//-----------------------------------------------------------------------------
		public void Execute(int index)
		{
			var pos = Positions[index].Value;
			var rot = Rotations[index].Value;
			var vel = Velocity[index].Value;
			var speed = math.length(vel);
			pos += vel * TimeDelta;

			var gridIndex = GridUtilties.WorldToIndex(GridSettings, pos);
			var terrainHeight = (gridIndex < 0 ? pos.y : Heights[gridIndex]);
			var currUp = math.up(rot);
			var normal = gridIndex < 0 ? currUp : Normals[gridIndex];

			var targetHeight = terrainHeight + 3;
			pos.y = pos.y + (targetHeight - pos.y) * math.min(TimeDelta * (speed + 1), 1);

			// clamp position
			float minWorldX = -GridSettings.worldSize.x * .5f;
			float maxWorldX = -minWorldX;
			float minWorldZ = -GridSettings.worldSize.y * .5f;
			float maxWorldZ = -minWorldZ;
			
			if (pos.x < minWorldX)
				pos.x = minWorldX + 1;
			if (pos.x > maxWorldX)
				pos.x = maxWorldX - 1;
			if (pos.z < minWorldZ)
				pos.z = minWorldZ + 1;
			if (pos.z > maxWorldZ)
				pos.z = maxWorldZ - 1;

			var currDir = math.forward(rot);
			var normalDiff = normal - currUp;
			var newUp = math.normalize(currUp + normalDiff * math.min(TimeDelta * 10, 1));
			var newDir = currDir;
			if (speed > .1f)
			{
				var speedPer = (speed / SteerParams.MaxSpeed) * 1.75f;
				var desiredDir = math.normalize(vel);
				var dirDiff = desiredDir - currDir;
				newDir = math.normalize(currDir + dirDiff * math.min(TimeDelta * SteerParams.RotationSpeed * speedPer, 1));
			}
			rot = quaternion.lookRotation(newDir, newUp);
			Positions[index] = new Position(pos);
			Rotations[index] = new Rotation(rot);
		}
	}

}
