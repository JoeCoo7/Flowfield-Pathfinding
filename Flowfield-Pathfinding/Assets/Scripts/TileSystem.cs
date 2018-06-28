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
    public int2 cellsPerBlock;
    public int2 blockCount;
    public float agentSeparationWeight;
    public float agentAlignmentWeight;
    public float agentTargetFlowfieldWeight;
    public float agentTerrainFlowfieldWeight;
    public float agentRadius;
}

[UpdateInGroup(typeof(ProcessGroup))]
[UpdateAfter(typeof(AgentSystem))]
public class TileSystem : JobComponentSystem
{
    static uint s_QueryHandle = uint.MaxValue;

    public static readonly int2 k_InvalidGoal = new int2(-1, -1);

    public static bool IsGoalValid(int2 goal)
    {
        var result = (goal != k_InvalidGoal);
        return result.x && result.y;
    }

    [Inject] ECSInput.InputDataGroup m_input;

    [Inject] AgentSystem m_AgentSystem;

    NativeArray<int2> m_Offsets;

    int2 m_Goal = new int2(197, 232);

    protected override void OnCreateManager(int capacity)
    {
        base.OnCreateManager(capacity);

        m_Offsets = new NativeArray<int2>(GridUtilties.Offset.Length, Allocator.Persistent);
        m_Offsets.CopyFrom(GridUtilties.Offset);
        lastGeneratedQueryHandle = s_QueryHandle;
        cachedFlowFields = new NativeHashMap<int2, NativeArray<float3>>(16, Allocator.Persistent);
        m_CompletedFlowFields = new List<GoalFlowFieldPair>();
        latestFlowField = m_EmptyFlowField = new NativeArray<float3>(0, Allocator.Persistent);
    }

    NativeArray<float3> m_EmptyFlowField;

