using Tile;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace FlowField
{
    //-----------------------------------------------------------------------------
    [BurstCompile]
    public struct ComputeFimEikonalFlowFieldJob : IJobParallelFor
    {
        [ReadOnly] public GridSettings Settings;
        [ReadOnly] public NativeArray<float> DistanceMap;
        [ReadOnly] public NativeArray<int2> Offsets;
        public NativeArray<float3> Flowfield;

        //-----------------------------------------------------------------------------
        public void Execute(int index)
        {
            int2 grid = GridUtilties.Index2Grid(Settings, index);
            float weight = DistanceMap[index];
            Flowfield[index] = new float3(0);

            for (GridUtilties.Direction dir = 0; dir < GridUtilties.Direction.MAX; ++dir)
            {
                int2 dirOffset = Offsets[(int)dir];
                int neigborIdx = GridUtilties.Grid2Index(Settings, grid + dirOffset);
                if (neigborIdx == -1)
                    continue;

                float neighborWeight = DistanceMap[neigborIdx];
                if (weight <= neighborWeight)
                    continue;

                weight = neighborWeight;
                float3 direction = new float3(dirOffset.x, 0, dirOffset.y);
                Flowfield[index] = math.normalize(direction);
            }
        }
    }

    [BurstCompile]
    public struct ComputeDijkstraFlowFieldJob : IJobParallelFor
    {
        [ReadOnly] public GridSettings Settings;
        [ReadOnly] public NativeArray<float> DistanceMap;
        [ReadOnly] public NativeArray<int2> Offsets;
        public NativeArray<float3> Flowfield;

        //-----------------------------------------------------------------------------
        public void Execute(int index)
        {
            int2 gridPos = GridUtilties.Index2Grid(Settings, index);
            float weight = DistanceMap[index];
            Flowfield[index] = new float3(0);

            for (GridUtilties.Direction dir = 0; dir < GridUtilties.Direction.MAX; ++dir)
            {
                int2 dirOffset = Offsets[(int) dir];
                int neigborIdx = GridUtilties.Grid2Index(Settings, gridPos + dirOffset);
                if (neigborIdx == -1)
                    continue;

                float neighborWeight = DistanceMap[neigborIdx];
                if (weight <= neighborWeight)
                    continue;

                weight = neighborWeight;
                float3 direction = new float3(dirOffset.x, 0, dirOffset.y);
                Flowfield[index] = math.normalize(direction);
            }
        }
    }
}
