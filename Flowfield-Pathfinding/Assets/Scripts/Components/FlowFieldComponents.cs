using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowField
{
    [System.Serializable]
    public struct Data : ISharedComponentData
    {
		public NativeArray<float3> Value;
	//	public NativeArray<float> Height;
	//	public NativeArray<float3> Normal;
	}

	[System.Serializable]
    public struct Query : IComponentData
    {
        public uint Handle;
    }

    [System.Serializable]
    public struct Result : IComponentData
    {
        public uint Handle;
    }
}
