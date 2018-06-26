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
	static public AgentSteerData Instance;

	public float MaxSpeed;
	public float Acceleration;
	public float TerrainFieldWeight;
	public float TargetFieldWeight;
	public float SeparationRadius;
	public float SeparationWeight;
	public float AlignmentWeight;
	public float CohesionWeight;
	public float NeighbourHashCellSize;
	public float AlignmentHashCellSize;

	public void Initalize()
	{
		Instance = this;
	}

}
