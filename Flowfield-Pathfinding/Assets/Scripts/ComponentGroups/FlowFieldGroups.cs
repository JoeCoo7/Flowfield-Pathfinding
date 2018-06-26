using Unity.Collections;
using Unity.Entities;

namespace FlowField.Group
{
    public struct FlowFieldResult
    {
        [ReadOnly] public SharedComponentDataArray<FlowField.Data> flowFieldData;
        [ReadOnly] public ComponentDataArray<FlowField.Result> flowFieldResult;
    }
}
