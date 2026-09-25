using UnityEngine;
using TMPro;

[RequireComponent(typeof(Rigidbody))]
public class PushableObject : MonoBehaviour
{
    [Header("Prompt")]
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private float proximityDistance = 3f;

    [Header("Ground Lock")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckDistance = 5f;

    [Header("Ground Boundary")]
    [Tooltip("If enabled, the object cannot be pushed/pulled beyond the edges of the ground it starts on.")]
    [SerializeField] private bool constrainToGroundBounds = true;

    [Header("Void Reset")]
    [SerializeField] private float voidYThreshold = -20f;
    [SerializeField] private bool useCustomResetPosition;
    [SerializeField] private Vector3 customResetPosition;

    private Rigidbody rb;
    private Collider col;
    private Transform playerTransform;
    private Camera cachedCamera;
    private Canvas promptCanvas;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private bool isGrabbed;
    private bool isInVoid;

    // Ground boundary clamping
    private Bounds groundBounds;
    private float objectRadius;
    private bool hasGroundBounds;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        rb.isKinematic = true;
        rb.useGravity = false;

        startPosition = transform.position;
        startRotation = transform.rotation;

        playerTransform = GameObject.FindWithTag("Player")?.transform;
        cachedCamera = Camera.main;

        if (promptRoot != null)
        {
            promptCanvas = promptRoot.GetComponent<Canvas>();
            promptRoot.SetActive(false);
        }

        if (promptText != null)
            promptText.text = "Press F to move the object";

        if (constrainToGroundBounds)
            DetectGroundBounds();
    }

    void Update()
    {
        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
            cachedCamera = Camera.main;

        if (promptCanvas != null && cachedCamera != null && promptCanvas.worldCamera != cachedCamera)
            promptCanvas.worldCamera = cachedCamera;

        if (promptRoot != null && playerTransform != null)
        {
            bool inRange = Vector3.Distance(transform.position, playerTransform.position) <= proximityDistance;
            if (promptRoot.activeSelf != inRange)
                promptRoot.SetActive(inRange);
        }

        if (!isGrabbed && transform.position.y < voidYThreshold)
            ResetToStart();
    }

    void LateUpdate()
    {
        if (promptRoot != null && promptRoot.activeSelf && cachedCamera != null)
        {
            promptRoot.transform.rotation = Quaternion.LookRotation(
                cachedCamera.transform.position - promptRoot.transform.position);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Void"))
        {
            isInVoid = true;
            if (!isGrabbed)
                ResetToStart();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Void"))
            isInVoid = false;
    }

    public void OnGrabbed()
    {
        isGrabbed = true;
        rb.isKinematic = true;
        rb.useGravity = false;
        isInVoid = false;

        if (promptRoot != null)
            promptRoot.SetActive(false);
    }

    public void OnReleased()
    {
        isGrabbed = false;

        // Snap Y to ground
        float halfHeight = col != null ? col.bounds.extents.y : 0.5f;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, groundCheckDistance, groundMask))
        {
            Vector3 snapped = transform.position;
            snapped.y = hit.point.y + halfHeight;
            transform.position = snapped;
        }

        // Lock in place — kinematic, no gravity, no constraints needed
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    void ResetToStart()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;
        isGrabbed = false;

        transform.position = useCustomResetPosition ? customResetPosition : startPosition;
        transform.rotation = startRotation;
        isInVoid = false;
    }

    void DetectGroundBounds()
    {
        float halfHeight = col != null ? col.bounds.extents.y : 0.5f;
        Vector3 rayOrigin = transform.position + Vector3.up * 1f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20f, groundMask))
        {
            Collider groundCol = hit.collider;
            groundBounds = groundCol.bounds;
            hasGroundBounds = true;
        }

        if (col != null)
        {
            Bounds b = col.bounds;
            objectRadius = Mathf.Max(b.extents.x, b.extents.z);
        }
    }

    public Vector3 ClampToGroundArea(Vector3 position)
    {
        if (!hasGroundBounds) return position;

        Vector3 clamped = position;
        float minX = groundBounds.min.x + objectRadius;
        float maxX = groundBounds.max.x - objectRadius;
        float minZ = groundBounds.min.z + objectRadius;
        float maxZ = groundBounds.max.z - objectRadius;

        if (minX < maxX)
            clamped.x = Mathf.Clamp(clamped.x, minX, maxX);
        else
            clamped.x = (minX + maxX) * 0.5f;

        if (minZ < maxZ)
            clamped.z = Mathf.Clamp(clamped.z, minZ, maxZ);
        else
            clamped.z = (minZ + maxZ) * 0.5f;

        return clamped;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, proximityDistance);

        Gizmos.color = Color.red;
        Vector3 lineCenter = new Vector3(transform.position.x, voidYThreshold, transform.position.z);
        Gizmos.DrawLine(lineCenter + Vector3.left * 5f, lineCenter + Vector3.right * 5f);
        Gizmos.DrawLine(lineCenter + Vector3.forward * 5f, lineCenter + Vector3.back * 5f);

        if (useCustomResetPosition)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(customResetPosition, 0.3f);
        }

        if (hasGroundBounds)
        {
            Gizmos.color = Color.cyan;
            float y = groundBounds.center.y;
            Vector3 center = new Vector3(groundBounds.center.x, y, groundBounds.center.z);
            Vector3 size = new Vector3(
                groundBounds.size.x - objectRadius * 2f,
                0.05f,
                groundBounds.size.z - objectRadius * 2f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
