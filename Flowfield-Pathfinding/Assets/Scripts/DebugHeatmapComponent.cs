using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace DebugHeatmap
{
    [System.Serializable]
    public struct Component : ISharedComponentData
    {
        public NativeArray<int> Value;
    }

    struct Group
    {
        public EntityArray entities;

        [ReadOnly]
        public SharedComponentDataArray<Component> heatmaps;

        public NativeArray<int> GetOrCreateHeatmap(int length)
        {
            var heatmap = heatmaps[0].Value;
            if (heatmap.IsCreated && heatmap.Length != length)
                heatmap.Dispose();

            if (!heatmap.IsCreated)
                heatmap = new NativeArray<int>(length, Allocator.Persistent);

            return heatmap;
        }
    }

    public struct CopyHeatmapJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<int> inputHeatmap;

        [WriteOnly]
        public NativeArray<int> outputHeatmap;

        public void Execute(int index)
        {
            outputHeatmap[index] = inputHeatmap[index];
        }
    }
}
