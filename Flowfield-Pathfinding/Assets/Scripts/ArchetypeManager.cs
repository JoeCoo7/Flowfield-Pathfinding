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
                    ComponentType.Create<FlowField.Result>(),
                    ComponentType.Create<FlowField.Data>());

            Tile = entityManager.CreateArchetype(
                    ComponentType.Create<Tile.Position>(),
                    ComponentType.Create<Tile.Cost>(),
                    ComponentType.Create<Tile.Collision>(),
                    ComponentType.Create<GridSettings>());

            Agent = entityManager.CreateArchetype(
                ComponentType.Create<Unity.Transforms.Position>(),
                ComponentType.Create<Unity.Transforms.Rotation>(),
                ComponentType.Create<Unity.Transforms.TransformMatrix>(),
                ComponentType.Create<Unity.Rendering.MeshInstanceRenderer>(),
                ComponentType.Create<AgentData>(),
                ComponentType.Create<GridSettings>(),
                ComponentType.Create<FlowField.Data>());

            AgentWithQuery = entityManager.CreateArchetype(
                ComponentType.Create<Unity.Transforms.Position>(),
                ComponentType.Create<Unity.Transforms.Rotation>(),
                ComponentType.Create<Unity.Transforms.TransformMatrix>(),
                ComponentType.Create<Unity.Rendering.MeshInstanceRenderer>(),
                ComponentType.Create<AgentData>(),
                ComponentType.Create<GridSettings>(),
                ComponentType.Create<FlowField.Data>(),
                ComponentType.Create<FlowField.Query>());
        }

        public static void SetupTile(EntityManager em, Entity e, int2 pos, byte cost, float3 col, GridSettings settings)
        {
            em.SetComponentData(e, new Tile.Position { Value = pos });
            em.SetComponentData(e, new Tile.Cost { Value = cost });
            em.SetComponentData(e, new Tile.Collision { Value = col });
            em.SetSharedComponentData(e, settings);
        }

        public static void CreateAgent(EntityCommandBuffer ecb, float3 pos, Mesh mesh, Material mat, GridSettings settings, FlowField.Data flowField)
        {
            ecb.CreateEntity(Agent);
            ecb.SetComponent(new Unity.Transforms.Position { Value = pos });
            ecb.SetSharedComponent(new Unity.Rendering.MeshInstanceRenderer { mesh = mesh, material = mat });
            ecb.SetSharedComponent(settings);
            ecb.SetSharedComponent(flowField);
        }

        public static void CreateFlowFieldResult(EntityCommandBuffer ecb, uint handle, FlowField.Data flowFieldData)
        {
            ecb.CreateEntity(FlowFieldResult);
            ecb.SetComponent(new FlowField.Result { Handle = handle });
            ecb.SetSharedComponent(flowFieldData);
        }
    }
}