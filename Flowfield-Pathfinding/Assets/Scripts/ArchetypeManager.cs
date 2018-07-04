using Agent;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

//-----------------------------------------------------------------------------
public static class Archetypes
{
    public static EntityArchetype Tile;
    public static EntityArchetype Agent;
    public static EntityArchetype PlayerInput;

    public static void Initialize(EntityManager entityManager)
    {
        Tile = entityManager.CreateArchetype(
                ComponentType.Create<TransformMatrix>(),
                ComponentType.Create<Tile.TileMeshInstanceRenderer>(),
                ComponentType.Create<Tile.Position>(),
                ComponentType.Create<Tile.Cost>(),
                ComponentType.Create<Tile.FlowFieldHandle>(),
                ComponentType.Create<Tile.GridSettings>());

        Agent = entityManager.CreateArchetype(
            ComponentType.Create<Position>(),
            ComponentType.Create<Rotation>(),
            ComponentType.Create<TransformMatrix>(),
            ComponentType.Create<AgentMeshInstanceRenderer>(),
            ComponentType.Create<Velocity>(),
            ComponentType.Create<Selection>(),
            ComponentType.Create<Goal>(),
            ComponentType.Create<TargetReached>(),
            ComponentType.Create<Tile.GridSettings>());


        PlayerInput = entityManager.CreateArchetype(
            ComponentType.Create<ECSInput.PlayerInputTag>(),
            ComponentType.Create<ECSInput.MouseDoubleClick>(),
            ComponentType.Create<ECSInput.MousePosition>(),
            ComponentType.Create<ECSInput.InputButtons>());
    }

    //-----------------------------------------------------------------------------
    public static void SetupTile(EntityManager em, Entity e, Mesh mesh, Material mat, int2 pos, byte cost, float3 col, Tile.GridSettings settings)
    {
        em.SetComponentData(e, new Tile.Position { Value = pos });
        em.SetComponentData(e, new Tile.Cost { Value = cost });
        em.SetComponentData(e, new Tile.FlowFieldHandle { Handle = int.MaxValue });
        em.SetSharedComponentData(e, new Tile.TileMeshInstanceRenderer { Mesh = mesh, Material = mat });
        em.SetSharedComponentData(e, settings);
    }

    //-----------------------------------------------------------------------------
    public static void CreateAgent(EntityCommandBuffer ecb, float3 pos, Mesh mesh, Material mat, Tile.GridSettings settings)
    {
        ecb.CreateEntity(Agent);
        ecb.SetComponent(new Position { Value = pos });
        ecb.SetSharedComponent(new AgentMeshInstanceRenderer { mesh = mesh, material = mat, castShadows = UnityEngine.Rendering.ShadowCastingMode.On });
        ecb.SetSharedComponent(settings);
        ecb.SetComponent(new Goal { Current = TileSystem.k_InvalidHandle, Target = TileSystem.k_InvalidHandle });
        ecb.SetComponent(new TargetReached { Value = 0, CurrentGoal = TileSystem.k_InvalidHandle });
    }

    //-----------------------------------------------------------------------------
    public static void CreateInputSystem(EntityManager entityManager)
    {
        var inputSystemEntity =entityManager.CreateEntity(PlayerInput);
        entityManager.SetComponentData(inputSystemEntity, new ECSInput.PlayerInputTag());
        entityManager.SetComponentData(inputSystemEntity, new ECSInput.MouseDoubleClick());
        entityManager.SetComponentData(inputSystemEntity, new ECSInput.MousePosition());
        entityManager.SetSharedComponentData(inputSystemEntity, ECSInput.PlayerInputSystem.ProcessInputSettings());
    }
}
