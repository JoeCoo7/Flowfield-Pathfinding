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
}
