using System.Collections.Generic;
using Tile;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.PlayerLoop;
using Unity.Rendering;

//-----------------------------------------------------------------------------
// ReSharper disable once RequiredBaseTypesIsNotInherited
[UpdateInGroup(typeof(RenderingGroup))]
[UpdateAfter(typeof(PreLateUpdate.ParticleSystemBeginUpdateAll))]
[ExecuteInEditMode]
public class TileMeshInstanceRendererSystem : ComponentSystem
{
    // Instance renderer takes only batches of 1023
    Matrix4x4[] m_MatricesArray = new Matrix4x4[1023];
	List<TileMeshInstanceRenderer>  m_CacheduniqueRendererTypes = new List<TileMeshInstanceRenderer>(10);
	ComponentGroup m_InstanceRendererGroup;

	//-----------------------------------------------------------------------------
    public unsafe static void CopyMatrices(ComponentDataArray<TransformMatrix> transforms, int beginIndex, int length, Matrix4x4[] outMatrices)
    {
        fixed (Matrix4x4* matricesPtr = outMatrices)
        {
            Assert.AreEqual(sizeof(Matrix4x4), sizeof(TransformMatrix));
	        var matricesSlice = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<TransformMatrix>(matricesPtr, sizeof(Matrix4x4), length);
	        #if ENABLE_UNITY_COLLECTIONS_CHECKS
	        NativeSliceUnsafeUtility.SetAtomicSafetyHandle(ref matricesSlice, AtomicSafetyHandle.GetTempUnsafePtrSliceHandle());
	        #endif
            transforms.CopyTo(matricesSlice, beginIndex);
        }
    }

	//-----------------------------------------------------------------------------
	protected override void OnCreateManager(int capacity)
	{
	    // We want to find all TileMeshInstanceRenderer & TransformMatrix combinations and render them
	    m_InstanceRendererGroup = GetComponentGroup(
            ComponentType.ReadOnly<TileMeshInstanceRenderer>(),
            ComponentType.ReadOnly<TransformMatrix>());
	}

	//-----------------------------------------------------------------------------
	protected override void OnUpdate()
	{
        // Early out if we have disabled flow field rendering
        if (!Main.ActiveInitParams.m_drawFlowField)
            return;

		// We want to iterate over all unique MeshInstanceRenderer shared component data,
		// that are attached to any entities in the world
        EntityManager.GetAllUniqueSharedComponentDatas(m_CacheduniqueRendererTypes);
		var forEachFilter = m_InstanceRendererGroup.CreateForEachFilter(m_CacheduniqueRendererTypes);

        for (int i = 0;i != m_CacheduniqueRendererTypes.Count;i++)
        {
            // For each unique MeshInstanceRenderer data, we want to get all entities with a TransformMatrix
            // SharedComponentData gurantees that all those entities are packed togehter in a chunk with linear memory layout.
            // As a result the copy of the matrices out is internally done via memcpy.
            var renderer = m_CacheduniqueRendererTypes[i];
            var transforms = m_InstanceRendererGroup.GetComponentDataArray<TransformMatrix>(forEachFilter, i);

            // Graphics.DrawMeshInstanced has a set of limitations that are not optimal for working with ECS.
            // Specifically:
            // * No way to push the matrices from a job
            // * no NativeArray API, currently uses Matrix4x4[]
            // As a result this code is not yet jobified.
            // We are planning to adjust this API to make it more efficient for this use case.

            // For now, we have to copy our data into Matrix4x4[] with a specific upper limit of how many instances we can render in one batch.
            // So we just have a for loop here, representing each Graphics.DrawMeshInstanced batch
            int beginIndex = 0;
            while (beginIndex < transforms.Length)
            {
                int length = math.min(m_MatricesArray.Length, transforms.Length - beginIndex);
                CopyMatrices(transforms, beginIndex, length, m_MatricesArray);
	            for (int j = 0; j < m_MatricesArray.Length; ++j)
	            {
		            m_MatricesArray[j].m03 += Main.ActiveInitParams.m_cellSize / 2;
		            m_MatricesArray[j].m23 += Main.ActiveInitParams.m_cellSize / 2;
	            }
                Graphics.DrawMeshInstanced(renderer.Mesh, renderer.SubMesh, renderer.Material, m_MatricesArray, length, null, renderer.CastShadows, renderer.ReceiveShadows);
                beginIndex += length;
            }
        }

		m_CacheduniqueRendererTypes.Clear();
		forEachFilter.Dispose();
	}
}