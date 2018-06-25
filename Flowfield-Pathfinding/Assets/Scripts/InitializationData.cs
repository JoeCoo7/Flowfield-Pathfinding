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

	public int m_width = 32;
	public int m_height = 32;
	public int m_blockSize = 8;
	public float m_noiseScale = 3;
	public GameObject m_gridPrefab;
	public GameObject m_cameraObject;
	public Mesh AgentMesh;
	public Material AgentMaterial;

	static public InitializationData Instance;

	public void Initalize()
	{
		Instance = this;
		Instantiate(m_cameraObject);
		GridUtilties.CreateGrid(m_width, m_height, m_blockSize, GridFunc);
		CreateGridView();
	}

	byte GridFunc(int ii)
	{
		int2 coord = new int2(ii % m_width, ii / m_width);
		float2 per = new float2(coord.x, coord.y) / m_width;
		var n = Mathf.PerlinNoise(per.x * m_noiseScale, per.y * m_noiseScale) + .15f;
		float c = (n * 255);
		float f = ((259 * (c + 255)) / (255 * (259 - c)));
		c = (f * (c - 128) + 128);
		return (byte)math.clamp(c, 0, 255);
	}

}
