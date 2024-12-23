using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

public class DebugCube : MonoBehaviour
{
    Camera cam;
    Camera cam1;

    private Rect rect = new(0, 0, 1, 1);
    private Vector3[] frustumCorners = new Vector3[8];

    private float4[] planes = new float4[6];
    private float4[] planesExtracted = new float4[6];
    private float3[] planeCenters = new float3[6];
    private float3[] planeCentersExtracted = new float3[6];

    private float4[] planesUnity = new float4[6];
    private float3[] planeCentersUnity = new float3[6];


    void OnDrawGizmos()
    {
        DrawNDCCube();

        cam = Camera.main;
        cam1 = Camera.current;

        DrawFrustumCorners(cam.nearClipPlane, cam, cam1, Color.red).CopyTo(frustumCorners, 0);
        DrawFrustumCorners(cam.farClipPlane, cam, cam1, Color.green).CopyTo(frustumCorners, 4);
        GetFrustumCenter(frustumCorners);

        SetupPlanes(GetFrustumCenter(frustumCorners));
        DrawPlanes(ref planeCenters, ref planes, 50.0f, Color.magenta);
        // DrawPlanes(ref planeCentersUnity, ref planesUnity, 30.0f, Color.cyan);
        
        Matrix4x4 matrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false) * cam.transform.worldToLocalMatrix;
        // Debug.Log(matrix);
        ExtractFrustumPlanes(matrix);
        // DrawPlanes(ref planeCentersExtracted, ref planesExtracted, 35.0f, Color.yellow);

        DrawInFrustum(ref planesUnity); //p lanesExtracted
        // Debug.Log(inFrustum);

