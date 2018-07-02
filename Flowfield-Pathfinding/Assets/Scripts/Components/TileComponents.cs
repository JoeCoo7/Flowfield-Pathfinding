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

    public struct Position : IComponentData
    {
        public int2 Value;
    }

    public struct FlowFieldHandle : IComponentData
    {
        public int Handle;
    }

    public struct TileMeshInstanceRenderer : ISharedComponentData
    {
        public Mesh Mesh;
        public Material Material;
        public int SubMesh;
        public ShadowCastingMode CastShadows;
        public bool ReceiveShadows;
    }
}
