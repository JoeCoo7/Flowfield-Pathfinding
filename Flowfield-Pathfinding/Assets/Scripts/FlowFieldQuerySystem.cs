using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace FlowField
{
    [System.Serializable]
    public struct FlowFieldResult : IComponentData
    {
        public uint handle;
    }

    // TODO: SetSharedComponent can't use BurstCompile
    //[BurstCompile]
    struct UpdateFlowFieldOnAgents : IJobParallelFor
    {
        public struct UnitsWithQuery
        {
            public EntityArray entity;
            public ComponentDataArray<FlowFieldQuery> flowFieldQuery;
            public SharedComponentDataArray<FlowFieldData> flowFieldData;
        }

        [Inject]
        public UnitsWithQuery units;

        public struct FlowFieldResults
        {
            public ComponentDataArray<FlowFieldResult> flowFieldResult;
            public SharedComponentDataArray<FlowFieldData> flowFieldData;
        }

        [ReadOnly, Inject]
        public FlowFieldResults results;

        [ReadOnly]
        public EntityCommandBuffer commandBuffer;

        public void Execute(int index)
        {
            for (int i = 0; i < results.flowFieldData.Length; ++i)
            {
                if (units.flowFieldQuery[index].handle != results.flowFieldResult[i].handle)
                    continue;

                // Update the data and buffer the remove component
                // TODO: SetSharedComponent can't use BurstCompile
                commandBuffer.SetSharedComponent(units.entity[index], results.flowFieldData[i]);
                commandBuffer.RemoveComponent<FlowFieldQuery>(units.entity[index]);
                break;
            }
        }
    }

    public class FlowFieldQuerySystem : JobComponentSystem
    {
        public static EntityArchetype FlowFieldResultType;

        [Inject]
        EndFrameBarrier m_EndFrameBarrier;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            var entityManager = World.Active.GetOrCreateManager<EntityManager>();
            FlowFieldResultType = entityManager.CreateArchetype(
                ComponentType.Create<FlowFieldResult>(),
                ComponentType.Create<FlowFieldData>());
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var updateAgentsJob = new UpdateFlowFieldOnAgents
            {
                commandBuffer = m_EndFrameBarrier.CreateCommandBuffer()
            };
            return updateAgentsJob.Schedule(updateAgentsJob.units.entity.Length, 64, inputDeps);
        }
    }
}