        for (int i = 0; i < 6; i++)
        {
            // Debug.Log("plane " + i + " Distance: " + planesUnity[i].w);
        }
    }

    bool DrawInFrustum(ref float4[] planes)
    {
        float3 size = gameObject.GetComponent<BoxCollider>().size * (float3)transform.localScale;
        Bounds bounds = new Bounds(transform.position, size);
        bool inFrustum = InFrustum(ref planes, bounds); 
        Gizmos.color = Color.red;
        if (inFrustum)
        {
            Gizmos.color = Color.yellow;
        }
        Gizmos.DrawWireCube(transform.position, bounds.size);
        return inFrustum;
    }

    bool InFrustum(ref float4[] planes, Bounds bounds)
    {
        float3 mMin = bounds.min;
        float3 mMax = bounds.max;

        for (int i = 0; i < 6; i++)
        {
            int inView = 0;
            inView += dot(planes[i], float4(mMin.x, mMin.y, mMin.z, 1.0f)) < 0.0 ? 1 : 0;
            inView += dot(planes[i], float4(mMax.x, mMin.y, mMin.z, 1.0f)) < 0.0 ? 1 : 0;
            inView += dot(planes[i], float4(mMin.x, mMax.y, mMin.z, 1.0f)) < 0.0 ? 1 : 0;
            inView += dot(planes[i], float4(mMin.x, mMin.y, mMax.z, 1.0f)) < 0.0 ? 1 : 0;
            inView += dot(planes[i], float4(mMax.x, mMax.y, mMin.z, 1.0f)) < 0.0 ? 1 : 0;
            inView += dot(planes[i], float4(mMax.x, mMin.y, mMax.z, 1.0f)) < 0.0 ? 1 : 0;
            inView += dot(planes[i], float4(mMin.x, mMax.y, mMax.z, 1.0f)) < 0.0 ? 1 : 0;
            inView += dot(planes[i], float4(mMax.x, mMax.y, mMax.z, 1.0f)) < 0.0 ? 1 : 0;
            if (inView == 8) 
                return false;
        }
        int outV;
        outV = 0; for (int i = 0; i < 8; i++) outV += (frustumCorners[i].x > mMax.x) ? 1 : 0; if (outV == 8 ) return false;
        outV = 0; for (int i = 0; i < 8; i++) outV += (frustumCorners[i].x < mMin.x) ? 1 : 0; if (outV == 8 ) return false;
        outV = 0; for (int i = 0; i < 8; i++) outV += (frustumCorners[i].y > mMax.y) ? 1 : 0; if (outV == 8 ) return false;
        outV = 0; for (int i = 0; i < 8; i++) outV += (frustumCorners[i].y < mMin.y) ? 1 : 0; if (outV == 8 ) return false;
        outV = 0; for (int i = 0; i < 8; i++) outV += (frustumCorners[i].z > mMax.z) ? 1 : 0; if (outV == 8 ) return false;
        outV = 0; for (int i = 0; i < 8; i++) outV += (frustumCorners[i].z < mMin.z) ? 1 : 0; if (outV == 8 ) return false;
        return true;
    }

    void ExtractFrustumPlanes(float4x4 matrix)
    {
        float[] left = new float[4]; float[] right = new float[4];
        float[] bottom = new float[4]; float[] top = new float[4];
        float[] near = new float[4]; float[] far = new float[4];
        for (int i = 0; i < 4; i++)
        {
            right[i]  = matrix[i][3] + matrix[i][0];
            left[i]   = matrix[i][3] - matrix[i][0];
            top[i]    = matrix[i][3] + matrix[i][1];
            bottom[i] = matrix[i][3] - matrix[i][1];
            near[i]   = matrix[i][3] + matrix[i][2];
            far[i]    = matrix[i][3] - matrix[i][2];
        }
        // far[3] = matrix[3][2]/matrix[2][2];
        // far[3] = matrix[3][3] - matrix[3][2] / matrix[2][2];
        far[3] = matrix[3][3] + Camera.main.farClipPlane;

        float4 rightVec  = new float4(right[0],  right[1],  right[2],  right[3]);
        float4 leftVec   = new float4(left[0],  left[1],    left[2],   left[3]);
        float4 topVec    = new float4(top[0],    top[1],    top[2],    top[3]);
        float4 bottomVec = new float4(bottom[0], bottom[1], bottom[2], bottom[3]);
        float4 nearVec   = new float4(near[0],   near[1],   near[2],   near[3]);
        float4 farVec    = new float4(far[0],    far[1],    far[2],    far[3]);

        farVec = farVec * -1;  
        NormalizePlane(ref nearVec); NormalizePlane(ref farVec);
        NormalizePlane(ref leftVec); NormalizePlane(ref rightVec);
        NormalizePlane(ref bottomVec); NormalizePlane(ref topVec);

        planesExtracted[0] = new float4(-nearVec.xyz, -nearVec.w);
        planesExtracted[1] = new float4(-farVec.xyz, -farVec.w);
        planesExtracted[2] = new float4(-leftVec.xyz, -leftVec.w);
        planesExtracted[3] = new float4(-rightVec.xyz, -rightVec.w);
        planesExtracted[4] = new float4(-bottomVec.xyz, -bottomVec.w);
        planesExtracted[5] = new float4(-topVec.xyz, -topVec.w);
        
        for (int i = 0; i < 6; i++)
        {
            planeCentersExtracted[i] = GetPositionAlongDirection(
                new float3(0, 0, 0), planesExtracted[i].xyz, planesExtracted[i].w
            );
            // Debug.Log("plane " + i + " W component: " + planesExtracted[i].w);
        }
    }

    Vector3 GetPositionAlongDirection(Vector3 startPosition, Vector3 direction, float distance)
    {
        Vector3 normalizedDirection = direction.normalized;
        return startPosition - normalizedDirection * distance;
    }

    void NormalizePlane(ref float4 plane)
    {
        float magnitude = length(plane.xyz); // sqrt(plane.x * plane.x + plane.y * plane.y + plane.z * plane.z);
        plane /= magnitude;
    }

    Vector3 GetFrustumCenter(Vector3[] frustumCorners)
    {
        Vector3 sum = Vector3.zero;
        foreach (var corner in frustumCorners)
        {
            sum += corner;
        }
        Vector3 center = sum / frustumCorners.Length;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, new Vector3(30f, 30f, 30f));

        return center;
    }

    void DrawNDCCube()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 2);
    }

    Vector3[] DrawFrustumCorners(float clipPlane, Camera camera, Camera camera1, Color color) 
    {
        Vector3[] frustumCorners = new Vector3[4];
        camera.CalculateFrustumCorners(rect, clipPlane, Camera.MonoOrStereoscopicEye.Mono, frustumCorners);

        Gizmos.color = color;
        for (int i = 0; i < frustumCorners.Length; i++)
        {
            frustumCorners[i] = camera.transform.TransformPoint(frustumCorners[i]);
            float distance = Vector3.Distance(camera1.transform.position, frustumCorners[i]);
            Gizmos.DrawWireSphere(frustumCorners[i], distance * 0.01f);
        }
        return frustumCorners;
    }

    static float3 PlaneDirection(float3 a, float3 b, float3 c, float3 d)
    {
        float3 dir = normalize(cross(b - a, c - b));
        return dir;
    }

    static float3 PlaneMidpoint(float3 a, float3 b, float3 c, float3 d)
    {
        return (a + b + c + d) / 4.0f;
    }

    void SetupPlanes(float3 frustumCenter) // ll ul ur lr
    {
        SetupPlane(frustumCorners[0], frustumCorners[1], frustumCorners[2], frustumCorners[3], 0, frustumCenter); // near
        SetupPlane(frustumCorners[4], frustumCorners[5], frustumCorners[6], frustumCorners[7], 1, frustumCenter); // far
        SetupPlane(frustumCorners[0], frustumCorners[1], frustumCorners[4], frustumCorners[5], 2, frustumCenter); // left
        SetupPlane(frustumCorners[2], frustumCorners[3], frustumCorners[6], frustumCorners[7], 3, frustumCenter); // right
        SetupPlane(frustumCorners[0], frustumCorners[3], frustumCorners[4], frustumCorners[7], 4, frustumCenter); // bottom
        SetupPlane(frustumCorners[1], frustumCorners[2], frustumCorners[5], frustumCorners[6], 5, frustumCenter); // top
        SetupPlanesUnity();
    }


    void SetupPlane(float3 cornerA, float3 cornerB, float3 cornerC, float3 cornerD, int planeIndex, float3 frustumCenter)
    {
        float3 normal = PlaneDirection(cornerA, cornerB, cornerC, cornerD);
        float3 center = PlaneMidpoint(cornerA, cornerB, cornerC, cornerD);

        if (dot(normal, frustumCenter - center) < 0.0f)
        {
            normal = -normal;
        }

        planes[planeIndex].xyz = normal;
        planeCenters[planeIndex] = center;

        // SetupPlaneUnity(planeIndex, normal, center);
    }

    void SetupPlaneUnity(int planeIndex, float3 normal, float3 center)
    {
        Plane plane = new Plane(normal, center);
        planeCentersUnity[planeIndex] = GetPositionAlongDirection(
                new float3(0, 0, 0), plane.normal, plane.distance
        );
        planesUnity[planeIndex] = new float4(plane.normal, plane.distance);
    }

    void SetupPlanesUnity()
    {
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
        int[] indexes = { 4, 5, 0, 1, 2, 3 };
        for (int i = 0; i < 6; i++)
        {
            int k = indexes[i];
            planesUnity[i] = new float4(planes[k].normal, planes[k].distance);
            
            planeCentersUnity[i] = GetPositionAlongDirection(
                new float3(0, 0, 0), planes[k].normal, planes[k].distance
            );
        }
    }

    void DrawPlanes(ref float3[] planeCenters, ref float4[] planes, float planeSize, Color color)
    {
        for (int planeIndex = 0; planeIndex < 6; planeIndex++)
        {
            DrawPlane(planeCenters[planeIndex], planes[planeIndex].xyz, planeSize, color);
        }
    }

    void DrawPlanes(ref float3[] planeCenters, ref Plane[] planes, float planeSize, Color color)
    {
        for (int planeIndex = 0; planeIndex < 6; planeIndex++)
        {
            DrawPlane(planeCenters[planeIndex], planes[planeIndex].normal, planeSize, color);
        }
    }

    public void DrawPlane(float3 center, float3 normal, float size, Color color)
    {
        normal = normalize(normal);
        float3 right;
        if (dot(normal, Vector3.forward) > 0.999f || dot(normal, Vector3.forward) < -0.999f)
            right = normalize(cross(normal, Vector3.up)) * size;
        else
            right = normalize(cross(normal, Vector3.forward)) * size;

        float3 up = cross(normal, right);

        var corner0 = center + right + up;
        var corner1 = center - right + up;
        var corner2 = center - right - up;
        var corner3 = center + right - up;

        Gizmos.color = color;
        Gizmos.DrawLine(corner0, corner1);
        Gizmos.DrawLine(corner1, corner2);
        Gizmos.DrawLine(corner2, corner3);
        Gizmos.DrawLine(corner3, corner0);

        // Draw diagonal edges
        Gizmos.DrawLine(corner0, corner2);
        Gizmos.DrawLine(corner1, corner3);

        // Draw the normal line
        Gizmos.color = Color.red;
        Gizmos.DrawLine(center, center + normal * size);
    }
}
