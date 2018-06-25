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
		return (int2)(new float2(pos.x, pos.z) / grid.cellCount);
	}

	//size is in blocks, c is absolute pos
	public static int Grid2Index(GridSettings grid, int2 cell)
	{
		var blockCount = grid.blockCount;
		var blockSize = grid.cellsPerBlock;
		cell = math.clamp(cell, new int2(0, 0), new int2(blockCount.x * blockSize.x - 1, blockCount.y * blockSize.y - 1));
		int2 blockCoord = cell / blockSize;
		int2 localCoord = cell - blockCoord * blockSize;
		int blockIndex = blockCoord.y * blockCount.x + blockCoord.x;
		int localIndex = localCoord.y * blockSize.x + localCoord.x;
		return blockIndex * (blockSize.x * blockSize.y) + localIndex;
	}

	public static int2 Index2Grid(GridSettings grid, int i)
	{
		int2 blockCount = grid.blockCount;
		int2 blockSize = grid.cellsPerBlock;
		var bs = (blockSize.x * blockSize.y);
		int blockIndex = i / bs;
		int2 blockCoord = new int2(blockIndex % blockCount.x, blockIndex / blockCount.x);
		int localIndex = i - blockIndex * bs;
		int2 localCoord = new int2(localIndex % blockSize.x, localIndex / blockSize.x);
		return blockCoord * blockSize + localCoord;
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

	public static GridSettings CreateGrid(float worldWidth, float worldHeight, float gridSize, int cellsPerBlock, Func<GridSettings, int, byte> func)
	{
		var width = (int)(worldWidth / gridSize);
		var height = (int)(worldHeight / gridSize);
		var cellCount = new int2(width, height);
		var grid = new GridSettings()
		{
			worldSize = new float2(worldWidth, worldHeight),
			cellCount = cellCount,
			cellsPerBlock = cellsPerBlock,
			blockCount = cellCount / cellsPerBlock
		};

		var entityManager = World.Active.GetOrCreateManager<EntityManager>();
		var entities = new NativeArray<Entity>(width * height, Allocator.Persistent);
		var arch = entityManager.CreateArchetype(typeof(GridSettings), typeof(TileCost), typeof(TileDirection), typeof(TileCollision));
		entityManager.CreateEntity(arch, entities);

		for (int ii = 0; ii < entities.Length; ii++)
		{
			var e = entities[ii];
			entityManager.SetSharedComponentData(e, grid);
			entityManager.SetComponentData(e, new TileCost() { value = func(grid, ii) });
			entityManager.SetComponentData(e, new TileDirection() { value = 0 });
			entityManager.SetComponentData(e, new TileCollision() { value = 0 });
		}
		entities.Dispose();
		return grid;
	}

	public static readonly int2[] Offset = new[] {
		new int2(0, -1),    // N,
        new int2(0, 1),     // S,
        new int2(1, 0),     // E,
        new int2(-1, 0),    // W,
        new int2(1, -1),    // NE,
        new int2(1, 1),     // SE,
        new int2(-1, -1),   // NW,
        new int2(-1, 1),    // SW,
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
