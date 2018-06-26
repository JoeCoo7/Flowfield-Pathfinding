﻿using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowField
{
    [System.Serializable]
    public struct Data : ISharedComponentData
    {
	    public NativeArray<float3> Value;
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
