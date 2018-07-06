using System;
using Tile;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowField
{
		// This is not working -> I have currently now clue what's wrong!!
		//-----------------------------------------------------------------------------
		[BurstCompile]
		public struct ComputeFastIterativeMethodJob : IJob
		{
			public const double k_MinimumChange = 0.0001;
			public enum States { Open = 0, Narrow, Frozen }
			
			[ReadOnly] public GridSettings Settings;
			[ReadOnly, DeallocateOnJobCompletion] public NativeArray<int2> Goals;
			[ReadOnly, DeallocateOnJobCompletion] public NativeArray<int> Costs;
			[DeallocateOnJobCompletion] public NativeArray<int> FloodQueue;
			[DeallocateOnJobCompletion] public NativeArray<States> StateMap;
			[DeallocateOnJobCompletion] public NativeArray<int> Neighbours;
			[DeallocateOnJobCompletion] public NativeArray<double> EikonalNeighbours;

			public NativeArray<double> heatmap;
			public int numGoals;
			

			//-----------------------------------------------------------------------------
			public void Execute()
			{
				BurstQueue queue = new BurstQueue(FloodQueue);

				int gridLength = heatmap.Length;
				for (int index = 0; index < gridLength; index++)
					heatmap[index] = double.PositiveInfinity;
					
				for (int index = 0; index < numGoals; ++index)
				{
					var tileIndex = GridUtilties.Grid2Index(Settings, Goals[index]);
					heatmap[tileIndex] = 0; 
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
							
					var arrivalTime = heatmap[index];
					var improvedArrivalTime = SolveEikonal(index);
					if (double.IsInfinity(improvedArrivalTime))
						continue;
					
					heatmap[index] = improvedArrivalTime;
					if (improvedArrivalTime - arrivalTime < k_MinimumChange)
					{
						int numNeighbours = GridUtilties.GetCardinalNeighborIndices(Settings.cellCount.x, index, ref Neighbours);
						for (int neighbourIndex = 0; neighbourIndex < numNeighbours; ++neighbourIndex)
						{
							var neighbour = Neighbours[neighbourIndex];
							if (Costs[neighbour] >= TileSystem.k_Obstacle)
								continue;
							
							if (StateMap[neighbour] == States.Narrow)
								continue;
							
							arrivalTime = heatmap[neighbour];
							improvedArrivalTime = SolveEikonal(neighbour);
							if (TimeBetterThan(improvedArrivalTime,arrivalTime))
							{
								StateMap[neighbour] = States.Narrow;
								heatmap[neighbour] = improvedArrivalTime;
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
				return oldTime + k_MinimumChange < newTime;
			}

			
			//-----------------------------------------------------------------------------
			double SolveEikonal(int index)
			{
				int a = 2;
				var time = heatmap[index];

				int count = 0;
				for (int dim = 0; dim < 2; ++dim) {
					var neighbor0 = GetEikonalNeighbour(dim, 0, index);
					var neighbor1 = GetEikonalNeighbour(dim, 1, index);
					double minTInDim = Math.Min(neighbor0, neighbor1);
					if (!double.IsInfinity(minTInDim) && minTInDim < time)
						EikonalNeighbours[count++] = minTInDim;
					else
						a -=1;
				}

				if (a == 0)
					return double.PositiveInfinity;

				EikonalNeighbours.Sort();
				double newTime = 0;
				for (int i = 1; i <= a; ++i) {
					newTime = SolveEikonalDim(index, i);
					if (i == a || (newTime - EikonalNeighbours[i]) < k_MinimumChange)
						break;
				}
				return newTime;
			}

			
			//-----------------------------------------------------------------------------
			private double SolveEikonalDim(int index, int dim)
			{
				double cost = Costs[index];
				if (cost <= 0) cost = 1;
				
				// Solve for 1 dimension.
				if (dim == 1)
					return EikonalNeighbours[0] + 1f / cost;

				// Solve for any number > 1 of dimensions.
				double sumT = 0;
				double sumTT = 0;
				for (int i = 0; i < dim; ++i) 
				{
					sumT += EikonalNeighbours[i];
					sumTT += EikonalNeighbours[i]*EikonalNeighbours[i];
				}

				double a = dim;
				double b = -2*sumT;
				double c = sumTT - 1 / (cost * cost);
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
					var neighbourIndex = neighbour == 0 ? index - 1 : index - Settings.cellCount.x;
					return neighbourIndex >= Settings.cellCount.x * Settings.cellCount.x || neighbourIndex < 0 ? double.PositiveInfinity : heatmap[neighbourIndex];
				}
				else
				{
					var neighbourIndex = neighbour == 0 ? index + 1 : index + Settings.cellCount.x;
					return neighbourIndex >= Settings.cellCount.x * Settings.cellCount.x || neighbourIndex < 0 ? double.PositiveInfinity : heatmap[neighbourIndex];
				}
			}
		}
}