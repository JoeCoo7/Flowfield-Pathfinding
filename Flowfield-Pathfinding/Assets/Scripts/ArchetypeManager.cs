using ECSInput;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Manager
{
    public static class Archetype
    {
        public static EntityArchetype FlowFieldResult;
        public static EntityArchetype Tile;
        public static EntityArchetype Agent;
        public static EntityArchetype AgentWithQuery;
        public static EntityArchetype DebugHeatmapType;
        public static EntityArchetype PlayerInput;

        public static void Initialize(EntityManager entityManager)
        {
            FlowFieldResult = entityManager.CreateArchetype(
                    ComponentType.Create<FlowField.Result>(),
                    ComponentType.Create<FlowField.Data>());

            Tile = entityManager.CreateArchetype(
                    ComponentType.Create<Unity.Transforms.TransformMatrix>(),
                    ComponentType.Create<Tile.TileMeshInstanceRenderer>(),
                    ComponentType.Create<Tile.Position>(),
                    ComponentType.Create<Tile.Cost>(),
                    ComponentType.Create<Tile.Collision>(),
                    ComponentType.Create<Tile.FlowFieldHandle>(),
                    ComponentType.Create<GridSettings>());

            Agent = entityManager.CreateArchetype(
                ComponentType.Create<Position>(),
                ComponentType.Create<Rotation>(),
                ComponentType.Create<TransformMatrix>(),
                ComponentType.Create<MeshInstanceRenderer>(),
                ComponentType.Create<Velocity>(),
                ComponentType.Create<GridSettings>(),
                ComponentType.Create<FlowField.Data>());

            AgentWithQuery = entityManager.CreateArchetype(
                ComponentType.Create<Position>(),
                ComponentType.Create<Rotation>(),
                ComponentType.Create<TransformMatrix>(),
                ComponentType.Create<MeshInstanceRenderer>(),
                ComponentType.Create<Velocity>(),
                ComponentType.Create<GridSettings>(),
                ComponentType.Create<FlowField.Data>(),
                ComponentType.Create<FlowField.Query>());

            DebugHeatmapType = entityManager.CreateArchetype(
                ComponentType.Create<DebugHeatmap.Component>());

            PlayerInput = entityManager.CreateArchetype(
                ComponentType.Create<PlayerInputTag>(),
                ComponentType.Create<MouseDoubleClick>(),
                ComponentType.Create<MousePosition>(),
                ComponentType.Create<InputButtons>());
        }

        public static void SetupTile(EntityManager em, Entity e, Mesh mesh, Material mat, int2 pos, byte cost, float3 col, GridSettings settings)
        {
            em.SetComponentData(e, new Tile.Position { Value = pos });
            em.SetComponentData(e, new Tile.Cost { Value = cost });
            em.SetComponentData(e, new Tile.Collision { Value = col });
            em.SetComponentData(e, new Tile.FlowFieldHandle { Handle = uint.MaxValue });
            em.SetSharedComponentData(e, new Tile.TileMeshInstanceRenderer { mesh = mesh, material = mat });
            em.SetSharedComponentData(e, settings);
        }

        public static void CreateAgent(EntityCommandBuffer ecb, float3 pos, Mesh mesh, Material mat, GridSettings settings, FlowField.Data flowField)
        {
            ecb.CreateEntity(Agent);
            ecb.SetComponent(new Position { Value = pos });
            ecb.SetSharedComponent(new MeshInstanceRenderer { mesh = mesh, material = mat });
            ecb.SetSharedComponent(settings);
            ecb.SetSharedComponent(flowField);
        }

        public static void CreateInputSystem(EntityManager entityManager)
        {
            var inputSystemEntity =entityManager.CreateEntity(PlayerInput);
            entityManager.SetComponentData(inputSystemEntity, new PlayerInputTag());
            entityManager.SetComponentData(inputSystemEntity, new MouseDoubleClick());
            entityManager.SetComponentData(inputSystemEntity, new MousePosition());
            entityManager.SetSharedComponentData(inputSystemEntity, PlayerInputSystem.ProcessInputSettings());
        }
        
        public static void CreateFlowFieldResult(EntityCommandBuffer ecb, uint handle, FlowField.Data flowFieldData)
        {
            ecb.CreateEntity(FlowFieldResult);
            ecb.SetComponent(new FlowField.Result { Handle = handle });
            ecb.SetSharedComponent(flowFieldData);
        }
    }
}
