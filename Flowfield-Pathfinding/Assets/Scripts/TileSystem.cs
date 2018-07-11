using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using Agent;
using FlowField;
using Tile;
using Unity.Jobs.LowLevel.Unsafe;

//-----------------------------------------------------------------------------
[UpdateInGroup(typeof(ProcessGroup))]
[UpdateAfter(typeof(AgentSystem))]
public class TileSystem : JobComponentSystem
{
    //-----------------------------------------------------------------------------
    struct PendingJob
    {
        public int QueryHandle;
        public JobHandle JobHandle;
        public NativeArray<float3> FlowField;
        public NativeArray<float> DistanceMap;
    }
    
    //-----------------------------------------------------------------------------
    struct SmoothingParams
    {
        public float smoothAmount;
        public bool enableSmoothing;
        public bool hasChanged { get; private set; }
        private bool hasData;

        public void Update()
        {
            if (Main.Instance == null)
                return;

            var initParams = Main.ActiveInitParams;
            hasChanged = (enableSmoothing != initParams.m_smoothFlowField);
            hasChanged |= (Math.Abs(smoothAmount - initParams.m_smoothAmount) > float.Epsilon);
            hasChanged &= hasData;

            enableSmoothing = initParams.m_smoothFlowField;
            smoothAmount = initParams.m_smoothAmount;
            hasData = true;
        }
    }

    public const int k_MaxNumFlowFields = 10;
    public const int k_InvalidHandle = -1;
    public const int k_Obstacle = int.MaxValue;
    public const float k_ObstacleFloat = float.MaxValue;
    
	public Action<NativeArray<float>> OnNewDistanceMap;
    public NativeArray<float3> CachedFlowFields { get; private set; }
    public int LastGeneratedQueryHandle { get; private set; }
    public NativeArray<float> LastGeneratedDistanceMap { get; private set; }

    private static int s_QueryHandle = k_InvalidHandle;
    private int m_FlowFieldLength;
    private int2 m_Goal = new int2(197, 232);

    [Inject] ECSInput.InputDataGroup m_Input;
    [Inject] AgentSystem m_AgentSystem;
    private NativeArray<int2> m_Offsets;
    private List<PendingJob> m_PendingJobs = new List<PendingJob>();
    private SmoothingParams m_SmoothingParams;

    
    //-----------------------------------------------------------------------------
    protected override void OnCreateManager(int capacity)
    {
        base.OnCreateManager(capacity);

        m_Offsets = new NativeArray<int2>(GridUtilties.Offset.Length, Allocator.Persistent);
        m_Offsets.CopyFrom(GridUtilties.Offset);
        LastGeneratedQueryHandle = -1;
        m_SmoothingParams.Update();
    }

    //-----------------------------------------------------------------------------
    protected override void OnDestroyManager()
    {
        m_Offsets.Dispose();

        if (LastGeneratedDistanceMap.IsCreated)
            LastGeneratedDistanceMap.Dispose();

        if (CachedFlowFields.IsCreated)
            CachedFlowFields.Dispose();
    }

    //-----------------------------------------------------------------------------
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        m_SmoothingParams.Update();

        JobHandle updateAgentsJobHandle;
        var wasJobScheduled = ProcessPendingJobs(inputDeps, out updateAgentsJobHandle);

