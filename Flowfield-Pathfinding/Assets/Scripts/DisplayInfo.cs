using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public class DisplayInfo : MonoBehaviour
{
    void OnGUI()
    {
        var agentSystem = World.Active.GetExistingManager<AgentSystem>();
        if (agentSystem != null)
        {
            GUILayout.Label(string.Format("Agents: {0}", agentSystem.numAgents));
        }
    }
}
