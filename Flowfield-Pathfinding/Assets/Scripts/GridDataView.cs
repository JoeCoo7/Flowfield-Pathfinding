using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

public class GridDataView : MonoBehaviour
{
	public Material m_material;
	RenderTexture m_texture;
	ComputeBuffer m_computeBuffer;
	public ComputeShader m_computeShader;
	int m_computeMain;
	int m_width;
	int m_height;

	public void Init(InitializationData init)
	{
		transform.localScale = new Vector3(init.m_worldWidth, init.m_worldHeight, init.m_worldHeight);
		var entityManager = World.Active.GetOrCreateManager<EntityManager>();
		
		m_width = init.m_grid.cellCount.x;
		m_height = init.m_grid.cellCount.y;

		m_texture = new RenderTexture(m_width, m_height, 0);
		m_texture.enableRandomWrite = true;
		m_texture.filterMode = FilterMode.Point;
		m_texture.Create();
		m_material.mainTexture = m_texture;

		m_computeMain = m_computeShader.FindKernel("CSMain");
		m_computeShader.SetTexture(m_computeMain, "Result", m_texture);
		m_computeShader.SetInt("Stride", m_width);
		m_computeBuffer = new ComputeBuffer(m_width * m_height, 4 * 3);

		var rs = World.Active.GetOrCreateManager<RenderSystem>();
		rs.Render = new NativeArray<RenderData>(m_width * m_height, Allocator.Persistent);

	}
	private void OnDisable()
	{
		m_computeBuffer.Dispose();
	}

	private void OnDrawGizmosSelected()
	{
		var grid = InitializationData.Instance.m_grid;
		float2 cellSize = grid.worldSize / grid.cellCount;
		for (int y = 0; y < grid.blockCount.y; y++)
		{
			for (int x = 0; x < grid.blockCount.x; x++)
			{
				var coord = new float2(x, y);
				var center = (coord * cellSize * grid.cellsPerBlock - grid.worldSize * .5f) + cellSize * grid.cellsPerBlock * .5f;
				var size = grid.cellsPerBlock * cellSize;
				Gizmos.color = Color.blue;
				Gizmos.DrawWireCube(new Vector3(center.x, 0, center.y), new Vector3(size.x, 1, size.y));
			}
		}
	}

	private void LateUpdate()
	{
		var rs = World.Active.GetOrCreateManager<RenderSystem>();
		rs.lastJob.Complete();

		m_computeBuffer.SetData(rs.Render);
		m_computeShader.SetBuffer(m_computeMain, "colors", m_computeBuffer);
		m_computeShader.Dispatch(m_computeMain, m_width / 8,  m_height / 8, 1);
	}

	private void OnDestroy()
	{
		m_computeBuffer.Release();
	}
	
}
