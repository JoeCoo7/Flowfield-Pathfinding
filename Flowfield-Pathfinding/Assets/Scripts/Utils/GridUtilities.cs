using Unity.Mathematics;
using Unity.Collections;
using Unity.Entities;
using Tile;

//-----------------------------------------------------------------------------
public static class GridUtilties
{
    //-----------------------------------------------------------------------------
    public static int WorldToIndex(GridSettings grid, float3 pos)
    {
        return Grid2Index(grid, World2Grid(grid, pos));
    }

    //-----------------------------------------------------------------------------
    public static int2 World2Grid(GridSettings grid, float3 pos)
    {
        var newPos = new float2(pos.x, pos.z) + grid.worldSize * .5f;
        return (int2)(newPos / grid.cellSize);
    }

    //-----------------------------------------------------------------------------
    //size is in blocks, c is absolute pos
    public static int Grid2Index(GridSettings grid, int2 cell)
    {
        return Grid2Index(grid.cellCount, cell);
    }

    //-----------------------------------------------------------------------------
    public static int Grid2Index(int2 grid, int2 cell)
    {
        if (cell.x < 0 || cell.y < 0 || cell.x > grid.x - 1 || cell.y > grid.y - 1)
            return -1;

        return grid.x * cell.y + cell.x;
    }

    //-----------------------------------------------------------------------------
    public static int2 Index2Grid(int2 grid, int index)
    {
        if (index < 0 || index > (grid.x * grid.y - 1))
            return new int2(-1, -1);

        return new int2(index % grid.x, index / grid.x);
    }

    //-----------------------------------------------------------------------------
    public static int2 Index2Grid(GridSettings grid, int index)
    {
        return Index2Grid(grid.cellCount, index);
    }
    
    //-----------------------------------------------------------------------------
    public static T Neighbor<T>(GridSettings grid, NativeArray<T> data, int2 cell, int2 offset) where T : struct
    {
        return data[Grid2Index(grid, cell + offset)];
    }

    //-----------------------------------------------------------------------------
    public static T Neighbor<T>(GridSettings grid, NativeArray<T> data, int2 cell, int dx, int dz) where T : struct
    {
        return data[Grid2Index(grid, cell + new int2(dx, dz))];
    }

    //-----------------------------------------------------------------------------
    public static T Neighbor<T>(GridSettings grid, ComponentDataArray<T> data, int2 cell, int dx, int dz) where T : struct, IComponentData
    {
        return data[Grid2Index(grid, cell + new int2(dx, dz))];
    }

    //-----------------------------------------------------------------------------
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

    //-----------------------------------------------------------------------------
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
