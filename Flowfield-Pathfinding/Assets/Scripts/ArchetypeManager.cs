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
                ComponentType.Create<Unity.Transforms.Position>(),
                ComponentType.Create<Unity.Transforms.Rotation>(),
                ComponentType.Create<Unity.Transforms.TransformMatrix>(),
                ComponentType.Create<Unity.Rendering.MeshInstanceRenderer>(),
                ComponentType.Create<Agent.Velocity>(),
                ComponentType.Create<Agent.Selection>(),
                ComponentType.Create<GridSettings>(),
                ComponentType.Create<FlowField.Data>());

            AgentWithQuery = entityManager.CreateArchetype(
                ComponentType.Create<Unity.Transforms.Position>(),
                ComponentType.Create<Unity.Transforms.Rotation>(),
                ComponentType.Create<Unity.Transforms.TransformMatrix>(),
                ComponentType.Create<Unity.Rendering.MeshInstanceRenderer>(),
                ComponentType.Create<Agent.Velocity>(),
                ComponentType.Create<Agent.Selection>(),
                ComponentType.Create<GridSettings>(),
                ComponentType.Create<FlowField.Data>(),
                ComponentType.Create<FlowField.Query>());

            DebugHeatmapType = entityManager.CreateArchetype(
                ComponentType.Create<DebugHeatmap.Component>());

            PlayerInput = entityManager.CreateArchetype(
                ComponentType.Create<ECSInput.PlayerInputTag>(),
                ComponentType.Create<ECSInput.MouseDoubleClick>(),
                ComponentType.Create<ECSInput.MousePosition>(),
                ComponentType.Create<ECSInput.InputButtons>());
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
            ecb.SetComponent(new Unity.Transforms.Position { Value = pos });
            ecb.SetSharedComponent(new Unity.Rendering.MeshInstanceRenderer { mesh = mesh, material = mat, castShadows = UnityEngine.Rendering.ShadowCastingMode.On });
            ecb.SetSharedComponent(settings);
            ecb.SetSharedComponent(flowField);
        }

        public static void CreateInputSystem(EntityManager entityManager)
        {
            var inputSystemEntity =entityManager.CreateEntity(PlayerInput);
            entityManager.SetComponentData(inputSystemEntity, new ECSInput.PlayerInputTag());
            entityManager.SetComponentData(inputSystemEntity, new ECSInput.MouseDoubleClick());
            entityManager.SetComponentData(inputSystemEntity, new ECSInput.MousePosition());
            entityManager.SetSharedComponentData(inputSystemEntity, ECSInput.PlayerInputSystem.ProcessInputSettings());
        }
        
        public static void CreateFlowFieldResult(EntityCommandBuffer ecb, uint handle, FlowField.Data flowFieldData)
        {
            ecb.CreateEntity(FlowFieldResult);
            ecb.SetComponent(new FlowField.Result { Handle = handle });
            ecb.SetSharedComponent(flowFieldData);
        }
    }
}
