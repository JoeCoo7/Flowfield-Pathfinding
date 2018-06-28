﻿using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

public class PlayerSelectionSystem : JobComponentSystem
{
    [BurstCompile]
    struct SelectionJob : IJobParallelFor
    {
        public float2 start;
        public float2 stop;
        public float4x4 world2Clip;
        public Agent.Group.AgentSelection agentSelection;

        public void Execute(int index)
        {
            var position = agentSelection.position[index].Value;
            float4 agentVector = math.mul(world2Clip, new float4(position.x, position.y, position.z, 1));
            float2 screenPoint = (agentVector / -agentVector.w).xy;

            var result = math.all(start <= screenPoint) && math.all(screenPoint <= stop);
            var selectionValue = math.select(0, 1, result);
            agentSelection.selection[index] = new Agent.Selection { Value = (byte)selectionValue };
        }
    }

    [BurstCompile]
    struct SelectAllJob : IJobParallelFor
    {
        public Agent.Group.AgentSelection agentSelection;

        public void Execute(int index)
        {
            agentSelection.selection[index] = new Agent.Selection { Value = 1 };
        }
    }

    [Inject] ECSInput.InputDataGroup m_Input;
    [Inject] Agent.Group.AgentSelection m_AgentSelection;

    SelectionRect m_Selection;

    float3 m_Start;
    float3 m_Stop;

    protected override void OnCreateManager(int capacity)
    {
        base.OnCreateManager(capacity);
        m_Selection = UnityEngine.Object.FindObjectOfType<SelectionRect>();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var status = m_Input.Buttons[0].Values["SelectAll"].Status;
        if (status == ECSInput.InputButtons.UP)
        {
            var selectionJob = new SelectAllJob { agentSelection = m_AgentSelection };
            return selectionJob.Schedule(m_AgentSelection.Length, 64, inputDeps);
        }

        status = m_Input.Buttons[0].Values["SelectAgents"].Status;
        if (status == ECSInput.InputButtons.NONE)
            return inputDeps;

        if (status == ECSInput.InputButtons.DOWN)
        {
            m_Start = m_Input.MousePos[0].Value;
            m_Selection.start = m_Start;
            m_Selection.stop = m_Stop;
            m_Selection.enabled = true;
        }
        m_Stop = m_Input.MousePos[0].Value;
        m_Selection.stop = m_Stop;

        if (status == ECSInput.InputButtons.UP)
        {
            m_Selection.enabled = false;
        }

        if (m_Selection.enabled)
        {
            var job = new SelectionJob
            {
                start = Normalize(math.min(m_Start, m_Stop), UnityEngine.Screen.width, UnityEngine.Screen.height),
                stop = Normalize(math.max(m_Start, m_Stop), UnityEngine.Screen.width, UnityEngine.Screen.height),
                world2Clip = UnityEngine.Camera.main.projectionMatrix *
                    UnityEngine.Camera.main.GetComponent<UnityEngine.Transform>().worldToLocalMatrix,
                agentSelection = m_AgentSelection
            };
            return job.Schedule(m_AgentSelection.Length, 64, inputDeps);
        }

        return inputDeps;
    }

    static float2 Normalize(float3 p, int width, int height)
    {
        return new float2(p.x / (width / 2) - 1, p.y / (height / 2) - 1);
    }
}