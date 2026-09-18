using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 7.0f;
    [SerializeField] private float rotationSpeed = 12.0f;
    [SerializeField] private float airControlMultiplier = 0.85f;

    [Header("Auto-Jump Settings")]
    [SerializeField] private float jumpForce = 8.5f;
    [SerializeField] private float forwardJumpBoost = 2.5f;
    [SerializeField] private float gapCheckDistance = 1.1f;
    [SerializeField] private float groundCheckDistance = 0.4f;
    [SerializeField] private float jumpCooldown = 0.35f;

    [Header("Dependencies")]
    [SerializeField] private VirtualJoystick joystick;
    [SerializeField] private CharacterSquashAndStretch squashAndStretch;

    private Rigidbody rb;
    private bool isGrounded = false;
    private float lastJumpTime = -1f;
    private bool isEliminated = false;
    private bool wasGroundedLastFrame = false;

    public bool IsGrounded => isGrounded;
    public bool IsEliminated => isEliminated;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        if (squashAndStretch == null)
        {
            squashAndStretch = GetComponent<CharacterSquashAndStretch>();
        }
    }

    public void Initialize(VirtualJoystick joystickRef)
    {
        joystick = joystickRef;
        isEliminated = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void Update()
    {
        if (isEliminated) return;

        CheckGrounded();
        
        Vector3 moveInput = GetMoveInput();
        if (moveInput.sqrMagnitude > 0.02f)
        {
            CheckAutoJump(moveInput);
        }

        // Fall elimination check
        if (transform.position.y < -5.0f && !isEliminated)
        {
            Eliminate();
        }
    }

    private void FixedUpdate()
    {
        if (isEliminated) return;

        Vector3 moveInput = GetMoveInput();
        Vector3 moveDir = new Vector3(moveInput.x, 0, moveInput.z).normalized;

        if (moveDir.sqrMagnitude > 0.02f)
        {
            // Rotation facing movement
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);

            // Movement force/velocity
            float currentSpeed = moveSpeed * (isGrounded ? 1.0f : airControlMultiplier);
            Vector3 targetVelocity = moveDir * currentSpeed;
            targetVelocity.y = rb.linearVelocity.y;
            rb.linearVelocity = targetVelocity;
        }
        else if (isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x * 0.8f, rb.linearVelocity.y, rb.linearVelocity.z * 0.8f);
        }
    }

    private Vector3 GetMoveInput()
    {
        float h = 0f;
        float v = 0f;

        if (joystick != null)
        {
            h = joystick.Horizontal;
            v = joystick.Vertical;
        }

        if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f)
        {
            h = Input.GetAxisRaw("Horizontal");
            v = Input.GetAxisRaw("Vertical");
        }

        Vector3 input = new Vector3(h, 0, v);
        return Vector3.ClampMagnitude(input, 1.0f);
    }

    private void CheckGrounded()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.15f;
        isGrounded = Physics.Raycast(rayStart, Vector3.down, groundCheckDistance);

        if (isGrounded && !wasGroundedLastFrame && Time.time - lastJumpTime > 0.2f)
        {
            if (squashAndStretch != null) squashAndStretch.TriggerLandSquash();
        }

        wasGroundedLastFrame = isGrounded;
    }

    private void CheckAutoJump(Vector3 moveInput)
    {
        if (!isGrounded || Time.time - lastJumpTime < jumpCooldown) return;

        Vector3 moveDir = moveInput.normalized;
        Vector3 probeOrigin = transform.position + Vector3.up * 0.3f + moveDir * gapCheckDistance;

        bool hasGroundAhead = Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit hit, 1.5f);

        bool isGroundFalling = false;
        if (hasGroundAhead && hit.collider != null)
        {
            PlatformTile tile = hit.collider.GetComponent<PlatformTile>();
            if (tile != null && (!tile.IsAvailable || tile.IsFalling))
            {
                isGroundFalling = true;
            }
        }

        // Auto Jump condition: No ground ahead or ground ahead is falling/destroyed
        if (!hasGroundAhead || isGroundFalling)
        {
            PerformJump(moveDir);
        }
    }

    private void PerformJump(Vector3 moveDir)
    {
        lastJumpTime = Time.time;
        isGrounded = false;

        Vector3 jumpVel = rb.linearVelocity;
        jumpVel.y = jumpForce;
        jumpVel += moveDir * forwardJumpBoost;
        rb.linearVelocity = jumpVel;

        if (squashAndStretch != null)
        {
            squashAndStretch.TriggerJumpSquash();
        }
    }

    private void Eliminate()
    {
        isEliminated = true;
        gameObject.SetActive(false);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerEliminated(gameObject);
        }
    }
}
