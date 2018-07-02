using ECSInput;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;

//-----------------------------------------------------------------------------
public class UpdateTileWithFlowFieldSystem : JobComponentSystem
{
    [Inject] private Tile.Group.AllTiles m_Tiles;
    [Inject] private InputDataGroup m_Input;
    [Inject] private TileSystem m_TileSystem;
    
    
    //-----------------------------------------------------------------------------
    [BurstCompile]
    struct UpdateJob : IJobParallelFor
    {
        public Tile.Group.AllTiles tiles;
        
        [ReadOnly, DeallocateOnJobCompletion] public NativeArray<float3> FlowField;
        [ReadOnly] public NativeArray<float> TerrainHeight;
        public int Handle;

        public void Execute(int index)
        {
            if (tiles.handles[index].Handle == Handle)
                return;

            tiles.handles[index] = new Tile.FlowFieldHandle { Handle = Handle };

            var tileTransform = tiles.transforms[index];
            var position = tiles.position[index];
            var settings = tiles.settings[index];
            var tileIndex = GridUtilties.Grid2Index(settings, position.Value);
            var flowDirection = FlowField[tileIndex];
            var height = TerrainHeight[tileIndex];

            var scale = height < settings.heightScale * 0.4f ? new float3(settings.cellSize.x, settings.cellSize.x, settings.cellSize.x * 0.5f) : new float3(0);
            var pos = new float3(position.Value.x * settings.cellSize.x - settings.worldSize.x / 2.0f, height + 5.0f,
                position.Value.y * settings.cellSize.y - settings.worldSize.y / 2.0f);
            
            tileTransform.Value = math.mul(math.lookRotationToMatrix(pos, flowDirection, new float3(0.0f, 1.0f, 0.0f)), math.scale(scale));
            tiles.transforms[index] = tileTransform;
        }
    }

    //-----------------------------------------------------------------------------
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (m_Input.Buttons[0].Values["ShowFlowfield"].Status == InputButtons.UP)
            Main.ActiveInitParams.m_drawFlowField = !Main.ActiveInitParams.m_drawFlowField; 
        if (m_Input.Buttons[0].Values["ShowHeatmap"].Status == InputButtons.UP)
            Main.ActiveInitParams.m_drawHeatField = !Main.ActiveInitParams.m_drawHeatField;
        if (m_Input.Buttons[0].Values["SmoothFlowfield"].Status == InputButtons.UP)
            Main.ActiveInitParams.m_smoothFlowField = !Main.ActiveInitParams.m_smoothFlowField;
        
        var tileSystem = m_TileSystem;
        if (tileSystem.LastGeneratedQueryHandle == TileSystem.k_InvalidHandle || !Main.ActiveInitParams.m_drawFlowField)
            return inputDeps;

        var update = new UpdateJob
        {
            tiles = m_Tiles,
            Handle = tileSystem.LastGeneratedQueryHandle,
            FlowField = tileSystem.GetFlowFieldCopy(tileSystem.LastGeneratedQueryHandle, Allocator.TempJob),
            TerrainHeight = Main.TerrainHeight
        };

        return update.Schedule(update.tiles.transforms.Length, 64, inputDeps);
    }
}
