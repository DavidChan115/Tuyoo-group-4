using UnityEngine;
using System.Collections.Generic;

public class ShadowPlatform : MonoBehaviour
{
    [Header("References")]
    public Light pointLight;
    public GameObject shadowCaster;

    [Header("Ground Settings")]
    public float groundY = 0f;
    public float yOffset = 0.05f;

    [Header("Limits")]
    public float maxShadowDistance = 50f;
    public float maxShadowHeight = 100f;

    private GameObject platformChild;
    private Mesh shadowMesh;
    private MeshCollider meshCollider;
    private MeshRenderer meshRenderer;

    void Start()
    {
        platformChild = new GameObject("ShadowPlatformMesh");
        // Do NOT parent — mesh vertices are in local space, positioned at ground level
        platformChild.transform.position = new Vector3(0f, groundY + yOffset, 0f);
        platformChild.layer = gameObject.layer;

        shadowMesh = new Mesh();
        shadowMesh.name = "DynamicShadow";

        MeshFilter mf = platformChild.AddComponent<MeshFilter>();
        mf.mesh = shadowMesh;

        meshRenderer = platformChild.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        Material mat = new Material(shader);
        mat.color = new Color(0, 0, 0, 0.45f);
        meshRenderer.material = mat;

        meshCollider = platformChild.AddComponent<MeshCollider>();
        meshCollider.convex = true;
        meshCollider.isTrigger = false;
    }

    void Update()
    {
        if (pointLight == null)
        {
            meshCollider.enabled = false;
            return;
        }

        if (shadowCaster == null)
            shadowCaster = gameObject;

        UpdateShadow();
    }

    void UpdateShadow()
    {
        MeshFilter casterFilter = shadowCaster.GetComponent<MeshFilter>();
        if (casterFilter == null || casterFilter.sharedMesh == null)
        {
            shadowMesh.Clear();
            meshCollider.enabled = false;
            return;
        }

        Vector3[] vertices = casterFilter.sharedMesh.vertices;
        Vector3 lightPos = pointLight.transform.position;
        Transform casterTransform = shadowCaster.transform;
        float targetY = groundY + yOffset;

        // Keep the platform GameObject at the ground level
        platformChild.transform.position = new Vector3(0f, targetY, 0f);

        List<Vector2> projectedXZ = new List<Vector2>();

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldVert = casterTransform.TransformPoint(vertices[i]);
            Vector3 projected = ProjectPoint(worldVert, lightPos, targetY);

            if (!float.IsInfinity(projected.x) && !float.IsNaN(projected.x))
            {
                float distXZ = Vector2.Distance(
                    new Vector2(projected.x, projected.z),
                    new Vector2(lightPos.x, lightPos.z));

                if (distXZ < maxShadowDistance && projected.y < maxShadowHeight)
                {
                    projectedXZ.Add(new Vector2(projected.x, projected.z));
                }
            }
        }

        if (projectedXZ.Count < 3)
        {
            shadowMesh.Clear();
            meshCollider.enabled = false;
            return;
        }

        List<Vector2> hull = ConvexHull(projectedXZ);

        if (hull.Count < 3)
        {
            shadowMesh.Clear();
            meshCollider.enabled = false;
            return;
        }

        BuildMesh(hull);
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = shadowMesh;
        meshCollider.enabled = true;
    }

    Vector3 ProjectPoint(Vector3 worldPoint, Vector3 lightPos, float targetY)
    {
        Vector3 dir = worldPoint - lightPos;

        if (Mathf.Abs(dir.y) < 0.0001f)
            return new Vector3(float.PositiveInfinity, 0, float.PositiveInfinity);

        float t = (targetY - lightPos.y) / dir.y;

        if (t <= 0f)
            return new Vector3(float.PositiveInfinity, 0, float.PositiveInfinity);

        return lightPos + dir * t;
    }

    List<Vector2> ConvexHull(List<Vector2> points)
    {
        if (points.Count <= 1)
            return new List<Vector2>(points);

        // Remove duplicate points
        List<Vector2> unique = new List<Vector2>();
        for (int i = 0; i < points.Count; i++)
        {
            bool dup = false;
            for (int j = 0; j < unique.Count; j++)
            {
                if (Vector2.Distance(points[i], unique[j]) < 0.001f)
                {
                    dup = true;
                    break;
                }
            }
            if (!dup) unique.Add(points[i]);
        }

        if (unique.Count <= 2)
            return unique;

        unique.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));

        List<Vector2> hull = new List<Vector2>();

        // Lower hull
        for (int i = 0; i < unique.Count; i++)
        {
            while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], unique[i]) <= 0f)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(unique[i]);
        }

        // Upper hull
        int lowerCount = hull.Count;
        for (int i = unique.Count - 2; i >= 0; i--)
        {
            while (hull.Count > lowerCount && Cross(hull[hull.Count - 2], hull[hull.Count - 1], unique[i]) <= 0f)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(unique[i]);
        }

        hull.RemoveAt(hull.Count - 1);
        return hull;
    }

    float Cross(Vector2 O, Vector2 A, Vector2 B)
    {
        return (A.x - O.x) * (B.y - O.y) - (A.y - O.y) * (B.x - O.x);
    }

    void BuildMesh(List<Vector2> hullXZ)
    {
        shadowMesh.Clear();

        int n = hullXZ.Count;
        float thickness = 0.03f;

        // Vertices in LOCAL space — platformChild is at world (0, targetY, 0),
        // so world point (hx, targetY, hz) → local (hx, 0, hz)
        Vector3[] verts = new Vector3[n * 2];
        for (int i = 0; i < n; i++)
        {
            verts[i]     = new Vector3(hullXZ[i].x, 0f,           hullXZ[i].y);
            verts[i + n] = new Vector3(hullXZ[i].x, -thickness,   hullXZ[i].y);
        }

        int[] tris = new int[(n - 2) * 6 + n * 6];

        int ti = 0;
        int next;

        // Top face (fan, normal +Y)
        for (int i = 1; i < n - 1; i++)
        {
            tris[ti++] = 0;
            tris[ti++] = i + 1;
            tris[ti++] = i;
        }

        // Bottom face (fan, normal -Y)
        for (int i = 1; i < n - 1; i++)
        {
            tris[ti++] = n;
            tris[ti++] = n + i;
            tris[ti++] = n + i + 1;
        }

        // Side quads
        for (int i = 0; i < n; i++)
        {
            next = (i + 1) % n;

            tris[ti++] = i;
            tris[ti++] = i + n;
            tris[ti++] = next + n;

            tris[ti++] = i;
            tris[ti++] = next + n;
            tris[ti++] = next;
        }

        shadowMesh.vertices = verts;
        shadowMesh.triangles = tris;
        shadowMesh.RecalculateNormals();
        shadowMesh.RecalculateBounds();
    }
}
