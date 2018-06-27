using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace System
{
    public class UpdateTileWithFlowFieldSystem : JobComponentSystem
    {
        struct UpdateJob : IJobParallelFor
        {
            public Tile.Group.AllTiles tiles;

            public FlowField.Group.FlowFieldResult results;

            public void Execute(int index)
            {
                uint finalHandle = 0;
                FlowField.Data finalData = new FlowField.Data();
                for (int i = results.flowFieldData.Length - 1; i >= 0; --i)
                {
                    if (finalHandle > results.flowFieldResult[i].Handle)
                        continue;

                    finalHandle = results.flowFieldResult[i].Handle;
                    finalData = results.flowFieldData[i];
                }

                if (tiles.handles[index].Handle == finalHandle)
                    return;
                tiles.handles[index] = new Tile.FlowFieldHandle { Handle = finalHandle };

                var tileTransform = tiles.transforms[index];
                var position = tiles.position[index];
                var settings = tiles.settings[index];
                var flowFieldIndex = GridUtilties.Grid2Index(settings, position.Value);
                var flowDirection = finalData.Value[flowFieldIndex];

                tileTransform.Value = 
                    math.lookRotationToMatrix(
                        new float3(position.Value.x - settings.worldSize.x / 2.0f - 0.5f, 0.0f, position.Value.y - settings.worldSize.y / 2.0f - 0.5f),
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
            var update = new UpdateJob
            {
                tiles = m_Tiles,
                results = m_Results
            };

            return update.Schedule(update.tiles.transforms.Length, 64, inputDeps);
        }
    }
}
