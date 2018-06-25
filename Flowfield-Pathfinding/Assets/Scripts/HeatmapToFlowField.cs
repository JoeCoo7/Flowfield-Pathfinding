using Unity.Collections;
using Unity.Mathematics;

public static class HeatmapToFlowField
{

    public static NativeArray<byte> Compute(GridSettings settings, NativeArray<int>[] heatmaps)
    {
        var root = heatmaps[0];
        for (int i = 1; i < heatmaps.Length; ++i)
        {
            for (int j = 0; j < root.Length; ++j)
                root[j] = math.min(root[j], heatmaps[i][j]);
        }

        return Compute(settings, root);
    }


    public static NativeArray<byte> Compute(GridSettings settings, NativeArray<int> heatmap)
    {
        NativeArray<byte> flowField = new NativeArray<byte>(heatmap.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        for (int i = 0; i < heatmap.Length; ++i)
        {
            int weight = heatmap[i];
            flowField[i] = (byte)Direction.MAX;

            for (byte j = 0; j < (byte)Direction.MAX; ++j)
            {
                int neighbor = GridUtilties.Neighbor(settings, heatmap, GridUtilties.Index2Grid(settings, i), GridUtilties.Offset[j]);
                if (weight <= neighbor)
                    continue;

                weight = neighbor;
                flowField[i] = j;
            }
        }

        return flowField;
    }
}