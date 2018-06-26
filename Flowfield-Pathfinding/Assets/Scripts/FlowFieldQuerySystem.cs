//using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace FlowField
{
    public class FlowFieldQuerySystem : JobComponentSystem
    {
        public struct UnitsWithQuery
        {
            [ReadOnly] public EntityArray entity;
            public ComponentDataArray<FlowField.Query> flowFieldQuery;
            [ReadOnly] public SharedComponentDataArray<FlowField.Data> flowFieldData;
        }

        public struct FlowFieldResults
        {
            public ComponentDataArray<Result> flowFieldResult;
            [ReadOnly] public SharedComponentDataArray<FlowField.Data> flowFieldData;
        }

        //[BurstCompile]
        struct UpdateFlowFieldOnAgents : IJob
        {
            public UnitsWithQuery units;

            [ReadOnly]
            public FlowFieldResults results;

            public EntityCommandBuffer commandBuffer;

            public void Execute()
            {
                for (int index = 0; index < units.flowFieldQuery.Length; ++index)
                {
                    for (int i = 0; i < results.flowFieldData.Length; ++i)
                    {
                        if (units.flowFieldQuery[index].Handle != results.flowFieldResult[i].Handle)
                            continue;

                        // Update the data and buffer the remove component
                        commandBuffer.SetSharedComponent(units.entity[index], results.flowFieldData[i]);
                        commandBuffer.RemoveComponent<FlowField.Query>(units.entity[index]);
                        break;
                    }
                }
            }
        }

        [Inject]
        EndFrameBarrier m_EndFrameBarrier;

        [Inject]
        UnitsWithQuery m_Units;

        [Inject]
        FlowFieldResults m_Results;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var updateAgentsJob = new UpdateFlowFieldOnAgents
            {
                units = m_Units,
                results = m_Results,
                commandBuffer = m_EndFrameBarrier.CreateCommandBuffer()
            };
            return updateAgentsJob.Schedule(inputDeps);
        }
    }
}
