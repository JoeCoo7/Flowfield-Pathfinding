using Unity.Mathematics;
using UnityEngine;

public class AgentSteerData : ScriptableObject
{
#if UNITY_EDITOR
	[UnityEditor.MenuItem("Pathfinding/Create AgentSteerData Asset")]
	static void Create()
	{
		var obj = CreateInstance<AgentSteerData>();
		UnityEditor.AssetDatabase.CreateAsset(obj, "Assets/AgentSteerData.asset");
		UnityEditor.AssetDatabase.Refresh();
	}
#endif
	public AgentSteerParams m_Data;
}
