
using Unity.Collections;
using Unity.Entities;

namespace Agent.Group
{
    public struct WithQuery
    {
        [ReadOnly] public EntityArray entity;
        [ReadOnly] public SharedComponentDataArray<FlowField.Data> flowFieldData;

        public ComponentDataArray<FlowField.Query> flowFieldQuery;
        [ReadOnly] public int Length;
    }

    public struct Selected
    {
        [ReadOnly] public EntityArray entity;
        [ReadOnly] public SharedComponentDataArray<FlowField.Data> flowFieldData;
        [ReadOnly] public ComponentDataArray<Selection> selection;
        public SubtractiveComponent<FlowField.Query> flowFieldQuery;
        [ReadOnly] public int Length;
    }

    public struct SelectedWithQuery
    {
        [ReadOnly] public EntityArray entity;
        [ReadOnly] public SharedComponentDataArray<FlowField.Data> flowFieldData;
        [ReadOnly] public ComponentDataArray<Selection> selection;
        public ComponentDataArray<FlowField.Query> flowFieldQuery;
        [ReadOnly] public int Length;
    }

    public struct AgentSelection
    {
        [ReadOnly] public ComponentDataArray<Unity.Transforms.Position> position;
        [WriteOnly] public ComponentDataArray<Selection> selection;
        [ReadOnly] public int Length;
    }
}
