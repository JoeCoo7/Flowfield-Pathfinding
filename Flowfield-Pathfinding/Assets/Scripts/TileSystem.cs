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

[UpdateInGroup(typeof(ProcessGroup))]
[UpdateAfter(typeof(AgentSystem))]
public class TileSystem : JobComponentSystem
{
    static uint s_QueryHandle = 0;

    [Inject]
    EndFrameBarrier m_EndFrameBarrier;

    [Inject]
    Agent.Group.Selected m_Selected;

    [Inject]
    Agent.Group.SelectedWithQuery m_SelectedWithQuery;

    NativeArray<int2> m_Offsets;

    int2 m_Goal = new int2(197, 232);

    protected override void OnCreateManager(int capacity)
    {
        base.OnCreateManager(capacity);

        m_Offsets = new NativeArray<int2>(GridUtilties.Offset.Length, Allocator.Persistent);
        m_Offsets.CopyFrom(GridUtilties.Offset);
    }

    protected override void OnDestroyManager()
    {
        m_Offsets.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (!Input.GetMouseButtonDown(StandardInput.RIGHT_MOUSE_BUTTON))
            return inputDeps;

        if (!Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, Mathf.Infinity))
            return inputDeps;

        m_Goal = GridUtilties.World2Grid(Main.ActiveInitParams.m_grid, hit.point);

        return CreateJobs(inputDeps);
    }

    JobHandle CreateJobs(JobHandle inputDeps)
    {
        GridSettings gridSettings = Main.ActiveInitParams.m_grid;
        int numTiles = gridSettings.cellCount.x * gridSettings.cellCount.y;

        uint queryHandle = s_QueryHandle++;

        for (var i = 0; i < m_SelectedWithQuery.entity.Length; ++i)
        {
            var query = m_SelectedWithQuery.flowFieldQuery[i];
            query.Handle = queryHandle;
            m_SelectedWithQuery.flowFieldQuery[i] = query;
        }

        var buffer = m_EndFrameBarrier.CreateCommandBuffer();
        var newQuery = new FlowField.Query { Handle = queryHandle };
        for (var i = 0; i < m_Selected.entity.Length; ++i)
            buffer.AddComponent(m_Selected.entity[i], newQuery);

        // Create & Initialize heatmap
        var initializeHeatmapJob = new InitializeHeatmapJob()
        {
            settings = gridSettings,
            heatmap = new NativeArray<int>(numTiles, Allocator.Persistent, NativeArrayOptions.UninitializedMemory) // Deallocated in ComputeFlowFieldJob
        };

        // Compute heatmap from goals
        var computeHeatmapJob = new ComputeHeatmapJob()
        {
            settings = gridSettings,
            goals = new NativeArray<int2>(1, Allocator.TempJob),
            heatmap = initializeHeatmapJob.heatmap,
            offsets = m_Offsets,
            openSet = new NativeArray<int>(initializeHeatmapJob.heatmap.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory)
        };
        computeHeatmapJob.goals[0] = m_Goal;

        // Convert flowfield from heatmap
        var computeFlowFieldJob = new FlowField.ComputeFlowFieldJob
        {
            settings = gridSettings,
            heatmap = computeHeatmapJob.heatmap,
            flowfield = new NativeArray<float3>(numTiles, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            offsets = m_Offsets
        };

        var createResultJob = new CreateFlowFieldResultEntity
        {
            commandBuffer = buffer,
            handle = queryHandle,
            flowField = computeFlowFieldJob.flowfield
        };

        // Create all the jobs
        var initializeHeatmapHandle = initializeHeatmapJob.Schedule(this, 64, inputDeps);
        var computeHeatmapHandle = computeHeatmapJob.Schedule(initializeHeatmapHandle);
        var flowFieldHandle = computeFlowFieldJob.Schedule(numTiles, 64, computeHeatmapHandle);
        var createResultHandle = createResultJob.Schedule(flowFieldHandle);
        return createResultHandle;
    }

    const int k_Obstacle = int.MaxValue;

    const int k_Unvisited = k_Obstacle - 1;

    [BurstCompile]
    struct InitializeHeatmapJob : IJobProcessComponentData<Tile.Cost, Tile.Position>
    {
        [ReadOnly]
        public GridSettings settings;

        [WriteOnly]
        public NativeArray<int> heatmap;

        public void Execute([ReadOnly] ref Tile.Cost cost, [ReadOnly] ref Tile.Position position)
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
}
