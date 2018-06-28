using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

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
}
