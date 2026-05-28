using UnityEngine;

public class Test1 : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float jumpForce = 5f;

    private Rigidbody rb;
    private bool isGrounded;
    private bool jumpPressed;

    public float groundCheckDistance = 1.1f;
    public LayerMask groundMask = ~0; // defaults to "Everything"

    private Vector3 moveInput;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // Prevent tipping over
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        Debug.Log("test sucess");
    }

    void Update()
    {
        // Read input in Update (best practice for input)
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        moveInput = new Vector3(horizontalInput, 0, verticalInput);

        CheckGrounded();

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
            jumpPressed = true;
    }

    void FixedUpdate()
    {
        // Apply movement in FixedUpdate (sync with physics)
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

    void CheckGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        isGrounded = Physics.Raycast(origin, Vector3.down, groundCheckDistance, groundMask);
    }
}
