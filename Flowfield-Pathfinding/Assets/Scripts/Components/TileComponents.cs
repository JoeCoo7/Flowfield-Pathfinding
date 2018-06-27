using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tile
{
    [System.Serializable]
    public struct Cost : IComponentData
    {
        public byte Value;
    }

    public struct Collision : IComponentData
    {
        public float3 Value;
    }

    public struct Position : IComponentData
    {
        public int2 Value;
    }

    public struct FlowFieldHandle : IComponentData
    {
        public uint Handle;
    }

    public struct TileMeshInstanceRenderer : ISharedComponentData
    {
        public Mesh mesh;
        public Material material;
        public int subMesh;
        public ShadowCastingMode castShadows;
        public bool receiveShadows;
    }
}
