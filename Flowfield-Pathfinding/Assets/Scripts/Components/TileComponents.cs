using Unity.Entities;
using Unity.Mathematics;

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
}
