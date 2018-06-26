using Unity.Entities;
using UnityEngine;

public class Main : MonoBehaviour
{
	public InitializationData m_InitData;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	static void Initialize()
	{
        var entityManager = World.Active.GetOrCreateManager<EntityManager>();
        Manager.Archetype.Initialize(entityManager);

		FindObjectOfType<Main>().m_InitData.Initalize();
	}
}
