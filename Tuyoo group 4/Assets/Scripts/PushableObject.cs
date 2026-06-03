using UnityEngine;
using TMPro;

[RequireComponent(typeof(Rigidbody))]
public class PushableObject : MonoBehaviour
{
    // ═══════════════════════════════════════════════
    // Prompt Reference (pre-made in Editor)
    // ═══════════════════════════════════════════════
    [Header("Prompt Reference")]
    [Tooltip("Drag the pre-made World Space Canvas GameObject here.")]
    [SerializeField] private GameObject promptRoot;

    [Tooltip("Drag the TextMeshProUGUI component on the prompt canvas here.")]
    [SerializeField] private TextMeshProUGUI promptTextComponent;

    // ═══════════════════════════════════════════════
    // Proximity Settings
    // ═══════════════════════════════════════════════
    [Header("Proximity Settings")]
    [Tooltip("How close the player must be for the prompt to appear.")]
    [SerializeField] private float proximityDistance = 3f;

    // ═══════════════════════════════════════════════
    // Void Reset
    // ═══════════════════════════════════════════════
    [Header("Void Reset")]
    [Tooltip("If true, object resets to its world position at Start(). " +
             "If false, the Custom Reset Position below is used.")]
    [SerializeField] private bool useDefaultResetPosition = true;

    [Tooltip("Custom world-space reset position. Only used when " +
             "Use Default Reset Position is false.")]
    [SerializeField] private Vector3 customResetPosition;

    [Tooltip("Y world-coordinate below which the object is considered 'in the void'.")]
    [SerializeField] private float voidYThreshold = -20f;

    // ═══════════════════════════════════════════════
    // Private State
    // ═══════════════════════════════════════════════
    private Rigidbody rb;
    private Transform playerTransform;
    private Camera cachedCamera;
    private Canvas promptCanvas;

    private Vector3 defaultResetPosition;
    private Quaternion defaultResetRotation;
    private bool isInVoidTrigger;
    private bool diagnosticsLogged;

    private static readonly RigidbodyConstraints FreezeAllButY =
        RigidbodyConstraints.FreezePositionX |
        RigidbodyConstraints.FreezePositionZ |
        RigidbodyConstraints.FreezeRotation;

    // ═══════════════════════════════════════════════
    // Unity Messages
    // ═══════════════════════════════════════════════

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.None;

        defaultResetPosition = transform.position;
        defaultResetRotation = transform.rotation;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            playerTransform = player.transform;

        cachedCamera = FindAnyCamera();

        if (promptRoot != null)
        {
            promptCanvas = promptRoot.GetComponent<Canvas>();
            promptRoot.SetActive(false);
        }

        // Default text string.
        if (promptTextComponent != null)
            promptTextComponent.text = "Press F to move the object";

        LogDiagnostics();
    }

    private void Update()
    {
        RefreshCameraReference();

        // Keep the world-space Canvas pointed at the active camera
        // (without this, the Canvas won't render at all).
        if (promptCanvas != null && promptCanvas.worldCamera != cachedCamera)
            promptCanvas.worldCamera = cachedCamera;

        // ── Proximity prompt ──
        if (promptRoot != null && playerTransform != null)
        {
            bool inRange = Vector3.Distance(transform.position, playerTransform.position)
                           <= proximityDistance;

            if (promptRoot.activeSelf != inRange)
                promptRoot.SetActive(inRange);
        }

        // ── Void detection ──
        if (!rb.isKinematic && IsInVoid())
            ResetToDefault();
    }

    private void LateUpdate()
    {
        if (promptRoot == null || !promptRoot.activeSelf || cachedCamera == null)
            return;

        promptRoot.transform.rotation = Quaternion.LookRotation(
            cachedCamera.transform.position - promptRoot.transform.position);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Void"))
        {
            isInVoidTrigger = true;
            if (!rb.isKinematic)
                ResetToDefault();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Void"))
            isInVoidTrigger = false;
    }

    // ═══════════════════════════════════════════════
    // Public API — called by PlayerController
    // ═══════════════════════════════════════════════

    public void OnGrabbed()
    {
        rb.constraints = RigidbodyConstraints.None;
        rb.isKinematic = true;
        rb.useGravity = false;
        isInVoidTrigger = false;
    }

    public void OnReleased()
    {
        rb.position = transform.position;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Ensure the object is not embedded in the ground before enabling
        // physics.  When the player walks onto higher terrain while holding
        // the object, its Y gets pushed below the surface.  Without this
        // correction, the physics engine would eject it upward — the "bounce".
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            float halfHeight = col.bounds.extents.y + 0.05f;
            Vector3 rayOrigin = transform.position + Vector3.up * 2f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 10f))
            {
                float groundY = hit.point.y + halfHeight;
                if (groundY > transform.position.y)
                {
                    Vector3 corrected = rb.position;
                    corrected.y = groundY;
                    rb.position = corrected;
                    transform.position = corrected;
                }
            }
        }

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints = FreezeAllButY;
    }

    // ═══════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════

    private void RefreshCameraReference()
    {
        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
            cachedCamera = FindAnyCamera();
    }

    private static Camera FindAnyCamera()
    {
        Camera cam = Camera.main;
        if (cam != null)
            return cam;

        cam = FindObjectOfType<Camera>();
        if (cam != null)
            return cam;

        // Include inactive cameras. World Space Canvas just needs a Camera
        // reference to project its geometry — it renders fine even when the
        // referenced camera is inactive.
        Camera[] all = Resources.FindObjectsOfTypeAll<Camera>();
        if (all.Length > 0)
            return all[0];

        return null;
    }

    private bool IsInVoid()
    {
        return isInVoidTrigger || transform.position.y < voidYThreshold;
    }

    private void ResetToDefault()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.None;

        Vector3 targetPos = useDefaultResetPosition
            ? defaultResetPosition
            : customResetPosition;

        transform.position = targetPos;
        transform.rotation = defaultResetRotation;
        isInVoidTrigger = false;
    }

    // ═══════════════════════════════════════════════
    // Diagnostics
    // ═══════════════════════════════════════════════

    private void LogDiagnostics()
    {
        if (diagnosticsLogged) return;
        diagnosticsLogged = true;

        if (promptRoot == null)
            Debug.LogWarning($"[PushableObject] '{name}': Prompt Root is not assigned. " +
                             "The proximity text will not appear. Drag your World Space Canvas here.", this);

        if (promptTextComponent == null)
            Debug.LogWarning($"[PushableObject] '{name}': Prompt Text Component is not assigned. " +
                             "Drag the TextMeshProUGUI here.", this);

        if (playerTransform == null)
            Debug.LogWarning($"[PushableObject] '{name}': No GameObject tagged 'Player' found. " +
                             "The proximity check will never trigger.", this);

        if (cachedCamera == null)
            Debug.LogWarning($"[PushableObject] '{name}': No camera found. Billboarding won't work.", this);
    }

    // ═══════════════════════════════════════════════
    // Editor Gizmos
    // ═══════════════════════════════════════════════

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, proximityDistance);

        Gizmos.color = Color.red;
        Vector3 lineCenter = new Vector3(transform.position.x, voidYThreshold, transform.position.z);
        float span = 5f;
        Gizmos.DrawLine(lineCenter + Vector3.left * span, lineCenter + Vector3.right * span);
        Gizmos.DrawLine(lineCenter + Vector3.forward * span, lineCenter + Vector3.back * span);

        if (!useDefaultResetPosition)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(customResetPosition, 0.3f);
        }
    }
}