        if (!m_SmoothingParams.hasChanged)
        {
            if (m_Input.Buttons[0].Values["CreateGoal"].Status != ECSInput.InputButtons.UP)
                return wasJobScheduled ? updateAgentsJobHandle : inputDeps;

            if (!Physics.Raycast(Camera.main.ScreenPointToRay(m_Input.MousePos[0].Value), out RaycastHit hit, Mathf.Infinity))
                return wasJobScheduled ? updateAgentsJobHandle : inputDeps;

            m_Goal = GridUtilties.World2Grid(Main.ActiveInitParams.m_grid, hit.point);
        }
        return wasJobScheduled ? CreateJobs(updateAgentsJobHandle) : CreateJobs(inputDeps);
    }

    //-----------------------------------------------------------------------------
    JobHandle CreateJobs(JobHandle inputDeps)
    {
        GridSettings gridSettings = Main.ActiveInitParams.m_grid;
        if (s_QueryHandle == -1)
        {
            m_FlowFieldLength = gridSettings.cellCount.x * gridSettings.cellCount.y;
            CachedFlowFields = new NativeArray<float3>(m_FlowFieldLength * k_MaxNumFlowFields, Allocator.Persistent);
            s_QueryHandle = 0;
        }

        // Ensure goal is on the map
        m_Goal = math.clamp(m_Goal, new int2(0, 0), gridSettings.cellCount - new int2(1, 1));
        var goals = CalculateGoals(gridSettings, out int numGoals);

        int queryHandle = s_QueryHandle;
        s_QueryHandle = (s_QueryHandle + 1) % k_MaxNumFlowFields;

        var updateAgentsTargetGoalJobHandle = new UpdateAgentsTargetGoalJob
        {
            Goal = queryHandle,
            Position = m_Goal,
            Size = numGoals/2
        }.Schedule(this, inputDeps); 

        // Create & Initialize distance map
        var distanceMap = new NativeArray<float>(m_FlowFieldLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var costs = new NativeArray<int>(m_FlowFieldLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var floodQueue = new NativeArray<int>(distanceMap.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var flowField = new NativeArray<float3>(distanceMap.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        var initializeDistanceMapJobHandle = new InitializeHeatmapJob
        {
            Settings = gridSettings,
            Heatmap = distanceMap,
            Costs = costs
        }.Schedule(this, 64, inputDeps);

        JobHandle computeFlowFieldJobHandle;
        if (Main.Instance.m_InitData.m_eikonalFim)
        {
            var computeDistanceMap = new ComputeEikonalFimDistanceJob
            {
                Settings = gridSettings,
                Goals = goals,
                NumGoals = numGoals,
                Costs = costs,
                StateMap = new NativeArray<ComputeEikonalFimDistanceJob.States>(distanceMap.Length, Allocator.TempJob),
                Neighbours = new NativeArray<int>(8, Allocator.TempJob),
                EikonalNeighbours = new NativeArray<float>(4, Allocator.TempJob),
                FloodQueue = floodQueue,
                DistanceMap = distanceMap,

            }.Schedule(initializeDistanceMapJobHandle);
            
            computeFlowFieldJobHandle = new ComputeFimEikonalFlowFieldJob
            {
                Settings = gridSettings,
                DistanceMap = distanceMap,
                Flowfield = flowField,
                Offsets = m_Offsets
            }.Schedule(distanceMap.Length, 64, computeDistanceMap);
        }
        else
        {
            var computeDistanceMap = new ComputeDijkstraDistanceJob
            {
                NumGoals = numGoals,
                DistanceMap = distanceMap,
                FloodQueue = floodQueue,
                Goals = goals,
                Settings = gridSettings,
                Offsets = m_Offsets,
           }.Schedule(initializeDistanceMapJobHandle);
            
            computeFlowFieldJobHandle = new ComputeDijkstraFlowFieldJob
            {
                Settings = gridSettings,
                DistanceMap = distanceMap,
                Flowfield = flowField,
                Offsets = m_Offsets
            }.Schedule(distanceMap.Length, 64, computeDistanceMap);
        }

        JobHandle smoothFlowFieldJobHandle;
        if (Main.Instance.m_InitData.m_smoothFlowField)
        {
            smoothFlowFieldJobHandle = new SmoothFlowFieldJob
            {
                Settings = gridSettings,
                Offsets = m_Offsets,
                FloodQueue = floodQueue,
                DistanceMap = distanceMap,
                Flowfield = flowField,
                Costs = costs,
            }.Schedule(distanceMap.Length, 64, computeFlowFieldJobHandle);
        }
        else
        {
            smoothFlowFieldJobHandle = new SmoothFlowFieldJob.SmoothFlowFieldDeallocationJob
            {
                FloodQueue = floodQueue,
                Costs = costs,
                
            }.Schedule(computeFlowFieldJobHandle);
        }
            
        m_PendingJobs.Add(new PendingJob
        {
            QueryHandle = queryHandle,
            JobHandle = smoothFlowFieldJobHandle,
            FlowField = flowField,
            DistanceMap = distanceMap,
        });

        return JobHandle.CombineDependencies(initializeDistanceMapJobHandle, updateAgentsTargetGoalJobHandle);
    }

    //-----------------------------------------------------------------------------
    private NativeArray<int2> CalculateGoals(GridSettings _settings, out int _length)
    {
        var numAgents = m_AgentSystem.NumAgents;
        var radius = (int)( math.log(numAgents) * Main.ActiveInitParams.m_goalAgentFactor);
        var goalMin = math.max(new int2(m_Goal.x - radius, m_Goal.y - radius), new int2(0, 0));
        var goalMax = math.min(new int2(m_Goal.x + radius, m_Goal.y + radius), _settings.cellCount - new int2(1, 1));
        var dims = goalMax - goalMin;
        var maxNumGoals = math.max(1, dims.x * dims.y);
        var goals = new NativeArray<int2>(maxNumGoals, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        var goalIndex = 0;
        var radiusAsFloat = (float)radius;
        for (int x = goalMin.x; x < goalMax.x; ++x)
        {
            for (int y = goalMin.y; y < goalMax.y; ++y)
            {
                var p = new int2(x, y);
                if (math.distance(p, m_Goal) <= radiusAsFloat)
                    goals[goalIndex++] = p;
            }
        }

        if (goalIndex == 0)
            goals[goalIndex++] = m_Goal;

        _length = goalIndex;
        
        return goals;
    }
    
    //-----------------------------------------------------------------------------
    private bool ProcessPendingJobs(JobHandle inputDeps, out JobHandle updateAgentsJobHandle)
    {
        var availableGoals = new NativeArray<int>(m_PendingJobs.Count, Allocator.TempJob);
        var numAvailableGoals = 0;
		var newHeatMap = false;
        for (int i = m_PendingJobs.Count - 1; i >= 0; --i)
        {
            var pendingJob = m_PendingJobs[i];
            if (pendingJob.JobHandle.IsCompleted)
            {
                pendingJob.JobHandle.Complete();

                var queryHandle = pendingJob.QueryHandle;
                var flowField = pendingJob.FlowField;
                var heatmap = pendingJob.DistanceMap;

                var offset = flowField.Length * queryHandle;
                CachedFlowFields.Slice(offset, flowField.Length).CopyFrom(flowField);
                flowField.Dispose();

                if (LastGeneratedDistanceMap.IsCreated)
                    LastGeneratedDistanceMap.Dispose();

                LastGeneratedDistanceMap = heatmap;
				newHeatMap = true;
				availableGoals[numAvailableGoals++] = pendingJob.QueryHandle;

                LastGeneratedQueryHandle = queryHandle;
                m_PendingJobs.RemoveAt(i);
            }
        }

        if (numAvailableGoals == 0)
        {
            availableGoals.Dispose();
            updateAgentsJobHandle = new JobHandle();
            return false;
        }

		if (newHeatMap && OnNewDistanceMap != null)
			OnNewDistanceMap(LastGeneratedDistanceMap);


		updateAgentsJobHandle = new UpdateAgentsCurrentGoalJob
        {
            AvailableGoals = availableGoals,
            NumAvailableGoals = numAvailableGoals
        }.Schedule(this, inputDeps);
        return true;
    }


    //-----------------------------------------------------------------------------
    public NativeArray<float3> GetFlowFieldCopy(int handle, Allocator allocator)
    {
        if (handle == -1 || handle >= k_MaxNumFlowFields)
            return new NativeArray<float3>(0, allocator);

        var copy = new NativeArray<float3>(m_FlowFieldLength, allocator);
        CachedFlowFields.Slice(m_FlowFieldLength * handle, m_FlowFieldLength).CopyTo(copy);
        return copy;
    }
    
    //-----------------------------------------------------------------------------
    [BurstCompile]
    private  struct InitializeHeatmapJob : IJobProcessComponentData<Cost, Position>
    {
        [ReadOnly] public GridSettings Settings;
        [WriteOnly] public NativeArray<float> Heatmap;
        [WriteOnly] public NativeArray<int> Costs;

        public void Execute([ReadOnly] ref Cost cost, [ReadOnly] ref Position position)
        {
            var outputIndex = GridUtilties.Grid2Index(Settings, position.Value);
            Heatmap[outputIndex] = math.select(k_Obstacle, k_ObstacleFloat, cost.Value==byte.MaxValue);
            Costs[outputIndex] = cost.Value;
        }
    }

    //-----------------------------------------------------------------------------
    [BurstCompile]
    struct UpdateAgentsTargetGoalJob : IJobProcessComponentData<Selection, Goal>
    {
        public int Goal;
        public int2 Position;
        public int Size;

        //-----------------------------------------------------------------------------
        public void Execute([ReadOnly] ref Selection selectionFlag, ref Goal goal)
        {
            goal.Target = math.select(Goal, goal.Target, selectionFlag.Value == 0);
            goal.Position = Position;
            goal.Size = Size;
        }
    }

    //-----------------------------------------------------------------------------
    [BurstCompile]
    struct UpdateAgentsCurrentGoalJob : IJobProcessComponentData<Goal>
    {
        [ReadOnly, DeallocateOnJobCompletion] public NativeArray<int> AvailableGoals;
        public int NumAvailableGoals;

        //-----------------------------------------------------------------------------
        public void Execute(ref Goal agentGoal)
        {
            for (int i = 0; i < NumAvailableGoals; ++i)
            {
                var newGoal = AvailableGoals[i];
                var targetEqualsNewGoal = (agentGoal.Target == newGoal);

                // If the current goal is equal to the new goal, then invalid the current goal
                agentGoal.Current = math.select(agentGoal.Current, k_InvalidHandle, agentGoal.Current == newGoal);

                // If the target is equal to the new goal, then set current to the new goal
                agentGoal.Current = math.select(agentGoal.Current, agentGoal.Target, targetEqualsNewGoal);

                // If the target is equal to the new goal, then invalidate the target
                agentGoal.Target = math.select(agentGoal.Target, k_InvalidHandle, targetEqualsNewGoal);
            }
        }
    }
}
