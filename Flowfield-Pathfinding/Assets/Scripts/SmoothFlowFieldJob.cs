using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowField
{
    [BurstCompile]
    struct SmoothFlowFieldJob : IJob
    {
        [ReadOnly]
        public GridSettings settings;

        [ReadOnly, DeallocateOnJobCompletion]
        public NativeArray<int2> goals;

        [ReadOnly]
        public NativeArray<int2> offsets;

        public NativeArray<float3> flowfield;

        public void Execute()
        {
            /*
            BurstQueue queue = new BurstQueue(flowfield.Length);

            for (int i = 0; i < goals.Length; ++i)
            {
                var tileIndex = GridUtilties.Grid2Index(settings, goals[i]);
                queue.Enqueue(tileIndex);
            }

            // Search!
            while (queue.Length > 0)
            {
                var index = queue.Dequeue();
                var distance = flowfield[index];
                var newDistance = distance + 1;
                var grid = GridUtilties.Index2Grid(settings, index);

                for (GridUtilties.Direction dir = GridUtilties.Direction.N; dir <= GridUtilties.Direction.W; ++dir)
                {
                    var neighborGrid = grid + offsets[(int)dir];
                    var neighborIndex = GridUtilties.Grid2Index(settings, neighborGrid);

                    if (neighborIndex != -1 && heatmap[neighborIndex] != k_Obstacle && newDistance < heatmap[neighborIndex])
                    {
                        //heatmap[neighborIndex] = newDistance;

                        queue.Enqueue(neighborIndex);
                    }
                }
            }

            queue.Dispose();
            */

            /*
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
            */
        }
    }
}
