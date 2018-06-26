﻿using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Jobs;
using RSGLib;

[System.Serializable]
public struct GridSettings : ISharedComponentData
{
    public float2 worldSize;
    public int2 cellCount;
    public int2 cellsPerBlock;
    public int2 blockCount;
}

public struct TileCost : IComponentData
{
    public byte value;
}

public struct TileDirection : IComponentData
{
    public float3 value;
}

public struct TileCollision : IComponentData
{
    public float3 value;
}

public struct TilePosition : IComponentData
{
    public int2 value;
}

public class TileSystem : JobComponentSystem
{
    static uint s_QueryHandle = 0;

    [Inject]
    EndFrameBarrier endFrame;

    struct SelectedUnits
    {
        public EntityArray entities;
        [ReadOnly]
        SharedComponentDataArray<FlowFieldData> flowField;
        SubtractiveComponent<FlowFieldQuery> flowFieldQuery;
        //ComponentDataArray<SelectedUnit> selected;
    }

    struct SelectedUnitsWithQuery
    {
        public EntityArray entities;
        [ReadOnly]
        SharedComponentDataArray<FlowFieldData> flowField;
        ComponentDataArray<FlowFieldQuery> flowFieldQuery;
        //ComponentDataArray<SelectedUnit> selected;
    }

    [Inject]
    SelectedUnits selectedUnits;

    [Inject]
    SelectedUnits selectedUnitsWithQuery;

    struct Tile
    {
        public SharedComponentDataArray<GridSettings> Grid;
        public ComponentDataArray<TileCost> cost;
        public ComponentDataArray<TileCollision> collisionDirection;
        public ComponentDataArray<TilePosition> position;
        public readonly int length;
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (!Input.GetMouseButtonDown(StandardInput.LEFT_MOUSE_BUTTON))
            return inputDeps;

        if (!Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, Mathf.Infinity))
            return inputDeps;

        uint queryHandle = s_QueryHandle++;
        GridSettings gridSettings = InitializationData.Instance.m_grid;
        int numTiles = gridSettings.cellCount.x * gridSettings.cellCount.y;

        var buffer = endFrame.CreateCommandBuffer();
        var query = new FlowFieldQuery { handle = queryHandle };
        for (var i = 0; i < selectedUnits.entities.Length; ++i)
            buffer.AddComponent(selectedUnits.entities[i], query);
        for (var i = 0; i < selectedUnitsWithQuery.entities.Length; ++i)
            buffer.SetComponent(selectedUnitsWithQuery.entities[i], query);

        // Create & Initialize heatmap
        var initializeJob = new InitializeHeatmapJob()
        {
            settings = gridSettings,
            heatmap = new NativeArray<int>(numTiles, Allocator.Temp, NativeArrayOptions.UninitializedMemory)
        };

        // Compute heatmap from goals
        var heatmapJob = new ComputeHeatmapJob()
        {
            settings = gridSettings,
            goals = new NativeArray<int2>(1, Allocator.TempJob),
            heatmap = initializeJob.heatmap
        };
        heatmapJob.goals[0] = GridUtilties.World2Grid(gridSettings, hit.point);

        // Convert flowfield from heatmap
        var flowFieldJob = new FlowField.ComputeFlowFieldJob
        {
            settings = gridSettings,
            heatmap = heatmapJob.heatmap,
            flowfield = new NativeArray<float3>(numTiles, Allocator.Persistent, NativeArrayOptions.UninitializedMemory)
        };

        var createResultJob = new CreateFlowFieldResultEntity
        {
            commandBuffer = buffer,
            handle = queryHandle,
            flowField = flowFieldJob.flowfield
        };

        // Create all the jobs
        var initializeHandle = initializeJob.Schedule(numTiles, 64, inputDeps);
        var heatmapHandle = heatmapJob.Schedule(initializeHandle);
        var flowFieldHandle = flowFieldJob.Schedule(numTiles, 64, heatmapHandle);
        var createResultHandle = createResultJob.Schedule(flowFieldHandle);
        return createResultHandle;
    }

    const int k_Obstacle = int.MaxValue;

    const int k_Unvisited = k_Obstacle - 1;

    static void VisitNeighbor(GridSettings gridSettings, NativeArray<int> heatmap,
        NativeQueue<int> openSet, int2 grid, GridUtilties.Direction direction, int newDistance)
    {
        var neighborGrid = grid + GridUtilties.Offset[(int)direction];
        var neighborIndex = GridUtilties.Grid2Index(gridSettings, neighborGrid);

        if (heatmap[neighborIndex] != k_Obstacle && newDistance < heatmap[neighborIndex])
        {
            heatmap[neighborIndex] = newDistance;
            openSet.Enqueue(neighborIndex);
        }
    }

    [BurstCompile]
    struct InitializeHeatmapJob : IJobParallelFor
    {
        [ReadOnly]
        public GridSettings settings;

        struct TileData
        {
            public ComponentDataArray<TileCost> costs;
            public ComponentDataArray<TilePosition> positions;
        }

        [ReadOnly, Inject]
        TileData tileData;

        [WriteOnly]
        public NativeArray<int> heatmap;

        public void Execute(int index)
        {
            var outputIndex = GridUtilties.Grid2Index(settings, tileData.positions[index].value);
            heatmap[outputIndex] = math.select(k_Obstacle, k_Unvisited, tileData.costs[index].value == byte.MaxValue);
        }
    }

    [BurstCompile]
    struct ComputeHeatmapJob : IJob
    {
        [ReadOnly]
        public GridSettings settings;

        [ReadOnly]
        public NativeArray<int2> goals;

        //[ReadOnly]
        //public NativeArray<int> values;

        [WriteOnly]
        public NativeArray<int> heatmap;

        public void Execute()
        {
            var openSet = new NativeQueue<int>(Allocator.Temp);

            for (int i = 0; i < goals.Length; ++i)
            {
                var tileIndex = GridUtilties.Grid2Index(settings, goals[i]);
                heatmap[tileIndex] = 0;//values[i];
                openSet.Enqueue(tileIndex);
            }

            // Search!
            while (openSet.Count > 0)
            {
                var index = openSet.Dequeue();
                var distance = heatmap[index];
                var newDistance = distance + 1;
                var grid = GridUtilties.Index2Grid(settings, index);

                for (GridUtilties.Direction dir = GridUtilties.Direction.N; dir <= GridUtilties.Direction.W; ++dir)
                    VisitNeighbor(settings, heatmap, openSet, grid, dir, newDistance);
            }

            openSet.Dispose();
        }
    }

    [BurstCompile]
    struct CreateFlowFieldResultEntity : IJob
    {
        [ReadOnly]
        public uint handle;

        [ReadOnly]
        public NativeArray<float3> flowField;

        public EntityCommandBuffer commandBuffer;

        public void Execute()
        {
            commandBuffer.CreateEntity(FlowField.FlowFieldQuerySystem.FlowFieldResultType);
            commandBuffer.SetComponent(new FlowField.FlowFieldResult { handle = handle });
            commandBuffer.SetSharedComponent(new FlowFieldData { value = flowField });
        }
    }
}
