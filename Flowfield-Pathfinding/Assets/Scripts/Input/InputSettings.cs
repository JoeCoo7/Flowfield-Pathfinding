using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "InputSettings", menuName = "Pathfinding/InputSettings")]
public class InputSettings : ScriptableObject
{
	public Dictionary<string, string> Commands { get; set; }
	public Dictionary<string, string> Mods { get; set; }

	//-----------------------------------------------------------------------------
	private void OnEnable()
	{
		Commands = new Dictionary<string, string>
		{
			{"ShowDebug", ";mouse 0"},
		};
	}
}
