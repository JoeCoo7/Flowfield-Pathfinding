using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

//-----------------------------------------------------------------------------
namespace FlowField
{
    //-----------------------------------------------------------------------------
    [BurstCompile]
    struct CombineHeatmapsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<NativeArray<int>> sourceHeatmaps;
        [WriteOnly] public NativeArray<int> combinedHeatmap;

        //-----------------------------------------------------------------------------
        public void Execute(int index)
        {
            // Set the output value from the first heatmap
            combinedHeatmap[index] = sourceHeatmaps[0][index];

            // Combined the output value from the remainder heatmaps
            for (var i = 1; i < sourceHeatmaps.Length; ++i)
                combinedHeatmap[index] += sourceHeatmaps[i][index];
        }
    }
}
