
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
        [ReadOnly] public EntityArray entity;
        [ReadOnly] public SharedComponentDataArray<FlowField.Data> flowFieldData;
        [ReadOnly] ComponentDataArray<Selection> selected;
        public SubtractiveComponent<FlowField.Query> flowFieldQuery;
    }

    public struct SelectedWithQuery
    {
        [ReadOnly] public EntityArray entity;
        [ReadOnly] public SharedComponentDataArray<FlowField.Data> flowFieldData;
        [ReadOnly] ComponentDataArray<Selection> selected;
        public ComponentDataArray<FlowField.Query> flowFieldQuery;
    }

    public struct SelectedPositions
    {
        [ReadOnly] public EntityArray entity;
        [ReadOnly] public ComponentDataArray<Unity.Transforms.Position> position;
        [ReadOnly] public ComponentDataArray<Selection> selection;
        [ReadOnly] public int Length;
    }

    public struct UnselectedPositions
    {
        [ReadOnly] public EntityArray entity;
        [ReadOnly] public ComponentDataArray<Unity.Transforms.Position> position;
        [ReadOnly] public SubtractiveComponent<Selection> selection;
        [ReadOnly] public int Length;
    }
}
