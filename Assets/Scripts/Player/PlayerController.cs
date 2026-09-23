using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5.2f;
    [SerializeField] private float acceleration = 14.0f;
    [SerializeField] private float deceleration = 16.0f;
    [SerializeField] private float rotationSpeed = 16.0f;
    [SerializeField] private float airControlMultiplier = 0.75f;

    [Header("Auto-Jump Settings")]
    [SerializeField] private float jumpForce = 6.4f;
    [SerializeField] private float forwardJumpBoost = 1.3f;
    [SerializeField] private float gapCheckDistance = 1.25f;
    [SerializeField] private float groundCheckDistance = 0.35f;
    [SerializeField] private float jumpCooldown = 0.45f;
    [SerializeField] private float minGroundedDuration = 0.22f;

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

    private bool hasPlayedFallSound = false;
    private float lastGroundedY = 0f;

    private void Update()
    {
        if (isEliminated) return;

        CheckGrounded();

        if (isGrounded)
        {
            hasPlayedFallSound = false;
            lastGroundedY = transform.position.y;
        }
        else if (!hasPlayedFallSound && !isGrounded)
        {
            // Fall SFX only plays when plunging into a lower floor gap / void (falling > 2.2m below last ground)
            bool isPlungingDown = transform.position.y < (lastGroundedY - 2.2f) && (rb != null && rb.linearVelocity.y < -1.5f);
            bool isNearElimination = transform.position.y < (eliminationYThreshold + 6.0f);

            if (isPlungingDown || isNearElimination)
            {
                hasPlayedFallSound = true;
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayFall();
                }
            }
        }
        
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
        Vector3 currentHorizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

        if (moveDir.sqrMagnitude > 0.02f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);

            float currentTargetSpeed = moveSpeed * (isGrounded ? 1.0f : airControlMultiplier);
            Vector3 targetHorizontalVel = moveDir * currentTargetSpeed;
            Vector3 smoothedVel = Vector3.Lerp(currentHorizontalVel, targetHorizontalVel, acceleration * Time.fixedDeltaTime);

            rb.linearVelocity = new Vector3(smoothedVel.x, rb.linearVelocity.y, smoothedVel.z);
        }
        else if (isGrounded)
        {
            Vector3 smoothedStopVel = Vector3.Lerp(currentHorizontalVel, Vector3.zero, deceleration * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector3(smoothedStopVel.x, rb.linearVelocity.y, smoothedStopVel.z);
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
                if (characterAnimator != null) characterAnimator.TriggerLand();
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

        if (characterAnimator != null)
        {
            characterAnimator.TriggerJump();
        }

        if (squashAndStretch != null)
        {
            squashAndStretch.TriggerJumpSquash();
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayJump();
        }
    }

    private void Eliminate()
    {
        isEliminated = true;

        if (characterAnimator != null)
        {
            characterAnimator.SetEliminated(true);
        }
        gameObject.SetActive(false);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerEliminated(gameObject);
        }
    }
}
