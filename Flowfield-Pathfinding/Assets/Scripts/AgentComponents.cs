using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct Velocity : IComponentData
{
	public float3 Value;
}

[System.Serializable]
public struct AgentSteerParams
{
	[Range(0,1000)]
	public float MaxSpeed;
	[Range(0, 1000)]
	public float Acceleration;
	[Range(0, 10)]
	public float TerrainFieldWeight;
	[Range(0, 10)]
	public float TargetFieldWeight;
	[Range(.1f, 10)]
	public float SeparationRadius;
	[Range(0, 10)]
	public float SeparationWeight;
	[Range(0, 10)]
	public float AlignmentWeight;
	[Range(0, 10)]
	public float CohesionWeight;
	[Range(1, 20)]
	public float NeighbourHashCellSize;
	[Range(1, 10)]
	public float AlignmentRadius;
	[Range(0, 100)]
	public float RotationSpeed;
}


