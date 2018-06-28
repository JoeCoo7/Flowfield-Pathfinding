using Unity.Entities;
using Unity.Mathematics;

using Screen = UnityEngine.Screen;
using Camera = UnityEngine.Camera;
using Transform = UnityEngine.Transform;


public class PlayerSelectionSystem : ComponentSystem
{
    [Inject] ECSInput.InputDataGroup m_Input;
    [Inject] Agent.Group.SelectedPositions m_SelectedAgents;
    [Inject] Agent.Group.UnselectedPositions m_UnselectedAgents;

    SelectionRect m_Selection;

    float3 m_Start;
    float3 m_Stop;

    protected override void OnCreateManager(int capacity)
    {
        base.OnCreateManager(capacity);
        m_Selection = UnityEngine.Object.FindObjectOfType<SelectionRect>();
    }

    protected override void OnUpdate()
    {
        var status = m_Input.Buttons[0].Values["SelectAgents"].Status;
        if (status == ECSInput.InputButtons.NONE)
            return;

        if (status == ECSInput.InputButtons.DOWN)
        { 
            m_Start = m_Input.MousePos[0].Value;
            m_Selection.enabled = true;
        }
        m_Stop = m_Input.MousePos[0].Value;

        if (status == ECSInput.InputButtons.UP)
        { 
            AgentSelection();
            m_Selection.enabled = false;
        }
    }

    static float2 Normalize(float3 p, int width, int height)
    {
        return new float2(p.x / (width / 2) - 1, p.y / (height / 2) - 1);
    }

    void AgentSelection()
    {
        var start = Normalize(math.min(m_Start, m_Stop), Screen.width, Screen.height);
        var stop = Normalize(math.max(m_Start, m_Stop), Screen.width, Screen.height);
        float4x4 world2Clip = Camera.main.projectionMatrix * Camera.main.GetComponent<Transform>().worldToLocalMatrix;

        for (int index = 0; index < m_UnselectedAgents.Length; ++index)
        {
            var position = m_UnselectedAgents.position[index].Value;
            float4 agentVector = math.mul(world2Clip, new float4(position.x, position.y, position.z, 1));
            float2 screenPoint = (agentVector / -agentVector.w).xy;

            if (math.lessThanEqual(start.x, screenPoint.x) && math.lessThanEqual(screenPoint.x, stop.x) &&
                math.lessThanEqual(start.x, screenPoint.x) && math.lessThanEqual(screenPoint.x, stop.x))
                PostUpdateCommands.AddComponent(m_UnselectedAgents.entity[index], new Agent.Selection());
        }

        for (int index = 0; index < m_SelectedAgents.Length; ++index)
        {
            var position = m_SelectedAgents.position[index].Value;
            float4 agentVector = math.mul(world2Clip, new float4(position.x, position.y, position.z, 1));
            float2 screenPoint = (agentVector / -agentVector.w).xy;

            if (math.greaterThan(start.x, screenPoint.x) || math.greaterThan(screenPoint.x, stop.x) ||
                math.greaterThan(start.x, screenPoint.x) || math.greaterThan(screenPoint.x, stop.x))
                PostUpdateCommands.RemoveComponent<Agent.Selection>(m_SelectedAgents.entity[index]);
        }
    }
}