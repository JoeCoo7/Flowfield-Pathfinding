
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Tile
{
    //-----------------------------------------------------------------------------
    [Serializable]
    
    public struct AllTiles
    {
        [ReadOnly] public ComponentDataArray<Position> Position;
        [ReadOnly] public ComponentDataArray<Cost> Cost;
        [ReadOnly] public SharedComponentDataArray<GridSettings> Settings;
        public ComponentDataArray<TransformMatrix> Transforms;
        public ComponentDataArray<FlowFieldHandle> Handles;
    }
    
    
}
