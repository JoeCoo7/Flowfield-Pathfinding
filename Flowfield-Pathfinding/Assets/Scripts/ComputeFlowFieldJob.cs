using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowField
{
    [BurstCompile]
    struct ComputeFlowFieldJob : IJobParallelFor
    {
        [ReadOnly]
        public GridSettings settings;

        [ReadOnly]
        public NativeArray<int> heatmap;

        [WriteOnly]
        public NativeArray<float3> flowfield;

        public void Execute(int index)
        {
            int2 grid = GridUtilties.Index2Grid(settings, index);
            int weight = heatmap[index];
            flowfield[index] = new float3();

            for (Direction dir = 0; dir < Direction.MAX; ++dir)
            {
                int2 dirOffset = GridUtilties.Offset[(int)dir];
                int neighborWeight = GridUtilties.Neighbor(settings, heatmap, grid, dirOffset);
                if (weight <= neighborWeight)
                    continue;

                weight = neighborWeight;
                float3 direction = new float3(dirOffset.x, dirOffset.y, 0);
                flowfield[index] = math.normalize(direction);
            }
        }
    }
}
