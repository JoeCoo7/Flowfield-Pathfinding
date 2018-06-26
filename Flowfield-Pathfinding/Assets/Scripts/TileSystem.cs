using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
//using Unity.Burst;
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
}

public struct TileCost : IComponentData
{
    public byte value;
}

public struct TileCollision : IComponentData
{
    public float3 value;
}

public struct TilePosition : IComponentData
{
    public int2 value;
}

public class TileSystem : JobComponentSystem
{
    static uint s_QueryHandle = 0;

    [Inject]
    DebugHeatmap.Group m_DebugHeatmapGroup;

    [Inject]
    EndFrameBarrier m_EndFrameBarrier;

    struct SelectedUnits
    {
        public EntityArray entities;
        [ReadOnly]
        SharedComponentDataArray<FlowField.Data> flowField;
        SubtractiveComponent<FlowField.Query> flowFieldQuery;
        //ComponentDataArray<SelectedUnit> selected;
    }

    struct SelectedUnitsWithQuery
    {
        public EntityArray entities;
        [ReadOnly]
        SharedComponentDataArray<FlowField.Data> flowField;
        ComponentDataArray<FlowField.Query> flowFieldQuery;
        //ComponentDataArray<SelectedUnit> selected;
    }

    [Inject]
    SelectedUnits selectedUnits;

    [Inject]
    SelectedUnits selectedUnitsWithQuery;

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
        for (var i = 0; i < selectedUnits.entities.Length; ++i)
            buffer.AddComponent(selectedUnits.entities[i], query);
        for (var i = 0; i < selectedUnitsWithQuery.entities.Length; ++i)
            buffer.SetComponent(selectedUnitsWithQuery.entities[i], query);

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

        var debugHeatmap = m_DebugHeatmapGroup.GetOrCreateHeatmap(numTiles);
        var copyDebugHeatmapJob = new DebugHeatmap.CopyHeatmapJob()
        {
            inputHeatmap = heatmapJob.heatmap,
            outputHeatmap = debugHeatmap
        };
        buffer.SetSharedComponent(m_DebugHeatmapGroup.entities[0], new DebugHeatmap.Component { Value = debugHeatmap });

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
        var copyDebugHeatmapHandle = copyDebugHeatmapJob.Schedule(numTiles, 64, heatmapHandle);
        var flowFieldHandle = flowFieldJob.Schedule(numTiles, 64, copyDebugHeatmapHandle);
        var createResultHandle = createResultJob.Schedule(flowFieldHandle);
        return createResultHandle;
    }

    const int k_Obstacle = int.MaxValue;

    const int k_Unvisited = k_Obstacle - 1;

    //[BurstCompile]
    struct InitializeHeatmapJob : IJobProcessComponentData<TileCost, TilePosition>
    {
        [ReadOnly]
        public GridSettings settings;

        [WriteOnly]
        public NativeArray<int> heatmap;

        public void Execute(ref TileCost cost, ref TilePosition position)
        {
            var outputIndex = GridUtilties.Grid2Index(settings, position.value);
            heatmap[outputIndex] = math.select(k_Obstacle, k_Unvisited, cost.value == byte.MaxValue);
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
            var openSet = new NativeQueue<int>(Allocator.TempJob);

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
