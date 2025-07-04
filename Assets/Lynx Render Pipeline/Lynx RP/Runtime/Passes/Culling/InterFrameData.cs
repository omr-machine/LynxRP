using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace LynxRP
{
    public class InterFrameData
    {
        public struct MeshJobsData
        {
            public int indexCount;
            public int triCount;
            public int objCount;
            public List<MeshDefinitions.Vertex> meshBufferDefault;
            public List<int> finalOffsetSizes;
            public List<Matrix4x4> finalMatrices;
            public List<MeshDefinitions.BBox> BBs;
        }

        public MeshJobsData meshData = new() { 
            indexCount = 0, triCount = 0,
            meshBufferDefault = new(),
            finalOffsetSizes = new(),
            finalMatrices = new(),
            BBs = new()
        };

        readonly SortedDictionary<int, GameObject> instanceIDs = new();
        readonly SortedDictionary<int, (int, int)> meshBufferOffsets = new();
        readonly SortedDictionary<int, Matrix4x4> meshMatrices = new ();

        Bounds[] BBVerts = new Bounds[512];

        public void UpdateVertexBuffer()
        {
            var meshFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
            Debug.Log(meshFilters.Length);

            SortedSet<int> visibleObjectIDs = new();
            for (var i = 0; i < meshFilters.Length; ++i)
            {
                var go = meshFilters[i].gameObject;
                var mesh = meshFilters[i].sharedMesh;
                int instanceID = go.GetInstanceID();

                visibleObjectIDs.Add(instanceID);
                AddInstanceID(ref instanceID, ref go, ref mesh); 
            }

            RemoveInstanceIDs(ref visibleObjectIDs);
            
            meshData.objCount = instanceIDs.Count;
            UpdateMeshMatrices();
            PopulateDataForBuffer();
            UpdateBBs();
        }

        private void RemoveInstanceIDs(ref SortedSet<int> visibleObjectIDs)
        {
            SortedSet<int> instanceIDsToRemove = new();
            foreach (int instanceID in instanceIDs.Keys)
            {
                if (!visibleObjectIDs.Contains(instanceID))
                {
                    instanceIDsToRemove.Add(instanceID);
                }
            }

            SortedDictionary<int, int> meshesToRemove = new();
            foreach (int instanceIDToRemove in instanceIDsToRemove)
            {
                (int, int) OffsetNSize = meshBufferOffsets[instanceIDToRemove];
                meshesToRemove.Add(OffsetNSize.Item1, OffsetNSize.Item2);
                
                // Debug.Log($"Not found {instanceIDToRemove}");
                instanceIDs.Remove(instanceIDToRemove);
                meshBufferOffsets.Remove(instanceIDToRemove);
            }
        
            if (meshesToRemove.Count > 0)
            {
                for (int i = meshesToRemove.Count - 1; i >= 0; i--)
                {
                    int offsetToTrim = meshesToRemove.Keys.ElementAt(i);
                    int sizeToTrim   = meshesToRemove.Values.ElementAt(i);
                    int index = offsetToTrim + sizeToTrim;

                    // meshData.meshBufferDefault.RemoveRange(offsetToTrim, sizeToTrim);
                    for (int k = index - 1; k >= offsetToTrim; k--)
                    {
                        meshData.meshBufferDefault.RemoveAt(k);   
                    }

                    for (int m = i; m < meshBufferOffsets.Count; m++)
                    {
                        var meshBufferOffset = meshBufferOffsets.ElementAt(m);
                        int offset = meshBufferOffset.Value.Item1;
                        int size   = meshBufferOffset.Value.Item2;
                        if (offset >= offsetToTrim)
                        {
                            meshBufferOffsets[meshBufferOffset.Key] = (offset - sizeToTrim, size);
                        }
                    }

                    meshData.indexCount -= sizeToTrim;
                    meshData.triCount   -= sizeToTrim / 3;
                }
            }
        }

        void AddInstanceID(ref int instanceID, ref GameObject go, ref Mesh mesh)
        {
            if (!instanceIDs.ContainsKey(instanceID))
            {
                instanceIDs.Add(instanceID, go);

                int indexCount = (int)mesh.GetIndexCount(0);
                (int, int) OffsetNSize = (meshData.meshBufferDefault.Count, indexCount);
                meshBufferOffsets.Add(instanceID, OffsetNSize);

                meshData.indexCount += indexCount;
                meshData.triCount   += indexCount / 3;

                foreach (int index in mesh.GetIndices(0))
                {
                    MeshDefinitions.Vertex vertex = new()
                    {
                        position = mesh.vertices[index],
                        normal = mesh.normals[index],
                        color = (Vector4)Color.white,
                        uv = mesh.uv[index]
                    };
                    meshData.meshBufferDefault.Add(vertex);
                }
            }
        }

        public void UpdateMeshMatrices()
        {
            foreach (var kvp in instanceIDs)
            {
                int instanceID = kvp.Key;
                GameObject go = kvp.Value;
                meshMatrices[instanceID] = go.transform.localToWorldMatrix;
            }
        }

        public void UpdateBBs()
        {
            meshData.BBs.Clear(); 

            MeshDefinitions.BBox bbox = new();
            foreach (var kvp in instanceIDs)
            {
                Bounds bounds = kvp.Value.GetComponent<Renderer>().bounds;
                bbox.minCorner = bounds.min;
                bbox.maxCorner = bounds.max;
                bbox.length = distance(bbox.maxCorner, bbox.minCorner);
                meshData.BBs.Add(bbox);
            }
        }

        public void FillBBArray()
        {
            if (BBVerts.Length < instanceIDs.Count)
            {
                var BBVertsLength = MeshDefinitions.NextPowerOfTwo((uint)instanceIDs.Count);
                BBVerts = new Bounds[BBVertsLength];
            }

            for (int i = 0; i < instanceIDs.Count; i++)
            {
                BBVerts[i] = instanceIDs[i].GetComponent<Renderer>().bounds;
            }
        }

        public void CullBBArray(ref Camera camera, ref NativeArray<int> voteBuffer)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);

            for (int i = 0; i < BBVerts.Length; i++)
            {
                voteBuffer[i] = GeometryUtility.TestPlanesAABB(planes, BBVerts[i]) ? 1 : 0;
            }
        }

        public void JobsMeshesDispose()
        {

        }

        public void DebugPrintBounds()
        {
            for (int i = 0; i < BBVerts.Length; i++)
            {
                Debug.Log(BBVerts[i]);
            }
        }

        public void DebugArray(ref NativeArray<MeshDefinitions.Vertex> array)
        {
            for (int i = 0; i < array.Length; i++)
            {
                Debug.Log(array[i].position);
            }
        }

        public void DebugFinalList()
        { 
            Debug.Log("#####");
            Debug.Log(meshData.meshBufferDefault.Count);
            Debug.Log("%%%%%");
            // for (int i = 0; i < meshData.meshBufferDefault.Count; i++)
            // {
            //     Debug.Log(meshData.meshBufferDefault[i].position);
            // }
        }

        void PopulateDataForBuffer()
        {
            meshData.finalOffsetSizes.Clear();
            meshData.finalMatrices.Clear();

            foreach(var id in instanceIDs)
            {
                meshData.finalOffsetSizes.Add(meshBufferOffsets[id.Key].Item1);
                meshData.finalOffsetSizes.Add(meshBufferOffsets[id.Key].Item2);
                meshData.finalMatrices   .Add(meshMatrices     [id.Key]);
            }
        }

        public void DebugFinalMatrices()
        {
            List<int> ids = new(meshBufferOffsets.Keys);
            
            int k = 0;
            for (int i = 0; i < meshData.finalMatrices.Count; i++)
            {
                Debug.Log($"{ids[i]}: {meshData.finalOffsetSizes[k]}, {meshData.finalOffsetSizes[k+1]}, Matrix: {meshData.finalMatrices[i]}");
                k += 2;
            }
        }

        public void DebugInstanceIDs()
        {
            foreach (var (instanceID, go) in instanceIDs)
            {
                Debug.Log($"{instanceID} : {go.name}");
            }
        }

        public void DebugInstanceMatrices()
        {
            foreach (var (instanceID, matrix) in meshMatrices)
            {
                Debug.Log($"Instance ID: {instanceID}, Matrix: {matrix}");
            }
        }

        public void DebugInstanceBounds()
        {
            foreach (var bounds in meshData.BBs)
            {
                Debug.Log($"({bounds.minCorner.x}, {bounds.minCorner.y}, {bounds.minCorner.z}),"
                        + "\n                  "                 
                        + $"({bounds.maxCorner.x}, {bounds.maxCorner.y}, {bounds.maxCorner.z})");
                // Debug.Log($"Length: {bounds.Length}");
            }
        }

        internal void Dispose()
        {
            instanceIDs.Clear();
            meshBufferOffsets.Clear(); 
            meshMatrices.Clear();

            meshData.meshBufferDefault.Clear();
            meshData.finalOffsetSizes.Clear();
            meshData.finalMatrices.Clear();
            meshData.BBs.Clear();
        }
    }

    public class MeshDefinitions
    {
        public struct Vertex
        {
            public float3 position;
            public float3 normal;
            public float4 color;
            public float2 uv;
        }

        public struct BBox
        {
            public float3 minCorner;
            public float3 maxCorner;
            public float length;
        };

        public struct Line
        {
            public float3 position;
            public float3 color;
        };

        public static uint NextPowerOfTwo(uint n)
        {
            if (n == 0)
                return 1;

            if ((n & (n - 1)) == 0)
                return n;

            if (n > 0x80000000)  // 0x80000000 is 2^31
                throw new System.OverflowException("Next power of 2 would exceed uint.MaxValue");

            n |= n >> 1;
            n |= n >> 2;
            n |= n >> 4;
            n |= n >> 8;
            n |= n >> 16;

            return n + 1;
        }
    }
}
