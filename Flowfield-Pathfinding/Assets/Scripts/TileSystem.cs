using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public struct TileCost : IComponentData
{
    public byte cost;
}

public struct TileDirection : IComponentData
{
    public byte direction;
}

public struct TileCollision : IComponentData
{
    public byte direction;
}

public class TileSystem : ComponentSystem
{
    struct Tile
    {
        public ComponentDataArray<TileCost> cost;
        public ComponentDataArray<TileDirection> flowDirection;
        public ComponentDataArray<TileCollision> collisionDirection;
        public readonly int length;
    }

    protected override void OnUpdate()
    {

    }
}
