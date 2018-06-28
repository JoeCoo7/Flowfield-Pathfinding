using ECSInput;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;

namespace System
{
    public class UpdateTileWithFlowFieldSystem : JobComponentSystem
    {
        [BurstCompile]
        struct UpdateJob : IJobParallelFor
        {
            public Tile.Group.AllTiles tiles;
            
            [ReadOnly, DeallocateOnJobCompletion] public NativeArray<float3> flowField;
            [ReadOnly] public NativeArray<float> terrainHeight;

            public int handle;

            public void Execute(int index)
            {
                if (tiles.handles[index].Handle == handle)
                    return;

                tiles.handles[index] = new Tile.FlowFieldHandle { Handle = handle };

                var tileTransform = tiles.transforms[index];
                var position = tiles.position[index];
                var settings = tiles.settings[index];
                var tileIndex = GridUtilties.Grid2Index(settings, position.Value);
                var flowDirection = flowField[tileIndex];
                var height = terrainHeight[tileIndex];

                var scale = height < settings.heightScale * 0.4f ? new float3(settings.cellSize.x, settings.cellSize.x, settings.cellSize.x * 0.5f) : new float3(0);

                tileTransform.Value =
                    math.mul(
                        math.lookRotationToMatrix(
                            new float3(
                                position.Value.x * settings.cellSize.x - settings.worldSize.x / 2.0f,
                                height + 5.0f,
                                position.Value.y * settings.cellSize.y - settings.worldSize.y / 2.0f),
                            flowDirection, new float3(0.0f, 1.0f, 0.0f)),
                        math.scale(scale)
                        );
                tiles.transforms[index] = tileTransform;
            }
        }

        [Inject] Tile.Group.AllTiles m_Tiles;
        [Inject] InputDataGroup m_input;


        [Inject]
        TileSystem m_TileSystem;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (m_input.Buttons[0].Values["ShowFlowfield"].Status == InputButtons.UP)
                Main.ActiveInitParams.m_drawFlowField = !Main.ActiveInitParams.m_drawFlowField; 
            if (m_input.Buttons[0].Values["ShowHeatmap"].Status == InputButtons.UP)
                Main.ActiveInitParams.m_drawHeatField = !Main.ActiveInitParams.m_drawHeatField;
            if (m_input.Buttons[0].Values["SmoothFlowfield"].Status == InputButtons.UP)
                Main.ActiveInitParams.m_smoothFlowField = !Main.ActiveInitParams.m_smoothFlowField;
            
            var tileSystem = m_TileSystem;
            if (tileSystem.lastGeneratedQueryHandle == TileSystem.k_InvalidHandle || !Main.ActiveInitParams.m_drawFlowField)
                return inputDeps;

            var update = new UpdateJob
            {
                tiles = m_Tiles,
                handle = tileSystem.lastGeneratedQueryHandle,
                flowField = tileSystem.GetFlowFieldCopy(tileSystem.lastGeneratedQueryHandle, Allocator.TempJob),
                terrainHeight = Main.TerrainHeight
            };

            return update.Schedule(update.tiles.transforms.Length, 64, inputDeps);
        }
    }
}
