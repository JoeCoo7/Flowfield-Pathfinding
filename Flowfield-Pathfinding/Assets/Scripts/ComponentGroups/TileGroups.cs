
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Tile
{
    //-----------------------------------------------------------------------------
    [Serializable]
    public struct GridSettings : ISharedComponentData
    {
        public float2 worldSize;
        public float2 cellSize;
        public int2 cellCount;
        public float heightScale;
    }
    
    public struct AllTiles
    {
        [ReadOnly] public ComponentDataArray<Position> position;
        [ReadOnly] public SharedComponentDataArray<GridSettings> settings;
        public ComponentDataArray<TransformMatrix> transforms;
        public ComponentDataArray<FlowFieldHandle> handles;
    }
    
    
}
