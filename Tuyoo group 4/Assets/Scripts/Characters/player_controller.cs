using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("Drag your Cinemachine camera here. Falls back to Camera.main if empty.")]
    public Camera gameCamera;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 5f;
    public float rotationSpeed = 12f;

    [Header("Push/Pull")]
    [Tooltip("How much each unit of mass slows the player. Speed = base * clamp(1 - mass*factor, min, 1)")]
    public float pushMassFactor = 0.15f;
    [Tooltip("Slowest the player can go while pushing, as a fraction of base speed")]
    public float pushMinSpeedMultiplier = 0.2f;
    [Tooltip("Extra multiplier for how much mass slows rotation. Higher = rotation feels heavier.")]
    public float pushRotationSlowdown = 2f;
    public float pushDetectionRadius = 2.5f;
    public float pushPlaceDistance = 0.7f;
    public LayerMask pushableMask = ~0;

    public float groundCheckDistance = 1.1f;
    public LayerMask groundMask = ~0;

    private Rigidbody rb;
    private Transform visualModel;
    private bool isGrounded;
    private bool jumpPressed;

    // Raw input stored in Update, consumed in FixedUpdate
    private float horizontalInput;
    private float verticalInput;

    private bool isPushing;
    private GameObject pushedObject;
    private Rigidbody pushedRb;
    private float holdYOffset;
    private Collider pushedCollider;
    private GameObject nearestPushable;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.centerOfMass = Vector3.zero;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;

        Transform found = GetComponentInChildren<MeshRenderer>()?.transform
                       ?? GetComponentInChildren<SkinnedMeshRenderer>()?.transform;
        visualModel = found != null ? found : transform;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            PhysicsMaterial zeroFriction = new PhysicsMaterial();
            zeroFriction.dynamicFriction = 0f;
            zeroFriction.staticFriction = 0f;
            zeroFriction.frictionCombine = PhysicsMaterialCombine.Minimum;
            col.material = zeroFriction;
        }
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // --- Raw input ---
        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");

        if (Mathf.Abs(horizontalInput) < 0.1f) horizontalInput = 0f;
        if (Mathf.Abs(verticalInput) < 0.1f) verticalInput = 0f;

        // --- Ground check ---
        CheckGrounded();

        // --- Jump (disabled while pushing) ---
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && !isPushing)
            jumpPressed = true;

        // --- Push detection ---
        if (!isPushing)
            DetectPushable();
        else
            nearestPushable = null;

        // --- Grab / Release ---
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (isPushing)
                ReleaseObstacle();
            else if (nearestPushable != null)
                GrabObstacle(nearestPushable);
        }

        if (isPushing && pushedObject == null)
            ReleaseObstacle();
    }

    void FixedUpdate()
    {
        // --- Resolve camera ---
        Camera cam = gameCamera;
        if (cam == null) cam = Camera.main;
        if (cam == null) cam = FindObjectOfType<Camera>();

        // --- Compute camera-relative move direction ---
        Vector3 moveInput = Vector3.zero;

        if (cam != null && (Mathf.Abs(horizontalInput) > 0.001f || Mathf.Abs(verticalInput) > 0.001f))
        {
            Vector3 camForward = cam.transform.forward;
            Vector3 camRight = cam.transform.right;
            camForward.y = 0f;
            camRight.y = 0f;

            if (camForward.sqrMagnitude > 0.0001f && camRight.sqrMagnitude > 0.0001f)
            {
                camForward.Normalize();
                camRight.Normalize();
                moveInput = (camForward * verticalInput + camRight * horizontalInput).normalized;
            }
        }

        // --- Apply movement ---
        if (moveInput.sqrMagnitude > 0.01f)
        {
            float massFactor = 1f;
            if (isPushing && pushedRb != null)
                massFactor = Mathf.Clamp(1f - (pushedRb.mass * pushMassFactor), pushMinSpeedMultiplier, 1f);

            float rotationMassFactor = 1f;
            if (isPushing && pushedRb != null)
                rotationMassFactor = Mathf.Clamp(1f - (pushedRb.mass * pushMassFactor * pushRotationSlowdown), pushMinSpeedMultiplier, 1f);

            // Rotate visual model toward movement direction (camera unaffected — separate child)
            float currentRotationSpeed = rotationSpeed * rotationMassFactor;
            Quaternion targetRotation = Quaternion.LookRotation(moveInput);
            visualModel.rotation = Quaternion.Slerp(visualModel.rotation, targetRotation, currentRotationSpeed * Time.fixedDeltaTime);

            // Move via velocity change
            float currentSpeed = moveSpeed * massFactor;
            Vector3 targetHVel = moveInput * currentSpeed;
            Vector3 currentHVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            Vector3 deltaV = targetHVel - currentHVel;
            rb.AddForce(deltaV, ForceMode.VelocityChange);
        }
        else if (isGrounded)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }

        // --- Jump ---
        if (jumpPressed)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
            jumpPressed = false;
        }

        // --- Push object ---
        if (isPushing && pushedRb != null)
        {
            Vector3 targetPos = transform.position + visualModel.forward * pushPlaceDistance;
            targetPos.y = transform.position.y + holdYOffset;

            if (pushedCollider != null)
            {
                float halfHeight = pushedCollider.bounds.extents.y;
                Vector3 rayOrigin = targetPos + Vector3.up * halfHeight;
                int objectLayer = 1 << pushedObject.layer;
                LayerMask raycastMask = groundMask & ~objectLayer;
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                                    halfHeight + 2f, raycastMask))
                {
                    targetPos.y = Mathf.Max(targetPos.y,
                                            hit.point.y + halfHeight + 0.05f);
                }
            }

            PushableObject pushable = pushedObject.GetComponent<PushableObject>();
            bool hitBoundary = false;
            if (pushable != null)
            {
                Vector3 clampedPos = pushable.ClampToGroundArea(targetPos);
                float xzDelta = Vector2.Distance(
                    new Vector2(targetPos.x, targetPos.z),
                    new Vector2(clampedPos.x, clampedPos.z));
                targetPos = clampedPos;
                hitBoundary = xzDelta > 0.05f;
            }

            pushedRb.MovePosition(targetPos);

            if (hitBoundary)
                ReleaseObstacle();
        }
    }

    void CheckGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * 0.3f;
        isGrounded = Physics.SphereCast(origin, 0.3f, Vector3.down, out _, groundCheckDistance, groundMask);
    }

    void DetectPushable()
    {
        nearestPushable = null;
        float closestDist = float.MaxValue;

        Collider[] hits = Physics.OverlapSphere(transform.position, pushDetectionRadius, pushableMask);

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            Rigidbody hitRb = hit.attachedRigidbody;
            if (hitRb == null) continue;

            PushableObject pushable = hitRb.GetComponent<PushableObject>();
            if (pushable == null) continue;

            Vector3 dirToTarget = (hit.transform.position - transform.position).normalized;
            float dot = Vector3.Dot(visualModel.forward, dirToTarget);
            if (dot < 0.3f) continue;

            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                nearestPushable = hit.gameObject;
            }
        }
    }

    void GrabObstacle(GameObject obj)
    {
        isPushing = true;
        pushedObject = obj;
        pushedRb = obj.GetComponent<Rigidbody>();
        pushedCollider = obj.GetComponent<Collider>();

        holdYOffset = pushedRb.position.y - transform.position.y;

        PushableObject pushable = obj.GetComponent<PushableObject>();
        if (pushable != null)
            pushable.OnGrabbed();
        else if (pushedRb != null)
        {
            pushedRb.isKinematic = true;
            pushedRb.linearVelocity = Vector3.zero;
            pushedRb.angularVelocity = Vector3.zero;
        }

        Collider[] playerCols = GetComponentsInChildren<Collider>();
        Collider[] objCols = obj.GetComponentsInChildren<Collider>();
        foreach (Collider pc in playerCols)
            foreach (Collider oc in objCols)
                Physics.IgnoreCollision(pc, oc, true);
    }

    void ReleaseObstacle()
    {
        if (pushedObject != null)
        {
            PushableObject pushable = pushedObject.GetComponent<PushableObject>();
            if (pushable != null)
                pushable.OnReleased();

            Collider[] playerCols = GetComponentsInChildren<Collider>();
            Collider[] objCols = pushedObject.GetComponentsInChildren<Collider>();
            foreach (Collider pc in playerCols)
                foreach (Collider oc in objCols)
                    Physics.IgnoreCollision(pc, oc, false);
        }

        isPushing = false;
        pushedObject = null;
        pushedRb = null;
        pushedCollider = null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = isPushing ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pushDetectionRadius);

        if (nearestPushable != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(nearestPushable.transform.position, nearestPushable.GetComponent<Collider>().bounds.size);
            Gizmos.DrawLine(transform.position, nearestPushable.transform.position);
        }
    }
}
