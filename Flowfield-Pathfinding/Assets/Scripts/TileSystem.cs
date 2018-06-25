using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using System.ComponentModel;
using Unity.Collections;
using Unity.Mathematics;

[System.Serializable]
public struct GridSettings : ISharedComponentData
{
	public int width;
	public int height;
	public int2 blockSize;
	public int2 blockCount;
}

public struct TileCost : IComponentData
{
    public byte value;
}

public struct TileDirection : IComponentData
{
    public byte value;
}

public struct TileCollision : IComponentData
{
    public byte value;
}

public class TileSystem : ComponentSystem
{
    public static EntityArchetype s_TileType;


    struct Tile
    {
		public SharedComponentDataArray<GridSettings> Grid;
		public ComponentDataArray<TileCost> cost;
        public ComponentDataArray<TileDirection> flowDirection;
        public ComponentDataArray<TileCollision> collisionDirection;
        public readonly int length;
    }

    protected override void OnUpdate()
    {

    }
}
