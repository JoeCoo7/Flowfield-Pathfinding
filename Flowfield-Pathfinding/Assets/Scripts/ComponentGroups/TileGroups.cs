
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace Tile.Group
{
    public struct AllTiles
    {
        [ReadOnly] public ComponentDataArray<Tile.Position> position;
        [ReadOnly] public SharedComponentDataArray<GridSettings> settings;
        public ComponentDataArray<TransformMatrix> transforms;
        public ComponentDataArray<Tile.FlowFieldHandle> handles;
    }
}
