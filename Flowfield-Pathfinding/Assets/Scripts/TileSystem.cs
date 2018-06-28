using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using Agent;
using UnityEngine.Assertions;

[System.Serializable]
public struct GridSettings : ISharedComponentData
{
    public float2 worldSize;
    public float2 cellSize;
    public int2 cellCount;
    public float heightScale;
}

[UpdateInGroup(typeof(ProcessGroup))]
[UpdateAfter(typeof(AgentSystem))]
public class TileSystem : JobComponentSystem
{
    public const int k_MaxNumFlowFields = 10;

    public const int k_InvalidHandle = -1;

    static int s_QueryHandle = k_InvalidHandle;

    [Inject] ECSInput.InputDataGroup m_input;

    [Inject] AgentSystem m_AgentSystem;

    NativeArray<int2> m_Offsets;

    int2 m_Goal = new int2(197, 232);

    protected override void OnCreateManager(int capacity)
    {
        base.OnCreateManager(capacity);

        m_Offsets = new NativeArray<int2>(GridUtilties.Offset.Length, Allocator.Persistent);
        m_Offsets.CopyFrom(GridUtilties.Offset);
        lastGeneratedQueryHandle = -1;
    }

    protected override void OnDestroyManager()
    {
        m_Offsets.Dispose();

        if (lastGeneratedHeatmap.IsCreated)
            lastGeneratedHeatmap.Dispose();

        if (cachedFlowFields.IsCreated)
            cachedFlowFields.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        JobHandle updateAgentsJobHandle;
        var wasJobScheduled = ProcessPendingJobs(inputDeps, out updateAgentsJobHandle);

        if (m_input.Buttons[0].Values["CreateGoal"].Status != ECSInput.InputButtons.UP)
            return wasJobScheduled ? updateAgentsJobHandle : inputDeps;

        if (!Physics.Raycast(Camera.main.ScreenPointToRay(m_input.MousePos[0].Value), out RaycastHit hit, Mathf.Infinity))
            return wasJobScheduled ? updateAgentsJobHandle : inputDeps;

        m_Goal = GridUtilties.World2Grid(Main.ActiveInitParams.m_grid, hit.point);

        var flowFieldJobHandle = CreateJobs(inputDeps);
        return wasJobScheduled ? JobHandle.CombineDependencies(updateAgentsJobHandle, flowFieldJobHandle) : flowFieldJobHandle;
    }

