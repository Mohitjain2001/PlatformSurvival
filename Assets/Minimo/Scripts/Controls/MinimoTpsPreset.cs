using UnityEngine;

[CreateAssetMenu(fileName = "MinimoTpsPreset", menuName = "Minimo/TPS Preset")]
public class MinimoTpsPreset : ScriptableObject
{
    [Header("Movement")]
    [Tooltip("Configures move speed.")]
    [Min(0.1f)] public float moveSpeed = 6.6f;
    [Tooltip("Configures ground acceleration.")]
    [Min(0f)] public float groundAcceleration = 40f;
    [Tooltip("Configures turn acceleration.")]
    [Min(0f)] public float turnAcceleration = 55f;
    [Tooltip("Configures ground deceleration.")]
    [Min(0f)] public float groundDeceleration = 46f;
    [Tooltip("Configures air acceleration.")]
    [Min(0f)] public float airAcceleration = 13f;
    [Tooltip("Configures air control.")]
    [Range(0f, 1f)] public float airControl = 0.8f;
    [Tooltip("Configures reverse direction acceleration multiplier.")]
    [Min(0f)] public float reverseDirectionAccelerationMultiplier = 1.25f;
    [Tooltip("Configures rotation sharpness.")]
    [Min(0f)] public float rotationSharpness = 16f;
    [Tooltip("Configures snappy reverse turn multiplier.")]
    [Min(1f)] public float snappyReverseTurnMultiplier = 2.25f;
    [Tooltip("Configures reverse pivot threshold.")]
    [Range(-1f, 0f)] public float reversePivotThreshold = -0.35f;
    [Tooltip("Configures pivot brake deceleration.")]
    [Min(0f)] public float pivotBrakeDeceleration = 78f;
    [Tooltip("Configures crouch walk speed multiplier.")]
    [Range(0.05f, 1f)] public float crouchWalkSpeedMultiplier = 0.5f;

    [Header("Jump And Fall")]
    [Tooltip("Configures jump height.")]
    [Min(0.1f)] public float jumpHeight = 2.1f;
    [Tooltip("Configures gravity.")]
    public float gravity = -32f;
    [Tooltip("Configures jump hold time.")]
    [Min(0f)] public float jumpHoldTime = 0.18f;
    [Tooltip("Configures jump hold force.")]
    [Min(0f)] public float jumpHoldForce = 22f;
    [Tooltip("Configures fall gravity multiplier.")]
    [Min(0f)] public float fallGravityMultiplier = 3.1f;
    [Tooltip("Configures fast fall gravity multiplier.")]
    [Min(0f)] public float fastFallGravityMultiplier = 4.6f;
    [Tooltip("Configures fall animation vertical speed threshold.")]
    [Min(0f)] public float fallAnimationVerticalSpeedThreshold = 0.15f;
    [Tooltip("Configures apex vertical speed threshold.")]
    [Min(0f)] public float apexVerticalSpeedThreshold = 1.4f;
    [Tooltip("Configures apex gravity multiplier.")]
    [Range(0.1f, 1f)] public float apexGravityMultiplier = 0.58f;
    [Tooltip("Configures terminal velocity.")]
    public float terminalVelocity = -56f;
    [Tooltip("Configures coyote time.")]
    [Min(0f)] public float coyoteTime = 0.2f;
    [Tooltip("Configures jump buffer time.")]
    [Min(0f)] public float jumpBufferTime = 0.2f;
    [Tooltip("Configures minimum jump height.")]
    [Min(0.05f)] public float minimumJumpHeight = 0.85f;
    [Tooltip("Configures jump cut multiplier.")]
    [Range(0.1f, 1f)] public float jumpCutMultiplier = 0.5f;
    [Tooltip("Configures jump ceiling check distance.")]
    [Min(0f)] public float jumpCeilingCheckDistance = 0.14f;
    [Tooltip("Configures grounded vertical force.")]
    public float groundedVerticalForce = -4.5f;
    [Tooltip("Configures enable double jump.")]
    public bool enableDoubleJump = true;
    [Tooltip("Configures max air jumps.")]
    [Min(0)] public int maxAirJumps = 1;
    [Tooltip("Configures air jump height multiplier.")]
    [Range(0.25f, 1.5f)] public float airJumpHeightMultiplier = 0.92f;
    [Tooltip("Configures uphill speed penalty.")]
    [Range(0f, 1f)] public float slopeSpeedInfluence = 0.7f;
    [Tooltip("Configures uphill speed penalty.")]
    [Range(0f, 1f)] public float uphillSpeedPenalty = 0.2f;
    [Tooltip("Configures downhill speed boost.")]
    [Range(0f, 1f)] public float downhillSpeedBoost = 0.12f;
    [Tooltip("Configures slope anti slide deceleration.")]
    [Min(0f)] public float slopeAntiSlideDeceleration = 30f;
    [Tooltip("Configures slope alignment sharpness.")]
    [Min(0f)] public float slopeAlignmentSharpness = 14f;
    [Tooltip("Configures max step height.")]
    [Min(0f)] public float maxStepHeight = 0.4f;
    [Tooltip("Configures step check distance.")]
    [Min(0.01f)] public float stepCheckDistance = 0.38f;

