using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Agent
{
    public struct Velocity : IComponentData
    {
        public float3 Value;
    }

    public struct Selection : IComponentData
    {
        public byte Value;
    }

    public struct Goal : IComponentData
    {
        public int2 Current;
        public int2 Target;
    }
    public struct AgentMeshInstanceRenderer : ISharedComponentData
    {
        public Mesh mesh;
        public Material material;
        public int subMesh;
        public ShadowCastingMode castShadows;
        public bool receiveShadows;
    }
}
