using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 7.5f;
    [SerializeField] private float rotationSpeed = 14.0f;
    [SerializeField] private float airControlMultiplier = 0.85f;

    [Header("Auto-Jump Settings")]
    [SerializeField] private float jumpForce = 8.0f;
    [SerializeField] private float forwardJumpBoost = 2.2f;
    [SerializeField] private float gapCheckDistance = 1.35f;
    [SerializeField] private float groundCheckDistance = 0.35f;
    [SerializeField] private float jumpCooldown = 0.45f;
    [SerializeField] private float minGroundedDuration = 0.25f;

    [Header("Dependencies")]
    [SerializeField] private VirtualJoystick joystick;
    [SerializeField] private CharacterSquashAndStretch squashAndStretch;
    [SerializeField] private Character3DAnimator characterAnimator;

    private Rigidbody rb;
    private bool isGrounded = false;
    private float groundedDuration = 0f;
    private float lastJumpTime = -1f;
    private bool isEliminated = false;
    private bool wasGroundedLastFrame = false;
    private float eliminationYThreshold = -15.0f;

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
        if (characterAnimator == null)
        {
            characterAnimator = GetComponent<Character3DAnimator>();
        }
    }

    public void Initialize(VirtualJoystick joystickRef, float eliminationY = -15.0f)
    {
        joystick = joystickRef;
        eliminationYThreshold = eliminationY;
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

        // Multi-layer bottom elimination check
        if (transform.position.y < eliminationYThreshold && !isEliminated)
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
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);

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
        Vector3 checkOrigin = transform.position + Vector3.up * 0.25f;
        float radius = 0.25f;
        RaycastHit[] hits = Physics.SphereCastAll(checkOrigin, radius, Vector3.down, groundCheckDistance);

        bool foundGround = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col != null && !col.isTrigger && col.transform != transform && !col.transform.IsChildOf(transform))
            {
                foundGround = true;
                break;
            }
        }

        isGrounded = foundGround;

        if (isGrounded)
        {
            groundedDuration += Time.deltaTime;
            if (!wasGroundedLastFrame && Time.time - lastJumpTime > 0.2f)
            {
                if (squashAndStretch != null) squashAndStretch.TriggerLandSquash();
            }
        }
        else
        {
            groundedDuration = 0f;
        }

        wasGroundedLastFrame = isGrounded;
        if (characterAnimator != null) characterAnimator.SetGrounded(isGrounded);
    }

    private void CheckAutoJump(Vector3 moveInput)
    {
        // Require character to be grounded, grounded long enough, and past jump cooldown
        if (!isGrounded || groundedDuration < minGroundedDuration || Time.time - lastJumpTime < jumpCooldown) 
            return;

        // Require substantial movement input
        if (moveInput.sqrMagnitude < 0.15f) 
            return;

        Vector3 moveDir = moveInput.normalized;
        Vector3 probeOrigin = transform.position + Vector3.up * 0.35f + moveDir * gapCheckDistance;

        // Use SphereCast ahead to smoothly span hex seams without false triggers
        RaycastHit[] aheadHits = Physics.SphereCastAll(probeOrigin, 0.3f, Vector3.down, 1.8f);

        bool hasGroundAhead = false;
        bool isGroundFalling = false;

        for (int i = 0; i < aheadHits.Length; i++)
        {
            Collider col = aheadHits[i].collider;
            if (col != null && !col.isTrigger && col.transform != transform && !col.transform.IsChildOf(transform))
            {
                hasGroundAhead = true;
                PlatformTile tile = col.GetComponent<PlatformTile>();
                if (tile != null && (!tile.IsAvailable || tile.IsFalling))
                {
                    isGroundFalling = true;
                }
                break;
            }
        }

        // Auto Jump: only leap across genuine gaps or if the tile ahead is falling
        if (!hasGroundAhead || isGroundFalling)
        {
            PerformJump(moveDir);
        }
    }

    private void PerformJump(Vector3 moveDir)
    {
        lastJumpTime = Time.time;
        isGrounded = false;
        groundedDuration = 0f;

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
