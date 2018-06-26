using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace FlowField
{
    // TODO: SetSharedComponent can't use BurstCompile
    //[BurstCompile]
    struct UpdateFlowFieldOnAgents : IJobParallelFor
    {
        public struct UnitsWithQuery
        {
            public EntityArray entity;
            public ComponentDataArray<FlowField.Query> flowFieldQuery;
            public SharedComponentDataArray<FlowField.Data> flowFieldData;
        }

        [Inject]
        public UnitsWithQuery units;

        public struct FlowFieldResults
        {
            public ComponentDataArray<Result> flowFieldResult;
            public SharedComponentDataArray<FlowField.Data> flowFieldData;
        }

        [ReadOnly, Inject]
        public FlowFieldResults results;

        [ReadOnly]
        public EntityCommandBuffer commandBuffer;

        public void Execute(int index)
        {
            for (int i = 0; i < results.flowFieldData.Length; ++i)
            {
                if (units.flowFieldQuery[index].Handle != results.flowFieldResult[i].Handle)
                    continue;

                // Update the data and buffer the remove component
                // TODO: SetSharedComponent can't use BurstCompile
                commandBuffer.SetSharedComponent(units.entity[index], results.flowFieldData[i]);
                commandBuffer.RemoveComponent<FlowField.Query>(units.entity[index]);
                break;
            }
        }
    }

    public class FlowFieldQuerySystem : JobComponentSystem
    {
        [Inject]
        EndFrameBarrier m_EndFrameBarrier;

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
