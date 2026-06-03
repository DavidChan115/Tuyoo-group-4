using UnityEngine;
using System.Collections.Generic;

public class ShadowPlatform : MonoBehaviour
{
    [Header("References")]
    public Light[] lights;
    public GameObject shadowCaster;
    public GameObject[] groundObjects;
    public Material shadowMaterial;

    [Header("Spotlight")]
    public bool enforceSpotCone = true;

    [Header("Ground Settings")]
    public float groundY = 0f;
    public float yOffset = 0.05f;

    [Header("Quality")]
    public int samplesPerRing = 24;
    public float ringsPerUnit = 4f;

    [Header("Limits")]
    public float maxShadowDistance = 50f;

    [Header("Fade")]
    [Range(0f, 1f)]
    public float shadowNearOpacity = 0.6f;
    [Range(0f, 1f)]
    public float shadowFarOpacity = 0f;

    private GameObject platformChild;
    private Mesh shadowMesh;
    private MeshCollider meshCollider;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;

    private List<Vector2> debugHull = new List<Vector2>();
    private float debugTargetY;
    private bool debugHasShadow;
    private Vector2 obstacleCenterXZ;
    private float shadowHeight;

    void Start()
    {
        platformChild = new GameObject("ShadowPlatformMesh");
        platformChild.transform.position = Vector3.zero;
        platformChild.layer = 0;

        shadowMesh = new Mesh();
        shadowMesh.name = "DynamicShadow";

        meshFilter = platformChild.AddComponent<MeshFilter>();
        meshFilter.mesh = shadowMesh;

        meshRenderer = platformChild.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (shadowMaterial != null)
        {
            meshRenderer.material = shadowMaterial;
        }
        else
        {
            Shader shader = Shader.Find("Mobile/Particles/Alpha Blended");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            Material mat = new Material(shader);
            mat.color = new Color(0, 0, 0, 1f);
            meshRenderer.material = mat;
        }

        meshCollider = platformChild.AddComponent<MeshCollider>();
        meshCollider.convex = false;
        meshCollider.isTrigger = false;
    }

    void Update()
    {
        if (lights == null || lights.Length == 0)
        {
            meshCollider.enabled = false;
            debugHasShadow = false;
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
            ClearShadow();
            return;
        }

        if (!GetCylinderParams(casterFilter, shadowCaster.transform,
                out float objBottomY, out float objTopY, out float objRadius, out Vector3 objCenterXZ))
        {
            ClearShadow();
            return;
        }

        obstacleCenterXZ = new Vector2(objCenterXZ.x, objCenterXZ.z);
        shadowHeight = objBottomY + yOffset;
        float targetY = shadowHeight;

        List<Vector2> projectedXZ = new List<Vector2>();

        foreach (Light light in lights)
        {
            if (light == null || !light.enabled) continue;

            Vector3 lightPos = light.transform.position;

            Vector3 obstacleCenter = new Vector3(objCenterXZ.x, (objBottomY + objTopY) / 2f, objCenterXZ.z);
            float distanceToObstacle = Vector3.Distance(lightPos, obstacleCenter);
            if (distanceToObstacle - objRadius > light.range)
                continue;

            bool isSpot = light.type == LightType.Spot;
            float halfCone = isSpot ? light.spotAngle / 2f : 180f;
            Vector3 lightForward = light.transform.forward;

            // Obstacle-level cone check: skip this light entirely
            // if the obstacle isn't inside the spotlight beam
            if (enforceSpotCone && isSpot)
            {
                Vector3 toObstacle = (obstacleCenter - lightPos).normalized;
                float angleToObstacle = Vector3.Angle(lightForward, toObstacle);
                float angularRadius = Mathf.Atan(objRadius / Mathf.Max(distanceToObstacle, 0.001f)) * Mathf.Rad2Deg;

                if (angleToObstacle > halfCone + angularRadius)
                    continue;
            }

            float effectiveTop = Mathf.Min(objTopY, lightPos.y);
            float heightRange = effectiveTop - objBottomY;
            if (heightRange <= 0f) continue;

            int ringCount = Mathf.Max(2, Mathf.CeilToInt(heightRange * ringsPerUnit));

            for (int ring = 0; ring <= ringCount; ring++)
            {
                float t = (float)ring / ringCount;
                float sampleY = objBottomY + t * heightRange;

                for (int s = 0; s < samplesPerRing; s++)
                {
                    float angle = (float)s / samplesPerRing * Mathf.PI * 2f;
                    Vector3 samplePoint = new Vector3(
                        objCenterXZ.x + Mathf.Cos(angle) * objRadius,
                        sampleY,
                        objCenterXZ.z + Mathf.Sin(angle) * objRadius
                    );

                    Vector3 lightToSample = samplePoint - lightPos;

                    // Per-sample spotlight cone filter: only project points
                    // within the beam so the shadow follows the light direction
                    if (isSpot && Vector3.Angle(lightForward, lightToSample) > halfCone)
                        continue;

                    // Back-face culling: skip points the light can't reach
                    Vector3 surfaceNormal = new Vector3(
                        samplePoint.x - objCenterXZ.x,
                        0f,
                        samplePoint.z - objCenterXZ.z
                    ).normalized;

                    if (Vector3.Dot(lightToSample, surfaceNormal) >= 0f)
                        continue;

                    Vector3 projected = ProjectPoint(samplePoint, lightPos, targetY);

                    if (!float.IsInfinity(projected.x) && !float.IsNaN(projected.x))
                    {
                        float distXZ = Vector2.Distance(
                            new Vector2(projected.x, projected.z),
                            new Vector2(lightPos.x, lightPos.z));

                        if (distXZ < maxShadowDistance)
                            projectedXZ.Add(new Vector2(projected.x, projected.z));
                    }
                }
            }
        }

        // Anchor shadow to the obstacle's base only when light-projected
        // points exist, so no shadow appears when the light isn't hitting.
        if (projectedXZ.Count > 0)
        {
            for (int s = 0; s < samplesPerRing; s++)
            {
                float angle = (float)s / samplesPerRing * Mathf.PI * 2f;
                projectedXZ.Add(new Vector2(
                    objCenterXZ.x + Mathf.Cos(angle) * objRadius,
                    objCenterXZ.z + Mathf.Sin(angle) * objRadius));
            }
        }

        if (projectedXZ.Count < 3)
        {
            ClearShadow();
            return;
        }

        List<Vector2> hull = ConvexHull(projectedXZ);

        if (hull.Count < 3)
        {
            ClearShadow();
            return;
        }

        if (groundObjects != null && groundObjects.Length > 0)
        {
            Rect? groundRect = GetGroundBoundsXZ();
            if (groundRect.HasValue)
                hull = ClipHullToRect(hull, groundRect.Value);

            if (hull.Count < 3)
            {
                ClearShadow();
                return;
            }
        }

        debugHull = hull;
        debugTargetY = targetY;
        debugHasShadow = true;

        platformChild.transform.position = Vector3.zero;

        BuildMesh(hull, objRadius);
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = shadowMesh;
        meshCollider.enabled = true;
    }

