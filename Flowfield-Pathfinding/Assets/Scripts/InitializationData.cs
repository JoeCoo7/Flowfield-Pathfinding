using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class InitializationData : ScriptableObject
{
#if UNITY_EDITOR
	[UnityEditor.MenuItem("Pathfinding/Create Initialization Asset")]
	static void Create()
	{
		var obj = CreateInstance<InitializationData>();
		UnityEditor.AssetDatabase.CreateAsset(obj, "Assets/InitData.asset");
		UnityEditor.AssetDatabase.Refresh();
	}
#endif

	public int m_textureResolution = 2048;
	public float m_worldWidth = 100;
	public float m_worldHeight = 100;
	public float m_heightScale = 10;
	public float m_cellSize;
	public int m_goalAgentFactor = 100;
	public int m_cellsPerBlock = 8;
	public float m_noiseScale = 3;
	public float m_noiseMultiplier = 4;
	public float m_noiseShift = -2;
	public GameObject m_gridPrefab;
	public GameObject m_terrainPrefab;
	public GameObject m_cameraObject;
	[NonSerialized]
	public GridSettings m_grid;
    public Mesh TileDirectionMesh;
    public Material TileDirectionMaterial;
    public bool m_drawFlowField = false;

	static public NativeArray<float3> m_initialFlow;
	static public NativeArray<float> m_heightmap;

	public void Initalize()
	{
		Instantiate(m_cameraObject);

		var width = (int)(m_worldWidth / m_cellSize);
		var height = (int)(m_worldHeight / m_cellSize);
		var cellCount = new int2(width, height);

		m_grid = new GridSettings()
		{
			worldSize = new float2(m_worldWidth, m_worldHeight),
			cellCount = cellCount,
			cellSize = new float2(m_cellSize, m_cellSize)
		};

		m_heightmap = new NativeArray<float>(width * height, Allocator.Persistent);
		m_initialFlow = new NativeArray<float3>(width * height, Allocator.Persistent);
		NativeArray<Color32> colormap = new NativeArray<Color32>(width * height, Allocator.Temp);
		NativeArray<float3> normalmap = new NativeArray<float3>(width * height, Allocator.Temp);

		CreateGrid(m_grid, m_heightmap, colormap, normalmap, m_initialFlow, GridFunc);

		var terrain = Instantiate(m_terrainPrefab).GetComponent<Terrain>();
		terrain.Init(m_heightmap, normalmap, colormap, new float3(m_worldWidth, m_heightScale, m_worldHeight), m_cellSize);

		colormap.Dispose();
		normalmap.Dispose();
	}

	public void Shutdown()
	{
		m_heightmap.Dispose();
		m_initialFlow.Dispose();
	}
	public Color color1;
	public Color color2;
	public Color color3;
	public Color color4;
	CellData GridFunc(float2 per, int2 coord)
	{
		var noise = Mathf.PerlinNoise(per.x * m_noiseScale, per.y * m_noiseScale);
		var cost = (byte)math.clamp((noise * m_noiseMultiplier + m_noiseShift) * 255, 0, 255);
		Color color;
		var height = noise;
		if (height < .2f)
			color = color1;
		else if (height < .4f)
			color = Color.Lerp(color2, color1, (.4f - height) * 5);
		else if (height < .6f)
			color = Color.Lerp(color3, color2, (.6f - height) * 5);
		else if (height < .8f)
			color = Color.Lerp(color4, color3, (.8f - height) * 5);
		else
			color = color4;
		return new CellData() { cost = cost, height = noise * m_heightScale, color = color };
	}

	public struct CellData
	{
		public byte cost;
		public float height;
		public Color32 color;
	}

	public static void CreateGrid(GridSettings grid, 
		NativeArray<float> heightmap,
		NativeArray<Color32> colormap,
		NativeArray<float3> normalmap,
		NativeArray<float3> flowMap,
		Func<float2, int2, CellData> func)
	{
		var entityManager = World.Active.GetOrCreateManager<EntityManager>();
		var entities = new NativeArray<Entity>(grid.cellCount.x * grid.cellCount.y, Allocator.Temp);

		entityManager.CreateEntity(Manager.Archetype.Tile, entities);
		var cells = new CellData[entities.Length];
		for (int ii = 0; ii < entities.Length; ii++)
		{
			var coord = GridUtilties.Index2Grid(grid, ii);
			float2 per = new float2(coord) / grid.cellCount;
			cells[ii] = func(per, coord);
		}

		float inv255 = 1f / 255f;
		for (int ii = 0; ii < entities.Length; ii++)
		{
			int2 coord = GridUtilties.Index2Grid(grid, ii);
			var cd = cells[ii];
			colormap[ii] = cd.color;
			heightmap[ii] = cd.height;
			float[] s = new float[8];
			for (int i = 0; i < GridUtilties.Offset.Length; ++i)
			{
				var index = GridUtilties.Grid2Index(grid, coord + GridUtilties.Offset[i]);
				if (index != -1)
					s[i] = cells[index].height;
				else
					s[i] = 0.5f;
			}
			var normal = new float3(
				-(s[4] - s[6] + 2 * (s[2] - s[3]) + s[5] - s[7]),
				.5f,//.2f,
				-(s[7] - s[6] + 2 * (s[1] - s[0]) + s[5] - s[4]));

			normalmap[ii] = math.normalize(normal);
			normal.y = 0;
			math.normalize(normal);
			flowMap[ii] = normal * cd.cost * inv255;
//			colormap[ii] = new Color(normalmap[ii].x, normalmap[ii].y, normalmap[ii].z);
//			colormap[ii] = new Color(1 - cd.cost * inv255, 1 - cd.cost * inv255, 1 - cd.cost * inv255);
			Manager.Archetype.SetupTile(entityManager, entities[ii], Main.ActiveInitParams.TileDirectionMesh, Main.ActiveInitParams.TileDirectionMaterial, coord, cd.cost, new float3(), grid);
		}
		entities.Dispose();
	}

}
