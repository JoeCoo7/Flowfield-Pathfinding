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
    struct SmoothFlowFieldJob : IJob
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
                var cellDirectionCeiling = new int2((int)math.sign(flowDirection.x), (int)math.sign(flowDirection.z));
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
