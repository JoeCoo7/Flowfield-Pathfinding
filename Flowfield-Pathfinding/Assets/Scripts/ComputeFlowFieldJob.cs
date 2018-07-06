using Tile;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Experimental;

namespace FlowField
{
    //-----------------------------------------------------------------------------
    [BurstCompile]
    struct ComputeFlowFieldJob : IJobParallelFor
    {
        [ReadOnly] public GridSettings settings;
        [ReadOnly] public NativeArray<float> heatmap;
        [ReadOnly] public NativeArray<int2> offsets;

        public NativeArray<float3> flowfield;

        //-----------------------------------------------------------------------------
        public void Execute(int index)
        {
            int2 grid = GridUtilties.Index2Grid(settings, index);
            float weight = heatmap[index];
            flowfield[index] = new float3();
            flowfield[index] = new float3(0);

            for (GridUtilties.Direction dir = 0; dir < GridUtilties.Direction.MAX; ++dir)
            {
                int2 dirOffset = offsets[(int)dir];
                int neigborIdx = GridUtilties.Grid2Index(settings, grid + dirOffset);
                if (neigborIdx == -1)
                    continue;

                float neighborWeight = heatmap[neigborIdx];
                if (weight <= neighborWeight)
                    continue;

                weight = neighborWeight;
                float3 direction = new float3(dirOffset.x, 0, dirOffset.y);
                flowfield[index] = math.normalize(direction);
            }

            var cellCount = settings.cellCount.x;
            var minCellCount = cellCount + 1;
            if (index < minCellCount || index >= flowfield.Length - minCellCount)
                return;
            
            // don't smooth tiles which are next to obstacles
            if (heatmap[index] >= TileSystem.k_ObstacleFloat
                || heatmap[index + 1] >= TileSystem.k_ObstacleFloat
                || heatmap[index - 1] >= TileSystem.k_ObstacleFloat
                || heatmap[index + cellCount] >= TileSystem.k_ObstacleFloat
                || heatmap[index - cellCount] >= TileSystem.k_ObstacleFloat
                || heatmap[index + cellCount + 1] >= TileSystem.k_ObstacleFloat
                || heatmap[index - cellCount - 1] >= TileSystem.k_ObstacleFloat
                || heatmap[index + cellCount - 1] >= TileSystem.k_ObstacleFloat
                || heatmap[index - cellCount + 1] >= TileSystem.k_ObstacleFloat)
                return;
            
            flowfield[index] = new float3(0);
            float heat = heatmap[index];

            int neigborIndex = GridUtilties.Grid2Index(settings, grid + offsets[(int)GridUtilties.Direction.E]);
            float ax = (neigborIndex != -1 && heatmap[neigborIndex] < TileSystem.k_ObstacleFloat? heatmap[neigborIndex] : heat); 

            neigborIndex = GridUtilties.Grid2Index(settings, grid + offsets[(int)GridUtilties.Direction.W]);
            float bx = (neigborIndex != -1 && heatmap[neigborIndex] < TileSystem.k_ObstacleFloat? heatmap[neigborIndex] : heat); 
            
            neigborIndex = GridUtilties.Grid2Index(settings, grid + offsets[(int)GridUtilties.Direction.N]);
            float ay = (neigborIndex != -1 && heatmap[neigborIndex] < TileSystem.k_ObstacleFloat  ? heatmap[neigborIndex] : heat); 

            neigborIndex = GridUtilties.Grid2Index(settings, grid + offsets[(int)GridUtilties.Direction.S]);
            float by = (neigborIndex != -1 && heatmap[neigborIndex] < TileSystem.k_ObstacleFloat ? heatmap[neigborIndex] : heat); 
            
            flowfield[index] = math_experimental.normalizeSafe(new float3(bx - ax, 0, ay - by));
        }
       
        //-----------------------------------------------------------------------------
        public void Execute2(int index)
        {
            
        }
    }
}
