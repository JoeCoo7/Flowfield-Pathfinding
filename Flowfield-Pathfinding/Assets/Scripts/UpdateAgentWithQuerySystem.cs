using Unity.Entities;

namespace System
{
    public class UpdateAgentWithQuerySystem : ComponentSystem
    {
        [Inject]
        Agent.Group.WithQuery m_Units;

        [Inject]
        FlowField.Group.FlowFieldResult m_Results;

        protected override void OnUpdate()
        {
            for (int index = 0; index < m_Units.flowFieldQuery.Length; ++index)
            {
                for (int i = 0; i < m_Results.flowFieldData.Length; ++i)
                {
                    if (m_Units.flowFieldQuery[index].Handle != m_Results.flowFieldResult[i].Handle)
                        continue;

                    // Update the data and buffer the remove component
                    PostUpdateCommands.SetSharedComponent(m_Units.entity[index], m_Results.flowFieldData[i]);
                    PostUpdateCommands.RemoveComponent<FlowField.Query>(m_Units.entity[index]);
                    break;
                }
            }
        }
    }
}
