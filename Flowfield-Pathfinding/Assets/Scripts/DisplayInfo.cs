using UnityEngine;
using Unity.Entities;

public class DisplayInfo : MonoBehaviour
{
	private GUIStyle guiStyle = new GUIStyle(); //create a new variable
	void OnGUI()
    {
        var agentSystem = World.Active.GetExistingManager<AgentSystem>();
        if (agentSystem != null)
        {
			float per = agentSystem.numAgents / 50000f;
			guiStyle.fontSize = (int)(30 + per * 90);
			GUI.Label(new Rect(50, 10, Screen.width - 50, Screen.height - 100), 
				string.Format("Agents: {0}", agentSystem.numAgents), guiStyle);
        }
    }
}
