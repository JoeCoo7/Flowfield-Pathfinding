﻿using System.Collections;
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
		m_width = init.m_width;
		m_height = init.m_height;

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

	private void LateUpdate()
	{
		var rs = World.Active.GetOrCreateManager<RenderSystem>();
		rs.lastJob.Complete();

		m_computeBuffer.SetData(rs.Render);
		m_computeShader.SetBuffer(m_computeMain, "colors", m_computeBuffer);
		m_computeShader.Dispatch(m_computeMain, m_width / 8,  m_height / 8, 1);
	}

}
