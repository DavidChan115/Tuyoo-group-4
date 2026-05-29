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
    [Tooltip("Which face of the box acts as the reflective surface. The face centre is the origin of the normal.")]
    public MirrorFace reflectiveFace = MirrorFace.Forward;

    [Header("Light Source")]
    [Tooltip("The transform whose position is traced as the incoming light source (e.g. a Camera, a Light, or an empty).")]
    public Transform lightSource;

    [Header("Intensity")]
    [Tooltip("Scale factor for the reflected spotlight intensity. 1 = same as source, >1 = magnify, <1 = diminish.")]
    public float intensityMultiplier = 1f;
    [Tooltip("Apply inverse-square distance falloff so the reflection dims as the source moves farther away.")]
    public bool useDistanceFalloff = true;

    [Header("Reflected Spotlight")]
    [Tooltip("Inner + outer cone angle of the reflected spot.")]
    [Range(1f, 179f)]
    public float spotAngle = 30f;
    [Tooltip("How far the reflected spotlight reaches.")]
    public float spotRange = 20f;
    public Color spotColor = Color.white;
    [Tooltip("Optional — assign your own Light to use as the reflection ray. If left empty, a Spotlight is created automatically.")]
    public Light customReflectionLight;

    [Header("Debug")]
    public bool drawGizmos = true;

    private Light spotLight;
    private bool autoCreatedLight;
    private BoxCollider boxCol;

    // ----- Unity events -----

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

        // Incoming direction (from light → mirror face centre)
        Vector3 incomingDir = (faceCenter - lightSource.position).normalized;

        // Reflect across the face normal
        Vector3 reflectedDir = Vector3.Reflect(incomingDir, faceNormal);

        // Position spotlight at face centre, aim along reflected direction
        spotLight.transform.position = faceCenter;
        spotLight.transform.rotation = Quaternion.LookRotation(reflectedDir);

        // Compute intensity
        float sourceIntensity = 1f;
        Light srcLight = lightSource.GetComponent<Light>();
        if (srcLight != null)
            sourceIntensity = srcLight.intensity;

        float distance = Vector3.Distance(lightSource.position, faceCenter);
        float falloff = useDistanceFalloff ? 1f / Mathf.Max(1f, distance * distance) : 1f;

        spotLight.intensity = sourceIntensity * intensityMultiplier * falloff;

        if (autoCreatedLight)
        {
            spotLight.color = spotColor;
            spotLight.spotAngle = spotAngle;
            spotLight.range = spotRange;
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

        // Draw mirror face outline (a quad at the face centre)
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.5f);
        DrawFaceQuad(faceCenter, faceNormal);

        // Face normal
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(faceCenter, faceNormal * 1.5f);
        // Small sphere at normal origin
        Gizmos.DrawWireSphere(faceCenter, 0.05f);

        if (lightSource != null)
        {
            // Incoming ray (light → mirror)
            Vector3 incomingDir = (faceCenter - lightSource.position).normalized;
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(lightSource.position, incomingDir * Vector3.Distance(lightSource.position, faceCenter));

            // Reflected ray (mirror → world)
            Vector3 reflectedDir = Vector3.Reflect(incomingDir, faceNormal);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(faceCenter, reflectedDir * 3f);

            // Light source marker
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(lightSource.position, 0.15f);
        }
    }

    // ----- Public helpers -----

    /// <summary>Returns the world-space centre of the selected mirror face.</summary>
    public Vector3 GetFaceCenter()
    {
        if (boxCol == null) boxCol = GetComponent<BoxCollider>();
        Vector3 size = Vector3.Scale(boxCol.size, transform.lossyScale);
        Vector3 center = transform.TransformPoint(boxCol.center);

        Vector3 offset = GetFaceNormal() * (GetFaceThickness() * 0.5f);
        return center + offset;
    }

    /// <summary>Returns the world-space normal of the selected mirror face.</summary>
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

    // ----- Private helpers -----

    private float GetFaceThickness()
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

    private void CreateSpotlight()
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

    private void DrawFaceQuad(Vector3 center, Vector3 normal)
    {
        Vector3 size = Vector3.Scale(boxCol.size, transform.lossyScale);

        // Pick two orthogonal tangents that lie on the face
        Vector3 up, right;
        if (Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.99f)
        {
            // Up / Down — tangents are forward and right
            up = transform.forward;
            right = transform.right;
        }
        else if (Mathf.Abs(Vector3.Dot(normal, transform.right)) > 0.99f)
        {
            // Left / Right — tangents are forward and up
            up = transform.up;
            right = transform.forward;
        }
        else
        {
            // Forward / Back — tangents are right and up
            up = transform.up;
            right = transform.right;
        }

        float halfW, halfH;
        switch (reflectiveFace)
        {
            case MirrorFace.Forward:
            case MirrorFace.Back:
                halfW = size.x * 0.5f;
                halfH = size.y * 0.5f;
                break;
            case MirrorFace.Left:
            case MirrorFace.Right:
                halfW = size.z * 0.5f;
                halfH = size.y * 0.5f;
                break;
            default:
                halfW = size.x * 0.5f;
                halfH = size.z * 0.5f;
                break;
        }

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
