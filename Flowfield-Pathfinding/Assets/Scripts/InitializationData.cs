using System;
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

	[UnityEditor.MenuItem("Pathfinding/Create Grid View")]
	static void CreateGridView()
	{
		var view = Instantiate(Instance.m_gridPrefab).GetComponent<GridDataView>();
		view.Init(Instance);
	}

#endif

	public float m_worldWidth = 100;
	public float m_worldHeight = 100;
	public float m_cellSize;
	public int m_cellsPerBlock = 8;
	public float m_noiseScale = 3;
	public GameObject m_gridPrefab;
	public GameObject m_cameraObject;
	[NonSerialized]
	public GridSettings m_grid;
	public Mesh AgentMesh;
	public Material AgentMaterial;
	public float2 m_unitDistSize = new float2(30, 30);
	public int m_unitDistMaxTries = 30;
	public int m_unitDistCellSize = 1;
	public int m_unitDistNumPerClick = 100;
	
	
	static public InitializationData Instance;

	public void Initalize()
	{
		
		Instance = this;
		Instantiate(m_cameraObject);
		m_grid = GridUtilties.CreateGrid(m_worldWidth, m_worldHeight, m_cellSize, m_cellsPerBlock, GridFunc);
		CreateGridView();
	}

	byte GridFunc(GridSettings grid, int2 coord)
	{
		float2 per = new float2(coord.x, coord.y) / grid.cellCount.x;
		var n = Mathf.PerlinNoise(per.x * m_noiseScale, per.y * m_noiseScale) + .15f;
		float c = (n * 255);
		float f = ((259 * (c + 255)) / (255 * (259 - c)));
		c = (f * (c - 128) + 128);
		return (byte)math.clamp(c, 0, 255);
	}

}
