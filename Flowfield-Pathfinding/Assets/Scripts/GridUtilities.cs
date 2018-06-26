using Unity.Mathematics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using System;

public static class GridUtilties
{
    public static int WorldToIndex(GridSettings grid, float3 pos)
    {
        return Grid2Index(grid, World2Grid(grid, pos));
    }

    public static int2 World2Grid(GridSettings grid, float3 pos)
    {
        var newPos = new float2(pos.x, pos.z) + grid.worldSize * .5f;
        return (int2)(newPos / grid.cellSize);
    }

    //size is in blocks, c is absolute pos
    public static int Grid2Index(GridSettings grid, int2 cell)
    {
        return G2I(grid.cellCount, cell);
        //var blockCount = grid.blockCount;
        //var blockSize = grid.cellsPerBlock;
        //cell = math.clamp(cell, new int2(0, 0), new int2(blockCount.x * blockSize.x - 1, blockCount.y * blockSize.y - 1));
        //int2 blockCoord = cell / blockSize;
        //int2 localCoord = cell - blockCoord * blockSize;
        //int blockIndex = blockCoord.y * blockCount.x + blockCoord.x;
        //int localIndex = localCoord.y * blockSize.x + localCoord.x;
        //return blockIndex * (blockSize.x * blockSize.y) + localIndex;
    }

    public static int G2I(int2 grid, int2 cell)
    {
        if (cell.x < 0 || cell.y < 0 || cell.x > grid.x - 1 || cell.y > grid.y - 1)
            return -1;

        return grid.x * cell.y + cell.x;
    }

    public static int2 I2G(int2 grid, int index)
    {
        if (index < 0 || index > (grid.x * grid.y - 1))
            return new int2(-1, -1);

        return new int2(index % grid.x, index / grid.x);
    }

    public static int2 Index2Grid(GridSettings grid, int i)
    {
        return I2G(grid.cellCount, i);
        //int2 blockCount = grid.blockCount;
        //int2 blockSize = grid.cellsPerBlock;
        //var bs = (blockSize.x * blockSize.y);
        //int blockIndex = i / bs;
        //int2 blockCoord = new int2(blockIndex % blockCount.x, blockIndex / blockCount.x);
        //int localIndex = i - blockIndex * bs;
        //int2 localCoord = new int2(localIndex % blockSize.x, localIndex / blockSize.x);
        //return blockCoord * blockSize + localCoord;
    }
    public static T Neighbor<T>(GridSettings grid, NativeArray<T> data, int2 cell, int2 offset) where T : struct
    {
        return data[Grid2Index(grid, cell + offset)];
    }

    public static T Neighbor<T>(GridSettings grid, NativeArray<T> data, int2 cell, int dx, int dz) where T : struct
    {
        return data[Grid2Index(grid, cell + new int2(dx, dz))];
    }

    public static T Neighbor<T>(GridSettings grid, ComponentDataArray<T> data, int2 cell, int dx, int dz) where T : struct, IComponentData
    {
        return data[Grid2Index(grid, cell + new int2(dx, dz))];
    }
    public static GridSettings CreateGrid(ref NativeArray<float3> initialFlow, float worldWidth, float worldHeight, float cellSize, int cellsPerBlock, Func<GridSettings, int2, byte> func)
    {
        var width = (int)(worldWidth / cellSize);
        var height = (int)(worldHeight / cellSize);
        var cellCount = new int2(width, height);

        var grid = new GridSettings()
        {
            worldSize = new float2(worldWidth, worldHeight),
            cellCount = cellCount,
            cellsPerBlock = cellsPerBlock,
            blockCount = cellCount / cellsPerBlock,
            cellSize = new float2(cellSize, cellSize)
        };

        var entityManager = World.Active.GetOrCreateManager<EntityManager>();
        var entities = new NativeArray<Entity>(width * height, Allocator.Persistent);

        entityManager.CreateEntity(Manager.Archetype.Tile, entities);
        var costs = new byte[entities.Length];

        for (int ii = 0; ii < entities.Length; ii++)
        {
            int2 pos = GridUtilties.Index2Grid(grid, ii);
            costs[ii] = func(grid, pos);
            Manager.Archetype.SetupTile(entityManager, entities[ii], InitializationData.Instance.TileDirectionMesh, InitializationData.Instance.TileDirectionMaterial, pos, costs[ii], new float3(), grid);
        }

        initialFlow = new NativeArray<float3>(costs.Length, Allocator.Persistent);

        float inv255 = 1f / 255f;
        for (int ii = 0; ii < initialFlow.Length; ii++)
        {
            int2 coord = Index2Grid(grid, ii);
            float[] s = new float[8];
            for (int i = 0; i < Offset.Length; ++i)
            {
                var index = Grid2Index(grid, coord + Offset[i]);
                if (index != -1)
                    s[i] = costs[index] * inv255;
                else
                    s[i] = 0.5f;
            }
            initialFlow[ii] = math.normalize(new float3(
                -(s[4] - s[6] + 2 * (s[2] - s[3]) + s[5] - s[7]), 
                .2f,
                -(s[7] - s[6] + 2 * (s[1] - s[0]) + s[5] - s[4])));

            //var s0 = costs[Grid2Index(grid, coord + new int2(-1, -1))] * inv255; s[6]
            //var s1 = costs[Grid2Index(grid, coord + new int2(0, -1))] * inv255; s[0]
            //var s2 = costs[Grid2Index(grid, coord + new int2(1, -1))] * inv255; s[4]
            //var s3 = costs[Grid2Index(grid, coord + new int2(-1, 0))] * inv255; s[3]
            //var s5 = costs[Grid2Index(grid, coord + new int2(1, 0))] * inv255; s[2]
            //var s6 = costs[Grid2Index(grid, coord + new int2(-1, 1))] * inv255; s[7]
            //var s7 = costs[Grid2Index(grid, coord + new int2(0, 1))] * inv255; s[1]
            //var s8 = costs[Grid2Index(grid, coord + new int2(1, 1))] * inv255; s[5]
            //var normal = math.normalize(new float3(-(s[4] - s[6] + 2 * (s[2] - s[3]) + s[5] - s[7]), .2f,
            //    -(s[7] - s[6] + 2 * (s[1] - s[0]) + s[5] - s[4])));
        }
        entities.Dispose();

        return grid;
    }

    public static readonly int2[] Offset = new[] {
        new int2(0, -1),    // N,0
        new int2(0, 1),     // S,1
        new int2(1, 0),     // E,2
        new int2(-1, 0),    // W,3
        new int2(1, -1),    // NE,4
        new int2(1, 1),     // SE,5
        new int2(-1, -1),   // NW,6
        new int2(-1, 1),    // SW,7
    };

    public enum Direction
    {
        N,
        S,
        E,
        W,
        NE,
        SE,
        NW,
        SW,
        MAX
    }
}
