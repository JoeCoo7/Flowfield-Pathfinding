using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

public static class FlowFieldCache
{
    struct Entry
    {
        public uint chunk;
        public int2[] destinations;
        public int[] weights;
    }

    static Dictionary<Entry, NativeArray<byte>> m_CachedFields = new Dictionary<Entry, NativeArray<byte>>();

    public static bool TryGetCachedFlowField(int2 destination, out NativeArray<byte> flowField)
    {
        return TryGetCachedFlowField(0, new[] { destination }, new[] { 0 }, out flowField);
    }

    static bool TryGetCachedFlowField(uint chunkID, int2[] destinations, int[] weights, out NativeArray<byte> flowField)
    {
        var entry = new Entry();
        entry.chunk = chunkID;
        entry.destinations = destinations;
        entry.weights = weights;

        return m_CachedFields.TryGetValue(entry, out flowField);
    }

    public static void CacheFlowField(int2 destination, NativeArray<byte> flowField)
    {
        CacheFlowField(0, new[] { destination }, new[] { 0 }, flowField);
    }

    static void CacheFlowField(uint chunkID, int2[] destinations, int[] weights, NativeArray<byte> flowField)
    {
        var entry = new Entry();
        entry.chunk = chunkID;
        entry.destinations = destinations;
        entry.weights = weights;

        m_CachedFields[entry] = flowField;
    }
}
