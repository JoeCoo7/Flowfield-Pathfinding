using Tile;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Experimental;

namespace FlowField
{
    [BurstCompile]
    public struct SmoothFlowFieldJob : IJobParallelFor
    {
        [ReadOnly] public GridSettings Settings;
        [ReadOnly] public NativeArray<int2> Offsets;
        [ReadOnly, DeallocateOnJobCompletion] public NativeArray<int> FloodQueue;
        [ReadOnly, DeallocateOnJobCompletion] public NativeArray<int> Costs;
        [ReadOnly] public NativeArray<float> DistanceMap;
        public NativeArray<float3> Flowfield;
        
        //-----------------------------------------------------------------------------
        public void Execute(int index)
        {
//            // don't smooth tiles which are next to obstacles
//            if (DistanceMap[index] >= TileSystem.k_ObstacleFloat
//                || DistanceMap[index + 1] >= TileSystem.k_ObstacleFloat
//                || DistanceMap[index - 1] >= TileSystem.k_ObstacleFloat
//                || DistanceMap[index + cellCount] >= TileSystem.k_ObstacleFloat
//                || DistanceMap[index - cellCount] >= TileSystem.k_ObstacleFloat
//                || DistanceMap[index + cellCount + 1] >= TileSystem.k_ObstacleFloat
//                || DistanceMap[index - cellCount - 1] >= TileSystem.k_ObstacleFloat
//                || DistanceMap[index + cellCount - 1] >= TileSystem.k_ObstacleFloat
//                || DistanceMap[index - cellCount + 1] >= TileSystem.k_ObstacleFloat)
//                return;
            
            var cellCount = Settings.cellCount.x;
            var minCellCount = cellCount + 1;
            if (index < minCellCount || index >= Flowfield.Length - minCellCount)
                return;

            int2 gridPos = GridUtilties.Index2Grid(Settings, index);
            Flowfield[index] = new float3(0);
            float distance = DistanceMap[index];

            int neigborIndex = GridUtilties.Grid2Index(Settings, gridPos + Offsets[(int) GridUtilties.Direction.E]);
            float ax = (neigborIndex != -1 && DistanceMap[neigborIndex] < TileSystem.k_ObstacleFloat ? DistanceMap[neigborIndex] : distance);

            neigborIndex = GridUtilties.Grid2Index(Settings, gridPos + Offsets[(int) GridUtilties.Direction.W]);
            float bx = (neigborIndex != -1 && DistanceMap[neigborIndex] < TileSystem.k_ObstacleFloat ? DistanceMap[neigborIndex] : distance);

            neigborIndex = GridUtilties.Grid2Index(Settings, gridPos + Offsets[(int) GridUtilties.Direction.N]);
            float ay = (neigborIndex != -1 && DistanceMap[neigborIndex] < TileSystem.k_ObstacleFloat ? DistanceMap[neigborIndex] : distance);

            neigborIndex = GridUtilties.Grid2Index(Settings, gridPos + Offsets[(int) GridUtilties.Direction.S]);
            float by = (neigborIndex != -1 && DistanceMap[neigborIndex] < TileSystem.k_ObstacleFloat ? DistanceMap[neigborIndex] : distance);

            Flowfield[index] = math_experimental.normalizeSafe(new float3(ax - bx, 0, ay - by) * math.max((byte.MaxValue - Costs[index]), 1));
        }
        
        //-----------------------------------------------------------------------------
        [BurstCompile]
        public struct SmoothFlowFieldDeallocationJob : IJob
        {
            [ReadOnly, DeallocateOnJobCompletion] public NativeArray<int> FloodQueue;
            [ReadOnly, DeallocateOnJobCompletion] public NativeArray<int> Costs;
    
            //-----------------------------------------------------------------------------
            public void Execute()
            {
            }
        }
    
        //-----------------------------------------------------------------------------
        // this does not take obstacles into account -> currently disabled 
        [BurstCompile]
        struct SmoothFlowFieldJob2 : IJob
        {
            [ReadOnly] public GridSettings Settings;
            [ReadOnly] public NativeArray<int2> Offsets;
            [ReadOnly, DeallocateOnJobCompletion] public NativeArray<int> FloodQueue;
    
            public NativeArray<float3> Flowfield;
            public float SmoothAmount;
    
            //-----------------------------------------------------------------------------
            public void Execute()
            {
                for (int i = 0; i < Flowfield.Length; ++i)
                {
                    var cellIndex = FloodQueue[i];
                    if (cellIndex < 0 || cellIndex >= Flowfield.Length)
                        continue;

                    var flowDirection = Flowfield[cellIndex];
                    var cellGrid = GridUtilties.Index2Grid(Settings, cellIndex);
                    var cellDirectionCeiling = new int2((int) math.sign(flowDirection.x), (int) math.sign(flowDirection.z));
                    var backPropagationCellGrid = cellGrid + cellDirectionCeiling;
                    var backPropagationCellIndex = GridUtilties.Grid2Index(Settings, backPropagationCellGrid);
                    if (backPropagationCellIndex < 0)
                        continue;
                    var backPropagationCellDirection = Flowfield[backPropagationCellIndex];

                    Flowfield[cellIndex] = math_experimental.normalizeSafe(
                        flowDirection * (1.0f - SmoothAmount) + backPropagationCellDirection * SmoothAmount);
                }
            }
        }
    }
}
