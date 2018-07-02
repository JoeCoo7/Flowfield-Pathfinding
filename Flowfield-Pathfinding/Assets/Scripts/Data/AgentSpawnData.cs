using Unity.Mathematics;
using UnityEngine;

public class AgentSpawnData : ScriptableObject
{
#if UNITY_EDITOR
	[UnityEditor.MenuItem("Pathfinding/Create AgetnSpawnData Asset")]
	static void Create()
	{
		var obj = CreateInstance<AgentSpawnData>();
		UnityEditor.AssetDatabase.CreateAsset(obj, "Assets/AgentSpawnData.asset");
		UnityEditor.AssetDatabase.Refresh();
	}
#endif

	public Mesh AgentMesh;
	public Material AgentMaterial;
	public float AgentSpawnRadius = 50;
	public int AgentDistMaxTries = 30;
	public int AgentDistCellSize = 1;
	public int AgentDistNumPerClick = 100;
}
