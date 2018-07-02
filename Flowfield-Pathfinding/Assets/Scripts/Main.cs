using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class Main : MonoBehaviour
{
	public int m_activeSteerData = 0;
	public InitializationData m_InitData;
	public AgentSpawnData m_AgentSpawnData;
	public AgentSteerData[] m_AgentSteerData;
	public static Main Instance;

	public static AgentSteerParams ActiveSteeringParams
	{
		get { return Instance.m_AgentSteerData[Instance.m_activeSteerData].m_Data; }
	}

	public static AgentSpawnData ActiveSpawnParams
	{
		get { return Instance.m_AgentSpawnData; }
	}

	public static InitializationData ActiveInitParams
	{
		get { return Instance.m_InitData; }
	}

	public static NativeArray<float3> TerrainFlow
	{
		get { return Instance.m_InitData.m_terrainFlow; }
	}

	public static NativeArray<float3> TerrainColors
	{
		get { return Instance.m_InitData.m_terrainColors; }
	}
	public static NativeArray<float3> TerrainNormals
	{
		get { return Instance.m_InitData.m_terrainNormals; }
	}

	public static NativeArray<float> TerrainHeight
	{
		get { return Instance.m_InitData.m_terrainHeights; }
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	static void Initialize()
	{
        var entityManager = World.Active.GetOrCreateManager<EntityManager>();
        Archetypes.Initialize(entityManager);
		Archetypes.CreateInputSystem(entityManager);
		
		Instance = FindObjectOfType<Main>();
		Instance.m_InitData.Initalize();
	}

	private void OnDisable()
	{
		m_InitData.Shutdown();
	}

	private void LateUpdate()
	{
		//m_InitData.LateUpdate();
	}

}
