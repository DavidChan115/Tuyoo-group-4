using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 5f;
    public float rotationSpeed = 12f;

    [Header("Push/Pull")]
    [Tooltip("How much each unit of mass slows the player. Speed = base * clamp(1 - mass*factor, min, 1)")]
    public float pushMassFactor = 0.15f;
    [Tooltip("Slowest the player can go while pushing, as a fraction of base speed")]
    public float pushMinSpeedMultiplier = 0.2f;
    public float pushDetectionRadius = 2.5f;
    public float pushPlaceDistance = 0.7f;
    public LayerMask pushableMask = ~0;

    private Rigidbody rb;
    private bool isGrounded;
    private bool jumpPressed;
    private Transform visualModel;

    public float groundCheckDistance = 1.1f;
    public LayerMask groundMask = ~0;

    private Vector3 moveInput;
    private Camera cachedCamera;

    private bool isPushing;
    private GameObject pushedObject;
    private Rigidbody pushedRb;
    private GameObject nearestPushable;
    private int debugFrameCounter;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        Transform found = GetComponentInChildren<MeshRenderer>()?.transform
                       ?? GetComponentInChildren<SkinnedMeshRenderer>()?.transform;
        visualModel = found != null ? found : transform;
        rb.centerOfMass = Vector3.zero;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            PhysicsMaterial zeroFriction = new PhysicsMaterial();
            zeroFriction.dynamicFriction = 0f;
            zeroFriction.staticFriction = 0f;
            zeroFriction.frictionCombine = PhysicsMaterialCombine.Minimum;
            col.material = zeroFriction;
        }

        cachedCamera = FindObjectOfType<Camera>();

        if (cachedCamera == null)
            Debug.LogError("[PlayerController] No camera found in scene. Camera-relative movement will not work.");

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("test sucess");
    }

    void Update()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        if (Mathf.Abs(horizontalInput) < 0.1f) horizontalInput = 0f;
        if (Mathf.Abs(verticalInput) < 0.1f) verticalInput = 0f;

        if (cachedCamera != null)
        {
            Vector3 camForward = cachedCamera.transform.forward;
            Vector3 camRight = cachedCamera.transform.right;
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
        if (moveInput.sqrMagnitude > 0.01f)
        {
            float massFactor = 1f;
            if (isPushing && pushedRb != null)
                massFactor = Mathf.Clamp(1f - (pushedRb.mass * pushMassFactor), pushMinSpeedMultiplier, 1f);

            float currentRotationSpeed = rotationSpeed * massFactor;
            Quaternion targetRotation = Quaternion.LookRotation(new Vector3(moveInput.x, 0f, moveInput.z));
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, currentRotationSpeed * Time.fixedDeltaTime));

            float currentSpeed = moveSpeed * massFactor;
            Vector3 targetHVel = new Vector3(moveInput.x, 0f, moveInput.z) * currentSpeed;
            Vector3 currentHVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            Vector3 deltaV = targetHVel - currentHVel;

            rb.AddForce(deltaV, ForceMode.VelocityChange);
        }
        else if (isGrounded)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }

        if (jumpPressed)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
            jumpPressed = false;
        }

        if (isPushing && pushedRb != null)
        {
            Vector3 targetPos = transform.position + visualModel.forward * pushPlaceDistance;
            targetPos.y = pushedRb.position.y;
            pushedRb.MovePosition(targetPos);
        }

        debugFrameCounter++;
        if (debugFrameCounter % 60 == 0 || rb.angularVelocity.sqrMagnitude > 0.001f)
        {
            Debug.Log("[RotationDebug] frame=" + debugFrameCounter
                + " moveInput.magnitude=" + moveInput.magnitude.ToString("F4")
                + " angularVelocity=" + rb.angularVelocity.ToString("F4")
                + " |angularVelocity|=" + rb.angularVelocity.magnitude.ToString("F4")
                + " rotation.yaw=" + rb.rotation.eulerAngles.y.ToString("F2")
                + " isGrounded=" + isGrounded
                + " constraints=" + rb.constraints);
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

        PushableObject pushable = obj.GetComponent<PushableObject>();
        if (pushable != null)
        {
            pushable.OnGrabbed();  // handles isKinematic + cancels void reset
        }
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
            Collider[] playerCols = GetComponentsInChildren<Collider>();
            Collider[] objCols = pushedObject.GetComponentsInChildren<Collider>();
            foreach (Collider pc in playerCols)
                foreach (Collider oc in objCols)
                    Physics.IgnoreCollision(pc, oc, false);

            // Let the PushableObject decide whether to stay put or fall.
            PushableObject pushable = pushedObject.GetComponent<PushableObject>();
            if (pushable != null)
            {
                pushable.OnReleased();
            }
            // else if (pushedRb != null)
            //{
                // pushedRb.isKinematic = true;
                // pushedRb.linearVelocity = Vector3.zero;
                // pushedRb.angularVelocity = Vector3.zero;
            // }
        }

        isPushing = false;
        pushedObject = null;
        pushedRb = null;
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
