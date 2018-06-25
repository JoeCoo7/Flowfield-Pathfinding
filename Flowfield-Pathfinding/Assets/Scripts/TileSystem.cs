using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using System.ComponentModel;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Jobs;

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
    public byte value;
}

public struct TileCollision : IComponentData
{
    public byte value;
}

public class TileSystem : ComponentSystem
{
    public static EntityArchetype s_TileType;

    ComponentGroup m_CostGroup;

    struct Tile
    {
		public SharedComponentDataArray<GridSettings> Grid;
		public ComponentDataArray<TileCost> cost;
        public ComponentDataArray<TileDirection> flowDirection;
        public ComponentDataArray<TileCollision> collisionDirection;
        public readonly int length;
    }

    protected override void OnUpdate()
    {

    }

    void Initialize()
    {
        m_CostGroup = GetComponentGroup(typeof(TileCost));
    }

    static int GetTileIndex(int width, int2 position)
    {
        return position.x + position.y * width;
    }

    const int s_Obstacle = int.MaxValue;
    const int s_Unvisited = s_Obstacle - 1;

    static void VisitNeighbor(NativeArray<int> heatmap, ref NativeQueue<int> openSet, int index, int newDistance)
    {
        if (heatmap[index] == s_Unvisited || heatmap[index] > newDistance)
        {
            heatmap[index] = newDistance;
            openSet.Enqueue(index);
        }
    }

    struct ChunkInputData
    {
        public int2 size;
        public NativeArray<int2> goals;
        public NativeArray<byte> costs;
    }

    struct ChunkOutputData
    {
        public NativeArray<int> heatmap;
    }

    [BurstCompile]
    struct ComputeHeatmapChunkJob : IJobParallelFor
    {
        [Unity.Collections.ReadOnly]
        NativeArray<ChunkInputData> inputData;

        NativeArray<ChunkOutputData> outputData;

        public void Execute(int chunkData)
        {
            var width = inputData[chunkData].size.x;
            var height = inputData[chunkData].size.y;
            var costs = inputData[chunkData].costs;
            var heatmap = outputData[chunkData].heatmap;

            for (int i = 0; i < costs.Length; ++i)
            {
                heatmap[i] = math.select(s_Obstacle, s_Unvisited, costs[i] == byte.MaxValue);
            }

            var openSet = new NativeQueue<int>(Allocator.Temp);
            var goals = inputData[chunkData].goals;
            for (int i = 0; i < goals.Length; ++i)
            {
                var tileIndex = GetTileIndex(width, goals[i]);
                heatmap[tileIndex] = 0;
                openSet.Enqueue(tileIndex);
            }

            // Search!
            while (openSet.Count > 0)
            {
                var index = openSet.Dequeue();
                var distance = heatmap[index];

                // Get neighbors
                var x = index % width;
                var y = index / width;
                var newDistance = distance + 1;

                if (x < width - 1) // right
                    VisitNeighbor(heatmap, ref openSet, index + 1, newDistance);
                if (x > 0) // left
                    VisitNeighbor(heatmap, ref openSet, index - 1, newDistance);
                if (y > 0) // up
                    VisitNeighbor(heatmap, ref openSet, index - width, newDistance);
                if (y < height - 1) // down
                    VisitNeighbor(heatmap, ref openSet, index + width, newDistance);
            }

            outputData[chunkData] = new ChunkOutputData() { heatmap = heatmap };
        }
    }

    public static NativeArray<int> ComputeHeatmap(int width, int height, NativeArray<byte> costs, int2 goal)
    {
        var n = width * height;
        var heatmap = new NativeArray<int>(n, Allocator.Persistent);
        var maxValue = int.MaxValue;
        var maxTraversable = maxValue - 1;
        for (int i = 0; i < n; ++i)
        {
            heatmap[i] = (byte)math.select(maxValue, maxTraversable, costs[i] == byte.MaxValue);
        }
        heatmap[GetTileIndex(width, goal)] = 0;

        // Search!
        var openSet = new NativeQueue<int>(Allocator.Temp);
        openSet.Enqueue(GetTileIndex(width, goal));
        while (openSet.Count > 0)
        {
            var index = openSet.Dequeue();
            var distance = heatmap[index];

            // Get neighbors
            var x = index % width;
            var y = index / width;
            var newDistance = distance + 1;

            if (x < width - 1) // right
                VisitNeighbor(heatmap, ref openSet, index + 1, newDistance);
            if (x > 0) // left
                VisitNeighbor(heatmap, ref openSet, index - 1, newDistance);
            if (y > 0) // up
                VisitNeighbor(heatmap, ref openSet, index - width, newDistance);
            if (y < height - 1) // down
                VisitNeighbor(heatmap, ref openSet, index + width, newDistance);
        }

        return heatmap;
    }
}
