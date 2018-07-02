using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;
using UnityEngine.Rendering;

//-----------------------------------------------------------------------------
public class TerrainColoring : MonoBehaviour
{
    private Texture2D m_ColorTexture;
    private Color32[] m_TerrainColors;
    private Color32[] m_HeatmapColors;
    private bool m_ShowingHeatField = false;
    
    //-----------------------------------------------------------------------------
    public void Init(NativeArray<float> heightMap, NativeArray<float3> normalmap, NativeArray<float3> colormap, float3 size, float cellSize)
    {
        int width = (int)(size.x / cellSize);
        int depth = (int)(size.z / cellSize);
        m_ColorTexture = new Texture2D(width, depth);
        m_HeatmapColors = new Color32[width * depth];
        m_TerrainColors = new Color32[width * depth];
        for (int i = 0; i < m_TerrainColors.Length; i++)
        {
            m_TerrainColors[i] = new Color(colormap[i].x, colormap[i].y, colormap[i].z);
            m_HeatmapColors[i] = new Color(0, 0, 0);
        }
        m_ColorTexture.SetPixels32(m_TerrainColors);
        m_ColorTexture.Apply();

        var mat = GetComponent<MeshRenderer>().sharedMaterial;
        mat.SetTexture("_MainTex", m_ColorTexture);

        var mesh = GenerateMesh(heightMap, normalmap, size, cellSize);
        GetComponent<MeshFilter>().sharedMesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;
        GetComponent<MeshRenderer>().receiveShadows = true;
        GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;

        World.Active.GetOrCreateManager<TileSystem>().OnNewHeatMap += OnNewHeatMap;
    }

    //-----------------------------------------------------------------------------
    private void OnNewHeatMap(NativeArray<int> map)
    {
        if (Main.ActiveInitParams.m_drawHeatField)
        {
            var scale = (1f / (Main.ActiveInitParams.m_grid.cellCount.x * 1.25f));
            for (int index = 0; index < m_HeatmapColors.Length; index++)
            {
                var c = 0f;
                if (map[index] == int.MaxValue)
                    m_HeatmapColors[index] = m_TerrainColors[index];
                else
                {
                    var h = map[index] * scale;
                    if (h < 1)
                        c = (1 - h) * (1 - h) * (1 - h);
                    
                    m_HeatmapColors[index] = new Color(c, c, c) + m_TerrainColors[index];
                }
            }
            m_ColorTexture.SetPixels32(m_HeatmapColors);
            m_ColorTexture.Apply();
            m_ShowingHeatField = true;
        }
        else
        {
            if (m_ShowingHeatField)
            {
                m_ColorTexture.SetPixels32(m_TerrainColors);
                m_ColorTexture.Apply();
                m_ShowingHeatField = true;
            }
        }
    }

    //-----------------------------------------------------------------------------
    private Mesh GenerateMesh(NativeArray<float> data, NativeArray<float3> normalmap, float3 size, float cellSize)
    {
        int width = (int)(size.x / cellSize);
        int depth = (int)(size.z / cellSize);
        Mesh mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;
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
