using System;
using Unity.Collections;

public static class HeatmapToFlowField
{
    static int GetNeighborIndex(GridSettings settings, int index, Direction direction)
    {
        // TODO: Fix this
        return index;
    }

    public static NativeArray<byte> Compute(GridSettings settings, NativeArray<NativeArray<byte>> heatmaps)
    {
        var root = heatmaps[0];
        for (int i = 1; i < heatmaps.Length; ++i)
        {
            for (int j = 0; j < root.Length; ++j)
                root[j] = Math.Min(root[j], heatmaps[i][j]);
        }

        return Compute(settings, root);
    }


    public static NativeArray<byte> Compute(GridSettings settings, NativeArray<byte> heatmap)
    {
        NativeArray<byte> flowField = new NativeArray<byte>(heatmap.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        for(int i = 0; i < heatmap.Length; ++i)
        {
            byte weight = heatmap[i];
            flowField[i] = (byte)Direction.MAX;

            for (byte j = 0; j < (byte)Direction.MAX; ++j)
            {
                int neighbor = GetNeighborIndex(settings, i, (Direction)j);
                if (weight <= heatmap[neighbor])
                    continue;

                weight = heatmap[neighbor];
                flowField[i] = j;
            }
        }

        return flowField;
    }
}