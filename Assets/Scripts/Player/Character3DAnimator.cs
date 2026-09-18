using UnityEngine;

/// <summary>
/// Procedural 3D Character Runner Rig and Animator.
/// Creates a cute, lively Fall Guys / 3D Runner style character with:
/// - Rounded Head with animated visor/eyes
/// - Torso / Body
/// - Left and Right Arms that swing naturally
/// - Left and Right Legs that step and run
/// - Full State Animation: Idle breathing, Running cycle, In-Air jumping, and Elimination fall!
/// </summary>
public class Character3DAnimator : MonoBehaviour
{
    [Header("Bone References")]
    [SerializeField] private Transform torso;
    [SerializeField] private Transform head;
    [SerializeField] private Transform leftArm;
    [SerializeField] private Transform rightArm;
    [SerializeField] private Transform leftLeg;
    [SerializeField] private Transform rightLeg;

    [Header("Movement & State References")]
    [SerializeField] private Rigidbody rb;

    private float runCycleTime = 0f;
    private float idleCycleTime = 0f;
    private bool isGrounded = true;
    private bool isEliminated = false;

    private Vector3 initialTorsoPos;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (torso != null) initialTorsoPos = torso.localPosition;
    }

    public void SetEliminated(bool eliminated)
    {
        isEliminated = eliminated;
    }

    public void SetGrounded(bool grounded)
    {
        isGrounded = grounded;
    }

    private void Update()
    {
        if (rb == null) return;

        Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        float speed = horizontalVel.magnitude;
        bool isMoving = speed > 0.4f;

        if (isEliminated)
        {
            AnimateEliminated();
            return;
        }

        if (!isGrounded)
        {
            AnimateInAir();
            return;
        }

        if (isMoving)
        {
            AnimateRunning(speed);
        }
        else
        {
            AnimateIdle();
        }
    }

    private void AnimateRunning(float speed)
    {
        float runSpeedMultiplier = Mathf.Clamp(speed * 2.2f, 8f, 18f);
        runCycleTime += Time.deltaTime * runSpeedMultiplier;

        float sin = Mathf.Sin(runCycleTime);
        float cos = Mathf.Cos(runCycleTime);

        // Torso bobs up and down while sprinting
        if (torso != null)
        {
            torso.localPosition = initialTorsoPos + new Vector3(0, Mathf.Abs(sin) * 0.08f, 0);
            torso.localRotation = Quaternion.Euler(sin * 3f, 0, cos * 4f);
        }

        // Legs run cycle (Opposite alternation)
        float legAngle = sin * 42f;
        if (leftLeg != null)
        {
            leftLeg.localRotation = Quaternion.Euler(legAngle, 0, 0);
        }
        if (rightLeg != null)
        {
            rightLeg.localRotation = Quaternion.Euler(-legAngle, 0, 0);
        }

        // Arms swing opposite to legs (Natural running gait)
        float armAngle = -sin * 48f;
        if (leftArm != null)
        {
            leftArm.localRotation = Quaternion.Euler(armAngle, 0, 15f + Mathf.Abs(cos) * 8f);
        }
        if (rightArm != null)
        {
            rightArm.localRotation = Quaternion.Euler(-armAngle, 0, -15f - Mathf.Abs(cos) * 8f);
        }

        // Head bounce
        if (head != null)
        {
            head.localRotation = Quaternion.Euler(-Mathf.Abs(sin) * 5f, 0, 0);
        }
    }

    private void AnimateIdle()
    {
        idleCycleTime += Time.deltaTime * 3f;
        float sin = Mathf.Sin(idleCycleTime);

        // Gentle breathing bob
        if (torso != null)
        {
            torso.localPosition = Vector3.Lerp(torso.localPosition, initialTorsoPos + new Vector3(0, sin * 0.025f, 0), Time.deltaTime * 10f);
            torso.localRotation = Quaternion.Slerp(torso.localRotation, Quaternion.identity, Time.deltaTime * 8f);
        }

        // Gentle resting limbs
        if (leftLeg != null) leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, Quaternion.identity, Time.deltaTime * 10f);
        if (rightLeg != null) rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, Quaternion.identity, Time.deltaTime * 10f);

        if (leftArm != null) leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, Quaternion.Euler(0, 0, 12f + sin * 3f), Time.deltaTime * 10f);
        if (rightArm != null) rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, Quaternion.Euler(0, 0, -12f - sin * 3f), Time.deltaTime * 10f);

        if (head != null) head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(sin * 2f, 0, 0), Time.deltaTime * 8f);
    }

    private void AnimateInAir()
    {
        // Jump pose: Arms thrown up high, legs tucked
        float t = Time.deltaTime * 12f;

        if (leftArm != null) leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, Quaternion.Euler(-135f, 0, 25f), t);
        if (rightArm != null) rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, Quaternion.Euler(-135f, 0, -25f), t);

        if (leftLeg != null) leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, Quaternion.Euler(-25f, 0, 0), t);
        if (rightLeg != null) rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, Quaternion.Euler(20f, 0, 0), t);

        if (head != null) head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(-15f, 0, 0), t);
    }

    private void AnimateEliminated()
    {
        // Flailing panic animation while tumbling into void
        float tumble = Time.time * 15f;
        if (leftArm != null) leftArm.localRotation = Quaternion.Euler(Mathf.Sin(tumble) * 70f, 0, 45f);
        if (rightArm != null) rightArm.localRotation = Quaternion.Euler(Mathf.Cos(tumble) * 70f, 0, -45f);
        if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(Mathf.Cos(tumble * 1.2f) * 50f, 0, 0);
        if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(Mathf.Sin(tumble * 1.2f) * 50f, 0, 0);
    }
}
