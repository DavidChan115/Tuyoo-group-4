using UnityEngine;

public class PointLightController : MonoBehaviour
{
    [Header("Pickup / Drop")]
    public Transform player;
    public Vector3 heldLocalPosition = new Vector3(-1.52f, 0.98f, 0.12f);

    [Header("Flicker")]
    public float baseIntensity = 10f;
    public float flickerAmount = 1.5f;
    public float flickerSpeed = 6f;
    public float flutterAmount = 0.3f;
    public float flutterSpeed = 18f;

    private new Light light;
    private bool isHeld = true;
    private float originalIntensity;

    void Start()
    {
        light = GetComponent<Light>();
        originalIntensity = light.intensity;
    }

    void Update()
    {
        if (light == null) return;

        HandlePickupDrop();
        ApplyFlicker();
    }

    void HandlePickupDrop()
    {
        if (player == null) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (isHeld)
            {
                // Drop: unparent, stay at world position
                transform.SetParent(null);
                isHeld = false;
            }
            else
            {
                // Pick up: teleport to player, re-parent
                transform.SetParent(player);
                transform.localPosition = heldLocalPosition;
                isHeld = true;
            }
        }
    }

    void ApplyFlicker()
    {
        float t = Time.time;

        // Primary slow flicker via Perlin noise
        float primary = Mathf.PerlinNoise(t * flickerSpeed, 0f);
        primary = (primary - 0.5f) * 2f; // remap [0,1] to [-1, 1]

        // Secondary fast flutter
        float flutter = Mathf.PerlinNoise(t * flutterSpeed, 7.3f);
        flutter = (flutter - 0.5f) * 2f;

        float flicker = primary * flickerAmount + flutter * flutterAmount;
        light.intensity = baseIntensity + flicker;
    }

}
