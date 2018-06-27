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
			{"SpawnAgents", ";mouse 0"},
		};
	}
}
 