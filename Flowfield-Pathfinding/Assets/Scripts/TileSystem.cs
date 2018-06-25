using UnityEngine;
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
    //public static EntityArchetype s_TileType;

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

        int numChunks = 1;
        var gridSettings = InitializationData.Instance.m_grid;
        var numTiles = gridSettings.cellCount.x * gridSettings.cellCount.y;
        var initializeJob = new InitializeHeatmapJob()
        {
            gridSettings = gridSettings,
            heatmap = new NativeArray<int>(
                numTiles, Allocator.Temp, NativeArrayOptions.UninitializedMemory)
        };
        var initializeHandle = initializeJob.Schedule(numTiles, 64, inputDeps);

        var heatmapJob = new ComputeHeatmapChunkJob()
        {
            gridSettings = gridSettings,
            chunkGoals = new NativeArray<NativeArray<int2>>(numChunks, Allocator.Temp),
            heatmaps = new NativeArray<NativeArray<int>>(numChunks, Allocator.Temp)
        };

        var goals = new NativeArray<int2>(1, Allocator.TempJob);
        goals[0] = GridUtilties.World2Grid(gridSettings, hit.point);
        heatmapJob.chunkGoals[0] = goals;
        heatmapJob.heatmaps[0] = initializeJob.heatmap;

        var heatmapHandle = heatmapJob.Schedule(numChunks, 8, initializeHandle);

        var flowFieldJob = new ComputeFlowFieldJob()
        {
            gridSettings = gridSettings,
            heatmaps = heatmapJob.heatmaps,
            flowFields = new NativeArray<NativeArray<float3>>(numChunks, Allocator.Temp)
        };

        m_FlowFieldHandle = flowFieldJob.Schedule(numChunks, 8, heatmapHandle);

        return m_FlowFieldHandle;
    }

    void LateUpdate()
    {
        m_FlowFieldHandle.Complete();
        // TODO: Do something with the flow field
    }

    JobHandle m_FlowFieldHandle;

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
        public GridSettings gridSettings;

        [ReadOnly, Inject]
        public ComponentDataArray<TileCost> tileCosts;

        [ReadOnly, Inject]
        public ComponentDataArray<TilePosition> tilePositions;

        [WriteOnly]
        public NativeArray<int> heatmap;

        public void Execute(int index)
        {
            var outputIndex = GridUtilties.Grid2Index(gridSettings, tilePositions[index].value);
            heatmap[outputIndex] = math.select(k_Obstacle, k_Unvisited, tileCosts[index].value == byte.MaxValue);
        }
    }

    [BurstCompile]
    struct ComputeHeatmapChunkJob : IJobParallelFor
    {
        [ReadOnly]
        public GridSettings gridSettings;

        [ReadOnly]
        public NativeArray<NativeArray<int2>> chunkGoals;

        public NativeArray<NativeArray<int>> heatmaps;

        public void Execute(int chunkIndex)
        {
            var goals = chunkGoals[chunkIndex];
            var heatmap = heatmaps[chunkIndex];

            var openSet = new NativeQueue<int>(Allocator.Temp);
            for (int i = 0; i < goals.Length; ++i)
            {
                var tileIndex = GridUtilties.Grid2Index(gridSettings, goals[i]);
                heatmap[tileIndex] = 0;
                openSet.Enqueue(tileIndex);
            }

            // Search!
            while (openSet.Count > 0)
            {
                var index = openSet.Dequeue();
                var distance = heatmap[index];
                var newDistance = distance + 1;
                var grid = GridUtilties.Index2Grid(gridSettings, index);

                VisitNeighbor(gridSettings, heatmap, openSet, grid, GridUtilties.Direction.N, newDistance);
                VisitNeighbor(gridSettings, heatmap, openSet, grid, GridUtilties.Direction.S, newDistance);
                VisitNeighbor(gridSettings, heatmap, openSet, grid, GridUtilties.Direction.E, newDistance);
                VisitNeighbor(gridSettings, heatmap, openSet, grid, GridUtilties.Direction.W, newDistance);
            }

            openSet.Dispose();
        }
    }

    [BurstCompile]
    struct ComputeFlowFieldJob : IJobParallelFor
    {
        [ReadOnly]
        public GridSettings gridSettings;

        [ReadOnly, DeallocateOnJobCompletion]
        public NativeArray<NativeArray<int>> heatmaps;

        [WriteOnly, Inject]
        public ComponentDataArray<TileDirection> tileDirections;

        public void Execute(int index)
        {
            var heatmap = heatmaps[index];
            flowFields[index] = HeatmapToFlowField.Compute(gridSettings, heatmap);
        }
    }

    //public static NativeArray<int> ComputeHeatmap(int width, int height, NativeArray<byte> costs, int2 goal)
    //{
    //    var n = width * height;
    //    var heatmap = new NativeArray<int>(n, Allocator.Persistent);
    //    var maxValue = int.MaxValue;
    //    var maxTraversable = maxValue - 1;
    //    for (int i = 0; i < n; ++i)
    //    {
    //        heatmap[i] = (byte)math.select(maxValue, maxTraversable, costs[i] == byte.MaxValue);
    //    }
    //    heatmap[GetTileIndex(width, goal)] = 0;

    //    // Search!
    //    var openSet = new NativeQueue<int>(Allocator.Temp);
    //    openSet.Enqueue(GetTileIndex(width, goal));
    //    while (openSet.Count > 0)
    //    {
    //        var index = openSet.Dequeue();
    //        var distance = heatmap[index];

    //        // Get neighbors
    //        var x = index % width;
    //        var y = index / width;
    //        var newDistance = distance + 1;

    //        if (x < width - 1) // right
    //            VisitNeighbor(heatmap, ref openSet, index + 1, newDistance);
    //        if (x > 0) // left
    //            VisitNeighbor(heatmap, ref openSet, index - 1, newDistance);
    //        if (y > 0) // up
    //            VisitNeighbor(heatmap, ref openSet, index - width, newDistance);
    //        if (y < height - 1) // down
    //            VisitNeighbor(heatmap, ref openSet, index + width, newDistance);
    //    }

    //    return heatmap;
    //}
}
