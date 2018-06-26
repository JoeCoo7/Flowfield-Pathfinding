
using Unity.Collections;
using Unity.Entities;

namespace Agent.Group
{
    public struct WithQuery
    {
        [ReadOnly] public EntityArray entity;
        [ReadOnly] public SharedComponentDataArray<FlowField.Data> flowFieldData;

        public ComponentDataArray<FlowField.Query> flowFieldQuery;
    }

    public struct Selected
    {
        [ReadOnly] public SharedComponentDataArray<FlowField.Data> flowFieldData;

        public EntityArray entity;
        //ComponentDataArray<SelectedUnit> selected;
        public SubtractiveComponent<FlowField.Query> flowFieldQuery;
    }

    public struct SelectedWithQuery
    {
        [ReadOnly] public SharedComponentDataArray<FlowField.Data> flowFieldData;

        public EntityArray entity;
        //ComponentDataArray<SelectedUnit> selected;
        public ComponentDataArray<FlowField.Query> flowFieldQuery;
    }
}
