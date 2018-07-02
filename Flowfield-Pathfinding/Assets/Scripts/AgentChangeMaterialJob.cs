using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Rendering;
using UnityEngine.Rendering;

//-----------------------------------------------------------------------------
namespace Agent
{
    //-----------------------------------------------------------------------------
    [BurstCompile]
    struct AgentChangeMaterialJob : IJobParallelFor
    {
        [ReadOnly] public EntityArray entites;
        [ReadOnly] public EntityCommandBuffer.Concurrent commandBuffer;
        public MeshInstanceRenderer m_meshInstanceRenderer;

        public void Execute(int index)
        {
            commandBuffer.SetSharedComponent(entites[index], m_meshInstanceRenderer);
        }
    }
}
