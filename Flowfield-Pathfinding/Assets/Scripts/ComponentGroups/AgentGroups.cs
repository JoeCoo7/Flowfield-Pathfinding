
using Unity.Collections;
using Unity.Entities;

namespace Agent.Group
{
    public struct AgentSelection
    {
        [ReadOnly] public ComponentDataArray<Unity.Transforms.Position> position;
        [WriteOnly] public ComponentDataArray<Selection> selection;
        [ReadOnly] public readonly int Length;
    }
}
