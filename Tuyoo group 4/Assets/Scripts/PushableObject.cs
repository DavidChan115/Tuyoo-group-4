using UnityEngine;
using TMPro;
using System.Collections;

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

    [Tooltip("How many seconds the object falls before being reset.")]
    [SerializeField] private float resetDelay = 2f;

    // ═══════════════════════════════════════════════
    // Private State
    // ═══════════════════════════════════════════════
    private Rigidbody rb;
    private Transform playerTransform;
    private Camera cachedCamera;

    // void
    private Vector3 defaultResetPosition;
    private Quaternion defaultResetRotation;
    private bool isInVoidTrigger;
    private bool isResetting;
    private Coroutine resetCoroutine;

    // ═══════════════════════════════════════════════
    // Unity Messages
    // ═══════════════════════════════════════════════

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;

        defaultResetPosition = transform.position;
        defaultResetRotation = transform.rotation;

        // Cache player.
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            playerTransform = player.transform;

        // ★ DEFAULT TEXT SET ON THE LINE BELOW (line 82).
        //    This overrides whatever is on the pre-made text object at runtime.
        if (promptTextComponent != null)
            promptTextComponent.text = "Press F to move the object";

        // Ensure prompt starts hidden.
        if (promptRoot != null)
            promptRoot.SetActive(false);
    }

    private void Update()
    {
        RefreshCameraReference();

        // ── Proximity prompt toggle ──
        if (promptRoot != null && playerTransform != null)
        {
            bool inRange = Vector3.Distance(transform.position, playerTransform.position)
                           <= proximityDistance;

            if (promptRoot.activeSelf != inRange)
                promptRoot.SetActive(inRange);
        }

        // ── Void fall detection (runs while object is non-kinematic / falling) ──
        if (!isResetting && !rb.isKinematic && IsInVoid())
        {
            resetCoroutine = StartCoroutine(VoidResetRoutine());
        }
    }

    private void LateUpdate()
    {
        // ── Billboard the prompt root toward the active camera ──
        if (promptRoot == null || !promptRoot.activeSelf || cachedCamera == null)
            return;

        // Canvas renders on its XY plane, visible from its -Z side.
        // Make +Z point away from camera so -Z faces it.
        promptRoot.transform.rotation = Quaternion.LookRotation(
            cachedCamera.transform.position - promptRoot.transform.position);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Void"))
        {
            isInVoidTrigger = true;
            // If already falling and in the void, start the reset timer.
            if (!isResetting && !rb.isKinematic)
                resetCoroutine = StartCoroutine(VoidResetRoutine());
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

    /// <summary>Called by PlayerController when the player grabs this object.</summary>
    public void OnGrabbed()
    {
        // Cancel any in-progress void reset.
        if (resetCoroutine != null)
        {
            StopCoroutine(resetCoroutine);
            resetCoroutine = null;
        }
        isResetting = false;
        isInVoidTrigger = false;

        rb.isKinematic = true;
        rb.useGravity = false;
    }

    /// <summary>Called by PlayerController when the player releases this object.</summary>
    public void OnReleased()
    {
        // Always let the object fall when released — gravity handles ground,
        // the void-detection loop in Update() handles the void.
        rb.isKinematic = false;
        rb.useGravity = true;
    }

    // ═══════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════

    private void RefreshCameraReference()
    {
        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
            cachedCamera = Camera.main;
    }

    private bool IsInVoid()
    {
        return isInVoidTrigger || transform.position.y < voidYThreshold;
    }

    // ═══════════════════════════════════════════════
    // Void Reset Coroutine
    // ═══════════════════════════════════════════════

    private IEnumerator VoidResetRoutine()
    {
        isResetting = true;

        // Let the object continue falling for the configured delay.
        yield return new WaitForSeconds(resetDelay);

        // Kill all physics movement, then teleport.
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;

        Vector3 targetPos = useDefaultResetPosition
            ? defaultResetPosition
            : customResetPosition;

        transform.position = targetPos;
        transform.rotation = defaultResetRotation;

        isResetting = false;
        isInVoidTrigger = false;
        resetCoroutine = null;
    }

    // ═══════════════════════════════════════════════
    // Editor Gizmos
    // ═══════════════════════════════════════════════

    private void OnDrawGizmosSelected()
    {
        // Proximity ring.
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, proximityDistance);

        // Void Y-threshold line.
        Gizmos.color = Color.red;
        Vector3 lineCenter = new Vector3(transform.position.x, voidYThreshold, transform.position.z);
        float span = 5f;
        Gizmos.DrawLine(lineCenter + Vector3.left * span, lineCenter + Vector3.right * span);
        Gizmos.DrawLine(lineCenter + Vector3.forward * span, lineCenter + Vector3.back * span);

        // Custom reset position.
        if (!useDefaultResetPosition)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(customResetPosition, 0.3f);
        }
    }
}
