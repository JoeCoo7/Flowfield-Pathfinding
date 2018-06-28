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
	public float m_goalAgentFactor = 0.5f;
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
    public bool m_smoothFlowField = true;

    [Range(0, 1)]
    public float m_smoothAmount = 0.9f;

	[NonSerialized]
	public NativeArray<float3> m_terrainFlow;
	[NonSerialized]
	public NativeArray<float3> m_terrainNormals;
	[NonSerialized]
	public NativeArray<float3> m_terrainColors;
	[NonSerialized]
	public NativeArray<float> m_terrainHeights;

	Terrain m_terrain;
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
            cellSize = new float2(m_cellSize, m_cellSize),
            heightScale = m_heightScale
		};

		m_terrainHeights = new NativeArray<float>(width * height, Allocator.Persistent);
		m_terrainFlow = new NativeArray<float3>(width * height, Allocator.Persistent);
		m_terrainNormals = new NativeArray<float3>(width * height, Allocator.Persistent);
		m_terrainColors = new NativeArray<float3>(width * height, Allocator.Persistent);
		BuildWorld();
	}

	public void BuildWorld()
	{
		CreateGrid(m_grid, m_terrainHeights, m_terrainColors, m_terrainNormals, m_terrainFlow, GridFunc);

		if (m_terrain == null)
			m_terrain = Instantiate(m_terrainPrefab).GetComponent<Terrain>();
		m_terrain.Init(m_terrainHeights, m_terrainNormals, m_terrainColors, new float3(m_worldWidth, m_heightScale, m_worldHeight), m_cellSize);
	}

	public void Shutdown()
	{
		m_terrainHeights.Dispose();
		m_terrainFlow.Dispose();
		m_terrainNormals.Dispose();
		m_terrainColors.Dispose();
	}


	public AnimationCurve terrainHeightCurve;
	public Gradient terrainColor;

	CellData GridFunc(float2 per, int2 coord)
	{
		var noise = Mathf.PerlinNoise(per.x * m_noiseScale, per.y * m_noiseScale);
		var noise2 = Mathf.PerlinNoise(per.x * m_noiseScale * 3, per.y * m_noiseScale * 3);
		var cost = (byte)math.clamp((noise * m_noiseMultiplier + m_noiseShift) * 255, 0, 255);
		Color color = terrainColor.Evaluate(math.clamp(noise,0 ,1));
		var height = math.clamp(terrainHeightCurve.Evaluate(noise * .9f + noise2 * .1f), 0, 1);
		return new CellData() { cost = cost, height = height * m_heightScale, color = new float3(color.r, color.g, color.b) };
	}

	public struct CellData
	{
		public byte cost;
		public float height;
		public float3 color;
	}

	public static void CreateGrid(GridSettings grid, 
		NativeArray<float> heightmap,
		NativeArray<float3> colormap,
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
				cd.height,//.2f,
				-(s[7] - s[6] + 2 * (s[1] - s[0]) + s[5] - s[4]));

			normalmap[ii] = math.normalize(normal);
			normal.y = 0;
			normal = math.normalize(normal);
			flowMap[ii] = normal * cd.cost * inv255;
			Manager.Archetype.SetupTile(entityManager, entities[ii], Main.ActiveInitParams.TileDirectionMesh, Main.ActiveInitParams.TileDirectionMaterial, coord, cd.cost, new float3(), grid);
		}
		entities.Dispose();
	}

}