    protected override void OnDestroyManager()
    {
        m_Offsets.Dispose();
        foreach (var kvp in m_Cache)
        {
            var entry = kvp.Value;
            entry.heatmap.Dispose();
            entry.flowField.Dispose();
        }
        if (m_EmptyFlowField.IsCreated)
            m_EmptyFlowField.Dispose();

        cachedFlowFields.Dispose();
        m_Cache.Clear();
        m_CompletedFlowFields.Clear();
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
        uint queryHandle = ++s_QueryHandle;

        var updateAgentsTargetGoalJobHandle = new UpdateAgentsTargetGoalJob
        {
            newGoal = m_Goal
        }.Schedule(this, inputDeps);

        // Create & Initialize heatmap
        var heatmap = new NativeArray<int>(gridSettings.cellCount.x * gridSettings.cellCount.y,
            Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        var initializeHeatmapJobHandle = new InitializeHeatmapJob()
        {
            settings = gridSettings,
            heatmap = heatmap
        }.Schedule(this, 64, inputDeps);

        // Compute heatmap from goals
        var numAgents = m_AgentSystem.numAgents;
        var radius = numAgents / Main.ActiveInitParams.m_goalAgentFactor;
        var goalMin = math.max(new int2(m_Goal.x - radius, m_Goal.y - radius), new int2(0, 0));
        var goalMax = math.min(new int2(m_Goal.x + radius, m_Goal.y + radius), gridSettings.cellCount - new int2(1, 1));
        var dims = goalMax - goalMin;
        var maxNumGoals = math.max(1, dims.x * dims.y);
        var goals = new NativeArray<int2>(maxNumGoals, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        // TODO: Change to circle
        var goalIndex = 0;
        for (int x = goalMin.x; x < goalMax.x; ++x)
        {
            for (int y = goalMin.y; y < goalMax.y; ++y)
            {
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
            heatmap = heatmap,
            offsets = m_Offsets,
            floodQueue = floodQueue
        }.Schedule(initializeHeatmapJobHandle);

        var flowField = new NativeArray<float3>(heatmap.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
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
            goal = m_Goal,
            jobHandle = smoothFlowFieldJobHandle,
            cacheEntry = new CacheEntry
            {
                heatmap = heatmap,
                flowField = flowField
            }
        });

        return JobHandle.CombineDependencies(smoothFlowFieldJobHandle, updateAgentsTargetGoalJobHandle);
    }

    bool ProcessPendingJobs(JobHandle inputDeps, out JobHandle updateAgentsJobHandle)
    {
        var availableGoals = new NativeArray<int2>(m_PendingJobs.Count, Allocator.TempJob);
        var numAvailableGoals = 0;

        for (int i = m_PendingJobs.Count - 1; i >= 0; --i)
        {
            var pendingJob = m_PendingJobs[i];
            if (pendingJob.jobHandle.IsCompleted)
            {
                var queryHandle = pendingJob.queryHandle;
                m_Cache.Add(queryHandle, pendingJob.cacheEntry);

                m_CompletedFlowFields.Add(new GoalFlowFieldPair
                {
                    goal = pendingJob.goal,
                    flowField = pendingJob.cacheEntry.flowField
                });

                cachedFlowFields.Remove(pendingJob.goal);
                cachedFlowFields.TryAdd(pendingJob.goal, pendingJob.cacheEntry.flowField);
                availableGoals[numAvailableGoals++] = pendingJob.goal;

                lastGeneratedQueryHandle = queryHandle;
                m_PendingJobs.RemoveAt(i);

                latestFlowField = pendingJob.cacheEntry.flowField;
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

    struct CacheEntry
    {
        public NativeArray<int> heatmap;
        public NativeArray<float3> flowField;
    }

    struct PendingJob
    {
        public uint queryHandle;
        public int2 goal;
        public JobHandle jobHandle;
        public CacheEntry cacheEntry;
    }

    struct GoalFlowFieldPair
    {
        public int2 goal;
        public NativeArray<float3> flowField;
    }

    List<GoalFlowFieldPair> m_CompletedFlowFields;

    List<PendingJob> m_PendingJobs = new List<PendingJob>();

    public NativeHashMap<int2, NativeArray<float3>> cachedFlowFields { get; private set; }

    public NativeArray<float3> latestFlowField { get; private set; }

    public NativeHashMap<int2, NativeArray<float3>> CopyFlowFieldCache(Allocator allocator)
    {
        var copy = new NativeHashMap<int2, NativeArray<float3>>(m_CompletedFlowFields.Count, allocator);
        foreach (var pair in m_CompletedFlowFields)
        {
            copy.Remove(pair.goal);
            copy.TryAdd(pair.goal, pair.flowField);
        }
        return copy;
    }

    Dictionary<uint, CacheEntry> m_Cache = new Dictionary<uint, CacheEntry>();

    public uint lastGeneratedQueryHandle { get; private set; }

    public NativeArray<float3> GetFlowField(uint handle)
    {
        if (m_Cache.TryGetValue(handle, out CacheEntry cacheEntry))
            return cacheEntry.flowField;

        return new NativeArray<float3>();
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

        [ReadOnly]
        public NativeArray<int2> offsets;

        public NativeArray<int> heatmap;

        public NativeArray<int> floodQueue;

        public void Execute()
        {
            BurstQueue queue = new BurstQueue(floodQueue);
            
            for (int i = 0; i < goals.Length; ++i)
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
        public int2 newGoal;

        public void Execute([ReadOnly] ref Selection selectionFlag, ref Goal goal)
        {
            goal.Target = math.select(newGoal, goal.Target, selectionFlag.Value == 0);
        }
    }

    [BurstCompile]
    struct UpdateAgentsCurrentGoalJob : IJobProcessComponentData<Goal>
    {
        [ReadOnly, DeallocateOnJobCompletion]
        public NativeArray<int2> availableGoals;

        public int numAvailableGoals;

        public void Execute(ref Goal agentGoal)
        {
            for (int i = 0; i < numAvailableGoals; ++i)
            {
                var result = agentGoal.Target == availableGoals[i];
                agentGoal.Current = math.select(agentGoal.Current, agentGoal.Target, result.x && result.y);
            }
        }
    }
}