    [Header("Character Feel - Jump Blob")]
    [Tooltip("Configures enable character feel.")]
    public bool enableCharacterFeel = true;
    [Tooltip("Configures enable jump blob effect.")]
    public bool enableJumpBlobEffect = true;
    [Tooltip("Configures jump stretch.")]
    [Min(0f)] public float jumpStretch = 0.11f;
    [Tooltip("Configures jump stretch decay speed.")]
    [Min(0f)] public float jumpStretchDecaySpeed = 6f;
    [Tooltip("Configures jump blob horizontal compression.")]
    [Min(0f)] public float jumpBlobHorizontalCompression = 0.4f;
    [Tooltip("Configures jump blob vertical stretch.")]
    [Min(0f)] public float jumpBlobVerticalStretch = 1f;
    [Tooltip("Configures dash speed.")]
    [Min(0f)] public float jumpBlobVerticalVelocityInfluence = 0.45f;

    [Header("Gelismis Aksiyonlar")]
    [Tooltip("Configures dash speed.")]
    [Min(0f)] public float dashSpeed = 14f;
    [Tooltip("Configures dash duration.")]
    [Min(0.01f)] public float dashDuration = 0.17f;
    [Tooltip("Configures dash cooldown.")]
    [Min(0f)] public float dashCooldown = 0.45f;
    [Tooltip("Configures ground pound speed.")]
    [Min(1f)] public float groundPoundSpeed = 42f;
    [Tooltip("Configures ground pound acceleration.")]
    [Min(1f)] public float groundPoundAcceleration = 240f;
    [Tooltip("Configures crouch height.")]
    [Min(0.5f)] public float crouchHeight = 1.25f;
    [Tooltip("Configures slide duration.")]
    [Min(0.01f)] public float slideDuration = 0.62f;
    [Tooltip("Configures slide deceleration.")]
    [Min(0f)] public float slideInitialBoost = 2.5f;
    [Tooltip("Configures slide deceleration.")]
    [Min(0f)] public float slideDeceleration = 17f;
    [Tooltip("Configures dive forward speed.")]
    [Min(0f)] public float diveForwardSpeed = 13.5f;
    [Tooltip("Configures dive forward boost.")]
    [Min(0f)] public float diveForwardBoost = 5f;
    [Tooltip("Configures dive downward speed.")]
    [Min(0f)] public float diveDownwardSpeed = 11f;
    [Tooltip("Configures dive gravity multiplier.")]
    [Min(0f)] public float diveGravityMultiplier = 1.7f;
    [Tooltip("Configures dive duration.")]
    [Min(0.01f)] public float diveDuration = 0.5f;
    [Tooltip("Configures roll base speed.")]
    [Min(0f)] public float rollBaseSpeed = 9f;
    [Tooltip("Configures roll momentum conversion.")]
    [Range(0f, 1.5f)] public float rollMomentumConversion = 0.78f;
    [Tooltip("Configures roll duration.")]
    [Min(0.01f)] public float rollDuration = 0.42f;
    [Tooltip("Configures roll duration multiplier.")]
    [Range(1f, 2f)] public float rollDurationMultiplier = 1.45f;
    [Tooltip("Configures roll landing resume duration ratio.")]
    [Range(0.25f, 1f)] public float rollLandingResumeDurationRatio = 0.85f;
    [Tooltip("Configures roll landing resume max duration.")]
    [Min(0.1f)] public float rollLandingResumeMaxDuration = 1.6f;
    [Tooltip("Configures roll deceleration.")]
    [Min(0f)] public float rollDeceleration = 24f;
    [Tooltip("Configures slope slide min angle.")]
    [Range(0f, 89f)] public float slopeSlideMinAngle = 11f;
    [Tooltip("Configures slope slide enter min speed.")]
    [Min(0f)] public float slopeSlideEnterMinSpeed = 4.4f;
    [Tooltip("Configures slope slide downhill acceleration.")]
    [Min(0f)] public float slopeSlideDownhillAcceleration = 34f;
    [Tooltip("Configures slope slide friction.")]
    [Min(0f)] public float slopeSlideFriction = 1.9f;
    [Tooltip("Configures slope slide max speed.")]
    [Min(0f)] public float slopeSlideMaxSpeed = 28f;
    [Tooltip("Configures slope momentum carry duration.")]
    [Min(0f)] public float slopeMomentumCarryDuration = 0.9f;
    [Tooltip("Configures slope momentum retention.")]
    [Range(0f, 1.5f)] public float slopeMomentumRetention = 0.9f;
    [Tooltip("Configures slope momentum decay.")]
    [Min(0f)] public float slopeMomentumDecay = 11f;
    [Tooltip("Configures slope jump speed multiplier.")]
    [Range(0.5f, 2f)] public float slopeJumpSpeedMultiplier = 1.12f;

