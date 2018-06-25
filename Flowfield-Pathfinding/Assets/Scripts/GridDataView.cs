using System.Collections;
using System.Collections.Generic;
using UnityEngine;




public class GridDataView : MonoBehaviour
{
	public int m_size = 32;
	public Material m_material;
	RenderTexture m_texture;
	ComputeBuffer m_computeBuffer;
	public ComputeShader m_computeShader;
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

	}

	// Update is called once per frame
	void Update ()
	{
		
	}

	private void LateUpdate()
	{
		//wait for job to complete

		//m_computeBuffer.SetData(rs.RenderData);
		m_computeShader.SetBuffer(m_computeMain, "colors", m_computeBuffer);
		m_computeShader.Dispatch(m_computeMain, m_size / 8, m_size / 8, 1);
	}

}
