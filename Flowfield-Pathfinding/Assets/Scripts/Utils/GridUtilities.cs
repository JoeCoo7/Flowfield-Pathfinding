using System;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Entities;
using Tile;

//-----------------------------------------------------------------------------
public static class GridUtilties
{
    //-----------------------------------------------------------------------------
    public static int WorldToIndex(GridSettings settings, float3 pos)
    {
        return Grid2Index(settings, World2Grid(settings, pos));
    }

    //-----------------------------------------------------------------------------
    public static float3 Index2World(GridSettings settings, int index)
    {
        var gridPos = Index2Grid(settings, index);
        return Grid2World(settings, gridPos);
    }

    //-----------------------------------------------------------------------------
    public static float3 Grid2World(GridSettings settings, int2 gridPos)
    {
        return new float3(gridPos.x * settings.cellSize.x  - settings.worldSize.x * 0.5f, 0, gridPos.y * settings.cellSize.y - settings.worldSize.y * 0.5f);
    }
    
    //-----------------------------------------------------------------------------
    public static int2 World2Grid(GridSettings settings, float3 pos)
    {
        var newPos = new float2(pos.x, pos.z) + settings.worldSize * 0.5f;
        return (int2)(newPos / settings.cellSize);
    }

    //-----------------------------------------------------------------------------
    public static int Grid2Index(GridSettings grid, int2 cell)
    {
        return Grid2Index(grid.cellCount, cell);
    }

    //-----------------------------------------------------------------------------
    public static int Grid2Index(int2 settings, int2 cell)
    {
        if (cell.x < 0 || cell.y < 0 || cell.x >= settings.x || cell.y >= settings.y)
            return -1;

        return settings.x * cell.y + cell.x;
    }

    //-----------------------------------------------------------------------------
    public static int2 Index2Grid(int2 grid, int index)
    {
        if (index < 0 || index >= grid.x * grid.y)
            return new int2(-1, -1);

        return new int2(index % grid.x, index / grid.x);
    }

    //-----------------------------------------------------------------------------
    public static int2 Index2Grid(GridSettings settings, int index)
    {
        return Index2Grid(settings.cellCount, index);
    }
    
    //-----------------------------------------------------------------------------
    public static T Neighbor<T>(GridSettings settings, NativeArray<T> data, int2 cell, int2 offset) where T : struct
    {
        return data[Grid2Index(settings, cell + offset)];
    }

    //-----------------------------------------------------------------------------
    public static T Neighbor<T>(GridSettings settings, NativeArray<T> data, int2 cell, int dx, int dz) where T : struct
    {
        return data[Grid2Index(settings, cell + new int2(dx, dz))];
    }

    //-----------------------------------------------------------------------------
    public static T Neighbor<T>(GridSettings settings, ComponentDataArray<T> data, int2 cell, int dx, int dz) where T : struct, IComponentData
    {
        return data[Grid2Index(settings, cell + new int2(dx, dz))];
    }

    //-----------------------------------------------------------------------------
    public static int GetCardinalNeighborIndices(int gridWidth, int currentIndex, ref NativeArray<int> neighbors)
    {
        var gridMaxIndex = gridWidth * gridWidth;
        var validNeighbours = 0;
        for (int neighbourIndex = 0; neighbourIndex < 4; neighbourIndex++)
        {
            var gridIndex = currentIndex + GetCardinalNeighborIndex(gridWidth, neighbourIndex);
            if (gridIndex < 0 || gridIndex >= gridMaxIndex)
                continue;

            neighbors[validNeighbours++] = gridIndex;
        }
        return validNeighbours;
    }

    
    //-----------------------------------------------------------------------------
    public static int GetNeighborIndices(int gridSize, int currentIndex, ref NativeArray<int> neighbors)
    {
        var gridMaxIndex = gridSize * gridSize;
        var validNeighbours = 0;
        for (int neighbourIndex = 0; neighbourIndex < 8; neighbourIndex++)
        {
            var gridIndex = currentIndex + GetNeighborIndex(gridSize, neighbourIndex);
            if (gridIndex < 0 || gridIndex >= gridMaxIndex)
                continue;

            neighbors[validNeighbours++] = gridIndex;
        }
        return validNeighbours;
    }
    
    //-----------------------------------------------------------------------------
    public static readonly int2[] Offset = {
        new int2(0, -1),    // N,0
        new int2(0, 1),     // S,1
        new int2(-1, 0),    // E,2
        new int2(1, 0),     // W,3
        new int2(1, -1),    // NE,4
        new int2(1, 1),     // SE,5
        new int2(-1, -1),   // NW,6
        new int2(-1, 1),    // SW,7
    };

    //-----------------------------------------------------------------------------
    public static int GetCardinalNeighborIndex(int gridSize, int neighborIndex)
    {
        switch (neighborIndex)
        {
            case 0: return -gridSize;
            case 1: return gridSize;
            case 2: return -1;
            case 3: return 1;
        }
        throw new ApplicationException("this should never happen!");
    }

    //-----------------------------------------------------------------------------
    public static int GetNeighborIndex(int gridSize, int neighborIndex)
    {
        switch (neighborIndex)
        {
            case 0: 
            case 1: 
            case 2: 
            case 3: return GetCardinalNeighborIndex(gridSize, neighborIndex);
            
            case 4: return -gridSize + 1;
            case 5: return gridSize + 1;
            case 6: return -gridSize - 1;
            case 7: return gridSize - 1;
        }
        throw new ApplicationException("this should never happen!");
    }
      
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
