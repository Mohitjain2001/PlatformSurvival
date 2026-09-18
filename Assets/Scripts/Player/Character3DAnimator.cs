using UnityEngine;

/// <summary>
/// Character 3D Animator Bridge for Minimo 3D Character (Character-01).
/// Drives the Animator Controller (Speed, Jump, Fall, Land) based on physics velocity and ground state.
/// </summary>
public class Character3DAnimator : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody rb;

    [Header("Tuning")]
    [SerializeField] private float maxSpeed = 5.2f;

    private bool isGrounded = true;
    private bool isEliminated = false;
    private float landTimer = 0f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int FallHash = Animator.StringToHash("Fall");
    private static readonly int LandHash = Animator.StringToHash("Land");

    public Animator RuntimeAnimator => animator;

    public void SetupAnimator(Animator anim, Rigidbody body)
    {
        animator = anim;
        rb = body;
    }

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    public void SetGrounded(bool grounded)
    {
        if (!isGrounded && grounded)
        {
            TriggerLand();
        }
        isGrounded = grounded;
    }

    public void SetEliminated(bool eliminated)
    {
        isEliminated = eliminated;
        if (animator != null && isEliminated)
        {
            animator.SetBool(FallHash, true);
            animator.SetBool(JumpHash, false);
            animator.SetFloat(SpeedHash, 0f);
        }
    }

    public void TriggerJump()
    {
        if (animator != null)
        {
            animator.SetBool(JumpHash, true);
            animator.SetBool(FallHash, false);
            animator.SetBool(LandHash, false);
        }
    }

    public void TriggerLand()
    {
        if (animator != null)
        {
            animator.SetBool(LandHash, true);
            animator.SetBool(JumpHash, false);
            animator.SetBool(FallHash, false);
            landTimer = 0.22f;
        }
    }

    private void Update()
    {
        if (animator == null) return;

        if (landTimer > 0f)
        {
            landTimer -= Time.deltaTime;
            if (landTimer <= 0f)
            {
                animator.SetBool(LandHash, false);
            }
        }

        if (isEliminated)
        {
            animator.SetBool(FallHash, true);
            return;
        }

        if (rb != null)
        {
            Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            float currentSpeed = horizontalVel.magnitude;

            // Speed parameter in BlendTree: 0 = Idle, 0.5 = Walk, 1.0 = Run
            float normalizedSpeed = 0f;
            if (currentSpeed > 0.12f)
            {
                normalizedSpeed = Mathf.Clamp01(currentSpeed / maxSpeed);
            }

            animator.SetFloat(SpeedHash, isGrounded ? normalizedSpeed : 0f);

            // In-air falling state
            if (!isGrounded)
            {
                if (rb.linearVelocity.y < -0.8f)
                {
                    animator.SetBool(FallHash, true);
                    animator.SetBool(JumpHash, false);
                }
            }
            else
            {
                animator.SetBool(FallHash, false);
            }
        }
    }
}
