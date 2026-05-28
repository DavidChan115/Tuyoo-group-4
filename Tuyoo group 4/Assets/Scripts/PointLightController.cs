using UnityEngine;

public class PointLightController : MonoBehaviour
{
    [Header("Pickup / Drop")]
    public Transform player;
    public float pickupRange = 3f;
    public Vector3 heldLocalPosition = new Vector3(-1.52f, 0.98f, 0.12f);

    [Header("Flicker")]
    public float baseIntensity = 10f;
    public float flickerAmount = 1.5f;
    public float flickerSpeed = 6f;
    public float flutterAmount = 0.3f;
    public float flutterSpeed = 18f;

    private new Light light;
    private bool isHeld = true;
    private bool wasInRange = false;
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

        float dist = Vector3.Distance(transform.position, player.position);
        bool inRange = dist <= pickupRange;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (isHeld)
            {
                // Drop: unparent, stay at world position
                transform.SetParent(null);
                isHeld = false;
            }
            else if (inRange)
            {
                // Pick up: re-parent to player
                transform.SetParent(player);
                transform.localPosition = heldLocalPosition;
                isHeld = true;
            }
        }

        wasInRange = inRange;
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

    void OnDrawGizmosSelected()
    {
        if (!isHeld && player != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(player.position, pickupRange);
        }
    }
}
