using Unity.Entities;
using Unity.Mathematics;

public struct Velocity : IComponentData
{
	public float3 Value;
}

public struct AgentSteerParams : ISharedComponentData
{
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
}


