using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using RSGLib;

[System.Serializable]
public struct GridSettings : ISharedComponentData
{
    public float2 worldSize;
    public float2 cellSize;
    public int2 cellCount;
    public int2 cellsPerBlock;
    public int2 blockCount;
    public float agentSeparationWeight;
    public float agentAlignmentWeight;
    public float agentTargetFlowfieldWeight;
    public float agentTerrainFlowfieldWeight;
    public float agentRadius;
}

public class TileSystem : JobComponentSystem
{
    static uint s_QueryHandle = 0;

    [Inject]
    DebugHeatmap.Group m_DebugHeatmapGroup;

    [Inject]
    EndFrameBarrier m_EndFrameBarrier;

    [Inject]
    Agent.Group.Selected m_Selected;

    [Inject]
    Agent.Group.SelectedWithQuery m_SelectedWithQuery;

    NativeArray<int2> m_Offsets;

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

        if (!m_Offsets.IsCreated)
        {
            m_Offsets = new NativeArray<int2>(GridUtilties.Offset.Length, Allocator.Persistent);
            m_Offsets.CopyFrom(GridUtilties.Offset);
        }

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
            heatmap = initializeJob.heatmap,
            offsets = m_Offsets,
            openSet = new NativeArray<int>(initializeJob.heatmap.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory)
        };
        heatmapJob.goals[0] = GridUtilties.World2Grid(gridSettings, hit.point);

        var debugHeatmap = m_DebugHeatmapGroup.GetOrCreateHeatmap(numTiles);
        var copyDebugHeatmapJob = new DebugHeatmap.CopyHeatmapJob()
        {
            inputHeatmap = heatmapJob.heatmap,
            outputHeatmap = debugHeatmap
        };
        buffer.SetSharedComponent(m_DebugHeatmapGroup.entities[0], new DebugHeatmap.Component { Value = debugHeatmap, Time = Time.realtimeSinceStartup });

        // Convert flowfield from heatmap
        var flowFieldJob = new FlowField.ComputeFlowFieldJob
        {
            settings = gridSettings,
            heatmap = heatmapJob.heatmap,
            flowfield = new NativeArray<float3>(numTiles, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            offsets = m_Offsets
        };

        var createResultJob = new CreateFlowFieldResultEntity
        {
            commandBuffer = buffer,
            handle = queryHandle,
            flowField = flowFieldJob.flowfield
        };

        var updateFlowDirectionsJob = new UpdateFlowDirectionsJob
        {
            settings = gridSettings,
            flowField = createResultJob.flowField
        };

        // Create all the jobs
        var initializeHandle = initializeJob.Schedule(this, 64, inputDeps);
        var heatmapHandle = (lastHeatmapJob = heatmapJob.Schedule(initializeHandle));
        var copyDebugHeatmapHandle = copyDebugHeatmapJob.Schedule(numTiles, 64, heatmapHandle);
        var flowFieldHandle = flowFieldJob.Schedule(numTiles, 64, copyDebugHeatmapHandle);
        var createResultHandle = createResultJob.Schedule(flowFieldHandle);
        var updateFlowDirectionsHandle = updateFlowDirectionsJob.Schedule(this, 64, createResultHandle);
        return updateFlowDirectionsHandle;
    }
	public static JobHandle lastHeatmapJob;
    const int k_Obstacle = int.MaxValue;

    const int k_Unvisited = k_Obstacle - 1;

    [BurstCompile]
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

    [BurstCompile]
    struct ComputeHeatmapJob : IJob
    {
        [ReadOnly]
        public GridSettings settings;

        [ReadOnly, DeallocateOnJobCompletion]
        public NativeArray<int2> goals;

        [ReadOnly]
        public NativeArray<int2> offsets;

        //[ReadOnly]
        //public NativeArray<int> values;

        public NativeArray<int> heatmap;

        [DeallocateOnJobCompletion]
        public NativeArray<int> openSet;

        int queueStart;
        int queueEnd;
        int queueLength;

        void Enqueue(NativeArray<int> queue, int value)
        {
            queue[queueEnd] = value;
            queueEnd = (queueEnd + 1) % queue.Length;
            ++queueLength;
        }

        int Dequeue(NativeArray<int> queue)
        {
            var retVal = queue[queueStart];
            queueStart = (queueStart + 1) % queue.Length;
            --queueLength;
            return retVal;
        }

        public void Execute()
        {
            for (int i = 0; i < goals.Length; ++i)
            {
                var tileIndex = GridUtilties.Grid2Index(settings, goals[i]);
                heatmap[tileIndex] = 0;//values[i];
                Enqueue(openSet, tileIndex);
            }

            // Search!
            while (queueLength > 0)
            {
                var index = Dequeue(openSet);
                var distance = heatmap[index];
                var newDistance = distance + 1;
                var grid = GridUtilties.Index2Grid(settings, index);

                for (GridUtilties.Direction dir = GridUtilties.Direction.N; dir <= GridUtilties.Direction.W; ++dir)
                {
                    var neighborGrid = grid + offsets[(int)dir];
                    var neighborIndex = GridUtilties.Grid2Index(settings, neighborGrid);

                    if (neighborIndex != -1 && heatmap[neighborIndex] != k_Obstacle && newDistance < heatmap[neighborIndex])
                    {
                        heatmap[neighborIndex] = newDistance;
                        Enqueue(openSet, neighborIndex);
                    }
                }
            }
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

    struct UpdateFlowDirectionsJob : IJobProcessComponentData<Unity.Transforms.TransformMatrix, Tile.Position>
    {
        [ReadOnly]
        public GridSettings settings;

        [ReadOnly]
        public NativeArray<float3> flowField;

        [ReadOnly]
        public Unity.Transforms.TransformMatrix originTransform;

        public void Execute(ref Unity.Transforms.TransformMatrix transform, ref Tile.Position position)
        {
            var flowFieldIndex = GridUtilties.Grid2Index(settings, position.Value);
            var flowDirection = flowField[flowFieldIndex];
            var scale = new float3(0.2f, 0.2f, 0.8f);
            transform.Value = 
                math.mul(math.lookRotationToMatrix(
                    new float3(position.Value.x - settings.worldSize.x/2.0f - 0.5f, 0.0f, position.Value.y - settings.worldSize.y/2.0f - 0.5f),
                    flowDirection, new float3(0.0f, 1.0f, 0.0f)),
                math.scale(scale));

        }
    }
}
