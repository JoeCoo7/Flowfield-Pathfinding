using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using RSGLib;

[System.Serializable]
public struct GridSettings : ISharedComponentData
{
    public float2 worldSize;
    public float2 cellSize;
    public int2 cellCount;
    public int2 cellsPerBlock;
    public int2 blockCount;
    public float separationWeight;
    public float alignmentWeight;
}

public class TileSystem : JobComponentSystem
{
    static uint s_QueryHandle = 0;

    [Inject]
    EndFrameBarrier m_EndFrameBarrier;

    [Inject]
    Agent.Group.Selected m_Selected;

    [Inject]
    Agent.Group.SelectedWithQuery m_SelectedWithQuery;

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (!Input.GetMouseButtonDown(StandardInput.RIGHT_MOUSE_BUTTON))
            return inputDeps;

        if (!Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, Mathf.Infinity))
            return inputDeps;

        uint queryHandle = s_QueryHandle++;
        GridSettings gridSettings = InitializationData.Instance.m_grid;
        int numTiles = gridSettings.cellCount.x * gridSettings.cellCount.y;

        var buffer = m_EndFrameBarrier.CreateCommandBuffer();
        var query = new FlowField.Query { Handle = queryHandle };
        for (var i = 0; i < m_Selected.entity.Length; ++i)
            buffer.AddComponent(m_Selected.entity[i], query);
        for (var i = 0; i < m_SelectedWithQuery.entity.Length; ++i)
            buffer.SetComponent(m_SelectedWithQuery.entity[i], query);

        // Create & Initialize heatmap
        var initializeJob = new InitializeHeatmapJob()
        {
            settings = gridSettings,
            heatmap = new NativeArray<int>(numTiles, Allocator.Persistent, NativeArrayOptions.UninitializedMemory) // Deallocated in ComputeFlowFieldJob
        };

        // Compute heatmap from goals
        var heatmapJob = new ComputeHeatmapJob()
        {
            settings = gridSettings,
            goals = new NativeArray<int2>(1, Allocator.TempJob),
            heatmap = initializeJob.heatmap
        };
        heatmapJob.goals[0] = GridUtilties.World2Grid(gridSettings, hit.point);

        // Convert flowfield from heatmap
        var flowFieldJob = new FlowField.ComputeFlowFieldJob
        {
            settings = gridSettings,
            heatmap = heatmapJob.heatmap,
            flowfield = new NativeArray<float3>(numTiles, Allocator.Persistent, NativeArrayOptions.UninitializedMemory)
        };

        var createResultJob = new CreateFlowFieldResultEntity
        {
            commandBuffer = buffer,
            handle = queryHandle,
            flowField = flowFieldJob.flowfield
        };

        // Create all the jobs
        var initializeHandle = initializeJob.Schedule(this, 64, inputDeps);
        var heatmapHandle = heatmapJob.Schedule(initializeHandle);
        var flowFieldHandle = flowFieldJob.Schedule(numTiles, 64, heatmapHandle);
        var createResultHandle = createResultJob.Schedule(flowFieldHandle);
        return createResultHandle;
    }

    const int k_Obstacle = int.MaxValue;

    const int k_Unvisited = k_Obstacle - 1;

    //[BurstCompile]
    struct InitializeHeatmapJob : IJobProcessComponentData<Tile.Cost, Tile.Position>
    {
        [ReadOnly]
        public GridSettings settings;

        [WriteOnly]
        public NativeArray<int> heatmap;

        public void Execute(ref Tile.Cost cost, ref Tile.Position position)
        {
            var outputIndex = GridUtilties.Grid2Index(settings, position.Value);
            heatmap[outputIndex] = math.select(k_Unvisited, k_Obstacle, cost.Value == byte.MaxValue);
        }
    }

    //[BurstCompile]
    struct ComputeHeatmapJob : IJob
    {
        [ReadOnly]
        public GridSettings settings;

        [ReadOnly, DeallocateOnJobCompletion]
        public NativeArray<int2> goals;

        //[ReadOnly]
        //public NativeArray<int> values;

        public NativeArray<int> heatmap;

        public void Execute()
        {
            var openSet = new NativeQueue<int>(Allocator.Temp);

            for (int i = 0; i < goals.Length; ++i)
            {
                var tileIndex = GridUtilties.Grid2Index(settings, goals[i]);
                heatmap[tileIndex] = 0;//values[i];
                openSet.Enqueue(tileIndex);
            }

            // Search!
            while (openSet.Count > 0)
            {
                var index = openSet.Dequeue();
                var distance = heatmap[index];
                var newDistance = distance + 1;
                var grid = GridUtilties.Index2Grid(settings, index);

                for (GridUtilties.Direction dir = GridUtilties.Direction.N; dir <= GridUtilties.Direction.W; ++dir)
                {
                    var neighborGrid = grid + GridUtilties.Offset[(int)dir];
                    var neighborIndex = GridUtilties.Grid2Index(settings, neighborGrid);

                    if (heatmap[neighborIndex] != k_Obstacle && newDistance < heatmap[neighborIndex])
                    {
                        heatmap[neighborIndex] = newDistance;
                        openSet.Enqueue(neighborIndex);
                    }
                }
            }

            openSet.Dispose();
        }
    }

    //[BurstCompile]
    struct CreateFlowFieldResultEntity : IJob
    {
        [ReadOnly]
        public uint handle;

        [ReadOnly]
        public NativeArray<float3> flowField;

        public EntityCommandBuffer commandBuffer;

        public void Execute()
        {
            Manager.Archetype.CreateFlowFieldResult(commandBuffer, handle, new FlowField.Data { Value = flowField });
        }
    }
}
