using UnityEngine;

//-----------------------------------------------------------------------------
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

//-----------------------------------------------------------------------------
[System.Serializable]
public struct AgentSteerParams
{
	[Range(0, 100)] public float MaxSpeed;
	[Range(0, 100)] public float Acceleration;
	[Range(0, 10)] public float Drag;
	[Range(0, 100)] public float RotationSpeed;
	[Range(0, 100)] public float TerrainFieldWeight;
	[Range(0, 100)] public float TargetFieldWeight;
	[Range(.1f, 100)] public float SeparationRadius;
	[Range(0, 100)] public float SeparationWeight;
	[Range(0, 100)] public float CohesionWeight;
	[Range(0, 100)] public float AlignmentWeight;
	[Range(1, 10)] public float AlignmentRadius;
	[Range(0, 100)] public int MaxNeighborChecks;
	[Range(1, 100)] public float NeighbourHashCellSize;
}



