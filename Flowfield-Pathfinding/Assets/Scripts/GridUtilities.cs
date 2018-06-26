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
            Manager.Archetype.SetupTile(entityManager, entities[ii], pos, costs[ii], new float3(), grid);
		}

		initialFlow = new NativeArray<float3>(costs.Length, Allocator.Persistent);

		float inv255 = 1f / 255f;
		for (int ii = 0; ii < initialFlow.Length; ii++)
		{
			int2 coord = Index2Grid(grid, ii);
			var s0 = costs[Grid2Index(grid, coord + new int2(-1, -1))]* inv255;
			var s1 = costs[Grid2Index(grid, coord + new int2(0, -1))] * inv255;
			var s2 = costs[Grid2Index(grid, coord + new int2(1, -1))] * inv255;
			var s3 = costs[Grid2Index(grid, coord + new int2(-1, 0))] * inv255;
			var s5 = costs[Grid2Index(grid, coord + new int2(1, 0))] * inv255;
			var s6 = costs[Grid2Index(grid, coord + new int2(-1, 1))] * inv255;
			var s7 = costs[Grid2Index(grid, coord + new int2(0, 1))] * inv255;
			var s8 = costs[Grid2Index(grid, coord + new int2(1, 1))] * inv255;
			var normal = math.normalize(new float3(-(s2 - s0 + 2 * (s5 - s3) + s8 - s6), .2f, -(s6 - s0 + 2 * (s7 - s1) + s8 - s2)));
			initialFlow[ii] = normal;
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
