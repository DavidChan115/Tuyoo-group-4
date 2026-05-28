using UnityEngine;

public class Test1 : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 5f;

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

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
            jumpPressed = true;
    }

    void FixedUpdate()
    {
        // Rotate player body
        rb.MoveRotation(Quaternion.Euler(0f, yaw, 0f));

        // Use AddForce instead of directly setting velocity —
        // lets the physics engine handle collision response naturally
        Vector3 targetHVel = new Vector3(moveInput.x, 0f, moveInput.z) * moveSpeed;
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
        // SphereCast from center downward — the radius prevents missing surfaces
        // due to thin gaps or floating-point imprecision
        Vector3 origin = transform.position + Vector3.up * 0.3f;
        isGrounded = Physics.SphereCast(origin, 0.3f, Vector3.down, out _, groundCheckDistance, groundMask);
    }
}
