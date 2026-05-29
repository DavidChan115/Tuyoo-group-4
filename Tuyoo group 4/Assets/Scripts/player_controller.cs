using UnityEngine;

public class Test1 : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 5f;

    [Header("Push/Pull")]
    [Tooltip("How much each unit of mass slows the player. Speed = base * clamp(1 - mass*factor, min, 1)")]
    public float pushMassFactor = 0.15f;
    [Tooltip("Slowest the player can go while pushing, as a fraction of base speed")]
    public float pushMinSpeedMultiplier = 0.2f;
    public float pushDetectionRadius = 2.5f;
    public float pushPlaceDistance = 1.5f;
    public LayerMask pushableMask = ~0;

    [Header("Camera")]
    public Transform playerCamera;
    public float mouseSensitivity = 2f;

    private Rigidbody rb;
    private bool isGrounded;
    private bool jumpPressed;
    private float yaw;
    private float horizontalDist;
    private float cameraHeight;

    public float groundCheckDistance = 1.1f;
    public LayerMask groundMask = ~0;

    private Vector3 moveInput;

    private bool isPushing;
    private GameObject pushedObject;
    private FixedJoint pushJoint;
    private GameObject nearestPushable;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Zero-friction material so player slides along walls instead of sticking
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            PhysicsMaterial zeroFriction = new PhysicsMaterial();
            zeroFriction.dynamicFriction = 0f;
            zeroFriction.staticFriction = 0f;
            zeroFriction.frictionCombine = PhysicsMaterialCombine.Minimum;
            col.material = zeroFriction;
        }

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>()?.transform;

        // Read initial camera offset relative to player
        Vector3 offset = playerCamera.position - transform.position;
        cameraHeight = offset.y;
        horizontalDist = new Vector2(offset.x, offset.z).magnitude;

        // Set player facing direction to match camera's horizontal position
        yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("test sucess");
    }

    void Update()
    {
        // --- Mouse rotates player body ---
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        yaw += mouseX;

        // --- Camera-relative movement ---
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        if (playerCamera != null)
        {
            Vector3 camForward = playerCamera.forward;
            Vector3 camRight = playerCamera.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            moveInput = (camForward * verticalInput + camRight * horizontalInput).normalized;
        }
        else
        {
            moveInput = new Vector3(horizontalInput, 0f, verticalInput).normalized;
        }

        CheckGrounded();

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && !isPushing)
            jumpPressed = true;

        if (!isPushing)
            DetectPushable();
        else
            nearestPushable = null;

        if (Input.GetKeyDown(KeyCode.F))
        {
            if (isPushing)
            {
                Debug.Log("[Push] Releasing obstacle");
                ReleaseObstacle();
            }
            else if (nearestPushable != null)
            {
                Debug.Log("[Push] Grabbing: " + nearestPushable.name);
                GrabObstacle(nearestPushable);
            }
            else
            {
                Debug.Log("[Push] F pressed but no pushable nearby. nearestPushable is null.");
            }
        }

        if (isPushing && pushedObject == null)
            ReleaseObstacle();
    }

    void FixedUpdate()
    {
        // Rotate player body
        rb.MoveRotation(Quaternion.Euler(0f, yaw, 0f));

        // Use AddForce instead of directly setting velocity —
        // lets the physics engine handle collision response naturally
        float currentSpeed = moveSpeed;
        if (isPushing && pushedObject != null)
        {
            Rigidbody obstacleRb = pushedObject.GetComponent<Rigidbody>();
            float mass = obstacleRb != null ? obstacleRb.mass : 1f;
            float factor = Mathf.Clamp(1f - (mass * pushMassFactor), pushMinSpeedMultiplier, 1f);
            currentSpeed = moveSpeed * factor;
        }
        Vector3 targetHVel = new Vector3(moveInput.x, 0f, moveInput.z) * currentSpeed;
        Vector3 currentHVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 deltaV = targetHVel - currentHVel;

        rb.AddForce(deltaV, ForceMode.VelocityChange);

        if (jumpPressed)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
            jumpPressed = false;
        }
    }

    void LateUpdate()
    {
        // Camera always behind the player, looking at them
        if (playerCamera != null)
        {
            Vector3 behind = transform.position - transform.forward * horizontalDist + Vector3.up * cameraHeight;
            playerCamera.position = behind;
            playerCamera.LookAt(transform.position);
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
            if (hitRb == null)
            {
                Debug.Log("[Push] Skipping " + hit.name + " — no Rigidbody");
                continue;
            }

            PushableObject pushable = hitRb.GetComponent<PushableObject>();
            if (pushable == null)
            {
                Debug.Log("[Push] Skipping " + hit.name + " — no PushableObject component");
                continue;
            }

            Vector3 dirToTarget = (hit.transform.position - transform.position).normalized;
            float dot = Vector3.Dot(transform.forward, dirToTarget);
            if (dot < 0.3f)
            {
                Debug.Log("[Push] Skipping " + hit.name + " — behind player (dot=" + dot.ToString("F2") + ")");
                continue;
            }

            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                nearestPushable = hit.gameObject;
                Debug.Log("[Push] Found pushable: " + hit.name + " at distance " + dist.ToString("F2"));
            }
        }
    }

    void GrabObstacle(GameObject obj)
    {
        isPushing = true;
        pushedObject = obj;

        Vector3 targetPos = transform.position + transform.forward * pushPlaceDistance;
        targetPos.y = obj.transform.position.y;
        obj.transform.position = targetPos;

        Rigidbody obstacleRb = obj.GetComponent<Rigidbody>();
        if (obstacleRb != null)
        {
            obstacleRb.isKinematic = false;
            obstacleRb.linearVelocity = Vector3.zero;
        }

        pushJoint = gameObject.AddComponent<FixedJoint>();
        pushJoint.connectedBody = obstacleRb;
        pushJoint.breakForce = Mathf.Infinity;
        pushJoint.breakTorque = Mathf.Infinity;
    }

    void ReleaseObstacle()
    {
        if (pushedObject != null)
        {
            Rigidbody obstacleRb = pushedObject.GetComponent<Rigidbody>();
            if (obstacleRb != null)
            {
                obstacleRb.isKinematic = true;
                obstacleRb.linearVelocity = Vector3.zero;
            }
        }

        isPushing = false;
        pushedObject = null;

        if (pushJoint != null)
        {
            Destroy(pushJoint);
            pushJoint = null;
        }
    }

    void OnDestroy()
    {
        if (pushJoint != null)
            Destroy(pushJoint);
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
