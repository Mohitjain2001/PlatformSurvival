using UnityEngine;

/// <summary>
/// Controls procedural animation (Running limb swings, Idle breathing, Jump poses, and Fall flailing)
/// for 3D runner characters (both Player and AI Bots) without requiring external animator clips.
/// </summary>
public class CharacterProceduralAnimator : MonoBehaviour
{
    [Header("Limb Transforms")]
    [SerializeField] private Transform bodyRoot;
    [SerializeField] private Transform leftArm;
    [SerializeField] private Transform rightArm;
    [SerializeField] private Transform leftLeg;
    [SerializeField] private Transform rightLeg;

    [Header("Animation Speeds & Angles")]
    [SerializeField] private float runCycleSpeed = 16.0f;
    [SerializeField] private float armSwingAngle = 40.0f;
    [SerializeField] private float legSwingAngle = 45.0f;
    [SerializeField] private float bodyBounceHeight = 0.08f;
    [SerializeField] private float bodyTiltAngle = 10.0f;

    private Rigidbody rb;
    private PlayerController playerCtrl;
    private BotController botCtrl;

    private Vector3 originalBodyPos;
    private Quaternion originalLeftArmRot;
    private Quaternion originalRightArmRot;
    private Quaternion originalLeftLegRot;
    private Quaternion originalRightLegRot;

    private float runCycleTimer = 0f;

    public void SetupLimbs(Transform body, Transform lArm, Transform rArm, Transform lLeg, Transform rLeg)
    {
        bodyRoot = body;
        leftArm = lArm;
        rightArm = rArm;
        leftLeg = lLeg;
        rightLeg = rLeg;

        if (bodyRoot != null) originalBodyPos = bodyRoot.localPosition;
        if (leftArm != null) originalLeftArmRot = leftArm.localRotation;
        if (rightArm != null) originalRightArmRot = rightArm.localRotation;
        if (leftLeg != null) originalLeftLegRot = leftLeg.localRotation;
        if (rightLeg != null) originalRightLegRot = rightLeg.localRotation;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerCtrl = GetComponent<PlayerController>();
        botCtrl = GetComponent<BotController>();

        if (bodyRoot != null && originalBodyPos == Vector3.zero)
        {
            originalBodyPos = bodyRoot.localPosition;
            originalLeftArmRot = leftArm.localRotation;
            originalRightArmRot = rightArm.localRotation;
            originalLeftLegRot = leftLeg.localRotation;
            originalRightLegRot = rightLeg.localRotation;
        }
    }

    private void LateUpdate()
    {
        if (leftArm == null || rightArm == null || leftLeg == null || rightLeg == null) return;

        bool isGrounded = true;
        bool isEliminated = false;

        if (playerCtrl != null)
        {
            isGrounded = playerCtrl.IsGrounded;
            isEliminated = playerCtrl.IsEliminated;
        }
        else if (botCtrl != null)
        {
            isGrounded = botCtrl.IsGrounded;
            isEliminated = botCtrl.IsEliminated;
        }

        Vector3 horizVel = rb != null ? new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z) : Vector3.zero;
        float speed = horizVel.magnitude;

        if (isEliminated)
        {
            AnimateEliminatedFlail();
        }
        else if (!isGrounded)
        {
            AnimateInAirJump();
        }
        else if (speed > 0.4f)
        {
            AnimateRun(speed);
        }
        else
        {
            AnimateIdle();
        }
    }

    private void AnimateRun(float speed)
    {
        float speedFactor = Mathf.Clamp(speed / 6.0f, 0.6f, 1.5f);
        runCycleTimer += Time.deltaTime * runCycleSpeed * speedFactor;

        float sinWave = Mathf.Sin(runCycleTimer);
        float cosWave = Mathf.Cos(runCycleTimer);

        // Legs swing back and forth alternately
        leftLeg.localRotation = originalLeftLegRot * Quaternion.Euler(sinWave * legSwingAngle, 0, 0);
        rightLeg.localRotation = originalRightLegRot * Quaternion.Euler(-sinWave * legSwingAngle, 0, 0);

        // Arms swing opposite to legs (like real running humans)
        leftArm.localRotation = originalLeftArmRot * Quaternion.Euler(-sinWave * armSwingAngle, 0, sinWave * 5f);
        rightArm.localRotation = originalRightArmRot * Quaternion.Euler(sinWave * armSwingAngle, 0, -sinWave * 5f);

        // Body vertical bobbing and forward tilt
        if (bodyRoot != null)
        {
            float bounce = Mathf.Abs(sinWave) * bodyBounceHeight;
            bodyRoot.localPosition = originalBodyPos + new Vector3(0, bounce, 0);
            bodyRoot.localRotation = Quaternion.Euler(bodyTiltAngle, 0, -sinWave * 3.5f);
        }
    }

    private void AnimateIdle()
    {
        float breathe = Mathf.Sin(Time.time * 3.0f);

        // Return limbs smoothly to resting state
        leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, originalLeftLegRot, Time.deltaTime * 10f);
        rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, originalRightLegRot, Time.deltaTime * 10f);

        // Gentle arm idle sway
        leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, originalLeftArmRot * Quaternion.Euler(0, 0, breathe * 3f), Time.deltaTime * 8f);
        rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, originalRightArmRot * Quaternion.Euler(0, 0, -breathe * 3f), Time.deltaTime * 8f);

        if (bodyRoot != null)
        {
            bodyRoot.localPosition = Vector3.Lerp(bodyRoot.localPosition, originalBodyPos + new Vector3(0, breathe * 0.02f, 0), Time.deltaTime * 8f);
            bodyRoot.localRotation = Quaternion.Slerp(bodyRoot.localRotation, Quaternion.identity, Time.deltaTime * 10f);
        }
    }

    private void AnimateInAirJump()
    {
        // Jump pose: Arms thrown high / outward, knees slightly tucked
        Quaternion targetLArm = originalLeftArmRot * Quaternion.Euler(-60f, 0, 25f);
        Quaternion targetRArm = originalRightArmRot * Quaternion.Euler(-60f, 0, -25f);
        Quaternion targetLLeg = originalLeftLegRot * Quaternion.Euler(25f, 0, 0);
        Quaternion targetRLeg = originalRightLegRot * Quaternion.Euler(-15f, 0, 0);

        leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, targetLArm, Time.deltaTime * 12f);
        rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, targetRArm, Time.deltaTime * 12f);
        leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, targetLLeg, Time.deltaTime * 10f);
        rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, targetRLeg, Time.deltaTime * 10f);
    }

    private void AnimateEliminatedFlail()
    {
        // Fast panic flailing when falling into the void
        float panicTime = Time.time * 24.0f;
        float armFlailL = Mathf.Sin(panicTime) * 60f;
        float armFlailR = Mathf.Cos(panicTime) * 60f;
        float legFlailL = Mathf.Sin(panicTime * 1.2f) * 45f;
        float legFlailR = Mathf.Cos(panicTime * 1.2f) * 45f;

        leftArm.localRotation = originalLeftArmRot * Quaternion.Euler(-70f + armFlailL, 0, 30f);
        rightArm.localRotation = originalRightArmRot * Quaternion.Euler(-70f + armFlailR, 0, -30f);
        leftLeg.localRotation = originalLeftLegRot * Quaternion.Euler(legFlailL, 0, 0);
        rightLeg.localRotation = originalRightLegRot * Quaternion.Euler(legFlailR, 0, 0);
    }
}
