using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

namespace System
{
    [UpdateInGroup(typeof(ProcessGroup))]
    [UpdateAfter(typeof(TileSystem))]
    public class UpdateAgentWithQuerySystem : JobComponentSystem
    {
        [BurstCompile]
        struct UpdateJob : IJobParallelFor
        {
            public Agent.Group.WithQuery units;
            public FlowField.Group.FlowFieldResult results;
            public EntityCommandBuffer.Concurrent concurrent;

            public void Execute(int index)
            {
                for (int i = results.flowFieldData.Length - 1; i >= 0; --i)
                {
                    if (units.flowFieldQuery[index].Handle != results.flowFieldResult[i].Handle)
                        continue;

                    // Update the data and buffer the remove component
                    concurrent.SetSharedComponent(units.entity[index], results.flowFieldData[i]);
                    concurrent.RemoveComponent<FlowField.Query>(units.entity[index]);
                    break;
                }
            }
        }

        [Inject]
        Agent.Group.WithQuery m_Units;

        [Inject]
        FlowField.Group.FlowFieldResult m_Results;

        [Inject]
        EndFrameBarrier m_Barrier;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var update = new UpdateJob
            {
                units = m_Units,
                results = m_Results,
                concurrent = m_Barrier.CreateCommandBuffer()
            };

            return update.Schedule(update.units.entity.Length, 64, inputDeps);
        }
    }
}
