using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

public class DebugBB : MonoBehaviour
{
    Bounds boundsMesh, boundsWS;
    float3[] 
        boundsMeshVertices = new float3[8],
        boundsWSVertices = new float3[8];

    void OnDrawGizmos()
    {
        boundsMesh = gameObject.GetComponent<MeshFilter>().sharedMesh.bounds;
        FillBoundsVertices(ref boundsMeshVertices, ref boundsMesh);
        // DrawRectangularPrism(boundsMeshVertices, Color.red);
        
        ApplyTransforms(ref boundsMeshVertices, ref boundsWSVertices, transform.localToWorldMatrix);
        // DrawRectangularPrism(boundsWSVertices, Color.green);

        GetAABB(ref boundsWSVertices, ref boundsWS);
        FillBoundsVertices(ref boundsWSVertices, ref boundsWS);
        // DrawRectangularPrism(boundsWSVertices, Color.yellow);

        MyFunction(ref boundsMesh, transform.localToWorldMatrix);

        // Debug.Log(gameObject.GetComponent<Renderer>().bounds);
    }

    void MyFunction(ref Bounds bounds, Matrix4x4 matrix)
    {
        // shared mesh BB to vertices
        float3 bmin = bounds.min;
        float3 bmax = bounds.max;

        float3[] vertices = new float3[8];
        vertices[0] = new float3(bmin.x, bmin.y, bmin.z);
        vertices[1] = new float3(bmax.x, bmin.y, bmin.z);
        vertices[2] = new float3(bmax.x, bmax.y, bmin.z);
        vertices[3] = new float3(bmin.x, bmax.y, bmin.z);
        vertices[4] = new float3(bmin.x, bmin.y, bmax.z);
        vertices[5] = new float3(bmax.x, bmin.y, bmax.z);
        vertices[6] = new float3(bmax.x, bmax.y, bmax.z);
        vertices[7] = new float3(bmin.x, bmax.y, bmax.z);

        // shared mesh vertices to world space
        bmin = mul(matrix, new float4(vertices[0], 1.0f)).xyz;
        bmax = mul(matrix, new float4(vertices[0], 1.0f)).xyz;

        for (int i = 1; i < 8; i++)
        {
            vertices[i] = mul(matrix, new float4(vertices[i], 1.0f)).xyz;
            bmin = min(bmin, vertices[i]);
            bmax = max(bmax, vertices[i]);
        }

        vertices[0] = new float3(bmin.x, bmin.y, bmin.z);
        vertices[1] = new float3(bmax.x, bmin.y, bmin.z);
        vertices[2] = new float3(bmax.x, bmax.y, bmin.z);
        vertices[3] = new float3(bmin.x, bmax.y, bmin.z);
        vertices[4] = new float3(bmin.x, bmin.y, bmax.z);
        vertices[5] = new float3(bmax.x, bmin.y, bmax.z);
        vertices[6] = new float3(bmax.x, bmax.y, bmax.z);
        vertices[7] = new float3(bmin.x, bmax.y, bmax.z);

        DrawRectangularPrism(vertices, Color.blue);
    }

    void FillBoundsVertices(ref float3[] boundsVertices, ref Bounds bounds)
    {
        boundsVertices[0] = new float3(bounds.min.x, bounds.min.y, bounds.min.z);
        boundsVertices[1] = new float3(bounds.max.x, bounds.min.y, bounds.min.z);
        boundsVertices[2] = new float3(bounds.max.x, bounds.max.y, bounds.min.z);
        boundsVertices[3] = new float3(bounds.min.x, bounds.max.y, bounds.min.z);
        boundsVertices[4] = new float3(bounds.min.x, bounds.min.y, bounds.max.z);
        boundsVertices[5] = new float3(bounds.max.x, bounds.min.y, bounds.max.z);
        boundsVertices[6] = new float3(bounds.max.x, bounds.max.y, bounds.max.z);
        boundsVertices[7] = new float3(bounds.min.x, bounds.max.y, bounds.max.z);
    }

    void ApplyTransforms(ref float3[] boundsVertices, ref float3[] boundsVerticesOut, Matrix4x4 matrix)
    {
        for (int i = 0; i < 8; i++)
        {
            boundsVerticesOut[i] = mul(matrix, new float4(boundsVertices[i], 1.0f)).xyz;
        }
    }

    void GetAABB(ref float3[] boundsVertices, ref Bounds bounds)
    {
        float3 bbMin = min(boundsVertices[0], boundsVertices[1]);
        float3 bbMax = max(boundsVertices[0], boundsVertices[1]);

        for (int i = 0; i < 8; i++)
        {
            bbMin = min(bbMin, boundsVertices[i]);
            bbMax = max(bbMax, boundsVertices[i]);
        }

        float3 size = new float3(
            distance(bbMax.x, bbMin.x),
            distance(bbMax.y, bbMin.y),
            distance(bbMax.z, bbMin.z)
        );
        bounds = new Bounds(transform.position, size);
    }

    void DrawRectangularPrism(float3[] vertices, Color color)
    {
        Gizmos.color = color;

        // Draw front face
        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawLine(vertices[i], vertices[(i + 1) % 4]);
        }

        // Draw back face
        for (int i = 4; i < 8; i++)
        {
            Gizmos.DrawLine(vertices[i], vertices[(i + 1) % 4 + 4]);
        }

        // Draw edges between front and back faces
        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawLine(vertices[i], vertices[i + 4]);
        }
    }
}
