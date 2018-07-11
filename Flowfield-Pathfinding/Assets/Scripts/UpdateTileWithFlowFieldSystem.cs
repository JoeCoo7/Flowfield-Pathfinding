using ECSInput;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;

//-----------------------------------------------------------------------------
[UpdateInGroup(typeof(ProcessGroup))]
public class UpdateTileWithFlowFieldSystem : JobComponentSystem
{
    [Inject] private Tile.AllTiles m_Tiles;
    [Inject] private InputDataGroup m_Input;
    [Inject] private TileSystem m_TileSystem;
    
    //-----------------------------------------------------------------------------
    [BurstCompile]
    struct UpdateJob : IJobParallelFor
    {
        public Tile.AllTiles Tiles;
        
        [ReadOnly, DeallocateOnJobCompletion] public NativeArray<float3> FlowField;
        [ReadOnly] public NativeArray<float> TerrainHeight;
        public Tile.GridSettings Settings;
        public int Handle;

        //-----------------------------------------------------------------------------
        public void Execute(int index)
        {
            if (Tiles.Handles[index].Handle == Handle)
                return;

            Tiles.Handles[index] = new Tile.FlowFieldHandle { Handle = Handle };

            var tileTransform = Tiles.Transforms[index];
            var position = Tiles.Position[index];
            var tileIndex = GridUtilties.Grid2Index(Settings, position.Value);
            var flowDirection = FlowField[tileIndex];
            var height = TerrainHeight[tileIndex];

            var scale = Tiles.Cost[index].Value < byte.MaxValue && !FlowField[tileIndex].Equals(new float3(0)) ? new float3(Settings.cellSize.x, Settings.cellSize.x, Settings.cellSize.x * 0.5f) : new float3(0);
            //var scale = new float3(Settings.cellSize.x, Settings.cellSize.x, Settings.cellSize.x * 0.5f);
            var pos = new float3(position.Value.x * Settings.cellSize.x - Settings.worldSize.x / 2.0f, height + 5.0f,
                position.Value.y * Settings.cellSize.y - Settings.worldSize.y / 2.0f);
            
            tileTransform.Value = math.mul(float4x4.lookAt(pos, flowDirection, new float3(0.0f, 1.0f, 0.0f)), float4x4.scale(scale));
            Tiles.Transforms[index] = tileTransform;
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
            Tiles = m_Tiles,
            Settings = m_Tiles.Settings[0], 
            Handle = tileSystem.LastGeneratedQueryHandle,
            FlowField = tileSystem.GetFlowFieldCopy(tileSystem.LastGeneratedQueryHandle, Allocator.TempJob),
            TerrainHeight = Main.TerrainHeight
        };

        return update.Schedule(update.Tiles.Transforms.Length, 64, inputDeps);
    }
}
