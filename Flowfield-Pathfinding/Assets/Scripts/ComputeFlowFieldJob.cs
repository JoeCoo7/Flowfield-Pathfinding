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

        [ReadOnly, DeallocateOnJobCompletion]
        public NativeArray<int> heatmap;

        [ReadOnly]
        public NativeArray<int2> offsets;

        [WriteOnly]
        public NativeArray<float3> flowfield;

        public void Execute(int index)
        {
            int2 grid = GridUtilties.Index2Grid(settings, index);
            int weight = heatmap[index];
            flowfield[index] = new float3();

            for (GridUtilties.Direction dir = 0; dir < GridUtilties.Direction.MAX; ++dir)
            {
                int2 dirOffset = offsets[(int)dir];
                int neigborIndex = GridUtilties.Grid2Index(settings, grid + dirOffset);
                if (neigborIndex == -1)
                    continue;

                int neighborWeight = heatmap[neigborIndex];
                if (weight <= neighborWeight)
                    continue;

                weight = neighborWeight;
                float3 direction = new float3(dirOffset.x, 0, dirOffset.y);
                flowfield[index] = math.normalize(direction);
            }
        }
    }
}
