using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SaturationController : MonoBehaviour
{
    [Tooltip("Starting saturation. -100 = full B&W, 0 = full color, -50 = muted.")]
    [Range(-100, 0)]
    [SerializeField]public float startSaturation = -80f;

    [Tooltip("Seconds to transition from B&W to full color.")]
    public float transitionDuration = 2f;

    private ColorAdjustments colorAdjustments;
    private bool colorRestored;
    private float transitionStartTime;

    void Start()
    {
        Volume volume = FindObjectOfType<Volume>();
        if (volume != null && volume.profile != null)
        {
            if (!volume.profile.TryGet(out colorAdjustments))
                colorAdjustments = volume.profile.Add<ColorAdjustments>(true);

            colorAdjustments.saturation.value = startSaturation;
        }
    }

    void Update()
    {
        if (colorRestored || colorAdjustments == null) return;

        bool allCollected = CollectableManager.Instance != null
                         && CollectableManager.Instance.AllCollected();

        bool endpointReached = EndPointNew.EndpointReached;

        if (allCollected && endpointReached)
        {
            colorRestored = true;
            transitionStartTime = Time.time;
        }
    }

    void LateUpdate()
    {
        if (!colorRestored || colorAdjustments == null) return;

        float elapsed = Time.time - transitionStartTime;
        float t = Mathf.Clamp01(elapsed / transitionDuration);

        colorAdjustments.saturation.value = Mathf.Lerp(startSaturation, 0f, t);
    }
}
