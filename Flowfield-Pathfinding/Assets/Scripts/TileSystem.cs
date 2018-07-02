using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using Agent;

//-----------------------------------------------------------------------------
[Serializable]
public struct GridSettings : ISharedComponentData
{
    public float2 worldSize;
    public float2 cellSize;
    public int2 cellCount;
    public float heightScale;
}

//-----------------------------------------------------------------------------
[UpdateInGroup(typeof(ProcessGroup))]
[UpdateAfter(typeof(AgentSystem))]
public class TileSystem : JobComponentSystem
{
    //-----------------------------------------------------------------------------
    struct PendingJob
    {
        public int queryHandle;
        public JobHandle jobHandle;
        public NativeArray<float3> flowField;
        public NativeArray<int> heatmap;
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
    private const int k_Obstacle = int.MaxValue;
    private const int k_Unvisited = k_Obstacle - 1;
    
	public System.Action<NativeArray<int>> OnNewHeatMap;
    public NativeArray<float3> CachedFlowFields { get; private set; }
    public int LastGeneratedQueryHandle { get; private set; }
    public NativeArray<int> LastGeneratedHeatmap { get; private set; }

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

        if (LastGeneratedHeatmap.IsCreated)
            LastGeneratedHeatmap.Dispose();

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

        int queryHandle = s_QueryHandle;
        s_QueryHandle = (s_QueryHandle + 1) % k_MaxNumFlowFields;

        var updateAgentsTargetGoalJobHandle = new UpdateAgentsTargetGoalJob
        {
            NewGoal = queryHandle
        }.Schedule(this, inputDeps);

