using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowField
{
    [System.Serializable]
    public struct Query : IComponentData
    {
        public int Handle;
    }

    [System.Serializable]
    public struct Result : IComponentData
    {
        public int Handle;
    }
}
