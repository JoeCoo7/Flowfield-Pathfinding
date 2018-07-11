using Tile;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

//-----------------------------------------------------------------------------
namespace FlowField
{
	[BurstCompile]
	public struct ComputeDijkstraDistanceJob : IJob
	{
		[ReadOnly] public GridSettings Settings;
		[ReadOnly, DeallocateOnJobCompletion] public NativeArray<int2> Goals;
		[ReadOnly] public NativeArray<int2> Offsets;
		public NativeArray<int> FloodQueue;
		
		public int NumGoals;
		public NativeArray<double> DistanceMap;

		//-----------------------------------------------------------------------------
		public void Execute()
		{
			BurstQueue queue = new BurstQueue(FloodQueue);
			for (int index = 0; index < NumGoals; ++index)
			{
				var tileIndex = GridUtilties.Grid2Index(Settings, Goals[index]);
				if (DistanceMap[tileIndex] < TileSystem.k_ObstacleFloat)
				{
					DistanceMap[tileIndex] = 0;
					queue.Enqueue(tileIndex);
				}
			}

			// Search!
			while (queue.Length > 0)
			{
				var index = queue.Dequeue();
				var newDistance = DistanceMap[index] + 1;
				var grid = GridUtilties.Index2Grid(Settings, index);

				for (GridUtilties.Direction dir = GridUtilties.Direction.N; dir <= GridUtilties.Direction.W; ++dir)
				{
					var neighborGrid = grid + Offsets[(int) dir];
					var neighborIndex = GridUtilties.Grid2Index(Settings, neighborGrid);

					if (neighborIndex != -1 && DistanceMap[neighborIndex] < TileSystem.k_ObstacleFloat && newDistance < DistanceMap[neighborIndex])
					{
						DistanceMap[neighborIndex] = newDistance;
						queue.Enqueue(neighborIndex);
					}
				}
			}
		}
	}
}
