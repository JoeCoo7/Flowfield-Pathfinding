using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "InputSettings", menuName = "Pathfinding/InputSettings")]
public class InputSettings : ScriptableObject
{
    public Dictionary<string, string> Commands;
    public Dictionary<string, string> Mods;

    //-----------------------------------------------------------------------------
    private void OnEnable()
    {
        Commands = new Dictionary<string, string>
        {
			{"SpawnAgents", "right shift|left shift;mouse 0"},
			{"CreateGoal", ";mouse 1"},
            {"SelectAgents", "~right shift|~left shift;mouse 0" },
            {"SelectAll", "right ctrl|left ctrl;a" },
            {"ShowFlowfield", "left shift|right shift;x" },
            {"ShowHeatmap", "left shift|right shift;y" },
            {"SmoothFlowfield", "left shift|right shift;c" }
        };
    }
}
 