using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;

public class Terrain : MonoBehaviour
{
	Texture2D m_colorTexture;
	Color32[] m_terrainColors;
	Color32[] m_heatmapColors;
	public void Init(NativeArray<float> heightMap, NativeArray<float3> normalmap, NativeArray<float3> colormap, float3 size, float cellSize)
	{
		int width = (int)(size.x / cellSize);
		int depth = (int)(size.z / cellSize);
		m_colorTexture = new Texture2D(width, depth);
		m_heatmapColors = new Color32[width * depth];
		m_terrainColors = new Color32[width * depth];
		for (int i = 0; i < m_terrainColors.Length; i++)
		{
			m_terrainColors[i] = new Color(colormap[i].x, colormap[i].y, colormap[i].z);
			m_heatmapColors[i] = new Color(0, 0, 0);
		}
		m_colorTexture.SetPixels32(m_terrainColors);
		m_colorTexture.Apply();

		var mat = GetComponent<MeshRenderer>().sharedMaterial;
		mat.SetTexture("_MainTex", m_colorTexture);
		
		var mesh = GenerateMesh(heightMap, normalmap, size, cellSize);
		GetComponent<MeshFilter>().sharedMesh = mesh;
		GetComponent<MeshCollider>().sharedMesh = mesh;
		GetComponent<MeshRenderer>().receiveShadows = true;

		World.Active.GetOrCreateManager<TileSystem>().OnNewHeatMap += OnNewHeatMap;
	}

	void OnNewHeatMap(NativeArray<int> map)
	{
		var scale = (1f / (Main.ActiveInitParams.m_grid.cellCount.x * 1.25f));
		for (int i = 0; i < m_heatmapColors.Length; i++)
		{
			var c = 0f;
			if (map[i] == int.MaxValue)
			{
				m_heatmapColors[i] = m_terrainColors[i];
			}
			else
			{
				var h = map[i] * scale;
				if (h < 1)
					c = (1 - h) * (1 - h) * (1 - h);
				m_heatmapColors[i] = new Color(c, c, c) + m_terrainColors[i];
			}
		}
		m_colorTexture.SetPixels32(m_heatmapColors);
		m_colorTexture.Apply();
	}

	Mesh GenerateMesh(NativeArray<float> data, NativeArray<float3> normalmap, float3 size, float cellSize)
	{
		int width = (int)(size.x / cellSize);
		int depth = (int)(size.z / cellSize);
		Mesh mesh = new Mesh();
		mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
		var verts = new List<Vector3>(data.Length);
		var normals = new List<Vector3>(data.Length);
		var indices = new List<int>(data.Length * 3);
		var uvs = new List<Vector2>(data.Length);
		for (int ii = 0; ii < data.Length; ii++)
		{
			normals.Add(normalmap[ii]);
			int2 coord = new int2(ii % width, ii / width);
			float2 per = new float2(coord.x, coord.y) / width;
			var h = data[ii];
			verts.Add(new Vector3(per.x * size.x, h, per.y * size.z) - new Vector3(size.x * .5f, 0, size.z * .5f));
			if (coord.x < width - 1 && coord.y < depth - 1)
			{
				int v0 = ii;
				int v1 = coord.y * width + (coord.x + 1);
				int v2 = (coord.y + 1) * width + (coord.x + 1);
				int v3 = (coord.y + 1) * width + coord.x;
				indices.AddRange(new[] { v0, v2, v1 }); //tri 1
				indices.AddRange(new[] { v0, v3, v2 }); //tri 2
			}
			uvs.Add(new Vector2(per.x, per.y));
		}
		mesh.SetVertices(verts);
		mesh.SetUVs(0, uvs);
		mesh.SetNormals(normals);
		mesh.SetIndices(indices.ToArray(), MeshTopology.Triangles, 0);
		return mesh;
	}

}
