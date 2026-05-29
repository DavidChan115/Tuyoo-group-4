using UnityEngine;

public class PointLightController : MonoBehaviour
{
    [Header("Pickup / Drop")]
    public Transform player;
    public Vector3 heldLocalPosition = new Vector3(-0.1f, 0.43f, -0.8f);
    public float flySpeed = 8f;
    public float flySnapDistance = 0.1f;

    [Header("Spotlight")]
    public Light spotLight;

    [Header("Flicker")]
    public float baseIntensity = 10f;
    public float flickerAmount = 1.5f;
    public float flickerSpeed = 6f;
    public float flutterAmount = 0.3f;
    public float flutterSpeed = 18f;

    private new Light light;
    private bool isHeld = true;
    private bool isFlying;
    private float originalIntensity;

    void Start()
    {
        light = GetComponent<Light>();
        originalIntensity = light.intensity;
    }

    void Update()
    {
        if (light == null) return;

        if (isFlying)
            FlyTowardPlayer();
        else
            HandlePickupDrop();

        ApplyFlicker();
        OrientSpotlight();
    }

    void OrientSpotlight()
    {
        if (spotLight == null || player == null) return;
        if (!isHeld && !isFlying) return;

        spotLight.transform.rotation = Quaternion.LookRotation(player.forward, Vector3.up);
    }

    void FlyTowardPlayer()
    {
        Vector3 targetWorldPos = player.TransformPoint(heldLocalPosition);
        Vector3 toTarget = targetWorldPos - transform.position;
        float distance = toTarget.magnitude;

        if (distance <= flySnapDistance)
        {
            transform.SetParent(player);
            transform.localPosition = heldLocalPosition;
            isFlying = false;
            isHeld = true;
            return;
        }

        float step = flySpeed * Time.deltaTime;
        transform.position += toTarget.normalized * Mathf.Min(step, distance);
    }

    void HandlePickupDrop()
    {
        if (player == null) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (isHeld)
            {
                transform.SetParent(null);
                isHeld = false;
            }
            else
            {
                isFlying = true;
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
