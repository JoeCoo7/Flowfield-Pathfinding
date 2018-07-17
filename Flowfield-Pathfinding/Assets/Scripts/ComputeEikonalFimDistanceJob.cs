using System;
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
		public enum States
		{
			Open = 0,
			Narrow,
			Frozen
		}
		private const double k_MinimumChange = 1e-4f;
		private const int k_Dimensions = 2;

		[ReadOnly] public GridSettings Settings;
		[ReadOnly, DeallocateOnJobCompletion] public NativeArray<int2> Goals;
		[ReadOnly] public int NumGoals;
		[ReadOnly] public NativeArray<int> Costs;
		[DeallocateOnJobCompletion] public NativeArray<States> StateMap;
		[DeallocateOnJobCompletion] public NativeArray<int> Neighbours;

		public NativeArray<int> FloodQueue;
		public NativeArray<double> DistanceMap;
		private double m_neighbourTime1;
		private double m_neighbourTime2;


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
			var time = DistanceMap[index];
			double velocity = Math.Max(byte.MaxValue - Costs[index], 1);

			m_neighbourTime1 = double.PositiveInfinity;
			m_neighbourTime2 = double.PositiveInfinity;

			int validDimensionCount = k_Dimensions;
			// x neighbours (E,W)
			double minTimeInDim = Math.Min(GetEikonalNeighbour(true, 0, index), GetEikonalNeighbour(true, 1, index));
			if (!double.IsInfinity(minTimeInDim) && minTimeInDim < time)
				m_neighbourTime1 = minTimeInDim;
			else
				validDimensionCount -= 1;

			// y neighbours (N,S)
			minTimeInDim = Math.Min(GetEikonalNeighbour(false, 0, index), GetEikonalNeighbour(false, 1, index));
			if (!double.IsInfinity(minTimeInDim) && minTimeInDim < time)
				m_neighbourTime2 = minTimeInDim;
			else
				validDimensionCount -= 1;

			if (validDimensionCount == 0)
				return double.PositiveInfinity;

			if (m_neighbourTime1 > m_neighbourTime2)
			{
				var exchange = m_neighbourTime1;
				m_neighbourTime1 = m_neighbourTime2;
				m_neighbourTime2 = exchange;
			}

			double newTime = m_neighbourTime1 + 1f / velocity;
			if (validDimensionCount == 1)
				return m_neighbourTime1 + 1f / velocity;

			if (newTime - m_neighbourTime2 < k_MinimumChange)
				return newTime;

			return Solve2DEikonal(velocity);
		}

		//-----------------------------------------------------------------------------
		private double Solve2DEikonal(double velocity)
		{
			double b = -2 * (m_neighbourTime1 + m_neighbourTime2);
			double c = (m_neighbourTime1 * m_neighbourTime1 + m_neighbourTime2 * m_neighbourTime2) - 1 / (velocity * velocity);
			double quadTerm = b * b - 8 * c;
			if (quadTerm < 0)
				return double.PositiveInfinity;
			
			double newTime = (-b + Math.Sqrt(quadTerm)) / 4;
			if (m_neighbourTime2 <= newTime)
				return newTime;
			
			return m_neighbourTime2 + 1f / velocity;
		}

		//-----------------------------------------------------------------------------
		private double GetEikonalNeighbour(bool xAxis, int neighbour, int index)
		{
			if (xAxis)
			{
				var neighbourIndex = neighbour == 0 ? index - 1 : index + 1;
				return neighbourIndex >= Settings.cellCount.x * Settings.cellCount.x || neighbourIndex < 0 ? double.PositiveInfinity : DistanceMap[neighbourIndex];
			}
			else
			{
				var neighbourIndex = neighbour == 0 ? index - Settings.cellCount.x : index + Settings.cellCount.x;
				return neighbourIndex >= Settings.cellCount.x * Settings.cellCount.y || neighbourIndex < 0 ? double.PositiveInfinity : DistanceMap[neighbourIndex];
			}
		}
	}
}