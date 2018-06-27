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

	static public AgentSpawnData Instance;
	
	public Mesh AgentMesh;
	public Material AgentMaterial;
	public float2 AgentDistSize = new float2(30, 30);
	public int AgentDistMaxTries = 30;
	public int AgentDistCellSize = 1;
	public int AgentDistNumPerClick = 100;
	public int AgentDistSpawningThreshold = 128;

	public void Initalize()
	{
		Instance = this;
	}

}
