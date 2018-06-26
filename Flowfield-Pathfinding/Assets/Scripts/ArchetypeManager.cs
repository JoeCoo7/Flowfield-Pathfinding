using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Manager
{
    public static class Archetype
    {
        public static EntityArchetype FlowFieldResult;

        public static EntityArchetype Tile;

        public static EntityArchetype Agent;

        public static EntityArchetype AgentWithQuery;

        public static void Initialize(EntityManager entityManager)
        {
            FlowFieldResult = entityManager.CreateArchetype(
                    ComponentType.Create<FlowField.FlowFieldResult>(),
                    ComponentType.Create<FlowFieldData>());

            Tile = entityManager.CreateArchetype(
                    ComponentType.Create<TilePosition>(),
                    ComponentType.Create<TileCost>(),
                    ComponentType.Create<TileCollision>(),
                    ComponentType.Create<GridSettings>());

            Agent = entityManager.CreateArchetype(
                ComponentType.Create<Unity.Transforms.Position>(),
                ComponentType.Create<Unity.Transforms.Rotation>(),
                ComponentType.Create<Unity.Transforms.TransformMatrix>(),
                ComponentType.Create<Unity.Rendering.MeshInstanceRenderer>(),
                ComponentType.Create<AgentData>(),
                ComponentType.Create<GridSettings>(),
                ComponentType.Create<FlowFieldData>());

            AgentWithQuery = entityManager.CreateArchetype(
                ComponentType.Create<Unity.Transforms.Position>(),
                ComponentType.Create<Unity.Transforms.Rotation>(),
                ComponentType.Create<Unity.Transforms.TransformMatrix>(),
                ComponentType.Create<Unity.Rendering.MeshInstanceRenderer>(),
                ComponentType.Create<AgentData>(),
                ComponentType.Create<GridSettings>(),
                ComponentType.Create<FlowFieldData>(),
                ComponentType.Create<FlowFieldQuery>());
        }

        public static void SetupTile(EntityManager em, Entity e, int2 pos, byte cost, float3 col, GridSettings settings)
        {
            em.SetComponentData(e, new TilePosition { value = pos });
            em.SetComponentData(e, new TileCost { value = cost });
            em.SetComponentData(e, new TileCollision { value = col });
            em.SetSharedComponentData(e, settings);
        }

        public static void CreateAgent(EntityCommandBuffer ecb, float3 pos, Mesh mesh, Material mat, GridSettings settings, FlowFieldData flowField)
        {
            ecb.CreateEntity(Agent);
            ecb.SetComponent(new Unity.Transforms.Position { Value = pos });
            ecb.SetSharedComponent(new Unity.Rendering.MeshInstanceRenderer { mesh = mesh, material = mat });
            ecb.SetSharedComponent(settings);
            ecb.SetSharedComponent(flowField);
        }

        public static void CreateFlowFieldResult(EntityCommandBuffer ecb, uint handle, FlowFieldData flowFieldData)
        {
            ecb.CreateEntity(FlowFieldResult);
            ecb.SetComponent(new FlowField.FlowFieldResult { handle = handle });
            ecb.SetSharedComponent(flowFieldData);
        }
    }
}