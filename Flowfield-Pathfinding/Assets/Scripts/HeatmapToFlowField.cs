using Unity.Collections;

public static class HeatmapToFlowField
{
    static byte min(byte a, byte b)
    {
        return a < b ? a : b;
    }

    public static NativeArray<byte> Compute(GridSettings settings, NativeArray<NativeArray<byte>> heatmaps)
    {
        var root = heatmaps[0];
        for (int i = 1; i < heatmaps.Length; ++i)
        {
            for (int j = 0; j < root.Length; ++j)
                root[j] = min(root[j], heatmaps[i][j]);
        }

        return Compute(settings, root);
    }


    public static NativeArray<byte> Compute(GridSettings settings, NativeArray<byte> heatmap)
    {
        NativeArray<byte> flowField = new NativeArray<byte>(heatmap.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        for (int i = 0; i < heatmap.Length; ++i)
        {
            byte weight = heatmap[i];
            flowField[i] = (byte)Direction.MAX;

            for (byte j = 0; j < (byte)Direction.MAX; ++j)
            {
                byte neighbor = GridUtilties.Neighbor(settings, heatmap, GridUtilties.Index2Grid(settings, i), GridUtilties.Offset[j]);
                if (weight <= neighbor)
                    continue;

                weight = neighbor;
                flowField[i] = j;
            }
        }

        return flowField;
    }
}