using UnityEngine;
using Unity.Entities;

public class DisplayInfo : MonoBehaviour
{
    void OnGUI()
    {
        var agentSystem = World.Active.GetExistingManager<AgentSystem>();
        if (agentSystem != null)
        {
            GUI.Label(new Rect(0, 0, 300, 50), "Agents: " + agentSystem.numAgents.ToString());
        }
    }
}