    Rect? GetGroundBoundsXZ()
    {
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        bool found = false;

        foreach (GameObject go in groundObjects)
        {
            if (go == null) continue;

            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                Bounds b = col.bounds;
                if (b.min.x < minX) minX = b.min.x;
                if (b.max.x > maxX) maxX = b.max.x;
                if (b.min.z < minZ) minZ = b.min.z;
                if (b.max.z > maxZ) maxZ = b.max.z;
                found = true;
            }
        }

        if (!found) return null;
        return new Rect(minX, minZ, maxX - minX, maxZ - minZ);
    }

    /// <summary>
    /// Sutherland–Hodgman polygon clipping: clips convexHull to a rectangle in XZ space.
    /// Rect x = world X, Rect y = world Z.
    /// </summary>
    List<Vector2> ClipHullToRect(List<Vector2> polygon, Rect bounds)
    {
        List<Vector2> output = new List<Vector2>(polygon);

        // Clip against 4 edges. edgeNormal points INSIDE the rectangle.
        output = ClipEdge(output, new Vector2(bounds.xMin, 0f), new Vector2( 1f, 0f));  // left   (x >= xMin)
        output = ClipEdge(output, new Vector2(bounds.xMax, 0f), new Vector2(-1f, 0f));  // right  (x <= xMax)
        output = ClipEdge(output, new Vector2(0f, bounds.yMin), new Vector2( 0f, 1f));  // bottom (z >= zMin)
        output = ClipEdge(output, new Vector2(0f, bounds.yMax), new Vector2( 0f,-1f));  // top    (z <= zMax)

        return output;
    }

    List<Vector2> ClipEdge(List<Vector2> polygon, Vector2 edgePoint, Vector2 edgeNormal)
    {
        if (polygon.Count == 0) return polygon;

        List<Vector2> output = new List<Vector2>();

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 current = polygon[i];
            Vector2 next = polygon[(i + 1) % polygon.Count];

            bool currentInside = Vector2.Dot(current - edgePoint, edgeNormal) >= 0f;
            bool nextInside    = Vector2.Dot(next    - edgePoint, edgeNormal) >= 0f;

            if (currentInside)
            {
                output.Add(current);

                if (!nextInside)
                {
                    Vector2 dir = next - current;
                    float denom = Vector2.Dot(dir, edgeNormal);
                    if (Mathf.Abs(denom) > 0.0001f)
                    {
                        float t = Vector2.Dot(edgePoint - current, edgeNormal) / denom;
                        output.Add(current + dir * t);
                    }
                }
            }
            else if (nextInside)
            {
                Vector2 dir = next - current;
                float denom = Vector2.Dot(dir, edgeNormal);
                if (Mathf.Abs(denom) > 0.0001f)
                {
                    float t = Vector2.Dot(edgePoint - current, edgeNormal) / denom;
                    output.Add(current + dir * t);
                }
            }
        }

        return output;
    }

    bool GetCylinderParams(MeshFilter filter, Transform casterTransform,
        out float bottomY, out float topY, out float radius, out Vector3 centerXZ)
    {
        // Prefer collider bounds — works on any object, no mesh import settings needed
        Collider col = shadowCaster.GetComponent<Collider>();
        if (col != null)
        {
            Bounds b = col.bounds;
            centerXZ = new Vector3(b.center.x, 0f, b.center.z);
            bottomY = b.min.y;
            topY   = b.max.y;
            radius = Mathf.Max(b.extents.x, b.extents.z);
            return true;
        }

        // Fallback: mesh vertices (only works if Read/Write is enabled on the mesh asset)
        Mesh mesh = filter.sharedMesh;
        if (mesh != null && mesh.isReadable)
        {
            Vector3[] vertices = mesh.vertices;
            if (vertices.Length == 0)
            {
                bottomY = topY = radius = 0f;
                centerXZ = Vector3.zero;
                return false;
            }

            Vector3 worldCenter = casterTransform.TransformPoint(mesh.bounds.center);
            centerXZ = new Vector3(worldCenter.x, 0f, worldCenter.z);

            float minY = float.MaxValue;
            float maxY = float.MinValue;
            float maxR = 0f;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 wv = casterTransform.TransformPoint(vertices[i]);

                if (wv.y < minY) minY = wv.y;
                if (wv.y > maxY) maxY = wv.y;

                float r = new Vector2(wv.x - centerXZ.x, wv.z - centerXZ.z).magnitude;
                if (r > maxR) maxR = r;
            }

            bottomY = minY;
            topY = maxY;
            radius = maxR;
            return true;
        }

        bottomY = topY = radius = 0f;
        centerXZ = Vector3.zero;
        return false;
    }

    void ClearShadow()
    {
        shadowMesh.Clear();
        meshCollider.enabled = false;
        debugHasShadow = false;
        debugHull.Clear();
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

        for (int i = 0; i < unique.Count; i++)
        {
            while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], unique[i]) <= 0f)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(unique[i]);
        }

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

    void BuildMesh(List<Vector2> hullXZ, float cylinderRadius)
    {
        shadowMesh.Clear();

        int n = hullXZ.Count;
        float thickness = 0.02f;
        float flatY = shadowHeight;

        float fadeRange = maxShadowDistance - cylinderRadius;
        if (fadeRange < 0.01f) fadeRange = 0.01f;

        float[] distFromCenter = new float[n];
        for (int i = 0; i < n; i++)
            distFromCenter[i] = Vector2.Distance(hullXZ[i], obstacleCenterXZ);

        Vector3[] verts = new Vector3[n * 2];
        Color[] colors = new Color[n * 2];
        for (int i = 0; i < n; i++)
        {
            verts[i]     = new Vector3(hullXZ[i].x, flatY,          hullXZ[i].y);
            verts[i + n] = new Vector3(hullXZ[i].x, flatY - thickness, hullXZ[i].y);

            float t = Mathf.Clamp01((distFromCenter[i] - cylinderRadius) / fadeRange);
            float alpha = Mathf.Lerp(shadowNearOpacity, shadowFarOpacity, t);
            Color vc = new Color(0f, 0f, 0f, alpha);
            colors[i]     = vc;
            colors[i + n] = vc;
        }

        int[] tris = new int[(n - 2) * 6 + n * 6];
        int ti = 0;
        int next;

        for (int i = 1; i < n - 1; i++)
        {
            tris[ti++] = 0;
            tris[ti++] = i + 1;
            tris[ti++] = i;
        }

        for (int i = 1; i < n - 1; i++)
        {
            tris[ti++] = n;
            tris[ti++] = n + i;
            tris[ti++] = n + i + 1;
        }

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
        shadowMesh.colors = colors;
        shadowMesh.triangles = tris;
        shadowMesh.RecalculateNormals();
        shadowMesh.RecalculateBounds();
    }

    void OnDrawGizmos()
    {
        // Draw ground bounds
        if (groundObjects != null && groundObjects.Length > 0)
        {
            Rect? r = GetGroundBoundsXZ();
            if (r.HasValue)
            {
                Gizmos.color = Color.cyan;
                Vector3 center = new Vector3(r.Value.center.x, 0f, r.Value.center.y);
                Vector3 size   = new Vector3(r.Value.width, 0.02f, r.Value.height);
                Gizmos.DrawWireCube(center, size);
            }
        }

        if (!debugHasShadow || debugHull.Count < 3) return;

        float y = debugTargetY;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < debugHull.Count; i++)
        {
            int next = (i + 1) % debugHull.Count;
            Vector3 a = new Vector3(debugHull[i].x, y, debugHull[i].y);
            Vector3 b = new Vector3(debugHull[next].x, y, debugHull[next].y);
            Gizmos.DrawLine(a, b);
        }

        Gizmos.color = Color.red;
        for (int i = 0; i < debugHull.Count; i++)
        {
            Vector3 p = new Vector3(debugHull[i].x, y, debugHull[i].y);
            Gizmos.DrawSphere(p, 0.1f);
        }
    }
}