    [Header("Tirmanma / Ledge (Opsiyonel)")]
    [Tooltip("Configures left hand ledge transform.")]
    public Transform leftHandLedgeTransform;
    [Tooltip("Configures right hand ledge transform.")]
    public Transform rightHandLedgeTransform;
    [Tooltip("Configures left hand ledge probe radius.")]
    [Min(0.005f)] public float leftHandLedgeProbeRadius = 0.08f;
    [Tooltip("Configures right hand ledge probe radius.")]
    [Min(0.005f)] public float rightHandLedgeProbeRadius = 0.08f;

    [Header("Camera")]
    [Tooltip("Configures follow distance.")]
    [Min(0.01f)] public float followDistance = 4.9f;
    [Tooltip("Configures min zoom distance.")]
    [Min(0.01f)] public float minZoomDistance = 2.1f;
    [Tooltip("Configures max zoom distance.")]
    [Min(0.01f)] public float maxZoomDistance = 7.8f;
    [Tooltip("Configures mouse xsensitivity.")]
    [Min(0f)] public float mouseXSensitivity = 200f;
    [Tooltip("Configures mouse ysensitivity.")]
    [Min(0f)] public float mouseYSensitivity = 155f;
    [Tooltip("Configures base camera fov.")]
    [Range(20f, 120f)] public float baseCameraFov = 60f;
    [Tooltip("Configures max boost camera fov.")]
    [Range(20f, 140f)] public float maxBoostCameraFov = 75f;
    [Tooltip("Configures air dash fov weight.")]
    [Range(0f, 1.5f)] public float airDashFovWeight = 0f;
    [Tooltip("Configures slope slide fov weight.")]
    [Range(0f, 1.5f)] public float slopeSlideFovWeight = 0f;
    [Tooltip("Configures fov speed reference.")]
    [Min(0.1f)] public float fovSpeedReference = 20f;
    [Tooltip("Configures speed fov weight.")]
    [Range(0f, 1.5f)] public float speedFovWeight = 0f;
    [Tooltip("Configures landing camera shake multiplier.")]
    [Range(0f, 2f)] public float landingCameraShakeMultiplier = 1f;
}
