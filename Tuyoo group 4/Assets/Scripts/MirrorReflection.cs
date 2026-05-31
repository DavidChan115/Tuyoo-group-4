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

    [Header("Light Source")]
    [Tooltip("The Spotlight whose beam is reflected. Must be a Spot light.")]
    public Transform lightSource;

    [Header("Intensity")]
    public float intensityMultiplier = 1f;
    public bool useDistanceFalloff = true;

    [Header("Reflected Spotlight")]
    [Range(1f, 179f)]
    public float spotAngle = 30f;
    public float spotRange = 20f;
    public Color spotColor = Color.white;
    [Tooltip("Optional — assign your own Light. If empty, one is created automatically.")]
    public Light customReflectionLight;

    [Header("Debug")]
    public bool drawGizmos = true;

    private Light spotLight;
    private bool autoCreatedLight;
    private BoxCollider boxCol;

    void Awake()
    {
        boxCol = GetComponent<BoxCollider>();

        if (customReflectionLight != null)
        {
            spotLight = customReflectionLight;
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
        if (lightSource == null || spotLight == null) return;

        Vector3 faceCenter = GetFaceCenter();
        Vector3 faceNormal = GetFaceNormal();

        // Mirror only reflects when it faces the light source
        Vector3 faceToSource = lightSource.position - faceCenter;
        if (Vector3.Dot(faceNormal, faceToSource) <= 0f)
        {
            DisableReflection();
            return;
        }

        Light srcLight = lightSource.GetComponent<Light>();
        float sourceHalfCone = srcLight != null && srcLight.type == LightType.Spot
            ? srcLight.spotAngle / 2f
            : 90f;

        // Check if the mirror face is within the spotlight's cone
        Vector3 toFaceCenter = faceCenter - lightSource.position;
        float angleToFace = Vector3.Angle(lightSource.forward, toFaceCenter);
        float distance = toFaceCenter.magnitude;

        float halfW, halfH;
        GetFaceHalfExtents(out halfW, out halfH);
        float faceAngularRadius = Mathf.Atan(Mathf.Max(halfW, halfH) / Mathf.Max(distance, 0.001f)) * Mathf.Rad2Deg;

        if (angleToFace > sourceHalfCone + faceAngularRadius)
        {
            DisableReflection();
            return;
        }

        // Try to find the exact hit point on the mirror face
        Vector3 reflectOrigin = faceCenter;
        float denom = Vector3.Dot(lightSource.forward, faceNormal);
        if (denom < 0f)
        {
            float t = Vector3.Dot(faceCenter - lightSource.position, faceNormal) / denom;
            if (t > 0f)
            {
                Vector3 hitPoint = lightSource.position + lightSource.forward * t;
                if (IsPointOnFace(hitPoint, faceCenter, faceNormal))
                    reflectOrigin = hitPoint;
            }
        }

        spotLight.enabled = true;

        // Compute reflection
        Vector3 incomingDir = (reflectOrigin - lightSource.position).normalized;
        Vector3 reflectedDir = Vector3.Reflect(incomingDir, faceNormal);

        spotLight.transform.position = reflectOrigin;
        spotLight.transform.rotation = Quaternion.LookRotation(reflectedDir);

        float sourceIntensity = srcLight != null ? srcLight.intensity : 1f;
        float falloff = useDistanceFalloff ? 1f / Mathf.Max(1f, distance * distance) : 1f;

        spotLight.intensity = sourceIntensity * intensityMultiplier * falloff;

        if (autoCreatedLight)
        {
            spotLight.color = spotColor;
            spotLight.range = spotRange;
        }
    }

    void DisableReflection()
    {
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

        if (lightSource != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(lightSource.position, 0.15f);

            // Spotlight forward ray
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(lightSource.position, lightSource.forward * 10f);

            // Cone check visualization
            Light srcLight = lightSource.GetComponent<Light>();
            float halfCone = srcLight != null && srcLight.type == LightType.Spot
                ? srcLight.spotAngle / 2f : 90f;

            Vector3 toFace = faceCenter - lightSource.position;
            float angleToFace = Vector3.Angle(lightSource.forward, toFace);
            float dist = toFace.magnitude;

            float halfW, halfH;
            GetFaceHalfExtents(out halfW, out halfH);
            float faceAngRad = Mathf.Atan(Mathf.Max(halfW, halfH) / Mathf.Max(dist, 0.001f)) * Mathf.Rad2Deg;

            // Green if face is in cone, red if not
            bool inCone = Vector3.Dot(faceNormal, lightSource.position - faceCenter) > 0f
                       && angleToFace <= halfCone + faceAngRad;

            Gizmos.color = inCone ? Color.green : Color.red;
            Gizmos.DrawLine(lightSource.position, faceCenter);

            if (inCone)
            {
                // Draw reflected ray
                Vector3 incomingDir = (faceCenter - lightSource.position).normalized;
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
