using Tile;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowField
{
		//-----------------------------------------------------------------------------
		[BurstCompile]
		public struct ComputeEikonalFimDistanceJob : IJob
		{
			public enum States { Open = 0, Narrow, Frozen }
			private const float k_MinimumChange = 0.01f;
			private const int k_Dimensions = 2;
			
			[ReadOnly] public GridSettings Settings;
			[ReadOnly, DeallocateOnJobCompletion] public NativeArray<int2> Goals;
			[ReadOnly] public int NumGoals;
			[ReadOnly] public NativeArray<int> Costs;
			[DeallocateOnJobCompletion] public NativeArray<States> StateMap;
			[DeallocateOnJobCompletion] public NativeArray<int> Neighbours;
			[DeallocateOnJobCompletion] public NativeArray<float> EikonalNeighbours;
			public NativeArray<int> FloodQueue;
			public NativeArray<float> DistanceMap;
			
			//-----------------------------------------------------------------------------
			public void Execute()
			{
				BurstQueue queue = new BurstQueue(FloodQueue);

				int gridLength = DistanceMap.Length;
				for (int index = 0; index < gridLength; index++)
					DistanceMap[index] = float.PositiveInfinity;
					
				for (int index = 0; index < NumGoals; ++index)
				{
					var tileIndex = GridUtilties.Grid2Index(Settings, Goals[index]);
					DistanceMap[tileIndex] = 0; 
					StateMap[tileIndex] = States.Frozen;

					int numNeighbours = GridUtilties.GetCardinalNeighborIndices(Settings.cellCount.x, tileIndex, ref Neighbours);
					for (int neighbourIndex = 0; neighbourIndex < numNeighbours; ++neighbourIndex)
					{
						if (StateMap[neighbourIndex] != States.Narrow && Costs[neighbourIndex] < TileSystem.k_Obstacle)
						{
							StateMap[neighbourIndex] = States.Narrow;
							queue.Enqueue(Neighbours[neighbourIndex]);
						}
					}
				}

				while (queue.Length > 0)
				{
					var index = queue.Dequeue();
					if (Costs[index] == TileSystem.k_Obstacle)
						continue;
							
					var arrivalTime = DistanceMap[index];
					var improvedArrivalTime = SolveEikonal(index);
					if (float.IsInfinity(improvedArrivalTime))
						continue;
					
					DistanceMap[index] = improvedArrivalTime;
					if (math.abs(improvedArrivalTime - arrivalTime) <= k_MinimumChange)
					{
						int numNeighbours = GridUtilties.GetCardinalNeighborIndices(Settings.cellCount.x, index, ref Neighbours);
						for (int neighbourIndex = 0; neighbourIndex < numNeighbours; ++neighbourIndex)
						{
							var neighbour = Neighbours[neighbourIndex];
							if (Costs[neighbour] >= TileSystem.k_Obstacle)
								continue;
							
							if (StateMap[neighbour] == States.Narrow)
								continue;
							
							arrivalTime = DistanceMap[neighbour];
							improvedArrivalTime = SolveEikonal(neighbour);
							if (TimeBetterThan(improvedArrivalTime,arrivalTime))
							{
								StateMap[neighbour] = States.Narrow;
								DistanceMap[neighbour] = improvedArrivalTime;
								queue.Enqueue(neighbour);
							}
						}
						StateMap[index] = States.Frozen;
					}
					else 
						queue.Enqueue(index);
				}
			}

			//-----------------------------------------------------------------------------
			private bool TimeBetterThan(float oldTime, float newTime)
			{
				if (float.IsInfinity(newTime) && float.IsInfinity(oldTime))
					return false;
				return oldTime + k_MinimumChange < newTime;
			}

			
			//-----------------------------------------------------------------------------
			float SolveEikonal(int index)
			{
				int validDimensionCount = k_Dimensions;
				var time = DistanceMap[index];

				int count = 0;
				for (int dimension = 0; dimension < k_Dimensions; ++dimension) {
					var neighbor0 = GetEikonalNeighbour(dimension, 0, index);
					var neighbor1 = GetEikonalNeighbour(dimension, 1, index);
					float minTimeInDim = math.min(neighbor0, neighbor1);
					if (!float.IsInfinity(minTimeInDim) && minTimeInDim < time)
						EikonalNeighbours[count++] = minTimeInDim;
					else
						validDimensionCount -=1;
				}

				if (validDimensionCount == 0)
					return float.PositiveInfinity;

				if (count > 1)
					EikonalNeighbours.Sort();
				
				float newTime = 0;
				for (int dimension = 1; dimension <= validDimensionCount; ++dimension) 
				{
					newTime = SolveEikonalInDimension(index, dimension);
					if (dimension == validDimensionCount || (newTime - EikonalNeighbours[dimension]) < k_MinimumChange)
						break;
				}
				return newTime;
			}

			//-----------------------------------------------------------------------------
			private float SolveEikonalInDimension(int index, int dim)
			{
				float velocity = byte.MaxValue - Costs[index] + 1;
				
				// Solve for x dimension.
				if (dim == 1)
					return EikonalNeighbours[0] + 1f / velocity;

				var time0 = EikonalNeighbours[0];
				var time1 = EikonalNeighbours[1];
				float timeB =  time0 + time1;
				float timeC = time0 * time0 + time1 * time1;
				
//				float timeB = 0;
//				float timeC = 0;
//				for (int i = 0; i < dim; ++i) {
//					timeB += EikonalNeighbours[i];
//					timeC += EikonalNeighbours[i]*EikonalNeighbours[i];
//				}				
				
				float a = dim;
				float b = -2*timeB;
				float c = timeC - 1 / (velocity * velocity);
				float quadTerm = b*b - 4*a*c;

				if (quadTerm < 0)
					return float.PositiveInfinity;
				return (-b + math.sqrt(quadTerm))/(2*a);
			}
				
			//-----------------------------------------------------------------------------
			private float GetEikonalNeighbour(int  dim, int neighbour, int index)
			{
				if (dim == 0)
				{
					var neighbourIndex = neighbour == 0 ? index - 1 : index + 1;
					return neighbourIndex >= Settings.cellCount.x * Settings.cellCount.x || neighbourIndex < 0 ? float.PositiveInfinity : DistanceMap[neighbourIndex];
				}
				else
				{
					var neighbourIndex = neighbour == 0 ? index - Settings.cellCount.x : index + Settings.cellCount.x;
					return neighbourIndex >= Settings.cellCount.x * Settings.cellCount.x || neighbourIndex < 0 ? float.PositiveInfinity : DistanceMap[neighbourIndex];
				}
			}
		}
}