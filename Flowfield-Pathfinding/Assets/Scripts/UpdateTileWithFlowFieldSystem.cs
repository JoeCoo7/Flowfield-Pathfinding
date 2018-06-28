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

            [ReadOnly, DeallocateOnJobCompletion]
            public NativeArray<float3> flowField;

            public int handle;

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
        TileSystem m_TileSystem;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var tileSystem = m_TileSystem;
            if (tileSystem.lastGeneratedQueryHandle == TileSystem.k_InvalidHandle || !Main.ActiveInitParams.m_drawFlowField)
                return inputDeps;

            var update = new UpdateJob
            {
                tiles = m_Tiles,
                handle = tileSystem.lastGeneratedQueryHandle,
                flowField = tileSystem.GetFlowFieldCopy(tileSystem.lastGeneratedQueryHandle, Allocator.TempJob)
            };

            return update.Schedule(update.tiles.transforms.Length, 64, inputDeps);
        }
    }
}
