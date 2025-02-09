using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LynxRP
{
    public class InterFrameData
    {
        public struct MeshJobsData
        {
            public ProcessMeshDataJob jobs;
            public JobHandle handle;
            public int indexCount;
            public int triCount;
        }

        public MeshJobsData meshData = new() { 
            indexCount = 0, triCount = 0
        };

        public void JobsMeshes()
        {
            var meshFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);

            meshData.jobs = new ProcessMeshDataJob();
            meshData.jobs.CreateInputArrays(meshFilters.Length);
            var inputMeshes = new List<Mesh>(meshFilters.Length);

            var indexStart = 0;
            var meshCount = 0;
            for (var i = 0; i < meshFilters.Length; ++i)
            {
                var mf = meshFilters[i];
                var go = mf.gameObject;

                var mesh = mf.sharedMesh;
                inputMeshes.Add(mesh);
                meshData.jobs.indexStart[meshCount] = indexStart;
                meshData.jobs.xform[meshCount] = go.transform.localToWorldMatrix;
                indexStart += (int)mesh.GetIndexCount(0);
                ++meshCount;
            }

            meshData.indexCount = indexStart;
            meshData.triCount = meshData.indexCount / 3;

            meshData.jobs.outputArray = new NativeArray<CullPass.Vertex>(meshData.indexCount, Allocator.TempJob);
            meshData.jobs.meshData = Mesh.AcquireReadOnlyMeshData(inputMeshes);

            meshData.handle = meshData.jobs.Schedule(meshCount, 4);
        }

        public void JobsMeshesDispose()
        {
            meshData.jobs.meshData.Dispose();
            meshData.jobs.outputArray.Dispose();
        }

        public void DebugJobsOutputArray()
        {
            for (int i = 0; i < meshData.jobs.outputArray.Length; ++i)
            {
                Debug.Log(meshData.jobs.outputArray[i].position);
            }
        }
    }
}
