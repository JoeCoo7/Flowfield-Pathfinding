﻿using System.Collections.Generic;
using Agent;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.PlayerLoop;

//-----------------------------------------------------------------------------
// this is a copy of MeshInstanceRendererSystem with some required changes
// ReSharper disable once RequiredBaseTypesIsNotInherited
[UpdateInGroup(typeof(RenderingGroup))]
[UpdateAfter(typeof(PreLateUpdate.ParticleSystemBeginUpdateAll))]
[ExecuteInEditMode]
public class AgentMeshInstanceRendererSystem : ComponentSystem
{
    // Instance renderer takes only batches of 1023
    private Matrix4x4[] m_MatricesArray = new Matrix4x4[512];
    private NativeArray<float3> m_Colors;
    private List<AgentMeshInstanceRenderer> m_CacheduniqueRendererTypes = new List<AgentMeshInstanceRenderer>(10);
    private ComponentGroup m_InstanceRendererGroup;
    private List<ComputeBuffer> m_ComputeBuffers = new List<ComputeBuffer>();
    private List<Material> m_Materials = new List<Material>();

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
        // We want to find all AgentMeshInstanceRenderer & TransformMatrix combinations and render them
        m_InstanceRendererGroup = GetComponentGroup(
            ComponentType.ReadOnly<AgentMeshInstanceRenderer>(),
            ComponentType.ReadOnly<TransformMatrix>(),
            ComponentType.ReadOnly<Selection>());
        m_Colors = new NativeArray<float3>(512, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
    }

    //-----------------------------------------------------------------------------
    protected override void OnDestroyManager()
    {
        base.OnDestroyManager();

        foreach (var buffer in m_ComputeBuffers)
            buffer.Release();

        m_Colors.Dispose();
    }

    //-----------------------------------------------------------------------------
    protected override void OnUpdate()
    {
        // We want to iterate over all unique MeshInstanceRenderer shared component data,
        // that are attached to any entities in the world
        EntityManager.GetAllUniqueSharedComponentDatas(m_CacheduniqueRendererTypes);
        var forEachFilter = m_InstanceRendererGroup.CreateForEachFilter(m_CacheduniqueRendererTypes);

        int drawIdx = 0;
        for (int i = 0; i != m_CacheduniqueRendererTypes.Count; i++)
        {
            // For each unique MeshInstanceRenderer data, we want to get all entities with a TransformMatrix
            // SharedComponentData gurantees that all those entities are packed togehter in a chunk with linear memory layout.
            // As a result the copy of the matrices out is internally done via memcpy.
            var renderer = m_CacheduniqueRendererTypes[i];
            var transforms = m_InstanceRendererGroup.GetComponentDataArray<TransformMatrix>(forEachFilter, i);
            var selection = m_InstanceRendererGroup.GetComponentDataArray<Selection>(forEachFilter, i);

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
                // Copy Matrices
                int length = math.min(m_MatricesArray.Length, transforms.Length - beginIndex);
                CopyMatrices(transforms, beginIndex, length, m_MatricesArray);
                for (int j = 0; j < m_MatricesArray.Length; ++j)
                {
                    m_MatricesArray[j].m03 += Main.ActiveInitParams.m_cellSize / 2;
                    m_MatricesArray[j].m23 += Main.ActiveInitParams.m_cellSize / 2;
                }

                if (drawIdx + 1 >= m_ComputeBuffers.Count)
                {
                    var computeBuffer = new ComputeBuffer(512, 3 * sizeof(float));
                    m_ComputeBuffers.Add(computeBuffer);
                    var material = new Material(renderer.material);
                    m_Materials.Add(material);
                }

                for (int x = 0; x < length; ++x)
                    m_Colors[x] = selection[beginIndex + x].Value == 1 ? new Vector3(0.5f, 1f, 0.5f) : new Vector3(0.5f, 0.5f, 1f);
                
                m_ComputeBuffers[drawIdx].SetData(m_Colors, 0, 0, length);
                m_Materials[drawIdx].SetBuffer("velocityBuffer", m_ComputeBuffers[drawIdx]);

                // !!! This will draw all meshes using the last material.  Probably need an array of materials.
                Graphics.DrawMeshInstanced(renderer.mesh, renderer.subMesh, m_Materials[drawIdx], m_MatricesArray, length, null, renderer.castShadows, renderer.receiveShadows);
                drawIdx++;
                beginIndex += length;
            }
        }

        m_CacheduniqueRendererTypes.Clear();
        forEachFilter.Dispose();
    }
}