        // Create & Initialize heatmap
        var heatmap = new NativeArray<int>(m_FlowFieldLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        var initializeHeatmapJobHandle = new InitializeHeatmapJob()
        {
            settings = gridSettings,
            heatmap = heatmap
        }.Schedule(this, 64, inputDeps);

        // Compute heatmap from goals
        var numAgents = m_AgentSystem.numAgents;
        var radius = (int)( math.log(numAgents) * Main.ActiveInitParams.m_goalAgentFactor);
        var goalMin = math.max(new int2(m_Goal.x - radius, m_Goal.y - radius), new int2(0, 0));
        var goalMax = math.min(new int2(m_Goal.x + radius, m_Goal.y + radius), gridSettings.cellCount - new int2(1, 1));
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

        var floodQueue = new NativeArray<int>(heatmap.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var computeHeatmapJobHandle = new ComputeHeatmapJob()
        {
            settings = gridSettings,
            goals = goals,
            numGoals = goalIndex,
            heatmap = heatmap,
            offsets = m_Offsets,
            floodQueue = floodQueue
        }.Schedule(initializeHeatmapJobHandle);

        var flowField = new NativeArray<float3>(heatmap.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var computeFlowFieldJobHandle = new FlowField.ComputeFlowFieldJob
        {
            settings = gridSettings,
            heatmap = heatmap,
            flowfield = flowField,
            offsets = m_Offsets
        }.Schedule(heatmap.Length, 64, computeHeatmapJobHandle);

        JobHandle finalJobHandle;
        if (Main.ActiveInitParams.m_smoothFlowField)
        {
            finalJobHandle = new FlowField.SmoothFlowFieldJob
            {
                Settings = gridSettings,
                Flowfield = flowField,
                FloodQueue = floodQueue,
                Offsets = m_Offsets,
                SmoothAmount = Main.ActiveInitParams.m_smoothAmount
            }.Schedule(computeFlowFieldJobHandle);
        }
        else
        {
            finalJobHandle = new DeallocFloodQueue
            {
                floodQueue = floodQueue
            }.Schedule(computeFlowFieldJobHandle);
        }

        m_PendingJobs.Add(new PendingJob
        {
            queryHandle = queryHandle,
            jobHandle = finalJobHandle,
            flowField = flowField,
            heatmap = heatmap
        });

        return JobHandle.CombineDependencies(initializeHeatmapJobHandle, updateAgentsTargetGoalJobHandle);
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
            if (pendingJob.jobHandle.IsCompleted)
            {
                pendingJob.jobHandle.Complete();

                var queryHandle = pendingJob.queryHandle;
                var flowField = pendingJob.flowField;
                var heatmap = pendingJob.heatmap;

                var offset = flowField.Length * queryHandle;
                CachedFlowFields.Slice(offset, flowField.Length).CopyFrom(flowField);
                flowField.Dispose();

                if (LastGeneratedHeatmap.IsCreated)
                    LastGeneratedHeatmap.Dispose();

                LastGeneratedHeatmap = heatmap;
				newHeatMap = true;
				availableGoals[numAvailableGoals++] = pendingJob.queryHandle;

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

		if (newHeatMap && OnNewHeatMap != null)
			OnNewHeatMap(LastGeneratedHeatmap);


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
    private struct DeallocFloodQueue : IJob
    {
        [ReadOnly, DeallocateOnJobCompletion]
        public NativeArray<int> floodQueue;
        public void Execute()
        {
            // This page intentionally left blank.
            // is this just for deallocating floodqueue 
        }
    }

    //-----------------------------------------------------------------------------
    [BurstCompile]
    private  struct InitializeHeatmapJob : IJobProcessComponentData<Tile.Cost, Tile.Position>
    {
        [ReadOnly] public GridSettings settings;
        [WriteOnly] public NativeArray<int> heatmap;

        public void Execute([ReadOnly] ref Tile.Cost cost, [ReadOnly] ref Tile.Position position)
        {
            var outputIndex = GridUtilties.Grid2Index(settings, position.Value);
            heatmap[outputIndex] = math.select(k_Unvisited, k_Obstacle, cost.Value == byte.MaxValue);
        }
    }

    //-----------------------------------------------------------------------------
    [BurstCompile]
    private struct ComputeHeatmapJob : IJob
    {
        [ReadOnly] public GridSettings settings;
        [ReadOnly, DeallocateOnJobCompletion] public NativeArray<int2> goals;
        [ReadOnly] public NativeArray<int2> offsets;

        public int numGoals;
        public NativeArray<int> heatmap;
        public NativeArray<int> floodQueue;

        //-----------------------------------------------------------------------------
        public void Execute()
        {
            BurstQueue queue = new BurstQueue(floodQueue);
            for (int index = 0; index < numGoals; ++index)
            {
                var tileIndex = GridUtilties.Grid2Index(settings, goals[index]);
                if (heatmap[tileIndex] != k_Obstacle)
                {
                    heatmap[tileIndex] = 0;
                    queue.Enqueue(tileIndex);
                }
            }

            // Search!
            while (queue.Length > 0)
            {
                var index = queue.Dequeue();
                var newDistance = heatmap[index] + 1;
                var grid = GridUtilties.Index2Grid(settings, index);

                for (GridUtilties.Direction dir = GridUtilties.Direction.N; dir <= GridUtilties.Direction.W; ++dir)
                {
                    var neighborGrid = grid + offsets[(int)dir];
                    var neighborIndex = GridUtilties.Grid2Index(settings, neighborGrid);

                    if (neighborIndex != -1 && heatmap[neighborIndex] != k_Obstacle && newDistance < heatmap[neighborIndex])
                    {
                        heatmap[neighborIndex] = newDistance;
                        queue.Enqueue(neighborIndex);
                    }
                }
            }
        }
    }

    //-----------------------------------------------------------------------------
    [BurstCompile]
    struct UpdateAgentsTargetGoalJob : IJobProcessComponentData<Selection, Goal>
    {
        public int NewGoal;

        //-----------------------------------------------------------------------------
        public void Execute([ReadOnly] ref Selection selectionFlag, ref Goal goal)
        {
            goal.Target = math.select(NewGoal, goal.Target, selectionFlag.Value == 0);
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
