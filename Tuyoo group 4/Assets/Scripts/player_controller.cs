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

        Vector3 targetVelocity = new Vector3(
            moveInput.x * moveSpeed,
            rb.linearVelocity.y,
            moveInput.z * moveSpeed
        );

        rb.linearVelocity = targetVelocity;

        if (jumpPressed)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
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
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        isGrounded = Physics.Raycast(origin, Vector3.down, groundCheckDistance, groundMask);
    }
}
