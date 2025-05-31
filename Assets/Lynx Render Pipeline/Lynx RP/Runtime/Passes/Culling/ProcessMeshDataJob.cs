using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace LynxRP
{
    using static MeshDefinitions;

    [BurstCompile]
    public struct ProcessMeshDataJob : IJobParallelFor
    {
        [ReadOnly] public Mesh.MeshDataArray meshData;
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int> indexStart;
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<float4x4> xform;
        
        [NativeDisableContainerSafetyRestriction] [WriteOnly] public NativeArray<Vertex> outputArray;
    
        [NativeDisableContainerSafetyRestriction] NativeArray<float3> tempVertices;
        [NativeDisableContainerSafetyRestriction] NativeArray<float3> tempNormals;
        [NativeDisableContainerSafetyRestriction] NativeArray<float4> tempColors;
        [NativeDisableContainerSafetyRestriction] NativeArray<float2> tempCoords;
        [NativeDisableContainerSafetyRestriction] NativeArray<int> tempIndices;
    
        public void CreateInputArrays(int meshCount)
        {
            indexStart = new NativeArray<int>(meshCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            xform = new NativeArray<float4x4>(meshCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        }
    
        public void Execute(int index)
        {
            var data = meshData[index];
            var vCount = data.vertexCount;
            var mat = xform[index];
            var tCount = data.GetSubMesh(0).indexCount;
            var hasColorAttribute = 0;
    
            if (!tempVertices.IsCreated || tempVertices.Length < vCount)
            {
                if (tempVertices.IsCreated) tempVertices.Dispose();
                tempVertices =
                    new NativeArray<float3>(vCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            }
    
            if (!tempNormals.IsCreated || tempNormals.Length < vCount)
            {
                if (tempNormals.IsCreated) tempNormals.Dispose();
                tempNormals =
                    new NativeArray<float3>(vCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            }
    
            if (!tempColors.IsCreated || tempColors.Length < vCount)
            {
                if (tempColors.IsCreated) tempColors.Dispose();
                tempColors =
                    new NativeArray<float4>(vCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            }
    
            if (!tempCoords.IsCreated || tempCoords.Length < vCount)
            {
                if (tempCoords.IsCreated) tempCoords.Dispose();
                tempCoords =
                    new NativeArray<float2>(vCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            }
    
            if (!tempIndices.IsCreated || tempIndices.Length < tCount)
            {
                if (tempIndices.IsCreated) tempIndices.Dispose();
                tempIndices = new NativeArray<int>(tCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            }
    
            data.GetVertices(tempVertices.Reinterpret<Vector3>());
            data.GetNormals(tempNormals.Reinterpret<Vector3>());
            if (data.HasVertexAttribute(VertexAttribute.Color))
            {
                hasColorAttribute = 1;
                data.GetColors(tempColors.Reinterpret<Color>());
            }
    
            data.GetUVs(0, tempCoords.Reinterpret<Vector2>());
            data.GetIndices(tempIndices.Reinterpret<int>(), 0);
    
            var tStart = indexStart[index];
            var vertex = new Vertex();
            for (var i = 0; i < tCount; ++i)
            {
                var tIndex = tempIndices[i];
                var localPosition = new float4(tempVertices[tIndex], 1);
                vertex.position = math.mul(mat, localPosition).xyz;
                vertex.normal = tempNormals[tIndex];
                vertex.color = hasColorAttribute > 0 ? tempColors[tIndex] : (Vector4)Color.white;
                vertex.uv = tempCoords[tIndex];
                //vertex.uv = tIndex;
                outputArray[tStart + i] = vertex;
            }
        }
    }

    [BurstCompile]
    public struct UpdateMeshMatricesJob : IJobParallelFor
    {
        [ReadOnly] public Mesh.MeshDataArray meshData;
        [DeallocateOnJobCompletion][ReadOnly] public NativeArray<int> indexStart;
        [DeallocateOnJobCompletion][ReadOnly] public NativeArray<float4x4> xform;

        [NativeDisableContainerSafetyRestriction][WriteOnly] public NativeArray<Vertex> outputArray;

        [NativeDisableContainerSafetyRestriction] NativeArray<float3> tempVertices;
        [NativeDisableContainerSafetyRestriction] NativeArray<int> tempIndices;

        public void CreateInputArrays(int meshCount)
        {
            indexStart = new NativeArray<int>(meshCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            xform = new NativeArray<float4x4>(meshCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        }

        public void Execute(int index)
        {
            var data = meshData[index];
            var vCount = data.vertexCount;
            var mat = xform[index];
            var tCount = data.GetSubMesh(0).indexCount;

            if (!tempVertices.IsCreated || tempVertices.Length < vCount)
            {
                if (tempVertices.IsCreated) tempVertices.Dispose();
                tempVertices =
                    new NativeArray<float3>(vCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            }

            if (!tempIndices.IsCreated || tempIndices.Length < tCount)
            {
                if (tempIndices.IsCreated) tempIndices.Dispose();
                tempIndices = new NativeArray<int>(tCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            }

            data.GetVertices(tempVertices.Reinterpret<Vector3>());

            data.GetIndices(tempIndices.Reinterpret<int>(), 0);

            var tStart = indexStart[index];
            var vertex = new Vertex();
            for (var i = 0; i < tCount; ++i)
            {
                var tIndex = tempIndices[i];
                var localPosition = new float4(tempVertices[tIndex], 1);
                vertex.position = math.mul(mat, localPosition).xyz;
                
                vertex.uv = outputArray[tStart + i].uv;
                vertex.color = outputArray[tStart + i].color;
                vertex.normal = outputArray[tStart + i].normal;
                outputArray[tStart + i] = vertex;
            }
        }
    }
}
