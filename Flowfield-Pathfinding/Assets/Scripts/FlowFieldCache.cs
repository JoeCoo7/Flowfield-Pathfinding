using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class FlowFieldCache
{
    struct Entry
    {
        public uint chunk;
        public uint[] destinations;
        public uint[] weights;
    }

    static Dictionary<Entry, NativeArray<byte>> m_CachedFields = new Dictionary<Entry, NativeArray<byte>>();

    public static bool TryGetCachedFlowField(uint destination, out NativeArray<byte> flowField)
    {
        return TryGetCachedFlowField(0, new[] { destination }, new[] { (uint)0 }, out flowField);
    }

    static bool TryGetCachedFlowField(uint chunkID, uint[] destinations, uint[] weights, out NativeArray<byte> flowField)
    {
        var entry = new Entry();
        entry.chunk = chunkID;
        entry.destinations = destinations;
        entry.weights = weights;

        return m_CachedFields.TryGetValue(entry, out flowField);
    }

    public static void CacheFlowField(uint destination, NativeArray<byte> flowField)
    {
        CacheFlowField(0, new[] { destination }, new[] { (uint)0 }, flowField);
    }

    static void CacheFlowField(uint chunkID, uint[] destinations, uint[] weights, NativeArray<byte> flowField)
    {
        var entry = new Entry();
        entry.chunk = chunkID;
        entry.destinations = destinations;
        entry.weights = weights;

        m_CachedFields[entry] = flowField;
    }
}
