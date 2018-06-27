using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using RSGLib;

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
    static uint s_QueryHandle = 0;

    struct TileCostAndPositionGroup
    {
        [ReadOnly] public ComponentDataArray<Tile.Cost> cost;
        [ReadOnly] public ComponentDataArray<Tile.Position> position;
        public readonly int Length;
    }

    [Inject] TileCostAndPositionGroup m_TileCostAndPositionGroup;
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
        uint queryHandle = s_QueryHandle++;

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

        // Copy inputs
        var costsCopy = new NativeArray<Tile.Cost>(m_TileCostAndPositionGroup.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var copyTileCostsJobHandle = new CopyComponentData<Tile.Cost>
        {
            Source = m_TileCostAndPositionGroup.cost,
            Results = costsCopy
        }.Schedule(m_TileCostAndPositionGroup.Length, 64, inputDeps);

        var positionsCopy = new NativeArray<Tile.Position>(m_TileCostAndPositionGroup.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var copyTilePositionsJobHandle = new CopyComponentData<Tile.Position>
        {
            Source = m_TileCostAndPositionGroup.position,
            Results = positionsCopy
        }.Schedule(m_TileCostAndPositionGroup.Length, 64, inputDeps);

        var copyTileInputsBarrierHandle = JobHandle.CombineDependencies(copyTileCostsJobHandle, copyTilePositionsJobHandle);

        // Create & Initialize heatmap
        var heatmap = new NativeArray<int>(m_TileCostAndPositionGroup.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var initializeHeatmapJobHandle = new InitializeHeatmapJob()
        {
            settings = gridSettings,
            costs = costsCopy,
            positions = positionsCopy,
            heatmap = heatmap
        }.Schedule(m_TileCostAndPositionGroup.Length, 64, copyTileInputsBarrierHandle);

        // Compute heatmap from goals
        var goals = new NativeArray<int2>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        goals[0] = m_Goal;
        var computeHeatmapJobHandle = new ComputeHeatmapJob()
        {
            settings = gridSettings,
            goals = goals,
            heatmap = heatmap,
            offsets = m_Offsets,
            openSet = new NativeArray<int>(heatmap.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory)
        }.Schedule(initializeHeatmapJobHandle);

        // Convert flowfield from heatmap
        var flowField = new NativeArray<float3>(heatmap.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var computeFlowFieldJobHandle = new FlowField.ComputeFlowFieldJob
        {
            settings = gridSettings,
            heatmap = heatmap,
            flowfield = flowField,
            offsets = m_Offsets
        }.Schedule(heatmap.Length, 64, computeHeatmapJobHandle);

        m_PendingJobs.Add(new PendingJob
        {
            queryHandle = queryHandle,
            jobHandle = computeFlowFieldJobHandle,
            cacheEntry = new CacheEntry
            {
                heatmap = heatmap,
                flowField = flowField
            }
        });

        return computeFlowFieldJobHandle;
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

    const int k_Obstacle = int.MaxValue;

    const int k_Unvisited = k_Obstacle - 1;

    [BurstCompile]
    struct InitializeHeatmapJob : IJobParallelFor
    {
        [ReadOnly]
        public GridSettings settings;

        [ReadOnly, DeallocateOnJobCompletion]
        public NativeArray<Tile.Cost> costs;

        [ReadOnly, DeallocateOnJobCompletion]
        public NativeArray<Tile.Position> positions;

        [WriteOnly]
        public NativeArray<int> heatmap;

        public void Execute(int index)
        {
            var outputIndex = GridUtilties.Grid2Index(settings, positions[index].Value);
            heatmap[outputIndex] = math.select(k_Unvisited, k_Obstacle, costs[index].Value == byte.MaxValue);
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

        //[ReadOnly]
        //public NativeArray<int> values;

        public NativeArray<int> heatmap;

        [DeallocateOnJobCompletion]
        public NativeArray<int> openSet;

        int queueStart;
        int queueEnd;
        int queueLength;

        void Enqueue(NativeArray<int> queue, int value)
        {
            queue[queueEnd] = value;
            queueEnd = (queueEnd + 1) % queue.Length;
            ++queueLength;
        }

        int Dequeue(NativeArray<int> queue)
        {
            var retVal = queue[queueStart];
            queueStart = (queueStart + 1) % queue.Length;
            --queueLength;
            return retVal;
        }

        public void Execute()
        {
            for (int i = 0; i < goals.Length; ++i)
            {
                var tileIndex = GridUtilties.Grid2Index(settings, goals[i]);
                heatmap[tileIndex] = 0;//values[i];
                Enqueue(openSet, tileIndex);
            }

            // Search!
            while (queueLength > 0)
            {
                var index = Dequeue(openSet);
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
                        Enqueue(openSet, neighborIndex);
                    }
                }
            }
        }
    }
}
