using System.Collections.Generic;
using Tile;
using Unity.Entities;
using UnityEngine;

//-----------------------------------------------------------------------------
namespace FlowField.UI
{
	//-----------------------------------------------------------------------------
	public class GridDebugSystem : ComponentSystem
	{
		private const int k_MaxLabels = 8192;
		
		[Inject] public AllTiles m_tiles;
		[Inject] public TileSystem m_tileSystem;
		
		private List<TileInfo> m_labelList;
		private bool m_visible;

		//-----------------------------------------------------------------------------
		protected override void OnUpdate()
		{
			if (!Main.Instance.m_InitData.m_drawHeatField)
			{
				if (!m_visible) return;
				int labelCount = m_labelList.Count;
				for (int index = 0; index < labelCount; index++)
					m_labelList[index].gameObject.SetActive(false);
				m_visible = false;
				return;
			}

			int count = m_tiles.Length;
			if (m_tiles.Length == 0 || !m_tileSystem.LastGeneratedDistanceMap.IsCreated)
				return;
			
			if (m_labelList == null)
			{
				m_labelList = new List<TileInfo>(k_MaxLabels);
				for (int index = 0; index < count; index++)
				{
					if (m_labelList.Count >= k_MaxLabels)
						continue;
					
					var pos = GridUtilties.Index2World(m_tiles.Settings[0], index);
					pos.y = Main.TerrainHeight[index] + 5;
					var gridTile = GameObject.Instantiate(Main.Instance.m_InitData.m_tileInfoPrefab, pos, Quaternion.identity);
					m_labelList.Add(gridTile.GetComponent<TileInfo>());
				}
			}

			count = m_labelList.Count;
			for (int index = 0; index < count; index++)
			{
				var label = m_labelList[index];
				label.SetText((float)m_tileSystem.LastGeneratedDistanceMap[index]);
				if (!m_visible)
					label.gameObject.SetActive(true);
			}

			m_visible = true;
		}
	}
}