    JobHandle CreateJobs(JobHandle inputDeps)
    {
        GridSettings gridSettings = Main.ActiveInitParams.m_grid;
        if (s_QueryHandle == -1)
        {
            m_FlowFieldLength = gridSettings.cellCount.x * gridSettings.cellCount.y;
            cachedFlowFields = new NativeArray<float3>(m_FlowFieldLength * k_MaxNumFlowFields, Allocator.Persistent);
            s_QueryHandle = 0;
        }

        int queryHandle = s_QueryHandle;
        s_QueryHandle = (s_QueryHandle + 1) % k_MaxNumFlowFields;

        var updateAgentsTargetGoalJobHandle = new UpdateAgentsTargetGoalJob
        {
            newGoal = queryHandle
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
        var radius = (int) math.log(numAgents) * Main.ActiveInitParams.m_goalAgentFactor;
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
                    goals[goalIndex++] = new int2(x, y);
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

        var smoothFlowFieldJobHandle = new FlowField.SmoothFlowFieldJob
        {
            settings = gridSettings,
            flowfield = flowField,
            floodQueue = floodQueue,
            offsets = m_Offsets
        }.Schedule(computeFlowFieldJobHandle);

        m_PendingJobs.Add(new PendingJob
        {
            queryHandle = queryHandle,
            jobHandle = smoothFlowFieldJobHandle,
            flowField = flowField,
            heatmap = heatmap
        });

        return JobHandle.CombineDependencies(smoothFlowFieldJobHandle, updateAgentsTargetGoalJobHandle);
    }

    int m_FlowFieldLength;

    bool ProcessPendingJobs(JobHandle inputDeps, out JobHandle updateAgentsJobHandle)
    {
        var availableGoals = new NativeArray<int>(m_PendingJobs.Count, Allocator.TempJob);
        var numAvailableGoals = 0;

        for (int i = m_PendingJobs.Count - 1; i >= 0; --i)
        {
            var pendingJob = m_PendingJobs[i];
            if (pendingJob.jobHandle.IsCompleted)
            {
                var queryHandle = pendingJob.queryHandle;
                var flowField = pendingJob.flowField;
                var heatmap = pendingJob.heatmap;

                var offset = flowField.Length * queryHandle;
                cachedFlowFields.Slice(offset, flowField.Length).CopyFrom(flowField);
                flowField.Dispose();

                if (lastGeneratedHeatmap.IsCreated)
                    lastGeneratedHeatmap.Dispose();

                lastGeneratedHeatmap = heatmap;

                availableGoals[numAvailableGoals++] = pendingJob.queryHandle;

                lastGeneratedQueryHandle = queryHandle;
                m_PendingJobs.RemoveAt(i);
            }
        }

        if (numAvailableGoals == 0)
        {
            availableGoals.Dispose();
            updateAgentsJobHandle = new JobHandle();
            return false;
        }

        updateAgentsJobHandle = new UpdateAgentsCurrentGoalJob
        {
            availableGoals = availableGoals,
            numAvailableGoals = numAvailableGoals
        }.Schedule(this, inputDeps);
        return true;
    }

    struct PendingJob
    {
        public int queryHandle;
        public JobHandle jobHandle;
        public NativeArray<float3> flowField;
        public NativeArray<int> heatmap;
    }

    struct GoalFlowFieldPair
    {
        public int goal;
        public NativeArray<float3> flowField;
    }

    List<PendingJob> m_PendingJobs = new List<PendingJob>();

    public NativeArray<float3> cachedFlowFields { get; private set; }

    public int lastGeneratedQueryHandle { get; private set; }

    public NativeArray<int> lastGeneratedHeatmap { get; private set; }

    public NativeArray<float3> GetFlowFieldCopy(int handle, Allocator allocator)
    {
        if (handle == -1 || handle >= k_MaxNumFlowFields)
            return new NativeArray<float3>(0, allocator);

        var copy = new NativeArray<float3>(m_FlowFieldLength, allocator);
        cachedFlowFields.Slice(m_FlowFieldLength * handle, m_FlowFieldLength).CopyTo(copy);
        return copy;
    }

    const int k_Obstacle = int.MaxValue;

    const int k_Unvisited = k_Obstacle - 1;

    [BurstCompile]
    struct InitializeHeatmapJob : IJobProcessComponentData<Tile.Cost, Tile.Position>
    {
        [ReadOnly]
        public GridSettings settings;

        [WriteOnly]
        public NativeArray<int> heatmap;

        public void Execute([ReadOnly] ref Tile.Cost cost, [ReadOnly] ref Tile.Position position)
        {
            var outputIndex = GridUtilties.Grid2Index(settings, position.Value);
            heatmap[outputIndex] = math.select(k_Unvisited, k_Obstacle, cost.Value == byte.MaxValue);
        }
    }

    [BurstCompile]
    struct ComputeHeatmapJob : IJob
    {
        [ReadOnly]
        public GridSettings settings;

        [ReadOnly, DeallocateOnJobCompletion]
        public NativeArray<int2> goals;

        public int numGoals;

        [ReadOnly]
        public NativeArray<int2> offsets;

        public NativeArray<int> heatmap;

        public NativeArray<int> floodQueue;

        public void Execute()
        {
            BurstQueue queue = new BurstQueue(floodQueue);
            
            for (int i = 0; i < numGoals; ++i)
            {
                var tileIndex = GridUtilties.Grid2Index(settings, goals[i]);
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

    [BurstCompile]
    struct UpdateAgentsTargetGoalJob : IJobProcessComponentData<Selection, Goal>
    {
        public int newGoal;

        public void Execute([ReadOnly] ref Selection selectionFlag, ref Goal goal)
        {
            goal.Target = math.select(newGoal, goal.Target, selectionFlag.Value == 0);
        }
    }

    [BurstCompile]
    struct UpdateAgentsCurrentGoalJob : IJobProcessComponentData<Goal>
    {
        [ReadOnly, DeallocateOnJobCompletion]
        public NativeArray<int> availableGoals;

        public int numAvailableGoals;

        public void Execute(ref Goal agentGoal)
        {
            for (int i = 0; i < numAvailableGoals; ++i)
                agentGoal.Current = math.select(agentGoal.Current, agentGoal.Target, agentGoal.Target == availableGoals[i]);
        }
    }
}
