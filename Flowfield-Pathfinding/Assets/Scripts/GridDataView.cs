using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

public class GridDataView : MonoBehaviour
{
	public int m_size = 32;
	public Material m_material;
	RenderTexture m_texture;
	ComputeBuffer m_computeBuffer;
	public ComputeShader m_computeShader;
	public float m_noiseScale = 3;
	public float m_contrast = 2;
	int m_computeMain;
	// Use this for initialization
	void Start ()
	{
		m_texture = new RenderTexture(m_size, m_size, 0);
		m_texture.enableRandomWrite = true;
		m_texture.filterMode = FilterMode.Point;
		m_texture.Create();
		m_material.mainTexture = m_texture;

		m_computeMain = m_computeShader.FindKernel("CSMain");
		m_computeShader.SetTexture(m_computeMain, "Result", m_texture);
		m_computeShader.SetInt("Stride", m_size);
		m_computeBuffer = new ComputeBuffer(m_size * m_size, 4 * 3);
		var rs = World.Active.GetOrCreateManager<RenderSystem>();
		rs.Render = new NativeArray<RenderData>(m_size * m_size, Allocator.Persistent);
		var entityManager = World.Active.GetOrCreateManager<EntityManager>();
		var entities = new NativeArray<Entity>(m_size * m_size, Allocator.Persistent);
		var arch = entityManager.CreateArchetype(typeof(TileCost));
		entityManager.CreateEntity(arch, entities);

		for (int ii = 0; ii < entities.Length; ii++)
		{
			int2 coord = new int2(ii % m_size, ii / m_size);
			float2 per = new float2(coord.x, coord.y) / m_size;
			var n = Mathf.PerlinNoise(per.x * m_noiseScale, per.y * m_noiseScale) + .15f;
			float c = (n * 255);
			float f = ((259 * (c + 255)) / (255 * (259 - c)));
			c = (f * (c - 128) + 128);
			entityManager.SetComponentData(entities[ii], new TileCost() { cost = (byte)math.clamp(c, 0, 255) });
		}
		entities.Dispose();
	}

	// Update is called once per frame
	void Update ()
	{
		
	}

	private void LateUpdate()
	{
		var rs = World.Active.GetOrCreateManager<RenderSystem>();
		rs.lastJob.Complete();

		m_computeBuffer.SetData(rs.Render);
		m_computeShader.SetBuffer(m_computeMain, "colors", m_computeBuffer);
		m_computeShader.Dispatch(m_computeMain, m_size / 8, m_size / 8, 1);
	}

}
