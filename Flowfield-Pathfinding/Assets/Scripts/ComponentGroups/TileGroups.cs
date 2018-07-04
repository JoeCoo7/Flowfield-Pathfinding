
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
        [ReadOnly] public ComponentDataArray<Position> position;
        [ReadOnly] public SharedComponentDataArray<GridSettings> settings;
        public ComponentDataArray<TransformMatrix> transforms;
        public ComponentDataArray<FlowFieldHandle> handles;
    }
    
    
}
