using System;
using Tile;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowField
{
		//-----------------------------------------------------------------------------
		[BurstCompile]
		public struct ComputeEikonalFimDistanceJob : IJob
		{
			public enum States { Open = 0, Narrow, Frozen }
			private const double k_MinimumChange = 1e-4f;
			private const int k_Dimensions = 2;
			
			[ReadOnly] public GridSettings Settings;
			[ReadOnly, DeallocateOnJobCompletion] public NativeArray<int2> Goals;
			[ReadOnly] public int NumGoals;
			[ReadOnly] public NativeArray<int> Costs;
			[DeallocateOnJobCompletion] public NativeArray<States> StateMap;
			[DeallocateOnJobCompletion] public NativeArray<int> Neighbours;
			[DeallocateOnJobCompletion] public NativeArray<double> EikonalNeighbours;
			
			public NativeArray<int> FloodQueue;
			public NativeArray<double> DistanceMap;
			
			//-----------------------------------------------------------------------------
			public void Execute()
			{
				BurstQueue queue = new BurstQueue(FloodQueue);

				int gridLength = DistanceMap.Length;
				for (int index = 0; index < gridLength; index++)
					DistanceMap[index] = double.PositiveInfinity;

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
					if (double.IsInfinity(improvedArrivalTime))
						continue;
					
					DistanceMap[index] = improvedArrivalTime;
					
					if (Math.Abs(improvedArrivalTime - arrivalTime) <= k_MinimumChange)
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
							if (TimeBetterThan(arrivalTime, improvedArrivalTime))
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
			private bool TimeBetterThan(double oldTime, double newTime)
			{
				if (double.IsInfinity(newTime) && double.IsInfinity(oldTime))
					return false;
				return newTime + k_MinimumChange < oldTime;
			}

			
			//-----------------------------------------------------------------------------
			double SolveEikonal(int index)
			{
				int validDimensionCount = k_Dimensions;
				var time = DistanceMap[index];

				int count = 0;
				for (int dimension = 0; dimension < k_Dimensions; ++dimension) {
					var neighbor0 = GetEikonalNeighbour(dimension, 0, index);
					var neighbor1 = GetEikonalNeighbour(dimension, 1, index);
					double minTimeInDim = Math.Min(neighbor0, neighbor1);
					if (!double.IsInfinity(minTimeInDim) && minTimeInDim < time)
						EikonalNeighbours[count++] = minTimeInDim;
					else
						validDimensionCount -=1;
				}

				if (validDimensionCount == 0)
					return double.PositiveInfinity;

				if (count > 1)
					EikonalNeighbours.Sort();
				
				double newTime = 0;
				for (int dimension = 1; dimension <= validDimensionCount; ++dimension) 
				{
					newTime = SolveEikonalInDimension(index, dimension);
					if (dimension == validDimensionCount || (newTime - EikonalNeighbours[dimension]) < k_MinimumChange)
						break;
				}
				return newTime;
			}

			//-----------------------------------------------------------------------------
			private double SolveEikonalInDimension(int index, int dim)
			{
				double velocity = byte.MaxValue - Costs[index] + 1;
				
				// Solve for x dimension.
				if (dim == 1)
					return EikonalNeighbours[0] + 1f / velocity;

				var time0 = EikonalNeighbours[0];
				var time1 = EikonalNeighbours[1];
				double timeB =  time0 + time1;
				double timeC = time0 * time0 + time1 * time1;
				
//				double timeB = 0;
//				double timeC = 0;
//				for (int i = 0; i < dim; ++i) {
//					timeB += EikonalNeighbours[i];
//					timeC += EikonalNeighbours[i]*EikonalNeighbours[i];
//				}				
				
				double a = dim;
				double b = -2*timeB;
				double c = timeC - 1 / (velocity * velocity);
				double quadTerm = b*b - 4*a*c;

				if (quadTerm < 0)
					return double.PositiveInfinity;
				return (-b + Math.Sqrt(quadTerm))/(2*a);
			}
				

			
			
			//-----------------------------------------------------------------------------
			private double GetEikonalNeighbour(int  dim, int neighbour, int index)
			{
				if (dim == 0)
				{
					var neighbourIndex = neighbour == 0 ? index - 1 : index + 1;
					return neighbourIndex >= Settings.cellCount.x * Settings.cellCount.x || neighbourIndex < 0 ? double.PositiveInfinity : DistanceMap[neighbourIndex];
				}
				else
				{
					var neighbourIndex = neighbour == 0 ? index - Settings.cellCount.x : index + Settings.cellCount.x;
					return neighbourIndex >= Settings.cellCount.x * Settings.cellCount.x || neighbourIndex < 0 ? double.PositiveInfinity : DistanceMap[neighbourIndex];
				}
			}
		}
}