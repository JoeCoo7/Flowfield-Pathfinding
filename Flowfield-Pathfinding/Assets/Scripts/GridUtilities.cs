using Unity.Mathematics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using System;

public static class GridUtilties
{

	//size is in blocks, c is absolute pos
	public static int World2Index(int2 blockCount, int2 blockSize, int2 worldPos)
	{
		worldPos = math.clamp(worldPos, new int2(0, 0), new int2(blockCount.x * blockSize.x - 1, blockCount.y * blockSize.y - 1));
		int2 blockCoord = worldPos / blockSize;
		int2 localCoord = worldPos - blockCoord * blockSize;
		int blockIndex = blockCoord.y * blockCount.x + blockCoord.x;
		int localIndex = localCoord.y * blockSize.x + localCoord.x;
		return blockIndex * (blockSize.x * blockSize.y) + localIndex;
	}

	public static int2 Index2World(int2 blockCount, int2 blockSize, int i)
	{
		var bs = (blockSize.x * blockSize.y);
		int blockIndex = i / bs;
		int2 blockCoord = new int2(blockIndex % blockCount.x, blockIndex / blockCount.x);
		int localIndex = i - blockIndex * bs;
		int2 localCoord = new int2(localIndex % blockSize.x, localIndex / blockSize.x);
		return blockCoord * blockSize + localCoord;
	}

	public static T Neighbor<T>(ComponentDataArray<T> data, int2 blockCount, int2 blockSize, int2 xz, int dx, int dz) where T : struct, IComponentData
	{
		return data[World2Index(blockCount, blockSize, xz + new int2(dx, dz))];
	}


	public static void CreateGrid(int width, int height, int blockSize, Func<int, byte> func)
	{
		var grid = new GridSettings() { width = width, height = height, blockSize = blockSize };

		var entityManager = World.Active.GetOrCreateManager<EntityManager>();
		var entities = new NativeArray<Entity>(width * height, Allocator.Persistent);
		var arch = entityManager.CreateArchetype(typeof(GridSettings), typeof(TileCost), typeof(TileDirection), typeof(TileCollision));
		entityManager.CreateEntity(arch, entities);

		for (int ii = 0; ii < entities.Length; ii++)
		{
			var e = entities[ii];
			entityManager.SetSharedComponentData(e, grid);
			entityManager.SetComponentData(e, new TileCost() { value = func(ii) });
			entityManager.SetComponentData(e, new TileDirection() { value = 0 });
			entityManager.SetComponentData(e, new TileCollision() { value = 0 });
		}
		entities.Dispose();

	}


}
