using System;
using Unity.Collections;
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
	public float m_noiseMultiplier = 4;
	public float m_noiseShift = -2;
	public GameObject m_gridPrefab;
	public GameObject m_cameraObject;
	[NonSerialized]
	public GridSettings m_grid;
    public Mesh TileDirectionMesh;
    public Material TileDirectionMaterial;
    public bool m_drawFlowField = false;
	
	static public InitializationData Instance;
	static public NativeArray<float3> m_initialFlow;

	public void Initalize()
	{
		Instance = this;
		Instantiate(m_cameraObject);
		m_grid = GridUtilties.CreateGrid(ref m_initialFlow, m_worldWidth, m_worldHeight, m_cellSize, m_cellsPerBlock, GridFunc);
		CreateGridView();
	}

	byte GridFunc(GridSettings grid, int2 coord)
	{
		float2 per = new float2(coord) / grid.cellCount;
		var n = Mathf.PerlinNoise(per.x * m_noiseScale, per.y * m_noiseScale) * m_noiseMultiplier + m_noiseShift;
		return (byte)math.clamp(n * 255, 0, 255);
	}

}
