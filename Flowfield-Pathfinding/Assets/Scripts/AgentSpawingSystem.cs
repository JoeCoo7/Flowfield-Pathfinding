using RSGLib;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public class AgentSpawingSystem : ComponentSystem
{
	public static EntityArchetype s_AgentType;
	public const int MAX_UNITS_PER_CLICK = 1;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	static void Initialize()
	{
		var entityManager = World.Active.GetOrCreateManager<EntityManager>();
		s_AgentType = entityManager.CreateArchetype(typeof(Position), typeof(Rotation), typeof(TransformMatrix), typeof(MeshInstanceRenderer));
	}
	
	protected override void OnUpdate()
	{
		if (!Input.GetMouseButtonDown(StandardInput.LEFT_MOUSE_BUTTON)) return;
		if (!Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, Mathf.Infinity)) return;
		
		for (int index = 0; index < MAX_UNITS_PER_CLICK; index++)
		{
			var pos = Random.insideUnitCircle;
			CreateAgent(new Vector3(pos.x + hit.point.x, 0, pos.y + hit.point.z));
		}
	}

	private void CreateAgent(float3 _pos)
	{
		PostUpdateCommands.CreateEntity(s_AgentType);
		PostUpdateCommands.SetComponent(new Position() { Value = _pos});
		PostUpdateCommands.SetComponent(new Rotation());
		PostUpdateCommands.SetComponent(new TransformMatrix());
		PostUpdateCommands.SetSharedComponent(new MeshInstanceRenderer() { mesh = InitializationData.Instance.AgentMesh, material = InitializationData.Instance.AgentMaterial });
	}
/*
	private void generate_poisson(int width, int height, int minDist, int pointsCount)
	{
		//Create the grid
		cellSize = minDist/sqrt(2);

		grid = Grid2D(Point(
			(ceil(width/cell_size),         //grid width
				ceil(height/cell_size))));      //grid height

		//RandomQueue works like a queue, except that it
		//pops a random element from the queue instead of
		//the element at the head of the queue
		processList = RandomQueue();
		samplePoints = List();

		//generate the first point randomly
		//and updates 

		firstPoint = Point(rand(width), rand(height));

		//update containers
		processList.push(firstPoint);
		samplePoints.push(firstPoint);
		grid[imageToGrid(firstPoint, cellSize)] = firstPoint;

		//generate other points from points in queue.
		while (not processList.empty())
		{
			point = processList.pop();
			for (i = 0; i < pointsCount; i++)
			{
				newPoint = generateRandomPointAround(point, minDist);
				//check that the point is in the image region
				//and no points exists in the point's neighbourhood
				if (inRectangle(newPoint) and
					not inNeighbourhood(grid, newPoint, minDist,
					cellSize))
				{
					//update containers
					processList.push(newPoint);
					samplePoints.push(newPoint);
					grid[imageToGrid(newPoint, cellSize)] =  newPoint;
				}
			}
		}
		return samplePoints;
	}
*/

	
}