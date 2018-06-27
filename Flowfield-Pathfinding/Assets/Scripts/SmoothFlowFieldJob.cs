using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Experimental;

namespace FlowField
{
    [BurstCompile]
    struct SmoothFlowFieldJob : IJob
    {
        [ReadOnly]
        public GridSettings settings;

        [ReadOnly]
        public NativeArray<int2> offsets;

        [ReadOnly, DeallocateOnJobCompletion]
        public NativeArray<int> floodQueue;

        public NativeArray<float3> flowfield;

        public void Execute()
        {
            for (int i = 0; i < flowfield.Length; ++i)
            {
                var cellIndex = floodQueue[i];
                if (cellIndex < 0 || cellIndex > flowfield.Length)
                    continue;

                var flowDirection = flowfield[cellIndex];
                var cellGrid = GridUtilties.Index2Grid(settings, cellIndex);
                var cellDirectionCeiling = new int2((int)math.sign(flowDirection.x), (int)math.sign(flowDirection.z));
                var backPropagationCellGrid = cellGrid + cellDirectionCeiling;
                var backPropagationCellIndex = GridUtilties.Grid2Index(settings, backPropagationCellGrid);
                var backPropagationCellDirection = flowfield[backPropagationCellIndex];

                float smoothAmount = 0.9f;
                flowfield[cellIndex] = math_experimental.normalizeSafe(
                    flowDirection * (1.0f - smoothAmount) + backPropagationCellDirection * smoothAmount);
            }
        }
    }
}
