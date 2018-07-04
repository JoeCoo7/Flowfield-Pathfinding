
using Tile;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Position = Unity.Transforms.Position;

namespace Agent
{
    //-----------------------------------------------------------------------------
    public struct SelectionGroup
    {
        [ReadOnly] public ComponentDataArray<Position> position;
        [WriteOnly] public ComponentDataArray<Selection> selection;
        [ReadOnly] public readonly int Length;
    }
    
    //-----------------------------------------------------------------------------
    public struct AllDataGroup 
    {
        [ReadOnly] public SharedComponentDataArray<GridSettings> GridSettings;
        [ReadOnly] public ComponentDataArray<Goal> Goals;
        
        public ComponentDataArray<TargetReached> TargetReached;
        public ComponentDataArray<Velocity> Velocities;
        public ComponentDataArray<Position> Positions;
        public ComponentDataArray<Rotation> Rotations;
        public readonly int Length;
    }
    
    //-----------------------------------------------------------------------------
    public struct SpawnGroup
    {
        [ReadOnly] public SharedComponentDataArray<GridSettings> GridSettings;
        [ReadOnly] public ComponentDataArray<Cost> TileCost;
    }
    
}
