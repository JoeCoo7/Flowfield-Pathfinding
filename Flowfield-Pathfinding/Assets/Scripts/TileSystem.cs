using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;

[System.Serializable]
public struct GridSettings : ISharedComponentData
{
    public float2 worldSize;
    public float2 cellSize;
    public int2 cellCount;
}

[UpdateInGroup(typeof(ProcessGroup))]
[UpdateAfter(typeof(AgentSystem))]
public class TileSystem : JobComponentSystem
{
    static uint s_QueryHandle = uint.MaxValue;

    [Inject] EndFrameBarrier m_EndFrameBarrier;
    [Inject] Agent.Group.Selected m_Selected;
    [Inject] Agent.Group.SelectedWithQuery m_SelectedWithQuery;
    [Inject] ECSInput.InputDataGroup m_input;

    NativeArray<int2> m_Offsets;

    int2 m_Goal = new int2(197, 232);

    protected override void OnCreateManager(int capacity)
    {
        base.OnCreateManager(capacity);

        m_Offsets = new NativeArray<int2>(GridUtilties.Offset.Length, Allocator.Persistent);
        m_Offsets.CopyFrom(GridUtilties.Offset);
        lastGeneratedQueryHandle = s_QueryHandle;
    }

    protected override void OnDestroyManager()
    {
        m_Offsets.Dispose();
        foreach (var kvp in m_Cache)
        {
            var entry = kvp.Value;
            entry.heatmap.Dispose();
            entry.flowField.Dispose();
        }
        m_Cache.Clear();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        ProcessPendingJobs(m_EndFrameBarrier.CreateCommandBuffer());

        if (m_input.Buttons[0].Values["CreateGoal"].Status != ECSInput.InputButtons.UP)
            return inputDeps;

        if (!Physics.Raycast(Camera.main.ScreenPointToRay(m_input.MousePos[0].Value), out RaycastHit hit, Mathf.Infinity))
            return inputDeps;

        m_Goal = GridUtilties.World2Grid(Main.ActiveInitParams.m_grid, hit.point);

        return CreateJobs(inputDeps);
    }

    JobHandle CreateJobs(JobHandle inputDeps)
    {
        GridSettings gridSettings = Main.ActiveInitParams.m_grid;
        uint queryHandle = ++s_QueryHandle;

        for (var i = 0; i < m_SelectedWithQuery.entity.Length; ++i)
        {
            var query = m_SelectedWithQuery.flowFieldQuery[i];
            query.Handle = queryHandle;
            m_SelectedWithQuery.flowFieldQuery[i] = query;
        }

        var buffer = m_EndFrameBarrier.CreateCommandBuffer();
        var newQuery = new FlowField.Query { Handle = queryHandle };
        for (var i = 0; i < m_Selected.entity.Length; ++i)
            buffer.AddComponent(m_Selected.entity[i], newQuery);

        // Create & Initialize heatmap
        var heatmap = new NativeArray<int>(gridSettings.cellCount.x * gridSettings.cellCount.y,
            Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        var initializeHeatmapJobHandle = new InitializeHeatmapJob()
        {
            settings = gridSettings,
            heatmap = heatmap
        }.Schedule(this, 64, inputDeps);

        // Compute heatmap from goals
        var goals = new NativeArray<int2>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        goals[0] = m_Goal;
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
            jobHandle = smoothFlowFieldJobHandle,
            cacheEntry = new CacheEntry
            {
                heatmap = heatmap,
                flowField = flowField
            }
        });

        return smoothFlowFieldJobHandle;
    }

    void ProcessPendingJobs(EntityCommandBuffer commandBuffer)
    {
        for (int i = m_PendingJobs.Count - 1; i >= 0; --i)
        {
            if (m_PendingJobs[i].jobHandle.IsCompleted)
            {
                var queryHandle = m_PendingJobs[i].queryHandle;
                m_Cache.Add(queryHandle, m_PendingJobs[i].cacheEntry);

                Manager.Archetype.CreateFlowFieldResult(commandBuffer, queryHandle,
                    new FlowField.Data { Value = m_PendingJobs[i].cacheEntry.flowField });

                lastGeneratedQueryHandle = queryHandle;

                m_PendingJobs.RemoveAt(i);
            }
        }
    }

    struct CacheEntry
    {
        public NativeArray<int> heatmap;
        public NativeArray<float3> flowField;
    }

    struct PendingJob
    {
        public uint queryHandle;
        public JobHandle jobHandle;
        public CacheEntry cacheEntry;
    }

    List<PendingJob> m_PendingJobs = new List<PendingJob>();

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
                heatmap[tileIndex] = 0;
                queue.Enqueue(tileIndex);
            }

            // Search!
            while (queue.Length > 0)
            {
                var index = queue.Dequeue();
                var distance = heatmap[index];
                var newDistance = distance + 1;
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
}
