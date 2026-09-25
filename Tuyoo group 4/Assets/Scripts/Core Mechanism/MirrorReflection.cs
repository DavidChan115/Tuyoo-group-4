using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class MirrorReflection : MonoBehaviour
{
    public enum MirrorFace
    {
        Forward,
        Back,
        Left,
        Right,
        Up,
        Down
    }

    [Header("Mirror Face")]
    [Tooltip("Which face of the box acts as the reflective surface.")]
    public MirrorFace reflectiveFace = MirrorFace.Forward;

    [Header("Light Sources")]
    [Tooltip("The Spotlights whose beams can be reflected. The mirror uses the first valid one.")]
    public Transform[] lightSources;

    [Header("Intensity")]
    public float intensityMultiplier = 1f;
    public bool useDistanceFalloff = true;
    [Tooltip("Minimum effective intensity at the mirror face required to trigger a reflection.")]
    public float minSourceIntensity = 0.01f;

    [Header("Reflected Spotlight")]
    [Range(1f, 179f)]
    public float spotAngle = 30f;
    public float spotRange = 20f;
    public Color spotColor = Color.white;
    [Tooltip("Optional — assign your own Light. If empty, one is created automatically.")]
    public Light customReflectionLight;

    [Header("Blocking")]
    [Tooltip("Objects on these layers will block the light from reaching the mirror.")]
    public LayerMask blockingMask = ~0;

    [Header("Debug")]
    public bool drawGizmos = true;

    private Light spotLight;
    private bool autoCreatedLight;
    private BoxCollider boxCol;

    // Track which source is active this frame (for gizmos)
    private Transform activeSource;

    void Awake()
    {
        boxCol = GetComponent<BoxCollider>();

        if (customReflectionLight != null)
        {
            spotLight = customReflectionLight;
            spotLight.shadows = LightShadows.Hard;
            autoCreatedLight = false;
        }
        else
        {
            CreateSpotlight();
            autoCreatedLight = true;
        }
    }

    void Update()
    {
        if (lightSources == null || lightSources.Length == 0 || spotLight == null)
        {
            DisableReflection();
            return;
        }

        Vector3 faceCenter = GetFaceCenter();
        Vector3 faceNormal = GetFaceNormal();

        // Try each light source — use the first one that passes all checks.
        foreach (Transform source in lightSources)
        {
            if (source == null) continue;

            Light srcLight = source.GetComponent<Light>();
            if (srcLight == null) continue;

            // --- Face-toward check ---
            Vector3 faceToSource = source.position - faceCenter;
            if (Vector3.Dot(faceNormal, faceToSource) <= 0f)
                continue;

            // --- Distance & range ---
            Vector3 toFaceCenter = faceCenter - source.position;
            float distance = toFaceCenter.magnitude;

            if (distance > srcLight.range)
                continue;

            // --- Intensity ---
            float sourceIntensity = srcLight.intensity;
            float falloff = useDistanceFalloff ? 1f / Mathf.Max(1f, distance * distance) : 1f;
            float effectiveIntensity = sourceIntensity * falloff;
            if (effectiveIntensity < minSourceIntensity)
                continue;

            // --- Cone check ---
            float sourceHalfCone = srcLight.type == LightType.Spot ? srcLight.spotAngle / 2f : 90f;
            float angleToFace = Vector3.Angle(source.forward, toFaceCenter);

            float halfW, halfH;
            GetFaceHalfExtents(out halfW, out halfH);
            float faceAngularRadius = Mathf.Atan(Mathf.Max(halfW, halfH) / Mathf.Max(distance, 0.001f)) * Mathf.Rad2Deg;

            if (angleToFace > sourceHalfCone + faceAngularRadius)
                continue;

            // --- Raycast: line of sight ---
            Vector3 rayOrigin = source.position;
            Vector3 rayDir = (faceCenter - source.position).normalized;
            float rayDist = distance - 0.1f;
            if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit blockHit, rayDist, blockingMask))
            {
                GameObject hitObj = blockHit.collider.gameObject;
                if (hitObj != gameObject
                    && hitObj != source.gameObject
                    && !hitObj.CompareTag("Player")
                    && !source.IsChildOf(hitObj.transform)
                    && !hitObj.transform.IsChildOf(source))
                {
                    continue;
                }
            }

            // --- Valid source found! ---
            activeSource = source;

            // Find exact hit point on face
            Vector3 reflectOrigin = faceCenter;
            float denom = Vector3.Dot(source.forward, faceNormal);
            if (denom < 0f)
            {
                float t = Vector3.Dot(faceCenter - source.position, faceNormal) / denom;
                if (t > 0f)
                {
                    Vector3 hitPoint = source.position + source.forward * t;
                    if (IsPointOnFace(hitPoint, faceCenter, faceNormal))
                        reflectOrigin = hitPoint;
                }
            }

            spotLight.enabled = true;

            Vector3 incomingDir = (reflectOrigin - source.position).normalized;
            Vector3 reflectedDir = Vector3.Reflect(incomingDir, faceNormal);

            spotLight.transform.position = reflectOrigin;
            spotLight.transform.rotation = Quaternion.LookRotation(reflectedDir);
            spotLight.intensity = sourceIntensity * intensityMultiplier * falloff;
            spotLight.range = spotRange;
            spotLight.color = spotColor;
            spotLight.spotAngle = spotAngle;

            return;
        }

        // No valid source found.
        activeSource = null;
        DisableReflection();
    }

    void DisableReflection()
    {
        spotLight.intensity = 0f;
        spotLight.enabled = false;
    }

    bool IsPointOnFace(Vector3 worldPoint, Vector3 faceCenter, Vector3 faceNormal)
    {
        Vector3 up, right;
        GetFaceTangents(faceNormal, out up, out right);

        float halfW, halfH;
        GetFaceHalfExtents(out halfW, out halfH);

        Vector3 toPoint = worldPoint - faceCenter;
        float u = Vector3.Dot(toPoint, right);
        float v = Vector3.Dot(toPoint, up);

        return Mathf.Abs(u) <= halfW + 0.001f && Mathf.Abs(v) <= halfH + 0.001f;
    }

    void GetFaceTangents(Vector3 normal, out Vector3 up, out Vector3 right)
    {
        if (Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.99f)
        {
            up = transform.forward;
            right = transform.right;
        }
        else if (Mathf.Abs(Vector3.Dot(normal, transform.right)) > 0.99f)
        {
            up = transform.up;
            right = transform.forward;
        }
        else
        {
            up = transform.up;
            right = transform.right;
        }
    }

    void GetFaceHalfExtents(out float halfW, out float halfH)
    {
        Vector3 size = Vector3.Scale(boxCol.size, transform.lossyScale);
        switch (reflectiveFace)
        {
            case MirrorFace.Forward:
            case MirrorFace.Back:
                halfW = size.x * 0.5f; halfH = size.y * 0.5f; break;
            case MirrorFace.Left:
            case MirrorFace.Right:
                halfW = size.z * 0.5f; halfH = size.y * 0.5f; break;
            default:
                halfW = size.x * 0.5f; halfH = size.z * 0.5f; break;
        }
    }

    void OnDestroy()
    {
        if (autoCreatedLight && spotLight != null)
            Destroy(spotLight.gameObject);
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        if (boxCol == null) boxCol = GetComponent<BoxCollider>();

        Vector3 faceCenter = GetFaceCenter();
        Vector3 faceNormal = GetFaceNormal();

        // Mirror face
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.5f);
        DrawFaceQuad(faceCenter, faceNormal);

        // Face normal
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(faceCenter, faceNormal * 1.5f);
        Gizmos.DrawWireSphere(faceCenter, 0.05f);

        if (lightSources == null) return;

        foreach (Transform source in lightSources)
        {
            if (source == null) continue;

            Light srcLight = source.GetComponent<Light>();
            if (srcLight == null) continue;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(source.position, 0.15f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(source.position, source.forward * 10f);

            float halfCone = srcLight.type == LightType.Spot ? srcLight.spotAngle / 2f : 90f;

            Vector3 toFace = faceCenter - source.position;
            float angleToFace = Vector3.Angle(source.forward, toFace);
            float dist = toFace.magnitude;

            float halfW, halfH;
            GetFaceHalfExtents(out halfW, out halfH);
            float faceAngRad = Mathf.Atan(Mathf.Max(halfW, halfH) / Mathf.Max(dist, 0.001f)) * Mathf.Rad2Deg;

            bool inCone = Vector3.Dot(faceNormal, source.position - faceCenter) > 0f
                       && angleToFace <= halfCone + faceAngRad;

            Gizmos.color = inCone ? Color.green : Color.red;
            Gizmos.DrawLine(source.position, faceCenter);

            if (inCone)
            {
                Vector3 incomingDir = (faceCenter - source.position).normalized;
                Vector3 reflectedDir = Vector3.Reflect(incomingDir, faceNormal);
                Gizmos.color = Color.green;
                Gizmos.DrawRay(faceCenter, reflectedDir * 3f);
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(faceCenter, 0.1f);
            }
        }
    }

    public Vector3 GetFaceCenter()
    {
        if (boxCol == null) boxCol = GetComponent<BoxCollider>();
        Vector3 size = Vector3.Scale(boxCol.size, transform.lossyScale);
        Vector3 center = transform.TransformPoint(boxCol.center);
        Vector3 offset = GetFaceNormal() * (GetFaceThickness() * 0.5f);
        return center + offset;
    }

    public Vector3 GetFaceNormal()
    {
        switch (reflectiveFace)
        {
            case MirrorFace.Forward:  return transform.forward;
            case MirrorFace.Back:     return -transform.forward;
            case MirrorFace.Left:     return -transform.right;
            case MirrorFace.Right:    return transform.right;
            case MirrorFace.Up:       return transform.up;
            case MirrorFace.Down:     return -transform.up;
            default:                  return transform.forward;
        }
    }

    float GetFaceThickness()
    {
        Vector3 size = Vector3.Scale(boxCol.size, transform.lossyScale);
        switch (reflectiveFace)
        {
            case MirrorFace.Forward:
            case MirrorFace.Back:     return size.z;
            case MirrorFace.Left:
            case MirrorFace.Right:    return size.x;
            case MirrorFace.Up:
            case MirrorFace.Down:     return size.y;
            default:                  return size.z;
        }
    }

    void CreateSpotlight()
    {
        if (spotLight != null) return;

        GameObject spotObj = new GameObject("ReflectionSpotlight");
        spotObj.transform.SetParent(transform);
        spotObj.transform.localPosition = Vector3.zero;
        spotObj.transform.localRotation = Quaternion.identity;
        spotObj.hideFlags = HideFlags.NotEditable;

        spotLight = spotObj.AddComponent<Light>();
        spotLight.type = LightType.Spot;
        spotLight.spotAngle = spotAngle;
        spotLight.range = spotRange;
        spotLight.color = spotColor;
        spotLight.intensity = 0f;
        spotLight.shadows = LightShadows.Hard;
    }

    void DrawFaceQuad(Vector3 center, Vector3 normal)
    {
        Vector3 up, right;
        GetFaceTangents(normal, out up, out right);

        float halfW, halfH;
        GetFaceHalfExtents(out halfW, out halfH);

        Vector3 cornerA = center + right * halfW + up * halfH;
        Vector3 cornerB = center - right * halfW + up * halfH;
        Vector3 cornerC = center - right * halfW - up * halfH;
        Vector3 cornerD = center + right * halfW - up * halfH;

        Gizmos.DrawLine(cornerA, cornerB);
        Gizmos.DrawLine(cornerB, cornerC);
        Gizmos.DrawLine(cornerC, cornerD);
        Gizmos.DrawLine(cornerD, cornerA);
    }
}
