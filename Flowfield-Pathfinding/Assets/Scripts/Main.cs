using Unity.Entities;
using UnityEngine;

public class Main : MonoBehaviour
{
	public InitializationData m_InitData;
	public AgentSpawnData m_AgentSpawnData;
	public AgentSteerData m_AgentSteerData;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	static void Initialize()
	{
        var entityManager = World.Active.GetOrCreateManager<EntityManager>();
        Manager.Archetype.Initialize(entityManager);

        var debugEntity = entityManager.CreateEntity(Manager.Archetype.DebugHeatmapType);
        entityManager.SetSharedComponentData(debugEntity, new DebugHeatmap.Component());
		
		var main = FindObjectOfType<Main>();
		main.m_InitData.Initalize();
		main.m_AgentSpawnData.Initalize();
		main.m_AgentSteerData.Initalize();
	}
}
