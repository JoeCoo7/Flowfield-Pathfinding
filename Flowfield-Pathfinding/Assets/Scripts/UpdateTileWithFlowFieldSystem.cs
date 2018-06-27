using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;

namespace System
{
    public class UpdateTileWithFlowFieldSystem : JobComponentSystem
    {
        struct UpdateJob : IJobParallelFor
        {
            public Tile.Group.AllTiles tiles;

            [ReadOnly]
            public NativeArray<float3> flowField;

            public uint handle;

            public void Execute(int index)
            {
                if (tiles.handles[index].Handle == handle)
                    return;

                tiles.handles[index] = new Tile.FlowFieldHandle { Handle = handle };

                var tileTransform = tiles.transforms[index];
                var position = tiles.position[index];
                var settings = tiles.settings[index];
                var flowFieldIndex = GridUtilties.Grid2Index(settings, position.Value);
                var flowDirection = flowField[flowFieldIndex];

                tileTransform.Value = 
                    math.lookRotationToMatrix(
                        new float3(
                            position.Value.x * settings.cellSize.x - settings.worldSize.x / 2.0f + settings.cellSize.x / 2.0f, 
                            0.0f, 
                            position.Value.y * settings.cellSize.y - settings.worldSize.y / 2.0f + settings.cellSize.y / 2.0f),
                        flowDirection, new float3(0.0f, 1.0f, 0.0f));
                tiles.transforms[index] = tileTransform;
            }
        }

        [Inject]
        Tile.Group.AllTiles m_Tiles;

        [Inject]
        FlowField.Group.FlowFieldResult m_Results;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (m_Results.flowFieldData.Length == 0 || !Main.ActiveInitParams.m_drawFlowField)
                return inputDeps;

            var tileSystem = World.Active.GetExistingManager<TileSystem>();

            var update = new UpdateJob
            {
                tiles = m_Tiles,
                handle = tileSystem.lastGeneratedQueryHandle,
                flowField = tileSystem.GetFlowField(tileSystem.lastGeneratedQueryHandle)
            };

            return update.Schedule(update.tiles.transforms.Length, 64, inputDeps);
        }
    }
}
