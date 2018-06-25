using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

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

    int m_Width = 10;

    int m_Height = 10;

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        var entityManager = World.Active.GetOrCreateManager<EntityManager>();
        s_TileType = entityManager.CreateArchetype(typeof(TileCost), typeof(TileDirection), typeof(TileCollision));
    }

    void CreateGrid()
    {
        var n = m_Width * m_Height;
        for (int index = 0; index < n; ++index)
        {
            PostUpdateCommands.CreateEntity(s_TileType);
            PostUpdateCommands.SetComponent(new TileCost() { value = 0 });
            PostUpdateCommands.SetComponent(new TileDirection() { value = 0 });
            PostUpdateCommands.SetComponent(new TileCollision() { value = 0 });
        }
    }
}
