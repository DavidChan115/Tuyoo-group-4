using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Volumetric beam visualization for Unity spotlights (URP).
/// Delete this file and SoftVolumetricBeam.shader to remove the experiment.
/// It never creates or edits a camera and does not alter gameplay light/shadow logic.
///
/// The visible beam is ray-marched analytically inside a bounding cone, so it
/// reads the same from every viewing angle and stops exactly where the light
/// meets opaque geometry (via the URP camera depth texture).
/// </summary>
[AddComponentMenu("ShadowBridge/Lighting/Light Beam Guide")]
[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
public class LightBeamGuide : MonoBehaviour
{
    [Header("Player Beam")]
    [Tooltip("HDR color of this spotlight's visible beam.")]
    [ColorUsage(true, true)]
    public Color beamColor = new Color(1f, 0.72f, 0.28f, 1f);
    [Tooltip("Beam density. Additive, so small values already read clearly in a dark scene.")]
    [Range(0f, 0.15f)]
    public float beamOpacity = 0.05f;
    [Tooltip("Ceiling for how bright the beam can get when looking straight down it. Keeps side-on and head-on views consistent.")]
    [Range(0.05f, 2f)]
    public float maxBrightness = 0.5f;

    [Header("Mirror Beam")]
    [Tooltip("On a player light: give every MirrorReflection's reflected spotlight its own beam at start-up.")]
    public bool showMirrorBeam = true;
    [ColorUsage(true, true)]
    public Color mirrorColor = new Color(0.3f, 0.72f, 1f, 1f);
    [Range(0f, 0.15f)]
    public float mirrorOpacity = 0.035f;

    [Header("Volumetric Quality")]
    [Tooltip("Ray-march samples per pixel. 12-24 is a good range.")]
    [Range(4, 48)]
    public int raymarchSteps = 16;
    [Tooltip("How quickly brightness decays along the beam. Higher = fades sooner.")]
    [Range(1f, 30f)]
    public float distanceFalloff = 5f;
    [Tooltip("Metres over which the beam fades in right at the light source.")]
    [Range(0f, 2f)]
    public float apexFade = 0.35f;
    [Range(0.1f, 8f)]
    public float noiseScale = 1.4f;
    [Range(0f, 2f)]
    public float noiseSpeed = 0.25f;
    [Tooltip("How much drifting dust texture shows in the beam. 0 = perfectly smooth.")]
    [Range(0f, 1f)]
    public float noiseStrength = 0.35f;
    [Tooltip("Modulate beam brightness with the spotlight's intensity flicker.")]
    public bool followLightFlicker = true;
    [Tooltip("Cut the beam where it meets opaque geometry using the URP depth texture.")]
    public bool clipWithSceneDepth = true;

    [Header("TestScene Preview")]
    [Tooltip("TestScene only: spawn a disposable mirror when the scene has no MirrorReflection.")]
    public bool previewMirrorIfMissing = false;

    [Tooltip("Assigned automatically. Keeps the shader referenced so player builds don't strip it.")]
    [SerializeField] Shader beamShader;

    // Maps the 0-0.15 opacity sliders onto additive HDR energy.
    const float IntensityScale = 6f;
    // Keeps the bounding cone well inside typical camera far planes.
    const float MaxDisplayRange = 200f;
    const int ConeSegments = 32;

    const string ShaderName = "ShadowBridge/Soft Volumetric Beam";
    // Guides added at runtime miss the script's default reference, so they reuse this.
    static Shader sharedShader;

    Light sourceLight;

    GameObject beamVolume;
    Mesh beamMesh;
    MeshRenderer beamRenderer;
    Material beamMaterial;

    float lastLength = -1f;
    float lastAngle = -1f;

    float referenceIntensity = -1f;
    bool depthTextureAvailable;

    MirrorReflection previewMirror;
    Material previewMirrorMaterial;
    bool previewSpawnAttempted;
    MirrorReflection owningMirror;

    static readonly int ConeOriginId = Shader.PropertyToID("_ConeOrigin");
    static readonly int ConeAxisId = Shader.PropertyToID("_ConeAxis");
    static readonly int ConeLengthId = Shader.PropertyToID("_ConeLength");
    static readonly int CosOuterId = Shader.PropertyToID("_CosOuter");
    static readonly int CosInnerId = Shader.PropertyToID("_CosInner");
    static readonly int BeamColorId = Shader.PropertyToID("_BeamColor");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int MaxBrightnessId = Shader.PropertyToID("_MaxBrightness");
    static readonly int FalloffId = Shader.PropertyToID("_Falloff");
    static readonly int ApexFadeId = Shader.PropertyToID("_ApexFade");
    static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
    static readonly int NoiseSpeedId = Shader.PropertyToID("_NoiseSpeed");
    static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
    static readonly int StepsId = Shader.PropertyToID("_Steps");
    static readonly int UseSceneDepthId = Shader.PropertyToID("_UseSceneDepth");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AttachInTestScene()
    {
        if (SceneManager.GetActiveScene().name != "TestScene")
            return;

        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
        foreach (Light light in lights)
        {
            if (light.type != LightType.Spot)
                continue;
            if (light.GetComponent<PointLightController>() == null)
                continue;
            if (light.GetComponentInParent<MirrorReflection>() != null)
                continue;
            if (light.GetComponent<LightBeamGuide>() == null)
            {
                LightBeamGuide guide = light.gameObject.AddComponent<LightBeamGuide>();
                guide.previewMirrorIfMissing = true;
            }
        }
    }

    void Awake()
    {
        sourceLight = GetComponent<Light>();
        if (sourceLight == null || sourceLight.type != LightType.Spot)
        {
            enabled = false;
            return;
        }
        owningMirror = FindOwningMirror();

        if (beamShader == null)
            beamShader = sharedShader != null ? sharedShader : Shader.Find(ShaderName);
        if (beamShader == null)
        {
            Debug.LogError("[LightBeamGuide] Soft Volumetric Beam shader was not found.");
            enabled = false;
            return;
        }
        sharedShader = beamShader;

        depthTextureAvailable = PipelineProvidesDepthTexture();
        if (clipWithSceneDepth && !depthTextureAvailable)
        {
            Debug.LogWarning("[LightBeamGuide] URP asset has Depth Texture disabled; " +
                             "the beam falls back to a raycast length instead of per-pixel clipping.");
        }

        beamMaterial = new Material(beamShader) { name = name + " Volumetric Beam" };
        beamVolume = CreateVolume(name + " BeamVolume", beamMaterial, out beamMesh, out beamRenderer);
    }

    void Start()
    {
        // Every mirror has created its reflection light in Awake by now.
        if (!showMirrorBeam || owningMirror != null)
            return;

        foreach (MirrorReflection mirror in Object.FindObjectsByType<MirrorReflection>(FindObjectsInactive.Include))
            AttachMirrorGuide(mirror);
    }

    MirrorReflection FindOwningMirror()
    {
        foreach (MirrorReflection mirror in Object.FindObjectsByType<MirrorReflection>(FindObjectsInactive.Include))
        {
            if (mirror.customReflectionLight == sourceLight)
                return mirror;
        }
        return GetComponentInParent<MirrorReflection>();
    }

    void AttachMirrorGuide(MirrorReflection mirror)
    {
        Light reflected = mirror.ReflectionLight != null ? mirror.ReflectionLight : mirror.customReflectionLight;
        if (reflected == null || reflected == sourceLight || reflected.GetComponent<LightBeamGuide>() != null)
            return;

        LightBeamGuide guide = reflected.gameObject.AddComponent<LightBeamGuide>();
        guide.beamColor = mirrorColor;
        guide.beamOpacity = mirrorOpacity;
        guide.maxBrightness = maxBrightness;
        guide.showMirrorBeam = false;
        guide.raymarchSteps = raymarchSteps;
        guide.distanceFalloff = distanceFalloff;
        guide.apexFade = apexFade;
        guide.noiseScale = noiseScale;
        guide.noiseSpeed = noiseSpeed;
        guide.noiseStrength = noiseStrength;
        guide.clipWithSceneDepth = clipWithSceneDepth;
    }

    void LateUpdate()
    {
        if (sourceLight == null ||
            !sourceLight.enabled ||
            (owningMirror != null && !owningMirror.displayReflectedBeam))
        {
            SetVisible(beamRenderer, false);
            return;
        }

        // PointLightController aims the spotlight during Update. Spawning here,
        // one frame later, keeps the disposable test mirror in the actual beam.
        if (!previewSpawnAttempted &&
            previewMirrorIfMissing &&
            SceneManager.GetActiveScene().name == "TestScene")
        {
            previewSpawnAttempted = true;
            SpawnPreviewMirror();
        }

        float range = sourceLight.range;
        float intensity = beamOpacity;
        if (owningMirror != null)
        {
            if (owningMirror.reflectedBeamDisplayRange > 0f)
                range = owningMirror.reflectedBeamDisplayRange;
            intensity = owningMirror.reflectedBeamDisplayIntensity;
        }

        // Flicker coupling only makes sense for a light whose intensity is
        // animated in place (PointLightController); mirror lights are re-driven
        // from the source every frame.
        float flicker = 1f;
        if (followLightFlicker && owningMirror == null)
        {
            if (referenceIntensity <= 0f)
                referenceIntensity = sourceLight.intensity;
            flicker = Mathf.Clamp(sourceLight.intensity / Mathf.Max(referenceIntensity, 1e-3f), 0f, 2f);
        }

        SetVisible(beamRenderer, true);
        ConfigureBeam(sourceLight, transform.position, transform.rotation,
            range, beamColor, intensity * flicker);
    }

    void ConfigureBeam(Light light, Vector3 origin, Quaternion rotation,
        float range, Color color, float intensity)
    {
        GameObject volume = beamVolume;
        Material material = beamMaterial;

        range = Mathf.Clamp(range, 0.1f, MaxDisplayRange);
        float outerAngle = Mathf.Clamp(light.spotAngle, 1f, 179f);
        float innerAngle = Mathf.Clamp(light.innerSpotAngle, 0f, outerAngle - 1f);

        // Without a depth texture the shader cannot see the floor, so shrink the
        // bounding cone to the first hit along the centre ray instead.
        float boundingLength = range;
        if (!(clipWithSceneDepth && depthTextureAvailable))
        {
            Vector3 forward = rotation * Vector3.forward;
            boundingLength = CastDistance(origin, forward, range, light.transform.root);
        }

        volume.transform.SetPositionAndRotation(origin, rotation);
        UpdateVolumeMesh(beamMesh, boundingLength, outerAngle, ref lastLength, ref lastAngle);

        material.SetVector(ConeOriginId, origin);
        material.SetVector(ConeAxisId, rotation * Vector3.forward);
        material.SetFloat(ConeLengthId, range);
        material.SetFloat(CosOuterId, Mathf.Cos(outerAngle * 0.5f * Mathf.Deg2Rad));
        material.SetFloat(CosInnerId, Mathf.Cos(innerAngle * 0.5f * Mathf.Deg2Rad));
        material.SetColor(BeamColorId, color);
        material.SetFloat(IntensityId, intensity * IntensityScale);
        material.SetFloat(MaxBrightnessId, maxBrightness);
        material.SetFloat(FalloffId, distanceFalloff);
        material.SetFloat(ApexFadeId, apexFade);
        material.SetFloat(NoiseScaleId, noiseScale);
        material.SetFloat(NoiseSpeedId, noiseSpeed);
        material.SetFloat(NoiseStrengthId, noiseStrength);
        material.SetFloat(StepsId, raymarchSteps);
        material.SetFloat(UseSceneDepthId, clipWithSceneDepth && depthTextureAvailable ? 1f : 0f);
    }

    void SpawnPreviewMirror()
    {
        if (Object.FindAnyObjectByType<MirrorReflection>() != null)
            return;

        GameObject mirrorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mirrorObject.name = "_PREVIEW_TestMirror";
        mirrorObject.transform.position = transform.position + transform.forward * 4.5f;

        Vector3 faceNormal = (-transform.forward + transform.right).normalized;
        mirrorObject.transform.rotation = Quaternion.LookRotation(faceNormal, Vector3.up);
        mirrorObject.transform.localScale = new Vector3(2.6f, 2.6f, 0.12f);

        Renderer mirrorRendererComponent = mirrorObject.GetComponent<Renderer>();
        if (mirrorRendererComponent != null)
        {
            mirrorRendererComponent.shadowCastingMode = ShadowCastingMode.Off;
            Renderer sourceRenderer = transform.root.GetComponentInChildren<MeshRenderer>();
            if (sourceRenderer != null && sourceRenderer.sharedMaterial != null)
            {
                previewMirrorMaterial = new Material(sourceRenderer.sharedMaterial);
                previewMirrorMaterial.name = "Test Mirror Material";
                Color glass = new Color(0.34f, 0.58f, 0.72f, 1f);
                if (previewMirrorMaterial.HasProperty("_BaseColor"))
                    previewMirrorMaterial.SetColor("_BaseColor", glass);
                previewMirrorMaterial.color = glass;
                mirrorRendererComponent.sharedMaterial = previewMirrorMaterial;
            }
        }

        previewMirror = mirrorObject.AddComponent<MirrorReflection>();
        previewMirror.lightSources = new Transform[] { transform };
        previewMirror.reflectiveFace = MirrorReflection.MirrorFace.Forward;
        previewMirror.spotAngle = sourceLight.spotAngle;
        previewMirror.spotRange = sourceLight.range;
        previewMirror.spotColor = mirrorColor;
        previewMirror.drawGizmos = false;
        previewMirror.intensityMultiplier = 8f;
        if (showMirrorBeam)
            AttachMirrorGuide(previewMirror);
    }

    static bool PipelineProvidesDepthTexture()
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        return urp != null && urp.supportsCameraDepthTexture;
    }

    static GameObject CreateVolume(string objectName, Material material,
        out Mesh mesh, out MeshRenderer renderer)
    {
        // World-space root object so parent scale never distorts the cone.
        GameObject volume = new GameObject(objectName);
        volume.hideFlags = HideFlags.HideAndDontSave;

        mesh = new Mesh { name = objectName + " Mesh" };
        MeshFilter filter = volume.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        renderer = volume.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        return volume;
    }

    void UpdateVolumeMesh(Mesh mesh, float length, float angle,
        ref float previousLength, ref float previousAngle)
    {
        length = Mathf.Max(length, 0.05f);
        if (Mathf.Abs(length - previousLength) < 0.015f &&
            Mathf.Abs(angle - previousAngle) < 0.1f)
            return;

        previousLength = length;
        previousAngle = angle;
        BuildCone(mesh, length, angle, ConeSegments);
    }

    /// <summary>
    /// Closed cone: apex at the origin, opening along +Z, capped at z = length.
    /// Winding makes the outside the front face so "Cull Front" in the shader
    /// draws only the far side of the volume.
    /// </summary>
    static void BuildCone(Mesh mesh, float length, float angleDegrees, int segments)
    {
        segments = Mathf.Max(8, segments);
        // Small margin so the bounding mesh never clips the soft outer edge.
        float radius = length * Mathf.Tan(angleDegrees * 0.5f * Mathf.Deg2Rad) * 1.04f + 0.02f;

        int apexIndex = 0;
        int ringStart = 1;
        int capCenterIndex = segments + 1;

        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 6];

        vertices[apexIndex] = Vector3.zero;
        vertices[capCenterIndex] = new Vector3(0f, 0f, length);
        for (int i = 0; i < segments; i++)
        {
            float a = (float)i / segments * Mathf.PI * 2f;
            vertices[ringStart + i] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, length);
        }

        int t = 0;
        for (int i = 0; i < segments; i++)
        {
            int current = ringStart + i;
            int next = ringStart + (i + 1) % segments;

            // Side, outward facing.
            triangles[t++] = apexIndex;
            triangles[t++] = next;
            triangles[t++] = current;

            // Cap, facing +Z.
            triangles[t++] = capCenterIndex;
            triangles[t++] = current;
            triangles[t++] = next;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    static float CastDistance(Vector3 origin, Vector3 direction, float range, Transform ignoreRoot)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, range, ~0, QueryTriggerInteraction.Ignore);
        float closest = range;

        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.transform;
            if (ignoreRoot != null &&
                (hitTransform == ignoreRoot || hitTransform.IsChildOf(ignoreRoot)))
                continue;

            if (hit.distance < closest)
                closest = hit.distance;
        }

        return closest;
    }

    static void SetVisible(Renderer renderer, bool visible)
    {
        if (renderer != null)
            renderer.enabled = visible;
    }

    void OnDisable()
    {
        SetVisible(beamRenderer, false);
    }

    void OnDestroy()
    {
        if (previewMirror != null)
            Destroy(previewMirror.gameObject);
        if (beamVolume != null)
            Destroy(beamVolume);
        if (beamMaterial != null)
            Destroy(beamMaterial);
        if (previewMirrorMaterial != null)
            Destroy(previewMirrorMaterial);
        if (beamMesh != null)
            Destroy(beamMesh);
    }
}
