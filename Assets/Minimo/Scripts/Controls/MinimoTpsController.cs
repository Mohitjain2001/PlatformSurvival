using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(MinimoMovingPlatformRider))]
[DisallowMultipleComponent]
[AddComponentMenu("Minimo/TPS Controller")]
public class MinimoTpsController : MonoBehaviour
{
    private const string DefaultGroundLayerName = "Ground";
    private const float LandingAnimatorPulseDuration = 0.1f;
    private const float GroundProbeSnapDistance = 0.06f;
    private const float JumpFallStartHeightTolerance = 0.01f;
    private const float DashJumpCancelCooldownDuration = 0.2f;
    private const float RollAutoCrouchSuppressDuration = 0.16f;
    private const int LedgeOverlapBufferSize = 16;
    private const int TeeterOverlapBufferSize = 12;
    private const int PhysicsHitBufferSize = 64;
    private const KeyCode RagdollToggleKey = KeyCode.T;
    private const float RagdollBurstBaseForce = 2.1f;
    private const float RagdollBurstRadius = 1.4f;
    private const float RagdollBurstUpwardModifier = 0.2f;
    private const float RagdollBurstTorque = 0.35f;
    private const float RagdollBurstRandomScale = 0.2f;
    private const float RagdollBurstMaxLinearSpeed = 3.25f;
    private const float RagdollBurstMaxAngularSpeed = 18f;
    private const float GamepadFreeCameraTriggerDeadZone = 0.1f;
    private const float ControlsHudWidth = 350f;
    private const float ControlsHudHeight = 281f;
    private const float PerformanceHudWidth = 290f;
    private const float PerformanceHudHeight = 137f;
    private const float HudCornerRadiusPixels = 10f;
    private const int StandUpOverlapBufferSize = 16;
    private static readonly int BaseColorShaderId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorShaderId = Shader.PropertyToID("_Color");
    private static readonly int SurfaceShaderId = Shader.PropertyToID("_Surface");
    private static readonly int BlendShaderId = Shader.PropertyToID("_Blend");
    private static readonly int SrcBlendShaderId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendShaderId = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWriteShaderId = Shader.PropertyToID("_ZWrite");
    private static readonly int AlphaClipShaderId = Shader.PropertyToID("_AlphaClip");
    private static readonly int OpacityShaderId = Shader.PropertyToID("_Opacity");
    private static readonly int HighlightShaderId = Shader.PropertyToID("_Highlight");
    private static readonly int ShadowShaderId = Shader.PropertyToID("_Shadow");
    private static readonly int[] DashGhostColorShaderIds =
    {
        BaseColorShaderId,
        ColorShaderId,
        HighlightShaderId,
        ShadowShaderId
    };
    private enum ControlProfile
    {
        Custom = 0,
        ItTakesTwoInspired = 1
    }

    private enum MovementState
    {
        Grounded = 0,
        Crouching = 1,
        Sliding = 2,
        Jumping = 3,
        Falling = 4,
        Dashing = 5,
        GroundPound = 6,
        Dive = 7,
        Rolling = 8,
        SlopeSliding = 9,
        Teetering = 10
    }

    private enum HudInputMode
    {
        KeyboardMouse = 0,
        Gamepad = 1
    }

    private sealed class DashGhostInstance
    {
        public GameObject root;
        public DashGhostPiece[] pieces;
        public Renderer[] renderers;
        public float startTime;
        public float lifetime;
        public float startOpacity;
    }

    private sealed class DashGhostSourceEntry
    {
        public Renderer sourceRenderer;
        public MeshFilter sourceMeshFilter;
        public SkinnedMeshRenderer sourceSkinnedRenderer;
        public bool isSkinned;
    }

    private sealed class DashGhostPiece
    {
        public Transform transform;
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
        public Mesh bakedMesh;
        public DashGhostSourceEntry sourceEntry;
        public int materialCount;
        public int sourceMaterialCount;
    }

    [Header("Preset")]
    [Tooltip("Configures gameplay preset.")]
    [SerializeField] private MinimoTpsPreset gameplayPreset;
    [Tooltip("Configures apply preset on start.")]
    [SerializeField] private bool applyPresetOnStart = false;

    [Header("Control Profile")]
    [Tooltip("Configures control profile.")]
    [SerializeField] private ControlProfile controlProfile = ControlProfile.ItTakesTwoInspired;
    [Tooltip("Configures apply control profile on start.")]
    [SerializeField] private bool applyControlProfileOnStart = true;

    [Header("Movement")]
    [Tooltip("Configures move speed.")]
    [SerializeField][Min(0.1f)] private float moveSpeed = 7.5f;
    [Tooltip("Configures ground acceleration.")]
    [SerializeField][Min(0f)] private float groundAcceleration = 100f;
    [Tooltip("Configures turn acceleration.")]
    [SerializeField][Min(0f)] private float turnAcceleration = 360f;
    [Tooltip("Configures ground deceleration.")]
    [SerializeField][Min(0f)] private float groundDeceleration = 1000f;
    [Tooltip("Configures enable run stop slide.")]
    [SerializeField] private bool enableRunStopSlide = true;
    [Tooltip("Configures run stop slide min speed.")]
    [SerializeField][Min(0f)] private float runStopSlideMinSpeed = 5.8f;
    [Tooltip("Configures run stop slide duration.")]
    [SerializeField][Min(0.01f)] private float runStopSlideDuration = 0.18f;
    [Tooltip("Configures run stop slide deceleration multiplier.")]
    [SerializeField][Range(0.05f, 1f)] private float runStopSlideDecelerationMultiplier = 0.42f;
    [Tooltip("Configures air acceleration.")]
    [SerializeField][Min(0f)] private float airAcceleration = 15f;
    [Tooltip("Configures air control.")]
    [SerializeField][Range(0f, 1f)] private float airControl = 0.84f;
    [Tooltip("Configures reverse direction acceleration multiplier.")]
    [SerializeField][Min(0f)] private float reverseDirectionAccelerationMultiplier = 1.38f;
    [Tooltip("Configures rotation sharpness.")]
    [SerializeField][Min(0f)] private float rotationSharpness = 45f;
    [Tooltip("Configures air rotation multiplier.")]
    [SerializeField][Range(0f, 1f)] private float airRotationMultiplier = 0.55f;
    [Tooltip("Configures snappy reverse turn multiplier.")]
    [SerializeField][Min(1f)] private float snappyReverseTurnMultiplier = 3f;
    [Tooltip("Configures reverse pivot threshold.")]
    [SerializeField][Range(-1f, 0f)] private float reversePivotThreshold = -0.24f;
    [Tooltip("Configures pivot brake deceleration.")]
    [SerializeField][Min(0f)] private float pivotBrakeDeceleration = 100f;
    [Tooltip("Configures facing speed threshold.")]
    [SerializeField][Range(0f, 0.25f)] private float movementInputDeadZone = 0.01f;
    [Tooltip("Configures facing speed threshold.")]
    [SerializeField][Min(0f)] private float facingSpeedThreshold = 0.2f;
    [Tooltip("Configures crouch walk speed multiplier.")]
    [FormerlySerializedAs("crouchMoveMultiplier")]
    [SerializeField][Range(0.05f, 1f)] private float crouchWalkSpeedMultiplier = 0.5f;
    [Tooltip("Configures enable sprint.")]
    [SerializeField] private bool enableSprint = true;
    [Tooltip("Configures sprint key.")]
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
    [Tooltip("Configures sprint speed multiplier.")]
    [SerializeField][Min(1f)] private float sprintSpeedMultiplier = 2f;

    [Header("Jump And Fall")]
    [Tooltip("Configures jump height.")]
    [SerializeField][Min(0.1f)] private float jumpHeight = 2.28f;
    [Tooltip("Configures gravity.")]
    [SerializeField] private float gravity = -33f;
    [Tooltip("Configures jump hold time.")]
    [SerializeField][Min(0f)] private float jumpHoldTime = 0.2f;
    [Tooltip("Configures jump hold force.")]
    [SerializeField][Min(0f)] private float jumpHoldForce = 23.5f;
    [Tooltip("Configures fall gravity multiplier.")]
    [SerializeField][Min(0f)] private float fallGravityMultiplier = 3.2f;
    [Tooltip("Configures fast fall gravity multiplier.")]
    [SerializeField][Min(0f)] private float fastFallGravityMultiplier = 5f;
    [Tooltip("Configures fall animation vertical speed threshold.")]
    [SerializeField][Min(0f)] private float fallAnimationVerticalSpeedThreshold = 0.15f;
    [Tooltip("Configures apex vertical speed threshold.")]
    [SerializeField][Min(0f)] private float apexVerticalSpeedThreshold = 1.2f;
    [Tooltip("Configures apex gravity multiplier.")]
    [SerializeField][Range(0.1f, 1f)] private float apexGravityMultiplier = 0.52f;
    [Tooltip("Configures terminal velocity.")]
    [SerializeField] private float terminalVelocity = -60f;
    [Tooltip("Configures coyote time.")]
    [SerializeField][Min(0f)] private float coyoteTime = 0.2f;
    [Tooltip("Configures jump buffer time.")]
    [SerializeField][Min(0f)] private float jumpBufferTime = 0.2f;
    [Tooltip("Configures jump landing cooldown duration.")]
    [SerializeField][Min(0f)] private float jumpLandingCooldownDuration = 0.1f;
    [Tooltip("Configures minimum jump height.")]
    [SerializeField][Min(0.05f)] private float minimumJumpHeight = 0.85f;
    [Tooltip("Configures jump cut multiplier.")]
    [SerializeField][Range(0.1f, 1f)] private float jumpCutMultiplier = 0.44f;
    [Tooltip("Configures jump ceiling check distance.")]
    [SerializeField][Min(0f)] private float jumpCeilingCheckDistance = 0.14f;
    [Tooltip("Configures jump headroom mask.")]
    [SerializeField] private LayerMask jumpHeadroomMask = ~0;
    [Tooltip("Configures grounded vertical force.")]
    [SerializeField] private float groundedVerticalForce = -4.7f;
    [Tooltip("Configures enable double jump.")]
    [SerializeField] private bool enableDoubleJump = true;
    [Tooltip("Configures max air jumps.")]
    [SerializeField][Min(0)] private int maxAirJumps = 1;
    [Tooltip("Configures air jump height multiplier.")]
    [SerializeField][Range(0.25f, 1.5f)] private float airJumpHeightMultiplier = 0.92f;
    [Tooltip("Configures enable wall jump.")]
    [SerializeField] private bool enableWallJump = true;
    [Tooltip("Configures wall jump probe distance.")]
    [SerializeField][Min(0.05f)] private float wallJumpProbeDistance = 0.55f;
    [Tooltip("Configures wall jump backward speed.")]
    [SerializeField][Min(0f)] private float wallJumpBackwardSpeed = 8.2f;
    [Tooltip("Configures wall jump max surface up dot.")]
    [SerializeField][Range(0f, 0.99f)] private float wallJumpMaxSurfaceUpDot = 0.35f;
    [Tooltip("Configures enable wall hang.")]
    [SerializeField] private bool enableWallHang = true;
    [Tooltip("Configures wall hang slide speed.")]
    [SerializeField][Min(0f)] private float wallHangSlideSpeed = 1.8f;
    [Tooltip("Configures wall hang horizontal damping.")]
    [SerializeField][Min(0f)] private float wallHangSlideEaseInDuration = 0.22f;
    [Tooltip("Configures wall hang horizontal damping.")]
    [SerializeField][Range(0f, 1f)] private float wallHangSlideEaseInStartAccelerationScale = 0.08f;
    [Tooltip("Configures wall hang horizontal damping.")]
    [SerializeField][Min(0f)] private float wallHangHorizontalDamping = 20f;
    [Tooltip("Configures wall hang stick speed.")]
    [SerializeField][Min(0f)] private float wallHangStickSpeed = 0.75f;
    [Tooltip("Configures wall hang release ground distance.")]
    [SerializeField][Min(0f)] private float wallHangReleaseGroundDistance = 0.3f;
    [Tooltip("Configures wall hang contact distance.")]
    [SerializeField][Min(0f)] private float wallHangContactDistance = 0.12f;
    [Tooltip("Configures wall hang facing angle tolerance.")]
    [SerializeField][Min(0f)] private float wallHangFacingAngleTolerance = 180f;
    [Tooltip("Configures enable ledge hang.")]
    [SerializeField][Range(0f, 180f)] private float wallHangReleaseInputAngle = 45f;
    [Tooltip("Configures enable ledge hang.")]
    [SerializeField] private bool enableLedgeHang = true;
    [Tooltip("Configures left hand ledge transform.")]
    [SerializeField] private Transform leftHandLedgeTransform;
    [Tooltip("Configures right hand ledge transform.")]
    [SerializeField] private Transform rightHandLedgeTransform;
    [Tooltip("Configures left hand ledge probe radius.")]
    [SerializeField][Min(0.005f)] private float leftHandLedgeProbeRadius = 0.08f;
    [Tooltip("Configures right hand ledge probe radius.")]
    [SerializeField][Min(0.005f)] private float rightHandLedgeProbeRadius = 0.08f;
    [Tooltip("Configures ledge hand contact padding.")]
    [SerializeField][Min(0f)] private float ledgeHandContactPadding = 0.04f;
    [Tooltip("Configures ledge grab assist distance.")]
    [SerializeField][Min(0f)] private float ledgeGrabAssistDistance = 0.14f;
    [Tooltip("Configures ledge top probe up distance.")]
    [SerializeField][Min(0.01f)] private float ledgeTopProbeUpDistance = 0.45f;
    [Tooltip("Configures ledge top probe down distance.")]
    [SerializeField][Min(0f)] private float ledgeTopProbeForwardInset = 0.08f;
    [Tooltip("Configures ledge top probe down distance.")]
    [SerializeField][Min(0.05f)] private float ledgeTopProbeDownDistance = 1.1f;
    [Tooltip("Configures ledge top surface min up dot.")]
    [SerializeField][Range(0f, 1f)] private float ledgeTopSurfaceMinUpDot = 0.65f;
    [Tooltip("Configures ledge top max horizontal gap.")]
    [SerializeField][Min(0f)] private float ledgeTopMaxHorizontalGap = 0.4f;
    [Tooltip("Configures ledge hang min edge height.")]
    [SerializeField][Min(0f)] private float ledgeHangMinEdgeHeight = 0.15f;
    [Tooltip("Configures ledge hang max edge height.")]
    [SerializeField][Min(0f)] private float ledgeHangMaxEdgeHeight = 1.6f;
    [Tooltip("Configures ledge hang lateral move speed.")]
    [SerializeField][Range(0f, 1f)] private float ledgeHangClimbForwardInputThreshold = 0.25f;
    [Tooltip("Configures ledge hang lateral move speed.")]
    [SerializeField][Min(0f)] private float ledgeHangLateralMoveSpeed = 1.4f;
    [Tooltip("Configures ledge hang corner stop distance.")]
    [SerializeField][Range(0f, 1f)] private float ledgeHangLateralInputThreshold = 0.08f;
    [Tooltip("Configures ledge hang corner stop distance.")]
    [SerializeField][Min(0f)] private float ledgeHangCornerStopDistance = 0.09f;
    [Tooltip("Configures ledge hang side animation threshold.")]
    [SerializeField][Range(0f, 1f)] private float ledgeHangSideAnimationThreshold = 0.18f;
    [Tooltip("Configures ledge hang hold before climb duration.")]
    [SerializeField][Min(0f)] private float ledgeHangHoldBeforeClimbDuration = 0.1f;
    [Tooltip("Configures ledge hang climb duration.")]
    [SerializeField][Min(0f)] private float ledgeHangEntryInputLockDuration = 0.12f;
    [Tooltip("Configures ledge hang climb duration.")]
    [SerializeField][Range(-1f, 0f)] private float ledgeHangReleaseBackInputThreshold = -0.2f;
    [Tooltip("Configures ledge hang climb duration.")]
    [SerializeField][Min(0.05f)] private float ledgeHangClimbDuration = 0.45f;
    [Tooltip("Configures ledge hang climb up distance.")]
    [SerializeField][Min(0f)] private float ledgeHangClimbUpDistance = 1.15f;
    [Tooltip("Configures ledge hang climb forward distance.")]
    [SerializeField][Min(0f)] private float ledgeHangClimbForwardDistance = 0.55f;
    [Tooltip("Configures ledge climb step forward bonus.")]
    [SerializeField][Min(0f)] private float ledgeClimbStepForwardBonus = 0.18f;
    [Tooltip("Configures ledge hang anchor outward offset.")]
    [SerializeField][Min(0f)] private float ledgeHangAnchorOutwardOffset = 0.02f;
    [Tooltip("Configures ledge hang anchor vertical offset.")]
    [SerializeField] private float ledgeHangAnchorVerticalOffset = 0.02f;
    [Tooltip("Configures ledge hang hand midpoint vertical bias.")]
    [SerializeField][Range(-1f, 1f)] private float ledgeHangHandMidpointVerticalBias = -0.35f;
    [Tooltip("Configures ledge hang vertical calibration offset.")]
    [SerializeField][Range(-1f, 1f)] private float ledgeHangVerticalCalibrationOffset = 0.2833f;
    [Tooltip("Configures ledge hang snap hands midpoint to edge.")]
    [SerializeField] private bool ledgeHangSnapHandsMidpointToEdge = true;
    [Tooltip("Configures ledge hang use anchor offsets.")]
    [SerializeField] private bool ledgeHangUseAnchorOffsets = false;
    [Tooltip("Configures ledge hang wall proximity offset.")]
    [SerializeField][Range(-0.2f, 0.2f)] private float ledgeHangWallProximityOffset = -0.03f;
    [Tooltip("Configures ledge hang hand surface target offset.")]
    [SerializeField][Range(-0.05f, 0.05f)] private float ledgeHangHandSurfaceTargetOffset = -0.004f;
    [Tooltip("Configures ledge hang dual hand surface tolerance.")]
    [SerializeField][Min(0f)] private float ledgeHangDualHandSurfaceTolerance = 0.004f;
    [Tooltip("Configures ledge hang dual hand max extra push.")]
    [SerializeField][Min(0f)] private float ledgeHangDualHandMaxExtraPush = 0.1f;
    [Tooltip("Configures ledge hang anchor align speed.")]
    [SerializeField][Min(0f)] private float ledgeHangAnchorAlignSpeed = 7.5f;
    [Tooltip("Configures ledge hang anchor align tolerance.")]
    [SerializeField][Min(0f)] private float ledgeHangAnchorAlignTolerance = 0.02f;
    [Tooltip("Configures wall post climb detach lock time.")]
    [SerializeField][Min(0f)] private float wallPostClimbDetachLockTime = 0.65f;
    [Tooltip("Configures ledge hang contact lost grace time.")]
    [SerializeField][Min(0f)] private float ledgeHangContactLostGraceTime = 0.12f;
    [Tooltip("Configures ledge hang regrab lock time.")]
    [SerializeField][Min(0f)] private float ledgeHangRegrabLockTime = 0.18f;
    [Tooltip("Configures ledge hang corner transition assist distance.")]
    [SerializeField][Min(0f)] private float ledgeHangCornerTransitionAssistDistance = 0.16f;
    [Tooltip("Configures ledge hang corner probe lateral offset.")]
    [SerializeField][Min(0f)] private float ledgeHangCornerProbeLateralOffset = 0.06f;
    [Tooltip("Configures ledge hang max entry snap distance.")]
    [SerializeField][Min(0.05f)] private float ledgeHangMaxEntrySnapDistance = 1.25f;
    [Tooltip("Configures ledge hang max entry horizontal distance.")]
    [SerializeField][Min(0.05f)] private float ledgeHangMaxEntryHorizontalDistance = 0.85f;
    [Tooltip("Configures ledge hang max entry vertical distance.")]
    [SerializeField][Min(0.05f)] private float ledgeHangMaxEntryVerticalDistance = 1.05f;
    [Tooltip("Configures ledge hang max hand above edge.")]
    [SerializeField][Min(0f)] private float ledgeHangMaxHandAboveEdge = 0.22f;
    [Tooltip("Configures ledge hang max entry wall distance.")]
    [SerializeField][Min(0.05f)] private float ledgeHangMaxEntryWallDistance = 0.65f;
    [Tooltip("Configures high fall min height for fall anim.")]
    [SerializeField][Min(0f)] private float highFallMinHeightForFallAnim = 1.75f;

    [Tooltip("Configures high fall double jump height tolerance.")]
    [SerializeField][Min(0f)] private float highFallDoubleJumpHeightTolerance = 0.2f;
    [Tooltip("Configures high fall min air time for roll.")]
    [SerializeField][Min(0f)] private float highFallMinAirTimeForRoll = 0.7f;

    [Header("Dash")]
    [Tooltip("Configures enable dash.")]
    [SerializeField] private bool enableDash = true;
    [Tooltip("Configures dash key.")]
    [SerializeField] private KeyCode dashKey = KeyCode.Q;
    [Tooltip("Configures dash speed.")]
    [SerializeField][Min(0f)] private float dashSpeed = 15.8f;
    [Tooltip("Configures dash duration.")]
    [SerializeField][Min(0.01f)] private float dashDuration = 0.6f;
    [Tooltip("Configures dash cooldown.")]
    [SerializeField][Min(0f)] private float dashCooldown = 1f;
    [Tooltip("Configures dash buffer time.")]
    [SerializeField][Min(0f)] private float dashBufferTime = 0.6f;
    [Tooltip("Configures dash gravity multiplier.")]
    [SerializeField][Min(0f)] private float dashGravityMultiplier = 0.25f;
    [Tooltip("Configures dash exit speed multiplier.")]
    [SerializeField][Range(0f, 1f)] private float dashExitSpeedMultiplier = 0.72f;
    [Tooltip("Configures dash camera steer sharpness.")]
    [SerializeField][Min(0f)] private float dashCameraSteerSharpness = 20f;
    [Tooltip("Configures enable dash ghost trail.")]
    [SerializeField] private bool enableDashGhostTrail = true;
    [Tooltip("Configures enable slide ghost trail.")]
    [SerializeField] private bool enableSlideGhostTrail = true;
    [Tooltip("Configures dash ghost density.")]
    [SerializeField][Min(0.005f)] private float dashGhostSpawnInterval = 0.035f;
    [Tooltip("Configures dash ghost density.")]
    [SerializeField][Range(0.1f, 2f)] private float dashGhostDensity = 0.5f;
    [Tooltip("Configures dash ghost max active count.")]
    [SerializeField][Min(1)] private int dashGhostMaxActiveCount = 5;
    [Tooltip("Configures dash ghost lifetime.")]
    [SerializeField][Min(0.01f)] private float dashGhostLifetime = 0.18f;
    [Tooltip("Configures dash ghost start opacity.")]
    [SerializeField][Range(0f, 1f)] private float dashGhostStartOpacity = 0.35f;
    [Tooltip("Configures dash ghost scale.")]
    [SerializeField][Min(0.01f)] private float dashGhostScale = 1f;
    [Tooltip("Configures dash ghost back offset.")]
    [SerializeField][Min(0f)] private float dashGhostBackOffset = 0.04f;
    [Tooltip("Configures dash ghost min planar speed.")]
    [SerializeField][Min(0f)] private float dashGhostMinPlanarSpeed = 0.2f;
    [Tooltip("Configures enable ground pound ghost trail.")]
    [SerializeField] private bool enableGroundPoundGhostTrail = true;
    [Tooltip("Configures ground pound ghost min fall speed.")]
    [SerializeField][Min(0f)] private float groundPoundGhostMinFallSpeed = 3.5f;
    [Tooltip("Configures dash ghost material.")]
    [SerializeField] private Material dashGhostMaterial;
    [Tooltip("Configures dash ghost tint.")]
    [SerializeField] private Color dashGhostTint = new Color(1f, 1f, 1f, 1f);

    [Header("Ground Pound")]
    [Tooltip("Configures enable ground pound.")]
    [SerializeField] private bool enableGroundPound = true;
    [Tooltip("Configures ground pound key.")]
    [SerializeField] private KeyCode groundPoundKey = KeyCode.LeftControl;
    [Tooltip("Configures ground pound buffer time.")]
    [SerializeField][Min(0f)] private float groundPoundBufferTime = 0.14f;
    [Tooltip("Configures ground pound start delay.")]
    [SerializeField][Min(0f)] private float groundPoundStartDelay = 0.05f;
    [Tooltip("Configures ground pound speed.")]
    [SerializeField][Min(1f)] private float groundPoundSpeed = 46f;
    [Tooltip("Configures ground pound acceleration.")]
    [SerializeField][Min(1f)] private float groundPoundAcceleration = 270f;
    [Tooltip("Configures ground pound horizontal damping.")]
    [SerializeField][Min(0f)] private float groundPoundHorizontalDamping = 24f;
    [Tooltip("Configures ground pound land squash bonus.")]
    [SerializeField][Min(0f)] private float groundPoundLandSquashBonus = 0.18f;
    [Tooltip("Configures ground pound land camera kick.")]
    [SerializeField][Min(0f)] private float groundPoundLandCameraKick = 0.2f;
    [Tooltip("Configures ground pound hard bounce back speed.")]
    [SerializeField][Min(0f)] private float groundPoundHardBounceBackSpeed = 6f;
    [Tooltip("Configures ground pound hard bounce up velocity.")]
    [SerializeField][Min(0f)] private float groundPoundHardBounceUpVelocity = 6f;
    [Tooltip("Configures ground pound hard land hold time.")]
    [SerializeField][Min(0f)] private float groundPoundHardBounceUpImpactBonus = 6f;
    [Tooltip("Configures ground pound hard land hold time.")]
    [SerializeField][Min(0f)] private float groundPoundHardLandHoldTime = 1f;

    [Header("Crouch And Slide")]
    [Tooltip("Configures enable crouch slide.")]
    [SerializeField] private bool enableCrouchSlide = true;
    [Tooltip("Configures crouch key.")]
    [SerializeField] private KeyCode crouchKey = KeyCode.C;
    [Tooltip("Configures crouch toggle mode.")]
    [SerializeField] private bool crouchToggleMode = true;
    [Tooltip("Configures crouch height.")]
    [SerializeField][Min(0.5f)] private float crouchHeight = 1.2f;
    [Tooltip("Configures crouch transition speed.")]
    [SerializeField][Min(0f)] private float crouchTransitionSpeed = 12f;
    [Tooltip("Configures slide buffer time.")]
    [SerializeField][Min(0f)] private float slideBufferTime = 3f;
    [Tooltip("Configures slide start min speed.")]
    [SerializeField][Min(0f)] private float slideStartMinSpeed = 3.8f;
    [Tooltip("Configures slide max speed.")]
    [SerializeField][Min(0f)] private float slideInitialBoost = 2.9f;
    [Tooltip("Configures slide max speed.")]
    [SerializeField][Min(0f)] private float slideMaxSpeed = 11f;
    [Tooltip("Configures slide duration.")]
    [SerializeField][Min(0.01f)] private float slideDuration = 3f;
    [Tooltip("Configures slide minimum hold time.")]
    [SerializeField][Min(0f)] private float slideMinimumHoldTime = 0.2f;
    [Tooltip("Configures slide deceleration.")]
    [SerializeField][Min(0f)] private float slideDeceleration = 10f;
    [Tooltip("Configures slide steer control.")]
    [SerializeField][Min(0f)] private float slideSteerControl = 4.5f;
    [Tooltip("Configures slide max steer angle.")]
    [SerializeField][Range(0f, 45f)] private float slideMaxSteerAngle = 30f;
    [Tooltip("Configures slide camera steer drift.")]
    [SerializeField][Min(0f)] private float slideCameraSteerDrift = 0.35f;
    [Tooltip("Configures slide end speed.")]
    [SerializeField][Min(0f)] private float slideEndSpeed = 2.2f;
    [Tooltip("Configures enable dive roll.")]
    [SerializeField] private bool lockSlideSpeedInLowClearance = true;

    [Header("Dive And Roll")]
    [Tooltip("Configures enable dive roll.")]
    [SerializeField] private bool enableDiveRoll = true;
    [Tooltip("Configures dive key.")]
    [SerializeField] private KeyCode diveKey = KeyCode.E;
    [Tooltip("Configures dive buffer time.")]
    [SerializeField][Min(0f)] private float diveBufferTime = 0.18f;
    [Tooltip("Configures dive forward speed.")]
    [SerializeField][Min(0f)] private float diveForwardSpeed = 14.2f;
    [Tooltip("Configures dive forward boost.")]
    [SerializeField][Min(0f)] private float diveForwardBoost = 5.5f;
    [Tooltip("Configures dive downward speed.")]
    [SerializeField][Min(0f)] private float diveDownwardSpeed = 11.8f;
    [Tooltip("Configures dive gravity multiplier.")]
    [SerializeField][Min(0f)] private float diveGravityMultiplier = 1.75f;
    [Tooltip("Configures dive steer control.")]
    [SerializeField][Min(0f)] private float diveSteerControl = 8f;
    [Tooltip("Configures dive duration.")]
    [SerializeField][Min(0.01f)] private float diveDuration = 0.5f;
    [Tooltip("Configures roll key.")]
    [SerializeField] private KeyCode rollKey = KeyCode.LeftAlt;
    [Tooltip("Configures roll buffer time.")]
    [SerializeField][Min(0f)] private float rollBufferTime = 0.6f;
    [Tooltip("Configures roll window time.")]
    [SerializeField][Min(0f)] private float rollWindowTime = 0.6f;
    [Tooltip("Configures roll base speed.")]
    [SerializeField][Min(0f)] private float rollMinImpactSpeed = 0.6f;
    [Tooltip("Configures roll base speed.")]
    [SerializeField][Min(0f)] private float rollBaseSpeed = 0.6f;
    [Tooltip("Configures roll momentum conversion.")]
    [SerializeField][Range(0f, 1.5f)] private float rollMomentumConversion = 0.78f;
    [Tooltip("Configures roll duration.")]
    [SerializeField][Min(0.01f)] private float rollDuration = 0.6f;
    [Tooltip("Configures roll duration multiplier.")]
    [SerializeField][Range(1f, 2f)] private float rollDurationMultiplier = 1.12f;
    [Tooltip("Configures roll landing resume duration ratio.")]
    [SerializeField][Range(0.25f, 1f)] private float rollLandingResumeDurationRatio = 0.85f;
    [Tooltip("Configures roll landing resume max duration.")]
    [SerializeField][Min(0.1f)] private float rollLandingResumeMaxDuration = 1.6f;
    [Tooltip("Configures roll deceleration.")]
    [SerializeField][Min(0f)] private float rollDeceleration = 0.5f;
    [Tooltip("Configures roll steer control.")]
    [SerializeField][Min(0f)] private float rollSteerControl = 2f;
    [Tooltip("Configures idle jump roll direction lock speed threshold.")]
    [SerializeField] private bool lockRollDirectionForIdleJumpLanding = true;
    [Tooltip("Configures idle jump roll direction lock speed threshold.")]
    [SerializeField][Min(0f)] private float idleJumpRollDirectionLockSpeedThreshold = 0.25f;
    [Tooltip("Configures dive miss recovery multiplier.")]
    [SerializeField][Range(0f, 1f)] private float diveMissRecoveryMultiplier = 0.22f;

    [Header("Slope Slide")]
    [Tooltip("Configures enable slope slide.")]
    [SerializeField] private bool enableSlopeSlide = true;
    [Tooltip("Configures slope slide min angle.")]
    [SerializeField][Range(0f, 89f)] private float slopeSlideMinAngle = 10f;
    [Tooltip("Configures slope slide enter min speed.")]
    [SerializeField][Min(0f)] private float slopeSlideEnterMinSpeed = 4.4f;
    [Tooltip("Configures slope slide downhill acceleration.")]
    [SerializeField][Min(0f)] private float slopeSlideDownhillAcceleration = 35f;
    [Tooltip("Configures slope slide friction.")]
    [SerializeField][Min(0f)] private float slopeSlideFriction = 1.55f;
    [Tooltip("Configures slope slide steer control.")]
    [SerializeField][Min(0f)] private float slopeSlideSteerControl = 2.8f;
    [Tooltip("Configures slope slide max steer angle.")]
    [SerializeField][Range(0f, 45f)] private float slopeSlideMaxSteerAngle = 30f;
    [Tooltip("Configures slope slide camera steer drift.")]
    [SerializeField][Min(0f)] private float slopeSlideCameraSteerDrift = 0.25f;
    [Tooltip("Configures slope slide max speed.")]
    [SerializeField][Min(0f)] private float slopeSlideMaxSpeed = 29f;
    [Tooltip("Configures slope slide exit flat angle.")]
    [SerializeField][Range(0f, 89f)] private float slopeSlideExitFlatAngle = 4f;
    [Tooltip("Configures slope momentum carry duration.")]
    [SerializeField][Min(0f)] private float slopeMomentumCarryDuration = 0.95f;
    [Tooltip("Configures slope momentum retention.")]
    [SerializeField][Range(0f, 1.5f)] private float slopeMomentumRetention = 0.91f;
    [Tooltip("Configures slope momentum decay.")]
    [SerializeField][Min(0f)] private float slopeMomentumDecay = 10.5f;
    [Tooltip("Configures slope jump speed multiplier.")]
    [SerializeField][Range(0.5f, 2f)] private float slopeJumpSpeedMultiplier = 1.13f;

    [Header("Teetering")]
    [Tooltip("Configures enable teetering.")]
    [SerializeField] private bool enableTeetering = true;
    [Tooltip("Configures teeter max entry speed.")]
    [SerializeField][Min(0f)] private float teeterMaxEntrySpeed = 4.2f;
    [Tooltip("Configures teeter probe forward distance.")]
    [SerializeField][Min(0f)] private float teeterProbeForwardDistance = 0.42f;
    [Tooltip("Configures teeter edge early detect distance.")]
    [SerializeField][Min(0f)] private float teeterEdgeEarlyDetectDistance = 0.28f;
    [Tooltip("Configures teeter probe depth.")]
    [SerializeField][Min(0.05f)] private float teeterProbeDepth = 0.9f;
    [Tooltip("Configures teeter snap back distance.")]
    [SerializeField][Min(0f)] private float teeterSnapBackDistance = 0.08f;
    [Tooltip("Configures teeter hold back speed.")]
    [SerializeField][Min(0f)] private float teeterHoldBackSpeed = 0.85f;
    [Tooltip("Configures teeter lock forward walk.")]
    [SerializeField][Range(0f, 1f)] private float teeterFallInputThreshold = 0.7f;
    [Tooltip("Configures teeter lock forward walk.")]
    [SerializeField][Range(0f, 1f)] private float teeterRecoverInputThreshold = 0.2f;
    [Tooltip("Configures teeter lock forward walk.")]
    [SerializeField][Range(0f, 1f)] private float teeterLateralExitInputThreshold = 0.12f;
    [Tooltip("Configures teeter lock forward walk.")]
    [SerializeField] private bool teeterLockForwardWalk = true;
    [Tooltip("Configures teeter facing edge alignment threshold.")]
    [SerializeField][Range(-1f, 1f)] private float teeterFacingEdgeAlignmentThreshold = 0.08f;
    [Tooltip("Configures teeter open space radius.")]
    [SerializeField][Min(0f)] private float teeterOpenSpaceRadius = 0.18f;
    [Tooltip("Configures teeter open space forward offset.")]
    [SerializeField][Min(0f)] private float teeterOpenSpaceForwardOffset = 0.09f;
    [Tooltip("Configures teeter open space vertical offset.")]
    [SerializeField][Min(0f)] private float teeterOpenSpaceVerticalOffset = 0.06f;
    [Tooltip("Configures teeter open space upper vertical offset.")]
    [SerializeField][Min(0f)] private float teeterOpenSpaceUpperVerticalOffset = 0.34f;
    [Tooltip("Configures teeter reentry lock duration.")]
    [SerializeField][Min(0f)] private float teeterReentryLockDuration = 0.12f;
    [Tooltip("Configures teeter audio cooldown.")]
    [SerializeField][Min(0f)] private float teeterAudioCooldown = 0.6f;
    [Tooltip("Configures ground probe offset.")]
    [SerializeField][Min(1f)] private float teeterNoInputEntrySpeedMultiplier = 1.8f;

    [Header("Ground Check")]
    [Tooltip("Configures ground probe offset.")]
    [SerializeField] private float groundProbeOffset = 0.1f;
    [Tooltip("Configures ground probe distance.")]
    [SerializeField] private float groundProbeDistance = 0.15f;
    [Tooltip("Configures ground probe radius.")]
    [SerializeField] private float groundProbeRadius = 0.4f;
    [Tooltip("Configures ignore ground probe upward speed.")]
    [SerializeField] private float ignoreGroundProbeUpwardSpeed = 1f;
    [Tooltip("Configures ground mask.")]
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("Configures custom climb mask.")]
    [SerializeField] private LayerMask customClimbMask = ~0;
    [Tooltip("Configures stand up block mask.")]
    [SerializeField] private LayerMask standUpBlockMask = ~0;
    [Tooltip("Configures minimum stand clearance height.")]
    [SerializeField][Min(0.1f)] private float minimumStandClearanceHeight = 1.155f;
    [Tooltip("Configures uphill speed penalty.")]
    [SerializeField][Range(0f, 1f)] private float slopeSpeedInfluence = 0.72f;
    [Tooltip("Configures uphill speed penalty.")]
    [SerializeField][Range(0f, 1f)] private float uphillSpeedPenalty = 0.22f;
    [Tooltip("Configures downhill speed boost.")]
    [SerializeField][Range(0f, 1f)] private float downhillSpeedBoost = 0.14f;
    [Tooltip("Configures slope anti slide deceleration.")]
    [SerializeField][Min(0f)] private float slopeAntiSlideDeceleration = 33f;
    [Tooltip("Configures slope alignment sharpness.")]
    [SerializeField][Min(0f)] private float slopeAlignmentSharpness = 16.5f;
    [Tooltip("Configures enable step up.")]
    [SerializeField] private bool enableStepUp = true;
    [Tooltip("Configures max step height.")]
    [SerializeField][Min(0f)] private float maxStepHeight = 0.42f;
    [Tooltip("Configures step check distance.")]
    [SerializeField][Min(0.01f)] private float stepCheckDistance = 0.4f;
    [Tooltip("Configures step forward offset.")]
    [SerializeField][Min(0f)] private float stepForwardOffset = 0.06f;
    [Tooltip("Configures step surface probe height.")]
    [SerializeField][Min(0f)] private float stepSurfaceProbeHeight = 0.08f;
    [Tooltip("Configures enable moving platform compensation.")]
    [SerializeField] private bool enableMovingPlatformCompensation = true;
    [Tooltip("Configures moving platform max delta per frame.")]
    [SerializeField][Range(0f, 1f)] private float movingPlatformRotationInfluence = 1f;
    [Tooltip("Configures moving platform max delta per frame.")]
    [SerializeField][Min(0f)] private float movingPlatformMaxDeltaPerFrame = 2f;
    [Tooltip("Configures disable platform compensation when parented.")]
    [SerializeField] private bool disablePlatformCompensationWhenParented = true;
    [Tooltip("Configures suppress gravity when parented to platform.")]
    [SerializeField] private bool suppressGravityWhenParentedToPlatform = true;

    [Header("Character Feel")]
    [Tooltip("Configures enable character feel.")]
    [SerializeField] private bool enableCharacterFeel = true;
    [Tooltip("Configures visual root.")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("Configures visual response.")]
    [SerializeField][Min(0f)] private float visualResponse = 10f;
    [Tooltip("Configures max side lean.")]
    [SerializeField][Min(0f)] private float maxSideLean = 5f;
    [Tooltip("Configures max forward lean.")]
    [SerializeField][Min(0f)] private float maxForwardLean = 5f;
    [Tooltip("Configures acceleration lean.")]
    [SerializeField][Min(0f)] private float accelerationLean = 0f;
    [Tooltip("Configures turn lean.")]
    [SerializeField][Min(0f)] private float turnLean = 0f;
    [Tooltip("Configures move stretch.")]
    [SerializeField][Min(0f)] private float moveStretch = 0f;
    [Tooltip("Configures acceleration stretch.")]
    [SerializeField][Min(0f)] private float accelerationStretch = 0f;
    [Tooltip("Configures brake squash.")]
    [SerializeField][Min(0f)] private float brakeSquash = 0f;
    [Tooltip("Configures jump stretch.")]
    [SerializeField][Min(0f)] private float jumpStretch = 0.11f;
    [Tooltip("Configures enable jump blob effect.")]
    [SerializeField] private bool enableJumpBlobEffect = true;
    [Tooltip("Configures jump blob horizontal compression.")]
    [SerializeField][Min(0f)] private float jumpBlobHorizontalCompression = 0.4f;
    [Tooltip("Configures jump blob vertical stretch.")]
    [SerializeField][Min(0f)] private float jumpBlobVerticalStretch = 1f;
    [Tooltip("Configures enable dash blob effect.")]
    [SerializeField][Min(0f)] private float jumpBlobVerticalVelocityInfluence = 0.45f;
    [Tooltip("Configures enable dash blob effect.")]
    [SerializeField] private bool enableDashBlobEffect = true;
    [Tooltip("Configures dash blob pulse.")]
    [SerializeField][Min(0f)] private float dashBlobPulse = 0.26f;
    [Tooltip("Configures dash blob horizontal compression.")]
    [SerializeField][Min(0f)] private float dashBlobHorizontalCompression = 0.28f;
    [Tooltip("Configures dash blob vertical compression.")]
    [SerializeField][Min(0f)] private float dashBlobVerticalCompression = 0.22f;
    [Tooltip("Configures dash blob forward stretch.")]
    [SerializeField][Min(0f)] private float dashBlobForwardStretch = 0.58f;
    [Tooltip("Configures dash blob decay speed.")]
    [SerializeField][Min(0f)] private float dashBlobDecaySpeed = 12f;
    [Tooltip("Configures fall shape amount.")]
    [SerializeField][Min(0f)] private float fallShapeAmount = 0.12f;
    [Tooltip("Configures fall shape max speed.")]
    [SerializeField][Min(0.01f)] private float fallShapeMaxSpeed = 24f;
    [Tooltip("Configures air side lean multiplier.")]
    [SerializeField][Range(0f, 1f)] private float airSideLeanMultiplier = 0.6f;
    [Tooltip("Configures air forward lean multiplier.")]
    [SerializeField][Range(0f, 1f)] private float airForwardLeanMultiplier = 0.45f;
    [Tooltip("Configures land squash.")]
    [SerializeField][Min(0f)] private float landSquash = 0.3f;
    [Tooltip("Configures land recover speed.")]
    [SerializeField][Min(0f)] private float landRecoverSpeed = 9f;
    [Tooltip("Configures min visual scale ratio.")]
    [SerializeField][Min(0f)] private float landingImpactStartSpeed = 1.5f;
    [Tooltip("Configures min visual scale ratio.")]
    [SerializeField][Min(0f)] private float landingImpactMinSpeed = 2f;
    [Tooltip("Configures min visual scale ratio.")]
    [SerializeField][Min(0f)] private float landingImpactMaxSpeed = 24f;
    [Tooltip("Configures min visual scale ratio.")]
    [SerializeField] private Vector3 minVisualScaleRatio = new Vector3(0.55f, 0.45f, 0.55f);
    [Tooltip("Configures jump stretch decay speed.")]
    [SerializeField][Min(0f)] private float jumpStretchDecaySpeed = 6f;
    [Tooltip("Configures grounded visual sink.")]
    [SerializeField][Min(0f)] private float groundedVisualSink = 0.035f;
    [Tooltip("Configures crouch visual sink bonus.")]
    [SerializeField][Min(0f)] private float crouchVisualSinkBonus = 0.02f;

    [Header("Camera")]
    [Tooltip("Configures follow distance.")]
    [SerializeField][Min(0.01f)] private float followDistance = 4.5f;
    [Tooltip("Configures min zoom distance.")]
    [SerializeField][Min(0.01f)] private float minZoomDistance = 2f;
    [Tooltip("Configures max zoom distance.")]
    [SerializeField][Min(0.01f)] private float maxZoomDistance = 8f;
    [Tooltip("Configures zoom level count.")]
    [SerializeField] private int zoomLevelCount = 5;
    [Tooltip("Configures zoom sharpness.")]
    [SerializeField][Min(0f)] private float zoomSharpness = 10f;
    [Tooltip("Configures shoulder offset.")]
    [SerializeField] private float shoulderOffset = 0f;
    [Tooltip("Configures pivot height.")]
    [SerializeField] private float pivotHeight = 0.55f;
    [Tooltip("Configures camera position smooth.")]
    [SerializeField][Min(0f)] private float cameraPositionSmooth = 0f;
    [Tooltip("Configures roll camera pivot release duration.")]
    [SerializeField][Min(0f)] private float rollCameraPivotReleaseDuration = 0.12f;
    [Tooltip("Configures roll camera pivot release sharpness.")]
    [SerializeField][Min(0f)] private float rollCameraPivotReleaseSharpness = 18f;
    [Tooltip("Configures roll camera pivot stabilization dead zone.")]
    [SerializeField][Min(0f)] private float rollCameraPivotStabilizationDeadZone = 0.035f;
    [Tooltip("Configures roll camera pivot stabilization sharpness.")]
    [SerializeField][Min(0f)] private float rollCameraPivotStabilizationSharpness = 16f;
    [Tooltip("Configures roll camera pivot stabilization max lag.")]
    [SerializeField][Min(0f)] private float rollCameraPivotStabilizationMaxLag = 0.28f;
    [Header("Ragdoll Free Camera")]
    [Tooltip("Configures ragdoll free camera move speed.")]
    [SerializeField][Min(0.1f)] private float ragdollFreeCameraMoveSpeed = 10f;
    [Tooltip("Configures ragdoll free camera look sensitivity multiplier.")]
    [SerializeField][Min(0.1f)] private float ragdollFreeCameraLookSensitivityMultiplier = 1f;
    [Tooltip("Configures ragdoll free camera fast multiplier.")]
    [SerializeField][Min(1f)] private float ragdollFreeCameraFastMultiplier = 2f;
    [Tooltip("Configures ragdoll free camera slow multiplier.")]
    [SerializeField][Range(0.05f, 1f)] private float ragdollFreeCameraSlowMultiplier = 0.35f;
    [Tooltip("Configures min pitch.")]
    [SerializeField] private float minPitch = -35f;
    [Tooltip("Configures max pitch.")]
    [SerializeField] private float maxPitch = 75f;
    [Tooltip("Configures mouse xsensitivity.")]
    [SerializeField][Min(0f)] private float mouseXSensitivity = 205f;
    [Tooltip("Configures mouse ysensitivity.")]
    [SerializeField][Min(0f)] private float mouseYSensitivity = 160f;
    [Tooltip("Configures wall jump camera turn speed.")]
    [SerializeField][Min(0f)] private float wallJumpCameraTurnSpeed = 840f;
    [Tooltip("Configures invert y.")]
    [SerializeField] private bool invertY = false;
    [Tooltip("Configures collision probe radius.")]
    [SerializeField][Min(0f)] private float collisionProbeRadius = 0.2f;
    [Tooltip("Configures collision buffer.")]
    [SerializeField][Min(0f)] private float collisionBuffer = 0.1f;
    [Tooltip("Configures camera collision min distance.")]
    [SerializeField][Min(0f)] private float cameraCollisionMinDistance = 0.25f;
    [Tooltip("Configures camera collision mask.")]
    [SerializeField] private LayerMask cameraCollisionMask = ~0;
    [Tooltip("Configures enable low clearance camera mode.")]
    [SerializeField] private bool enableLowClearanceCameraMode = true;
    [Tooltip("Configures low clearance camera pivot sharpness.")]
    [SerializeField][Min(0f)] private float lowClearanceCameraPivotSharpness = 14f;
    [Tooltip("Configures low clearance camera blend speed.")]
    [SerializeField][Min(0f)] private float lowClearanceCameraBlendSpeed = 4f;

    [Header("Camera Assist")]
    [Tooltip("Configures enable camera assist.")]
    [SerializeField] private bool enableCameraAssist = false;
    [Tooltip("Configures camera assist yaw weight.")]
    [SerializeField][Range(0f, 1f)] private float cameraAssistYawWeight = 0.35f;
    [Tooltip("Configures camera assist yaw sharpness.")]
    [SerializeField][Min(0f)] private float cameraAssistYawSharpness = 5f;
    [Tooltip("Configures camera look ahead distance.")]
    [SerializeField][Min(0f)] private float cameraLookAheadDistance = 0f;
    [Tooltip("Configures camera look ahead sharpness.")]
    [SerializeField][Min(0f)] private float cameraLookAheadSharpness = 8f;

    [Header("Speed FOV And Wind")]
    [Tooltip("Configures enable speed fov.")]
    [SerializeField] private bool enableSpeedFov = true;
    [Tooltip("Configures base camera fov.")]
    [SerializeField][Range(20f, 120f)] private float baseCameraFov = 61f;
    [Tooltip("Configures max boost camera fov.")]
    [SerializeField][Range(20f, 140f)] private float maxBoostCameraFov = 76f;
    [Tooltip("Configures fov boost sharpness.")]
    [SerializeField][Min(0f)] private float fovBoostSharpness = 7f;
    [Tooltip("Configures fov recover sharpness.")]
    [SerializeField][Min(0f)] private float fovRecoverSharpness = 5.5f;
    [Tooltip("Configures enable dash camera boost.")]
    [SerializeField] private bool enableDashCameraBoost = true;
    [Tooltip("Configures dash fov floor01.")]
    [SerializeField][Range(0f, 1f)] private float dashFovFloor01 = 0.72f;
    [Tooltip("Configures dash fov weight.")]
    [SerializeField][Range(0f, 2f)] private float dashFovWeight = 1.35f;
    [Tooltip("Configures dash fov boost sharpness.")]
    [SerializeField][Min(0f)] private float dashFovBoostSharpness = 16f;
    [Tooltip("Configures dash fov recover sharpness.")]
    [SerializeField][Min(0f)] private float dashFovRecoverSharpness = 8f;
    [Tooltip("Configures dash camera shake multiplier.")]
    [SerializeField][Range(0f, 2f)] private float dashCameraShakeMultiplier = 0.55f;
    [Tooltip("Configures dash motion blur volume.")]
    [SerializeField] private Component dashMotionBlurVolume;
    [Tooltip("Configures dash motion blur max weight.")]
    [SerializeField][Range(0f, 1f)] private float dashMotionBlurMaxWeight = 1f;
    [Tooltip("Configures dash motion blur boost sharpness.")]
    [SerializeField][Min(0f)] private float dashMotionBlurBoostSharpness = 20f;
    [Tooltip("Configures dash motion blur recover sharpness.")]
    [SerializeField][Min(0f)] private float dashMotionBlurRecoverSharpness = 11f;
    [Tooltip("Configures air dash fov weight.")]
    [SerializeField][Range(0f, 1.5f)] private float airDashFovWeight = 0.2f;
    [Tooltip("Configures slope slide fov weight.")]
    [SerializeField][Range(0f, 1.5f)] private float slopeSlideFovWeight = 0.558f;
    [Tooltip("Configures fov speed reference.")]
    [SerializeField][Min(0.1f)] private float fovSpeedReference = 20f;
    [Tooltip("Configures speed fov weight.")]
    [SerializeField][Range(0f, 1.5f)] private float speedFovWeight = 0f;
    [Tooltip("Configures enable wind overlay.")]
    [SerializeField] private bool enableWindOverlay = false;
    [Tooltip("Configures wind overlay max alpha.")]
    [SerializeField][Range(0f, 1f)] private float windOverlayMaxAlpha = 0.22f;
    [Tooltip("Configures wind overlay width.")]
    [SerializeField][Range(0.02f, 0.45f)] private float windOverlayWidth = 0.16f;
    [Tooltip("Configures wind overlay line count.")]
    [SerializeField][Range(1, 12)] private int windOverlayLineCount = 6;
    [Tooltip("Configures wind overlay sharpness.")]
    [SerializeField][Min(0f)] private float windOverlaySharpness = 7f;

    [Header("Camera Motion FX")]
    [Tooltip("Configures enable camera motion fx.")]
    [SerializeField] private bool enableCameraMotionFx = false;
    [Tooltip("Configures max camera bank angle.")]
    [SerializeField][Min(0f)] private float maxCameraBankAngle = 7f;
    [Tooltip("Configures camera bank sharpness.")]
    [SerializeField][Range(0f, 2f)] private float cameraBankVelocityInfluence = 0.95f;
    [Tooltip("Configures camera bank sharpness.")]
    [SerializeField][Range(0f, 2f)] private float cameraBankAccelerationInfluence = 0.55f;
    [Tooltip("Configures camera bank sharpness.")]
    [SerializeField][Min(0f)] private float cameraBankSharpness = 7f;
    [Tooltip("Configures teeter camera sway angle.")]
    [SerializeField][Min(0f)] private float teeterCameraSwayAngle = 2.1f;
    [Tooltip("Configures teeter camera sway frequency.")]
    [SerializeField][Min(0f)] private float teeterCameraSwayFrequency = 7f;
    [Tooltip("Configures camera shake max amplitude.")]
    [SerializeField][Min(0f)] private float cameraShakeMaxAmplitude = 0.11f;
    [Tooltip("Configures camera shake frequency.")]
    [SerializeField][Min(0f)] private float cameraShakeFrequency = 23f;
    [Tooltip("Configures camera shake damping.")]
    [SerializeField][Min(0f)] private float cameraShakeDamping = 8.5f;
    [Tooltip("Configures landing camera shake multiplier.")]
    [SerializeField][Range(0f, 2f)] private float landingCameraShakeMultiplier = 1f;

    [Header("Animation")]
    [Tooltip("Configures use animator parameters.")]
    [SerializeField] private bool useAnimatorParameters = true;
    [Tooltip("Configures animator override.")]
    [SerializeField] private Animator animatorOverride;
    [Tooltip("Configures anim speed param.")]
    [SerializeField] private string animSpeedParam = "Speed";
    [Tooltip("Configures anim move speed param.")]
    [SerializeField] private string animMoveSpeedParam = "MoveSpeed";
    [Tooltip("Configures anim vertical speed param.")]
    [SerializeField] private string animVerticalSpeedParam = "VerticalSpeed";
    [Tooltip("Configures anim grounded param.")]
    [SerializeField] private string animGroundedParam = "Grounded";
    [Tooltip("Configures anim fall param.")]
    [SerializeField] private string animFallParam = "Fall";
    [Tooltip("Configures anim hang param.")]
    [SerializeField] private string animHangParam = "Hang";
    [Tooltip("Configures anim hang left param.")]
    [SerializeField] private string animHangLeftParam = "HangLeft";
    [Tooltip("Configures anim hang right param.")]
    [SerializeField] private string animHangRightParam = "HangRight";
    [Tooltip("Configures anim climb param.")]
    [SerializeField] private string animClimbParam = "Climb";
    [Tooltip("Configures anim climb jump param.")]
    [SerializeField] private string animClimbJumpParam = "ClimbJump";
    [Tooltip("Configures anim crouch param.")]
    [SerializeField] private string animCrouchParam = "Crouch";
    [Tooltip("Configures anim slide param.")]
    [FormerlySerializedAs("animSlideTrigger")]
    [SerializeField] private string animSlideParam = "Slide";
    [Tooltip("Configures anim dash param.")]
    [FormerlySerializedAs("animDashTrigger")]
    [SerializeField] private string animDashParam = "Dash";
    [Tooltip("Configures anim ground pound param.")]
    [FormerlySerializedAs("animGroundPoundTrigger")]
    [SerializeField] private string animGroundPoundParam = "GroundPound";
    [Tooltip("Configures anim hard fall param.")]
    [SerializeField] private string animHardFallParam = "HardFall";
    [Tooltip("Configures anim hard land param.")]
    [SerializeField] private string animHardLandParam = "HardLand";
    [Tooltip("Configures anim state param.")]
    [SerializeField] private string animStateParam = "State";
    [Tooltip("Configures anim stop param.")]
    [SerializeField] private string animStopParam = "Stop";
    [Tooltip("Configures anim jump trigger.")]
    [SerializeField] private string animJumpTrigger = "Jump";
    [Tooltip("Configures anim jump mirror param.")]
    [SerializeField] private string animJumpMirrorParam = "Mirror";
    [Tooltip("Configures enable jump mirror alternation.")]
    [SerializeField] private bool enableJumpMirrorAlternation = true;
    [Tooltip("Configures anim dash mirror param.")]
    [SerializeField] private string animDashMirrorParam = "DashMirror";
    [Tooltip("Configures enable dash mirror alternation.")]
    [SerializeField] private bool enableDashMirrorAlternation = true;
    [Tooltip("Configures anim frontflip trigger.")]
    [SerializeField] private string animFrontflipTrigger = "DoubleJump";
    [Tooltip("Configures anim backflip trigger.")]
    [SerializeField] private string animBackflipTrigger = "Backflip";
    [Tooltip("Configures anim land param.")]
    [FormerlySerializedAs("animLandTrigger")]
    [SerializeField] private string animLandParam = "Land";
    [Tooltip("Configures anim dive param.")]
    [FormerlySerializedAs("animDiveTrigger")]
    [SerializeField] private string animDiveParam = "Dive";
    [Tooltip("Configures anim roll param.")]
    [FormerlySerializedAs("animRollTrigger")]
    [SerializeField] private string animRollParam = "Roll";
    [Tooltip("Configures anim teeter param.")]
    [FormerlySerializedAs("animTeeterTrigger")]
    [SerializeField] private string animTeeterParam = "Teeter";
    [Tooltip("Configures stop animation hold time.")]
    [SerializeField][Min(0.01f)] private float stopAnimationHoldTime = 0.24f;
    [Tooltip("Configures stop arm animator speed threshold.")]
    [SerializeField][Range(0f, 1f)] private float stopArmAnimatorSpeedThreshold = 0.9f;
    [Tooltip("Configures stop trigger animator speed threshold.")]
    [SerializeField][Range(0f, 1f)] private float stopTriggerAnimatorSpeedThreshold = 0.01f;
    [Tooltip("Configures stop sprint prime min planar speed.")]
    [SerializeField][Min(0f)] private float stopSprintPrimeMinPlanarSpeed = 0.2f;
    [Tooltip("Configures stop sprint release trigger speed01.")]
    [SerializeField][Range(0f, 1f)] private float stopSprintReleaseTriggerSpeed01 = 0.35f;
    [Tooltip("Configures hold stop bool until animation ends.")]
    [SerializeField] private bool holdStopBoolUntilAnimationEnds = true;
    [Tooltip("Configures stop animation state tag.")]
    [SerializeField] private string stopAnimationStateTag = "Stop";
    [Tooltip("Configures stop animation end normalized time.")]
    [SerializeField][Range(0f, 1f)] private float stopAnimationEndNormalizedTime = 0.98f;
    [Tooltip("Configures anim speed smoothness.")]
    [SerializeField][Min(0)] private int stopAnimationLayerIndex = 0;
    [Tooltip("Configures anim speed smoothness.")]
    [SerializeField][Min(0f)] private float animSpeedSmoothness = 7.5f;
    [Tooltip("Configures anim dash speed value.")]
    [SerializeField][Range(0f, 1f)] private float animDashSpeedValue = 1f;
    [Tooltip("Configures animator normal playback speed.")]
    [SerializeField][Range(0.01f, 2f)] private float animatorNormalPlaybackSpeed = 1f;
    [Tooltip("Configures animator dash playback speed.")]
    [SerializeField][Range(0.01f, 2f)] private float animatorDashPlaybackSpeed = 0.2f;
    [Tooltip("Configures animator playback speed linear rise.")]
    [SerializeField][Min(0f)] private float animatorPlaybackSpeedLinearRise = 12f;
    [Tooltip("Configures animator playback speed linear fall.")]
    [SerializeField][Min(0f)] private float animatorPlaybackSpeedLinearFall = 18f;
    [Tooltip("Configures anim speed linear rise.")]
    [SerializeField][Min(0f)] private float animSpeedLinearRise = 5.5f;
    [Tooltip("Configures anim speed linear fall.")]
    [SerializeField][Min(0f)] private float animSpeedLinearFall = 7.5f;
    [Tooltip("Configures anim speed linear rate scale.")]
    [SerializeField][Range(0.05f, 1f)] private float animSpeedLinearRateScale = 0.7f;
    [Tooltip("Configures anim move speed linear rise.")]
    [SerializeField][Min(0f)] private float animMoveSpeedLinearRise = 22f;
    [Tooltip("Configures anim move speed linear fall.")]
    [SerializeField][Min(0f)] private float animMoveSpeedLinearFall = 28f;
    [Tooltip("Configures anim vertical speed linear rate.")]
    [SerializeField][Min(0f)] private float animVerticalSpeedLinearRate = 45f;
    [Tooltip("Configures planar stop snap speed.")]
    [SerializeField][Min(0f)] private float planarStopSnapSpeed = 0.08f;
    [Tooltip("Configures animator speed zero epsilon.")]
    [SerializeField][Min(0f)] private float animatorSpeedZeroEpsilon = 0.02f;

    [Header("Feedback")]
    [Tooltip("Configures enable feedback.")]
    [SerializeField] private bool enableFeedback = false;
    [Tooltip("Configures audio source override.")]
    [SerializeField] private AudioSource audioSourceOverride;
    [Tooltip("Configures feedback volume.")]
    [SerializeField][Range(0f, 1f)] private float feedbackVolume = 0.85f;
    [Tooltip("Configures jump clip.")]
    [SerializeField] private AudioClip jumpClip;
    [Tooltip("Configures land clip.")]
    [SerializeField] private AudioClip landClip;
    [Tooltip("Configures dash clip.")]
    [SerializeField] private AudioClip dashClip;
    [Tooltip("Configures ground pound start clip.")]
    [SerializeField] private AudioClip groundPoundStartClip;
    [Tooltip("Configures ground pound land clip.")]
    [SerializeField] private AudioClip groundPoundLandClip;
    [Tooltip("Configures slide clip.")]
    [SerializeField] private AudioClip slideClip;
    [Tooltip("Configures teeter clip.")]
    [SerializeField] private AudioClip teeterClip;
    [Tooltip("Configures footstep clip.")]
    [SerializeField] private AudioClip footstepClip;
    [Header("Particles")]
    [Tooltip("Configures particle local position.")]
    [SerializeField] private Vector3 particleLocalPosition = new Vector3(0f, 0.05f, 0f);
    [Tooltip("Configures walking particle prefab.")]
    [SerializeField] private ParticleSystem walkingParticlePrefab;
    [Tooltip("Configures landing particle prefab.")]
    [SerializeField] private ParticleSystem landingParticlePrefab;
    [Tooltip("Configures jump camera kick.")]
    [SerializeField][Min(0f)] private float jumpCameraKick = 0.04f;
    [Tooltip("Configures land camera kick.")]
    [SerializeField][Min(0f)] private float landCameraKick = 0.1f;
    [Tooltip("Configures dash camera kick.")]
    [SerializeField][Min(0f)] private float dashCameraKick = 0.07f;
    [Tooltip("Configures camera kick recover speed.")]
    [SerializeField][Min(0f)] private float cameraKickRecoverSpeed = 6f;
    [Tooltip("Configures enable footsteps.")]
    [SerializeField] private bool enableFootsteps = true;
    [Tooltip("Configures footstep min speed.")]
    [SerializeField][Min(0f)] private float footstepMinSpeed = 2.2f;
    [Tooltip("Configures gamepad jump button.")]
    [SerializeField][Min(0.05f)] private float footstepBaseInterval = 0.45f;

    [Header("Gamepad")]
    [Tooltip("Configures gamepad jump button.")]
    [SerializeField] private bool enableGamepadInput = true;
    [Tooltip("Configures gamepad jump button.")]
    [SerializeField] private KeyCode gamepadJumpButton = KeyCode.JoystickButton0;
    [Tooltip("Configures gamepad sprint button.")]
    [SerializeField] private KeyCode gamepadSprintButton = KeyCode.None;
    [Tooltip("Configures use gamepad left trigger for sprint.")]
    [SerializeField] private bool useGamepadLeftTriggerForSprint = true;
    [Tooltip("Configures gamepad sprint axis.")]
    [SerializeField] private string gamepadSprintAxis = "RightTrigger";
    [Tooltip("Configures gamepad sprint dead zone.")]
    [SerializeField][Range(0f, 0.9f)] private float gamepadSprintDeadZone = 0.1f;
    [Tooltip("Configures gamepad dash button.")]
    [SerializeField] private KeyCode gamepadDashButton = KeyCode.JoystickButton3;
    [Tooltip("Configures gamepad ground pound button.")]
    [SerializeField] private KeyCode gamepadGroundPoundButton = KeyCode.JoystickButton5;
    [Tooltip("Configures gamepad dive button.")]
    [SerializeField] private KeyCode gamepadDiveButton = KeyCode.None;
    [Tooltip("Configures gamepad roll button.")]
    [SerializeField] private KeyCode gamepadRollButton = KeyCode.JoystickButton1;
    [Tooltip("Configures gamepad crouch button.")]
    [SerializeField] private KeyCode gamepadCrouchButton = KeyCode.JoystickButton2;
    [Tooltip("Configures gamepad kill button.")]
    [SerializeField] private KeyCode gamepadKillButton = KeyCode.JoystickButton4;
    [Tooltip("Configures gamepad move xaxis.")]
    [SerializeField] private string gamepadMoveXAxis = "LeftStickX";
    [Tooltip("Configures gamepad move yaxis.")]
    [SerializeField] private string gamepadMoveYAxis = "LeftStickY";
    [Tooltip("Configures gamepad look xaxis.")]
    [SerializeField] private string gamepadLookXAxis = "RightStickX";
    [Tooltip("Configures gamepad look yaxis.")]
    [SerializeField] private string gamepadLookYAxis = "RightStickY";
    [Tooltip("Configures gamepad look sensitivity.")]
    [SerializeField][Min(0f)] private float gamepadLookSensitivity = 185f;
    [Tooltip("Configures gamepad move dead zone.")]
    [SerializeField][Range(0f, 0.9f)] private float gamepadMoveDeadZone = 0.16f;
    [Tooltip("Configures gamepad look dead zone.")]
    [SerializeField][Range(0f, 0.9f)] private float gamepadLookDeadZone = 0.15f;

    [Header("Controls HUD")]
    [Tooltip("Configures show controls overlay.")]
    [SerializeField] private bool showControlsOverlay = true;
    [Tooltip("Configures controls overlay toggle key.")]
    [SerializeField] private KeyCode controlsOverlayToggleKey = KeyCode.F2;

    [Header("Performance HUD")]
    [Tooltip("Configures show state hud.")]
    [SerializeField] private bool showStateHud = true;
    [Tooltip("Configures state hud toggle key.")]
    [SerializeField] private KeyCode stateHudToggleKey = KeyCode.F1;
    [Tooltip("Configures hide and lock cursor.")]
    [SerializeField][Range(0.05f, 0.5f)] private float performanceSampleInterval = 0.18f;

    [Header("Cursor")]
    [Tooltip("Configures hide and lock cursor.")]
    [SerializeField] private bool hideAndLockCursor = true;
    [Tooltip("Configures unlock cursor with escape.")]
    [SerializeField] private bool unlockCursorWithEscape = false;

    [Header("Debug")]
    [Tooltip("Configures show debug overlay.")]
    [SerializeField] private bool showDebugOverlay = false;
    [Tooltip("Configures debug toggle key.")]
    [SerializeField] private KeyCode debugToggleKey = KeyCode.F3;
    [Tooltip("Configures debug gizmo toggle key.")]
    [SerializeField] private KeyCode debugGizmoToggleKey = KeyCode.F4;
    [Tooltip("Configures show debug gizmos.")]
    [SerializeField] private bool showDebugGizmos = false;
    [Tooltip("Configures draw ground probe gizmo.")]
    [SerializeField] private bool drawGroundProbeGizmo = true;
    [Tooltip("Configures draw slope gizmo.")]
    [SerializeField] private bool drawSlopeGizmo = true;
    [Tooltip("Configures draw step gizmo.")]
    [SerializeField] private bool drawStepGizmo = true;
    [Tooltip("Configures draw teeter gizmo.")]
    [SerializeField] private bool drawTeeterGizmo = true;
    [Tooltip("Configures draw teeter event gizmo.")]
    [SerializeField] private bool drawTeeterEventGizmo = true;
    [Tooltip("Configures draw dash gizmo.")]
    [SerializeField] private bool drawDashGizmo = true;
    [Tooltip("Configures draw dive gizmo.")]
    [SerializeField] private bool drawDiveGizmo = true;
    [Tooltip("Configures draw slide gizmo.")]
    [SerializeField] private bool drawSlideGizmo = true;
    [Tooltip("Configures draw action direction gizmo.")]
    [SerializeField] private bool drawActionDirectionGizmo = true;
    [Tooltip("Configures draw jump headroom gizmo.")]
    [SerializeField] private bool drawJumpHeadroomGizmo = true;
    [Tooltip("Configures draw landing gizmo.")]
    [SerializeField] private bool drawLandingGizmo = true;
    [Tooltip("Configures action gizmo history duration.")]
    [SerializeField][Min(0.1f)] private float actionGizmoHistoryDuration = 2.5f;
    [Tooltip("Configures draw camera pivot gizmo.")]
    [SerializeField] private bool drawCameraPivotGizmo = false;
    [Tooltip("Configures show gizmo hover label.")]
    [SerializeField] private bool showGizmoHoverLabel = true;
    [Tooltip("Configures gizmo hover pixel radius.")]
    [SerializeField][Min(4f)] private float gizmoHoverPixelRadius = 18f;
    [Tooltip("Configures gizmo hover label text color.")]
    [SerializeField] private Color gizmoHoverLabelTextColor = Color.white;
    [Tooltip("Configures gizmo hover label background color.")]
    [SerializeField] private Color gizmoHoverLabelBackgroundColor = new Color(0f, 0f, 0f, 0.8f);
    [Tooltip("Configures ground probe gizmo color.")]
    [SerializeField] private Color groundProbeGizmoColor = Color.yellow;
    [Tooltip("Configures slope normal gizmo color.")]
    [SerializeField] private Color slopeNormalGizmoColor = Color.green;
    [Tooltip("Configures slope downhill gizmo color.")]
    [SerializeField] private Color slopeDownhillGizmoColor = Color.red;
    [Tooltip("Configures step clear gizmo color.")]
    [SerializeField] private Color stepClearGizmoColor = Color.green;
    [Tooltip("Configures step blocked gizmo color.")]
    [SerializeField] private Color stepBlockedGizmoColor = Color.red;
    [Tooltip("Configures step surface gizmo color.")]
    [SerializeField] private Color stepSurfaceGizmoColor = Color.cyan;
    [Tooltip("Configures step candidate gizmo color.")]
    [SerializeField] private Color stepCandidateGizmoColor = Color.yellow;
    [Tooltip("Configures teeter grounded gizmo color.")]
    [SerializeField] private Color teeterGroundedGizmoColor = Color.green;
    [Tooltip("Configures teeter open edge gizmo color.")]
    [SerializeField] private Color teeterOpenEdgeGizmoColor = new Color(1f, 0.5f, 0f, 1f);
    [Tooltip("Configures teeter direction gizmo color.")]
    [SerializeField] private Color teeterDirectionGizmoColor = Color.magenta;
    [Tooltip("Configures teeter event gizmo color.")]
    [SerializeField] private Color teeterEventGizmoColor = new Color(1f, 0.45f, 0.1f, 1f);
    [Tooltip("Configures teeter snap back gizmo color.")]
    [SerializeField] private Color teeterSnapBackGizmoColor = new Color(0.2f, 0.9f, 1f, 1f);
    [Tooltip("Configures dash gizmo color.")]
    [SerializeField] private Color dashGizmoColor = new Color(1f, 0.2f, 0.9f, 1f);
    [Tooltip("Configures dive gizmo color.")]
    [SerializeField] private Color diveGizmoColor = new Color(0.25f, 0.75f, 1f, 1f);
    [Tooltip("Configures roll gizmo color.")]
    [SerializeField] private Color rollGizmoColor = new Color(1f, 0.7f, 0.2f, 1f);
    [Tooltip("Configures slide gizmo color.")]
    [SerializeField] private Color slideGizmoColor = new Color(0.35f, 1f, 0.35f, 1f);
    [Tooltip("Configures slope slide gizmo color.")]
    [SerializeField] private Color slopeSlideGizmoColor = new Color(1f, 0.85f, 0.15f, 1f);
    [Tooltip("Configures active slide prediction gizmo color.")]
    [SerializeField] private Color activeSlidePredictionGizmoColor = new Color(1f, 1f, 1f, 0.9f);
    [Tooltip("Configures jump headroom blocked gizmo color.")]
    [SerializeField] private Color jumpHeadroomBlockedGizmoColor = Color.red;
    [Tooltip("Configures jump headroom clear gizmo color.")]
    [SerializeField] private Color jumpHeadroomClearGizmoColor = Color.green;
    [Tooltip("Configures landing gizmo color.")]
    [SerializeField] private Color landingGizmoColor = new Color(1f, 0.8f, 0.2f, 1f);
    [Tooltip("Configures camera pivot gizmo color.")]
    [SerializeField] private Color cameraPivotGizmoColor = Color.cyan;
    [Tooltip("Configures debug state.")]
    [SerializeField] private MovementState debugState = MovementState.Grounded;
    private readonly FrameTiming[] performanceFrameTimings = new FrameTiming[1];
    private float performanceSampleTimer;
    private float displayedFps = 60f;
    private float displayedCpuFrameTimeMs;
    private float displayedGpuFrameTimeMs;
    private float displayedCpuUsagePercent;
    private float displayedGpuUsagePercent;
    private bool hasFrameTimingSample;
    private bool frameTimingUnsupportedLogged;
    private HudInputMode hudInputMode = HudInputMode.KeyboardMouse;
    private Font hudFont;
    private Texture2D hudControlsPanelTexture;
    private Texture2D hudControlsShadowTexture;
    private Texture2D hudPerformancePanelTexture;
    private Texture2D hudPerformanceShadowTexture;
    private GUIStyle hudTitleStyle;
    private GUIStyle hudLabelStyle;
    private GUIStyle hudValueStyle;
    private GUIStyle hudHintStyle;
    private bool hudStylesInitialized;

    private CharacterController controller;
    private Transform cameraTransform;
    private Animator runtimeAnimator;
    private AudioSource runtimeAudioSource;
    private Transform runtimeVisualRoot;

    private Vector2 rawMoveInput;
    private Vector2 moveInput;
    private Vector3 desiredMoveDirection;
    private Vector3 planarVelocity;
    private Vector3 previousPlanarVelocity;
    private Vector3 planarAcceleration;
    private Vector3 dashDirection = Vector3.forward;
    private Vector3 slideDirection = Vector3.forward;
    private Vector3 slideSteerReferenceDirection = Vector3.forward;
    private Vector3 diveDirection = Vector3.forward;
    private Vector3 rollDirection = Vector3.forward;
    private Vector3 teeterEdgeDirection = Vector3.forward;
    private Vector3 teeterFacingDirection = Vector3.forward;
    private Vector3 cameraLookAhead;
    private Vector3 groundNormal = Vector3.up;
    private Vector3 groundHitPoint;
    private Vector3 groundDownhillDirection;
    private Transform activeGroundPlatform;
    private Vector3 activeGroundPlatformLastPosition;
    private Quaternion activeGroundPlatformLastRotation = Quaternion.identity;
    private bool hasActiveGroundPlatformPose;
    private bool movingPlatformParentingActive;
    private Transform movingPlatformParentTransform;

    private Vector3 configuredCameraOffset;
    private Vector3 configuredCameraEuler;
    private Vector3 visualBaseScale = Vector3.one;
    private Vector3 visualBaseLocalPosition = Vector3.zero;
    private Quaternion visualBaseRotation = Quaternion.identity;
    private static readonly Vector3 LockedVisualRootLocalPosition = new Vector3(0f, -0.8f, 0f);
    private static readonly Vector3 StandingControllerCenter = new Vector3(0f, -0.25f, 0f);
    private static readonly Vector3 CrouchControllerCenter = new Vector3(0f, -0.5f, 0f);
    private static readonly Vector3 RollControllerCenter = new Vector3(0f, -0.5f, 0f);
    private static readonly Vector3 SlideControllerCenter = new Vector3(0f, -0.75f, 0f);
    private const float StandingControllerHeight = 1f;
    private const float CrouchControllerHeight = 0.5f;
    private const float RollSlideControllerHeight = 0.25f;
    private const float StandingControllerRadius = 0.25f;
    private const float CrouchControllerRadius = 0.25f;
    private const float RollSlideControllerRadius = 0.125f;
    private const float StandingControllerStepOffset = 0.4f;
    private const float CrouchControllerStepOffset = 0.4f;
    private const float RollSlideControllerStepOffset = 0.2f;

    private float verticalVelocity;
    private float yaw;
    private float pitch;
    private float currentZoomDistance;
    private float targetZoomDistance;
    private float jumpHoldTimer;
    private float dashTimer;
    private float slideTimer;
    private float slideExitLockTimer;
    private float runStopSlideTimer;
    private float diveTimer;
    private float rollTimer;
    private float lastTeeterAudioTime = float.NegativeInfinity;
    private float teeterReentryLockUntilTime = float.NegativeInfinity;
    private float groundPoundDelayTimer;
    private float slopeMomentumTimer;
    private float slopeMomentumSpeed;
    private float diveRollWindowEndTime = float.NegativeInfinity;
    private float pendingDiveRollSpeed;
    private float rollAutoCrouchSuppressUntilTime = float.NegativeInfinity;
    private bool hasPendingJumpingPlatformBounce;
    private Vector3 pendingJumpingPlatformBounceVelocity;
    private float landSquashAmount;
    private float jumpStretchPulse;
    private float dashBlobPulseValue;
    private float cameraKick;
    private float footstepTimer;
    private float currentCameraFov;
    private float targetCameraFov;
    private float dashCameraBoost01;
    private float dashMotionBlurWeight;
    private float windOverlayIntensity;
    private float currentCameraBankAngle;
    private float targetCameraBankAngle;
    private float cameraShakeAmplitude;
    private Vector2 cameraShakeOffset;
    private bool ragdollFreeCameraActive;
    private Vector3 ragdollFreeCameraPosition;
    private float ragdollFreeCameraYaw;
    private float ragdollFreeCameraPitch;
    private bool hasCachedGameplayCameraAnglesBeforeRagdoll;
    private float cachedGameplayYawBeforeRagdoll;
    private float cachedGameplayPitchBeforeRagdoll;
    private float slideLowClearanceLockedSpeed;
    private bool rollCameraPivotLockActive;
    private float rollCameraPivotLockedY;
    private float rollCameraPivotReleaseTimer;
    private bool lowClearanceCameraPivotInitialized;
    private float lowClearanceCameraPivotY;
    private float lowClearanceCameraBlend01;
    private int remainingAirJumps;

    private float standingHeight;
    private Vector3 standingCenter;
    private float standingRadius;
    private float crouchHeightResolved;
    private float groundSlopeAngle;
    private float lastSlopeSpeedFactor = 1f;

    private float lastGroundedTime = float.NegativeInfinity;
    private float lastJumpPressedTime = float.NegativeInfinity;
    private float jumpLandingCooldownUnlockTime = float.NegativeInfinity;
    private float jumpCutProtectedVelocity;
    private float lastDashPressedTime = float.NegativeInfinity;
    private float lastGroundPoundPressedTime = float.NegativeInfinity;
    private float lastSlidePressedTime = float.NegativeInfinity;
    private float lastDivePressedTime = float.NegativeInfinity;
    private float lastRollPressedTime = float.NegativeInfinity;
    private int rollInputVersion;
    private int consumedRollInputVersion;
    private float lastDashTime = float.NegativeInfinity;
    private float stopAnimationTimer;
    private bool stopPrimedFromAnimatorSpeed;
    private bool hadMovementInputLastFrame;
    private float landingAnimatorPulseTimer;
    private float runtimeAnimSpeedValue;
    private bool dashStartedFromStandstill;
    private float runtimeAnimMoveSpeedValue;
    private float runtimeAnimVerticalSpeedValue;
    private float runtimeAnimatorPlaybackSpeed = 1f;
    private float dashGhostSpawnTimer;
    private float dashJumpCancelCooldownUntilTime = float.NegativeInfinity;
    private float wallJumpCameraTurnRemaining;
    private float animatorSpeedSmoothed;
    private bool runtimeAnimFloatInitialized;
    private Vector3 lastAnimatorSamplePosition;
    private bool hasAnimatorSamplePosition;
    private bool useConfiguredCameraStart;
    private bool isGrounded;
    private bool wasGrounded;
    private bool jumpConsumed;
    private bool sprintHeld;
    private float sprintInputAmount;
    private bool crouchPressedThisFrame;
    private bool crouchHeld;
    private bool crouchTarget;
    private bool rollStartedFromCrouch;
    private bool wallHangActive;
    private bool ledgeHangActive;
    private bool ledgeClimbActive;
    private bool ragdollRuntimeActive;
    private Transform runtimeRagdollRoot;
    private Rigidbody[] runtimeRagdollBodies = Array.Empty<Rigidbody>();
    private Collider[] runtimeRagdollColliders = Array.Empty<Collider>();
    private bool[] ragdollColliderEnabledBeforeActivation = Array.Empty<bool>();
    private bool[] ragdollBodyKinematicBeforeActivation = Array.Empty<bool>();
    private bool[] ragdollBodyUseGravityBeforeActivation = Array.Empty<bool>();
    private bool[] ragdollBodyDetectCollisionsBeforeActivation = Array.Empty<bool>();
    private Vector3 wallHangNormal = Vector3.back;
    private bool wallJumpNoRotateActive;
    private bool wallJumpForwardBackLockActive;
    private Vector3 wallJumpForcedDirection = Vector3.back;
    private bool forceRollOnWallJumpLanding;
    private bool lockFacingUntilGroundInputAfterWallJump;
    private bool cursorUnlockedByEscape;
    private bool hasGroundNormal;
    private bool apexAssistActive;
    private bool awaitingDiveRoll;
    private bool autoRollFromMovingJump;
    private bool rollAirReentryPending;
    private bool rollAirHighFallTriggered;
    private float rollAirStartBottomY = float.NegativeInfinity;
    private float rollAirStartTime = float.NegativeInfinity;
    private bool rollDirectionSteerLockedForCurrentRoll;
    private bool slideLowClearanceLockActive;
    private bool jumpFallTrackingActive;
    private float jumpFallStartBottomY = float.NegativeInfinity;
    private float jumpFallHighestBottomY = float.NegativeInfinity;
    private float jumpFallStartTime = float.NegativeInfinity;
    private bool didPerformAirJumpSinceGrounded;
    private bool doubleJumpDashRollOnLandingPending;
    private bool doubleJumpApexTrackingActive;
    private float doubleJumpApexBottomY = float.NegativeInfinity;
    private float nonJumpFallStartBottomY = float.NegativeInfinity;
    private float nonJumpFallStartTime = float.NegativeInfinity;
    private bool trackingNonJumpFall;
    private bool nonJumpHighFallReached;
    private bool landingHandledThisFrame;
    private bool debugStepLowerBlocked;
    private bool debugStepUpperBlocked;
    private bool debugStepCandidateFound;
    private bool debugTeeterHasGroundAhead;
    private bool jumpMirrorRuntimeValue;
    private bool forceJumpMirrorToggleFromHangJump;
    private bool dashMirrorRuntimeValue;
    private bool climbJumpAnimatorActive;
    private bool forceFallAnimatorFromClimbJump;
    private bool wallHangReleaseHighFallCheckActive;
    private bool jumpAnimatorWasActive;
    private bool backflipAnimatorHoldActive;
    private bool backflipAnimatorWasActive;
    private bool stopAnimatorWasActive;
    private bool wallHangBlockedForCurrentJump;
    private bool wallHangBlockedUntilFalling;
    private bool groundPoundHardFallActive;
    private bool groundPoundHardLandActive;
    private bool groundPoundStartedFromMovement;
    private bool groundPoundBounceLockedUntilNextJump;
    private bool backflipJumpActive;
    private ParticleSystem walkingParticleInstance;
    private CollisionFlags lastMoveCollisionFlags;
    private float groundPoundHardLandReleaseTime = float.NegativeInfinity;
    private float wallInteractionUnlockFeetY = float.NegativeInfinity;
    private float wallHangRegrabLockUntilTime = float.NegativeInfinity;
    private float hangJumpCornerSnapAssistUntilTime = float.NegativeInfinity;
    private float wallHangSlideEaseElapsed;
    private float ledgeHangClimbUnlockTime = float.NegativeInfinity;
    private float ledgeHangInputLockUntilTime = float.NegativeInfinity;
    private float ledgeHangLastValidContactTime = float.NegativeInfinity;
    private float ledgeHangLateralInputRuntime;
    private float ledgeClimbProgress;
    private MovementState currentState;
    private Vector3 ledgeClimbStartPosition;
    private Vector3 ledgeClimbMidPosition;
    private Vector3 ledgeClimbTargetPosition;
    private Vector3 ledgeHangTopPoint;

    private Vector3 debugStepLowerOrigin;
    private Vector3 debugStepUpperOrigin;
    private Vector3 debugStepProbeOrigin;
    private Vector3 debugStepHitPoint;
    private Vector3 debugStepDirection = Vector3.forward;
    private Vector3 debugTeeterProbeOrigin;
    private Vector3 debugTeeterEdgeDirection = Vector3.forward;
    private Vector3 debugTeeterHitPoint;
    private float debugStepProbeDistance;
    private float debugStepProbeRadius;
    private float debugTeeterProbeRadius;
    private float debugTeeterProbeDistance;
    private Vector3 debugTeeterEnterPoint;
    private Vector3 debugTeeterSnapTargetPoint;
    private Vector3 debugTeeterEnterDirection = Vector3.forward;
    private float debugTeeterEnterTime = float.NegativeInfinity;
    private bool debugTeeterHadSnapBack;

    private Vector3 debugDashStartPoint;
    private Vector3 debugDashEndPoint;
    private Vector3 debugDashDirection = Vector3.forward;
    private float debugDashStartTime = float.NegativeInfinity;
    private float debugDashStartSpeed;

    private Vector3 debugDiveStartPoint;
    private Vector3 debugDivePredictedEndPoint;
    private Vector3 debugDiveDirection = Vector3.forward;
    private float debugDiveStartTime = float.NegativeInfinity;
    private float debugDiveStartSpeed;
    private float debugDiveStartVerticalSpeed;

    private Vector3 debugSlideStartPoint;
    private Vector3 debugSlidePredictedEndPoint;
    private Vector3 debugSlideDirection = Vector3.forward;
    private float debugSlideStartTime = float.NegativeInfinity;
    private float debugSlideStartSpeed;
    private bool debugSlideWasSlope;

    private Vector3 debugLastLandingPoint;
    private float debugLastLandingTime = float.NegativeInfinity;
    private float debugLastLandingImpactSpeed;
    private int cachedGroundLayerIndex = int.MinValue;

    private readonly HashSet<string> missingInputAxisNames = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> missingInputButtonNames = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<KeyCode, bool> gamepadButtonHeldCache = new Dictionary<KeyCode, bool>();
    private readonly Dictionary<KeyCode, bool> gamepadButtonDownCache = new Dictionary<KeyCode, bool>();
    private readonly Dictionary<KeyCode, bool> gamepadButtonUpCache = new Dictionary<KeyCode, bool>();
    private readonly Dictionary<KeyCode, bool> gamepadButtonPreviousHeld = new Dictionary<KeyCode, bool>();
    private readonly Dictionary<string, AnimatorControllerParameterType> animatorParameterTypes = new Dictionary<string, AnimatorControllerParameterType>(StringComparer.Ordinal);
    private readonly Collider[] ledgeOverlapBuffer = new Collider[LedgeOverlapBufferSize];
    private readonly Collider[] teeterOverlapBuffer = new Collider[TeeterOverlapBufferSize];
    private readonly Collider[] standUpTargetOverlapBuffer = new Collider[StandUpOverlapBufferSize];
    private readonly RaycastHit[] physicsHitBuffer = new RaycastHit[PhysicsHitBufferSize];
    private readonly List<DashGhostInstance> activeDashGhosts = new List<DashGhostInstance>();
    private readonly List<DashGhostInstance> dashGhostPool = new List<DashGhostInstance>();
    private readonly Dictionary<Material, Material> dashGhostMaterialCache = new Dictionary<Material, Material>();
    private DashGhostSourceEntry[] dashGhostSourceEntries = Array.Empty<DashGhostSourceEntry>();
    private MaterialPropertyBlock dashGhostPropertyBlock;
    private MaterialPropertyBlock dashGhostSourcePropertyBlock;
    private Transform dashGhostPoolRoot;
    private Transform dashGhostPoolSourceRoot;
    private int nextDashGhostPoolIndex;
    private const HideFlags DashGhostHideFlags = HideFlags.HideInHierarchy;
    private int gamepadButtonCacheFrame = -1;

    public void SetupCamera(Transform cameraRef, bool placeAtStart, Vector3 cameraOffset, Vector3 cameraEuler)
    {
        cameraTransform = cameraRef;
        useConfiguredCameraStart = placeAtStart;
        configuredCameraOffset = cameraOffset;
        configuredCameraEuler = cameraEuler;
    }

    public void ConfigureAnimator(Animator animator, bool enableParameterDriving)
    {
        animatorOverride = animator;
        useAnimatorParameters = enableParameterDriving;
        runtimeAnimator = animatorOverride;
        ApplyAnimatorDefaults();
    }

    public void SetMovingPlatformParentingState(bool attached, Transform platformTransform)
    {
        if (!attached || platformTransform == null)
        {
            movingPlatformParentingActive = false;
            movingPlatformParentTransform = null;
            return;
        }

        movingPlatformParentingActive = true;
        movingPlatformParentTransform = platformTransform;

        // Clear accumulated vertical speed while parent mode is active; platform movement carries the character.
        verticalVelocity = 0f;
        jumpHoldTimer = 0f;
    }

    public Vector3 GetCurrentVelocityWorld()
    {
        Vector3 upAxis = ResolveVerticalMotionUpAxis();
        if (upAxis.sqrMagnitude <= 0.0001f)
        {
            upAxis = Vector3.up;
        }

        return planarVelocity + upAxis.normalized * verticalVelocity;
    }

    private void QueueJumpingPlatformBounce(Vector3 bounceVelocity)
    {
        if (bounceVelocity.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        if (!hasPendingJumpingPlatformBounce || bounceVelocity.sqrMagnitude > pendingJumpingPlatformBounceVelocity.sqrMagnitude)
        {
            pendingJumpingPlatformBounceVelocity = bounceVelocity;
            hasPendingJumpingPlatformBounce = true;
        }
    }

    private void ConsumePendingJumpingPlatformBounce()
    {
        if (!hasPendingJumpingPlatformBounce)
        {
            return;
        }

        Vector3 bounceVelocity = pendingJumpingPlatformBounceVelocity;
        hasPendingJumpingPlatformBounce = false;
        pendingJumpingPlatformBounceVelocity = Vector3.zero;
        TryApplyJumpingPlatformBounceVelocity(bounceVelocity);
    }

    private bool TryApplyJumpingPlatformBounceVelocity(Vector3 worldBounceVelocity)
    {
        if (ragdollRuntimeActive)
        {
            return false;
        }

        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
        }

        if (controller == null || !controller.enabled)
        {
            return false;
        }

        if (worldBounceVelocity.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        Vector3 upAxis = ResolveVerticalMotionUpAxis();
        if (upAxis.sqrMagnitude <= 0.0001f)
        {
            upAxis = Vector3.up;
        }

        upAxis.Normalize();

        ForceExitCrouchForAction();
        ClearRollAirTracking();
        ClearGroundPoundRuntimeFlagsForJump();
        ForceEndDashOnJump();
        ResetDoubleJumpApexTracking();

        verticalVelocity = Vector3.Dot(worldBounceVelocity, upAxis);
        planarVelocity = Vector3.ProjectOnPlane(worldBounceVelocity, upAxis);
        jumpCutProtectedVelocity = Mathf.Max(0f, verticalVelocity);
        jumpHoldTimer = 0f;
        jumpStretchPulse = Mathf.Max(jumpStretchPulse, 1f);
        jumpConsumed = true;
        doubleJumpDashRollOnLandingPending = false;
        DetachFromMovingPlatformForAirborneAction();

        bool blockedFromStartWallContact = IsTouchingWallForJumpHangBlock();
        wallHangBlockedForCurrentJump = blockedFromStartWallContact;
        wallHangBlockedUntilFalling = blockedFromStartWallContact;
        ResetNonJumpFallTracking();
        autoRollFromMovingJump = false;
        isGrounded = false;
        slideExitLockTimer = 0f;
        lastJumpPressedTime = float.NegativeInfinity;
        lastGroundedTime = float.NegativeInfinity;
        awaitingDiveRoll = false;
        pendingDiveRollSpeed = 0f;
        diveRollWindowEndTime = float.NegativeInfinity;
        lockFacingUntilGroundInputAfterWallJump = false;
        backflipJumpActive = false;
        if (currentState == MovementState.SlopeSliding || slopeMomentumTimer > 0f)
        {
            planarVelocity *= slopeJumpSpeedMultiplier;
            slopeMomentumTimer = 0f;
            slopeMomentumSpeed = 0f;
        }

        BeginJumpFallTracking();
        bool upwardBounce = verticalVelocity > 0.01f;
        SetState(upwardBounce ? MovementState.Jumping : MovementState.Falling);
        SetFallAnimatorState(!upwardBounce);
        if (upwardBounce)
        {
            ToggleJumpMirrorOnJump();
            TriggerAnimator(animJumpTrigger);
            PlayClip(jumpClip);
            AddCameraKick(jumpCameraKick);
        }

        return true;
    }

    private bool IsParentedToMovingPlatform()
    {
        if (!movingPlatformParentingActive || movingPlatformParentTransform == null)
        {
            movingPlatformParentingActive = false;
            movingPlatformParentTransform = null;
            return false;
        }

        if (!transform.IsChildOf(movingPlatformParentTransform))
        {
            movingPlatformParentingActive = false;
            movingPlatformParentTransform = null;
            return false;
        }

        return true;
    }

    private void DetachFromMovingPlatformForAirborneAction()
    {
        if (TryGetComponent(out MinimoMovingPlatformRider platformRider))
        {
            platformRider.DetachForAirborneAction();
        }
        else
        {
            SetMovingPlatformParentingState(false, null);
        }
    }

    public void SetPreset(MinimoTpsPreset preset, bool applyNow)
    {
        gameplayPreset = preset;
        if (applyNow && gameplayPreset != null)
        {
            ApplyPreset(gameplayPreset);
        }
    }

    public void ApplyAssignedPresetIfEnabled()
    {
        if (!applyPresetOnStart || gameplayPreset == null)
        {
            return;
        }

        ApplyPreset(gameplayPreset);
    }

    public void RefreshControllerShapeCacheFromCurrentController()
    {
        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
        }

        ApplyLockedControllerStandingShape();
        CacheStandingShape();
    }

    public void ApplyPreset(MinimoTpsPreset preset)
    {
        if (preset == null)
        {
            return;
        }

        moveSpeed = preset.moveSpeed;
        groundAcceleration = preset.groundAcceleration;
        turnAcceleration = preset.turnAcceleration;
        groundDeceleration = preset.groundDeceleration;
        airAcceleration = preset.airAcceleration;
        airControl = Mathf.Clamp01(preset.airControl);
        reverseDirectionAccelerationMultiplier = preset.reverseDirectionAccelerationMultiplier;
        rotationSharpness = preset.rotationSharpness;
        snappyReverseTurnMultiplier = Mathf.Max(1f, preset.snappyReverseTurnMultiplier);
        reversePivotThreshold = Mathf.Clamp(preset.reversePivotThreshold, -1f, 0f);
        pivotBrakeDeceleration = Mathf.Max(0f, preset.pivotBrakeDeceleration);
        jumpHeight = preset.jumpHeight;
        gravity = preset.gravity;
        jumpHoldTime = preset.jumpHoldTime;
        jumpHoldForce = preset.jumpHoldForce;
        fallGravityMultiplier = preset.fallGravityMultiplier;
        fastFallGravityMultiplier = preset.fastFallGravityMultiplier;
        fallAnimationVerticalSpeedThreshold = Mathf.Max(0f, preset.fallAnimationVerticalSpeedThreshold);
        apexVerticalSpeedThreshold = Mathf.Max(0f, preset.apexVerticalSpeedThreshold);
        apexGravityMultiplier = Mathf.Clamp(preset.apexGravityMultiplier, 0.1f, 1f);
        terminalVelocity = preset.terminalVelocity;
        coyoteTime = preset.coyoteTime;
        jumpBufferTime = preset.jumpBufferTime;
        minimumJumpHeight = preset.minimumJumpHeight;
        jumpCutMultiplier = preset.jumpCutMultiplier;
        jumpCeilingCheckDistance = preset.jumpCeilingCheckDistance;
        groundedVerticalForce = preset.groundedVerticalForce;
        enableDoubleJump = preset.enableDoubleJump;
        maxAirJumps = Mathf.Max(0, preset.maxAirJumps);
        airJumpHeightMultiplier = Mathf.Clamp(preset.airJumpHeightMultiplier, 0.25f, 1.5f);
        slopeSpeedInfluence = Mathf.Clamp01(preset.slopeSpeedInfluence);
        uphillSpeedPenalty = Mathf.Clamp01(preset.uphillSpeedPenalty);
        downhillSpeedBoost = Mathf.Clamp01(preset.downhillSpeedBoost);
        slopeAntiSlideDeceleration = Mathf.Max(0f, preset.slopeAntiSlideDeceleration);
        slopeAlignmentSharpness = Mathf.Max(0f, preset.slopeAlignmentSharpness);
        maxStepHeight = Mathf.Max(0f, preset.maxStepHeight);
        stepCheckDistance = Mathf.Max(0.01f, preset.stepCheckDistance);
        enableCharacterFeel = preset.enableCharacterFeel;
        enableJumpBlobEffect = preset.enableJumpBlobEffect;
        jumpStretch = Mathf.Max(0f, preset.jumpStretch);
        jumpStretchDecaySpeed = Mathf.Max(0f, preset.jumpStretchDecaySpeed);
        jumpBlobHorizontalCompression = Mathf.Max(0f, preset.jumpBlobHorizontalCompression);
        jumpBlobVerticalStretch = Mathf.Max(0f, preset.jumpBlobVerticalStretch);
        jumpBlobVerticalVelocityInfluence = Mathf.Max(0f, preset.jumpBlobVerticalVelocityInfluence);
        dashSpeed = preset.dashSpeed;
        dashDuration = preset.dashDuration;
        dashCooldown = preset.dashCooldown;
        groundPoundSpeed = preset.groundPoundSpeed;
        groundPoundAcceleration = preset.groundPoundAcceleration;
        crouchHeight = preset.crouchHeight;
        float presetCrouchMultiplier = preset.crouchWalkSpeedMultiplier <= 0f
            ? 0.5f
            : preset.crouchWalkSpeedMultiplier;
        crouchWalkSpeedMultiplier = Mathf.Clamp(presetCrouchMultiplier, 0.05f, 1f);
        slideDuration = preset.slideDuration;
        slideInitialBoost = preset.slideInitialBoost;
        slideDeceleration = preset.slideDeceleration;
        diveForwardSpeed = preset.diveForwardSpeed;
        diveForwardBoost = preset.diveForwardBoost;
        diveDownwardSpeed = preset.diveDownwardSpeed;
        diveGravityMultiplier = preset.diveGravityMultiplier;
        diveDuration = preset.diveDuration;
        rollBaseSpeed = preset.rollBaseSpeed;
        rollMomentumConversion = preset.rollMomentumConversion;
        rollDuration = preset.rollDuration;
        rollDurationMultiplier = preset.rollDurationMultiplier;
        rollLandingResumeDurationRatio = preset.rollLandingResumeDurationRatio;
        rollLandingResumeMaxDuration = preset.rollLandingResumeMaxDuration;
        rollDeceleration = preset.rollDeceleration;
        slopeSlideMinAngle = preset.slopeSlideMinAngle;
        slopeSlideEnterMinSpeed = preset.slopeSlideEnterMinSpeed;
        slopeSlideDownhillAcceleration = preset.slopeSlideDownhillAcceleration;
        slopeSlideFriction = preset.slopeSlideFriction;
        slopeSlideMaxSpeed = preset.slopeSlideMaxSpeed;
        slopeMomentumCarryDuration = preset.slopeMomentumCarryDuration;
        slopeMomentumRetention = preset.slopeMomentumRetention;
        slopeMomentumDecay = preset.slopeMomentumDecay;
        slopeJumpSpeedMultiplier = preset.slopeJumpSpeedMultiplier;
        followDistance = preset.followDistance;
        minZoomDistance = preset.minZoomDistance;
        maxZoomDistance = preset.maxZoomDistance;
        mouseXSensitivity = preset.mouseXSensitivity;
        mouseYSensitivity = preset.mouseYSensitivity;
        baseCameraFov = preset.baseCameraFov;
        maxBoostCameraFov = preset.maxBoostCameraFov;
        airDashFovWeight = preset.airDashFovWeight;
        slopeSlideFovWeight = preset.slopeSlideFovWeight;
        fovSpeedReference = preset.fovSpeedReference;
        speedFovWeight = Mathf.Max(0f, preset.speedFovWeight);
        landingCameraShakeMultiplier = Mathf.Max(0f, preset.landingCameraShakeMultiplier);
        if (preset.leftHandLedgeTransform != null)
        {
            leftHandLedgeTransform = preset.leftHandLedgeTransform;
        }

        if (preset.rightHandLedgeTransform != null)
        {
            rightHandLedgeTransform = preset.rightHandLedgeTransform;
        }

        if (preset.leftHandLedgeProbeRadius > 0f)
        {
            leftHandLedgeProbeRadius = Mathf.Max(0.005f, preset.leftHandLedgeProbeRadius);
        }

        if (preset.rightHandLedgeProbeRadius > 0f)
        {
            rightHandLedgeProbeRadius = Mathf.Max(0.005f, preset.rightHandLedgeProbeRadius);
        }
        diveDuration = Mathf.Max(0.01f, diveDuration);
        rollDuration = Mathf.Max(0.01f, rollDuration);
        rollDurationMultiplier = Mathf.Clamp(rollDurationMultiplier, 1f, 2f);
        rollLandingResumeDurationRatio = Mathf.Clamp(rollLandingResumeDurationRatio, 0.25f, 1f);
        rollLandingResumeMaxDuration = Mathf.Max(0.1f, rollLandingResumeMaxDuration);
        slopeSlideMinAngle = Mathf.Clamp(slopeSlideMinAngle, 0f, 89f);
        slopeSlideExitFlatAngle = Mathf.Clamp(slopeSlideExitFlatAngle, 0f, 89f);
        maxBoostCameraFov = Mathf.Max(baseCameraFov, maxBoostCameraFov);
        fovSpeedReference = Mathf.Max(0.1f, fovSpeedReference);
        ClampJumpSettings();

        if (controller != null)
        {
            CacheStandingShape();
        }

        ResetAirJumps();
        targetZoomDistance = GetNearestZoomLevelDistance(followDistance);
        if (currentZoomDistance <= 0.001f)
        {
            currentZoomDistance = targetZoomDistance;
        }
    }

    private void ApplySelectedControlProfile()
    {
        if (controlProfile != ControlProfile.ItTakesTwoInspired)
        {
            return;
        }

        sprintKey = KeyCode.LeftShift;
        dashKey = KeyCode.Q;
        diveKey = KeyCode.E;
        rollKey = KeyCode.LeftAlt;
        groundPoundKey = KeyCode.LeftControl;
        crouchKey = KeyCode.C;

        ApplyStrictXboxGamepadBindings();
    }

    private void ApplyStrictXboxGamepadBindings()
    {
        gamepadJumpButton = KeyCode.JoystickButton0;        // A
        gamepadSprintButton = KeyCode.None;                 // RT analog sprint
        useGamepadLeftTriggerForSprint = true;
        gamepadSprintAxis = "RightTrigger";
        gamepadDashButton = KeyCode.JoystickButton3;        // Y
        gamepadGroundPoundButton = KeyCode.JoystickButton5; // RB
        gamepadDiveButton = KeyCode.None;                   // Gamepad dive disabled
        gamepadRollButton = KeyCode.JoystickButton1;        // B
        gamepadCrouchButton = KeyCode.JoystickButton2;      // X
        gamepadKillButton = KeyCode.JoystickButton4;        // LB
    }

    private void Reset()
    {
        controller = GetComponent<CharacterController>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying
            || EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling
            || EditorApplication.isUpdating
            || EditorUtility.IsPersistent(this))
        {
            return;
        }

        cachedGroundLayerIndex = int.MinValue;
        missingInputAxisNames.Clear();
        missingInputButtonNames.Clear();
        ResetGamepadButtonCaches();
        ResolveInputConflicts();
        ClampJumpSettings();
        controller = GetComponent<CharacterController>();
        CacheStandingShape();

        runtimeAnimator = animatorOverride;
        if (runtimeAnimator == null)
        {
            runtimeAnimator = GetComponentInChildren<Animator>(true);
        }

        ApplyAnimatorDefaults();
        if (useAnimatorParameters)
        {
            UpdateAnimatorParameters();
        }
    }
#endif

    private void Awake()
    {
        cachedGroundLayerIndex = int.MinValue;
        missingInputAxisNames.Clear();
        missingInputButtonNames.Clear();
        ResetGamepadButtonCaches();
        EnsureMovingPlatformRiderComponent();
        controller = GetComponent<CharacterController>();
        if (applyControlProfileOnStart)
        {
            ApplySelectedControlProfile();
        }

        ApplyAssignedPresetIfEnabled();
        ClampJumpSettings();
        ConsumeLegacyTuningReferences();
        ApplyLockedControllerStandingShape();

        ResolveInputConflicts();
        CacheStandingShape();
        ResolveRuntimeReferences();
    }

    private void EnsureMovingPlatformRiderComponent()
    {
        if (!TryGetComponent(out MinimoMovingPlatformRider _))
        {
            gameObject.AddComponent<MinimoMovingPlatformRider>();
        }
    }

    private void OnEnable()
    {
        ResetGamepadButtonCaches();
        ApplyCursorState();
    }

    private void OnDisable()
    {
        ResetGamepadButtonCaches();
        hasPendingJumpingPlatformBounce = false;
        pendingJumpingPlatformBounceVelocity = Vector3.zero;
        DestroyWalkingParticleInstance();
        ClearDashGhostInstances();
        ClearDashGhostMaterialCache();

        if (cameraTransform != null)
        {
            Camera runtimeCamera = cameraTransform.GetComponent<Camera>();
            if (runtimeCamera != null && enableSpeedFov)
            {
                runtimeCamera.fieldOfView = baseCameraFov;
            }
        }

        dashCameraBoost01 = 0f;
        dashMotionBlurWeight = 0f;
        if (dashMotionBlurVolume != null)
        {
            SetDashMotionBlurVolumeWeight(0f);
        }

        if (!hideAndLockCursor)
        {
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        ClearDashGhostInstances();
        ClearDashGhostMaterialCache();
        ReleaseHudResources();
    }

    private void Start()
    {
        RefreshControllerShapeCacheFromCurrentController();

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (cameraTransform != null && useConfiguredCameraStart)
        {
            Vector3 pivot = GetCameraPivot();
            Quaternion startRotation = Quaternion.Euler(configuredCameraEuler);
            cameraTransform.position = pivot + startRotation * configuredCameraOffset;
            cameraTransform.rotation = startRotation;
        }

        if (cameraTransform != null)
        {
            Vector3 cameraEuler = cameraTransform.eulerAngles;
            yaw = cameraEuler.y;
            pitch = NormalizePitch(cameraEuler.x);
        }
        else
        {
            yaw = transform.eulerAngles.y;
            pitch = 10f;
        }

        targetZoomDistance = GetNearestZoomLevelDistance(followDistance);
        currentZoomDistance = targetZoomDistance;
        Camera runtimeCamera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
        if (runtimeCamera != null)
        {
            if (baseCameraFov <= 0f)
            {
                baseCameraFov = runtimeCamera.fieldOfView;
            }

            currentCameraFov = enableSpeedFov ? baseCameraFov : runtimeCamera.fieldOfView;
            targetCameraFov = currentCameraFov;
            runtimeCamera.fieldOfView = currentCameraFov;
        }
        else
        {
            currentCameraFov = baseCameraFov;
            targetCameraFov = baseCameraFov;
        }

        dashCameraBoost01 = 0f;
        dashMotionBlurWeight = 0f;
        if (dashMotionBlurVolume != null)
        {
            SetDashMotionBlurVolumeWeight(0f);
        }

        InitializeVisualRoot();
        ForceLockVisualRootLocalPosition();
        UpdateGroundState();
        currentState = isGrounded ? MovementState.Grounded : MovementState.Falling;
        ResetAirJumps();
        debugState = currentState;
        wasGrounded = isGrounded;
        ApplyCursorState();
        UpdateAnimatorParameters();
    }

    private void Update()
    {
        UpdateHudInputMode();
        HandleDebugToggle();
        UpdatePerformanceHudMetrics();
        HandleRagdollInput();
        if (ragdollRuntimeActive)
        {
            ApplyCursorState();
            return;
        }

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f || controller == null || !controller.enabled)
        {
            return;
        }

        landingHandledThisFrame = false;
        ReadInput();
        ApplyMovingPlatformMotion(deltaTime);
        UpdateGroundState();
        EvaluateTeeteringState();
        ProcessBufferedActions();
        UpdatePlanarVelocity(deltaTime);
        EvaluateTeeteringState();
        planarAcceleration = (planarVelocity - previousPlanarVelocity) / Mathf.Max(deltaTime, 0.0001f);
        ApplyJumpCut();
        ApplyGravity(deltaTime);
        UpdateDoubleJumpApexTracking();
        float verticalBeforeMove = verticalVelocity;
        MoveCharacter(deltaTime, verticalBeforeMove);
        TryHandleLandingFallback(verticalBeforeMove);
        UpdateJumpFallPeakTracking();
        RefreshMovementState();
        UpdateWallJumpToRegularJumpTransition();
        UpdateClimbJumpAnimatorWallContactState();
        UpdateGroundPoundHardLandingState();
        UpdateCharacterFacing(deltaTime);
        UpdateCrouchShape(deltaTime);
        UpdateCharacterVisuals(deltaTime, verticalBeforeMove);
        ForceLockVisualRootLocalPosition();
        UpdateDashGhostTrail(deltaTime);
        UpdateAnimatorParameters();
        UpdateFeedback(deltaTime);
        UpdateParticleRuntime();
        UpdateZoomInput(deltaTime);
        ApplyCursorState();

        previousPlanarVelocity = planarVelocity;
        wasGrounded = isGrounded;
        debugState = currentState;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit == null || hit.collider == null)
        {
            return;
        }

        TryQueueJumpingPlatformBounceFromCollider(hit.collider, hit.normal);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
        {
            return;
        }

        // Apply the same bounce flow for trigger colliders so jump platform contacts are not missed.
        TryQueueJumpingPlatformBounceFromCollider(other, Vector3.up);
    }

    private void TryQueueJumpingPlatformBounceFromCollider(Collider hitCollider, Vector3 hitNormal)
    {
        MinimoJumpingPlatform jumpingPlatform = hitCollider.GetComponentInParent<MinimoJumpingPlatform>();
        if (jumpingPlatform == null)
        {
            return;
        }

        if (jumpingPlatform.TryComputeBounceVelocity(this, hitCollider, hitNormal, out Vector3 bounceVelocity))
        {
            QueueJumpingPlatformBounce(bounceVelocity);
        }
    }

    private void HandleRagdollInput()
    {
        bool keyboardKillPressed = MinimoInputBridge.GetKeyDown(RagdollToggleKey);
        bool gamepadKillPressed = IsGamepadButtonDown(gamepadKillButton);
        if (keyboardKillPressed || gamepadKillPressed)
        {
            if (ragdollRuntimeActive)
            {
                DeactivateRagdoll();
                return;
            }

            ActivateRagdoll();
        }
    }

    private void ActivateRagdoll()
    {
        Transform ragdollRoot = ResolveRagdollRoot();
        if (ragdollRoot == null)
        {
            return;
        }

        CacheRagdollRuntimeState(ragdollRoot);
        SetRagdollCollidersEnabled(runtimeRagdollColliders, true);
        SetRagdollRigidbodiesDynamic(runtimeRagdollBodies);
        ApplyRagdollBurstForces(runtimeRagdollBodies, ragdollRoot.position);

        if (runtimeAnimator != null)
        {
            runtimeAnimator.enabled = false;
        }

        if (controller != null)
        {
            controller.enabled = false;
        }

        MinimoMovingPlatformRider movingPlatformRider = GetComponent<MinimoMovingPlatformRider>();
        if (movingPlatformRider != null)
        {
            movingPlatformRider.enabled = false;
        }

        rawMoveInput = Vector2.zero;
        moveInput = Vector2.zero;
        desiredMoveDirection = Vector3.zero;
        // Clear any remaining dash or slide ghost trails immediately when entering ragdoll.
        ClearDashGhostInstances();
        dashGhostSpawnTimer = 0f;
        planarVelocity = Vector3.zero;
        previousPlanarVelocity = Vector3.zero;
        verticalVelocity = 0f;
        EnterRagdollFreeCameraMode();
        ragdollRuntimeActive = true;
    }

    private void DeactivateRagdoll()
    {
        ExitRagdollFreeCameraMode();
        RestoreRagdollRigidbodies();
        RestoreRagdollColliders();
        ragdollRuntimeActive = false;

        if (controller != null)
        {
            controller.enabled = true;
        }

        MinimoMovingPlatformRider movingPlatformRider = GetComponent<MinimoMovingPlatformRider>();
        if (movingPlatformRider != null)
        {
            movingPlatformRider.enabled = true;
        }

        if (runtimeAnimator != null)
        {
            runtimeAnimator.enabled = true;
            runtimeAnimator.Rebind();
            runtimeAnimator.Update(0f);
        }

        rawMoveInput = Vector2.zero;
        moveInput = Vector2.zero;
        desiredMoveDirection = Vector3.zero;
        planarVelocity = Vector3.zero;
        previousPlanarVelocity = Vector3.zero;
        verticalVelocity = groundedVerticalForce;

        UpdateGroundState();
        currentState = isGrounded ? MovementState.Grounded : MovementState.Falling;
        ResetAirJumps();
        ApplyAnimatorDefaults();
        UpdateAnimatorParameters();
    }

    private void CacheRagdollRuntimeState(Transform ragdollRoot)
    {
        runtimeRagdollRoot = ragdollRoot;
        runtimeRagdollColliders = ragdollRoot.GetComponentsInChildren<Collider>(true);
        runtimeRagdollBodies = ragdollRoot.GetComponentsInChildren<Rigidbody>(true);

        ragdollColliderEnabledBeforeActivation = new bool[runtimeRagdollColliders.Length];
        for (int i = 0; i < runtimeRagdollColliders.Length; i++)
        {
            ragdollColliderEnabledBeforeActivation[i] = runtimeRagdollColliders[i].enabled;
        }

        ragdollBodyKinematicBeforeActivation = new bool[runtimeRagdollBodies.Length];
        ragdollBodyUseGravityBeforeActivation = new bool[runtimeRagdollBodies.Length];
        ragdollBodyDetectCollisionsBeforeActivation = new bool[runtimeRagdollBodies.Length];
        for (int i = 0; i < runtimeRagdollBodies.Length; i++)
        {
            Rigidbody rb = runtimeRagdollBodies[i];
            ragdollBodyKinematicBeforeActivation[i] = rb.isKinematic;
            ragdollBodyUseGravityBeforeActivation[i] = rb.useGravity;
            ragdollBodyDetectCollisionsBeforeActivation[i] = rb.detectCollisions;
        }
    }

    private Transform ResolveRagdollRoot()
    {
        if (runtimeRagdollRoot != null)
        {
            return runtimeRagdollRoot;
        }

        if (runtimeVisualRoot != null)
        {
            return runtimeVisualRoot;
        }

        if (visualRoot != null)
        {
            return visualRoot;
        }

        if (runtimeAnimator != null)
        {
            return runtimeAnimator.transform;
        }

        return transform.Find("CharacterVisual");
    }

    private static void SetRagdollCollidersEnabled(Collider[] colliders, bool enabled)
    {
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col != null)
            {
                col.enabled = enabled;
            }
        }
    }

    private static void SetRagdollRigidbodiesDynamic(Rigidbody[] rigidbodies)
    {
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rb = rigidbodies[i];
            if (rb == null)
            {
                continue;
            }

            rb.isKinematic = false;
            rb.useGravity = true;
            rb.detectCollisions = true;
        }
    }

    private void ApplyRagdollBurstForces(Rigidbody[] rigidbodies, Vector3 burstCenter)
    {
        if (rigidbodies == null || rigidbodies.Length == 0)
        {
            return;
        }

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rb = rigidbodies[i];
            if (rb == null)
            {
                continue;
            }

            float randomScale = 1f + UnityEngine.Random.Range(-RagdollBurstRandomScale, RagdollBurstRandomScale);
            float burstForce = Mathf.Max(0.05f, RagdollBurstBaseForce * randomScale);
            rb.AddExplosionForce(burstForce, burstCenter, RagdollBurstRadius, RagdollBurstUpwardModifier, ForceMode.Impulse);
            rb.AddTorque(UnityEngine.Random.insideUnitSphere * RagdollBurstTorque, ForceMode.Impulse);
            SetRigidbodyLinearVelocity(
                rb,
                Vector3.ClampMagnitude(GetRigidbodyLinearVelocity(rb), RagdollBurstMaxLinearSpeed));
            rb.angularVelocity = Vector3.ClampMagnitude(rb.angularVelocity, RagdollBurstMaxAngularSpeed);
        }
    }

    private void RestoreRagdollRigidbodies()
    {
        for (int i = 0; i < runtimeRagdollBodies.Length; i++)
        {
            Rigidbody rb = runtimeRagdollBodies[i];
            if (rb == null)
            {
                continue;
            }

            SetRigidbodyLinearVelocity(rb, Vector3.zero);
            rb.angularVelocity = Vector3.zero;
            bool hasCachedState = i < ragdollBodyKinematicBeforeActivation.Length
                && i < ragdollBodyUseGravityBeforeActivation.Length
                && i < ragdollBodyDetectCollisionsBeforeActivation.Length;
            rb.isKinematic = hasCachedState ? ragdollBodyKinematicBeforeActivation[i] : true;
            rb.useGravity = hasCachedState ? ragdollBodyUseGravityBeforeActivation[i] : false;
            rb.detectCollisions = hasCachedState ? ragdollBodyDetectCollisionsBeforeActivation[i] : true;
            if (!rb.isKinematic)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }
    }

    private static Vector3 GetRigidbodyLinearVelocity(Rigidbody rb)
    {
        if (rb == null)
        {
            return Vector3.zero;
        }

#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
#pragma warning disable 0618
        return rb.velocity;
#pragma warning restore 0618
#endif
    }

    private static void SetRigidbodyLinearVelocity(Rigidbody rb, Vector3 value)
    {
        if (rb == null)
        {
            return;
        }

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = value;
#else
#pragma warning disable 0618
        rb.velocity = value;
#pragma warning restore 0618
#endif
    }

    private void RestoreRagdollColliders()
    {
        for (int i = 0; i < runtimeRagdollColliders.Length; i++)
        {
            Collider col = runtimeRagdollColliders[i];
            if (col == null)
            {
                continue;
            }

            bool hasCachedState = i < ragdollColliderEnabledBeforeActivation.Length;
            col.enabled = hasCachedState && ragdollColliderEnabledBeforeActivation[i];
        }
    }

    private void LateUpdate()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        if (cameraTransform != null)
        {
            if (ragdollRuntimeActive && ragdollFreeCameraActive)
            {
                UpdateRagdollFreeCamera(deltaTime);
                return;
            }

            UpdateCameraRotation(deltaTime);
            UpdateCameraMotionEffects(deltaTime);
            UpdateCameraPosition(deltaTime);
            UpdateDynamicCameraEffects(deltaTime);
        }
    }

    private void EnterRagdollFreeCameraMode()
    {
        if (cameraTransform == null)
        {
            ragdollFreeCameraActive = false;
            return;
        }

        ragdollFreeCameraActive = true;
        hasCachedGameplayCameraAnglesBeforeRagdoll = true;
        cachedGameplayYawBeforeRagdoll = yaw;
        cachedGameplayPitchBeforeRagdoll = pitch;

        ragdollFreeCameraPosition = cameraTransform.position;
        Vector3 cameraEuler = cameraTransform.eulerAngles;
        ragdollFreeCameraYaw = cameraEuler.y;
        ragdollFreeCameraPitch = NormalizePitch(cameraEuler.x);
    }

    private void ExitRagdollFreeCameraMode()
    {
        ragdollFreeCameraActive = false;
        if (hasCachedGameplayCameraAnglesBeforeRagdoll)
        {
            yaw = cachedGameplayYawBeforeRagdoll;
            pitch = cachedGameplayPitchBeforeRagdoll;
            hasCachedGameplayCameraAnglesBeforeRagdoll = false;
        }
    }

    private void UpdateRagdollFreeCamera(float deltaTime)
    {
        if (cameraTransform == null)
        {
            return;
        }

        Vector2 gamepadLook = ReadGamepadLookInput();
        Vector2 gamepadMove = ReadGamepadMoveInput();
        float lookMultiplier = Mathf.Max(0.1f, ragdollFreeCameraLookSensitivityMultiplier);
        float mouseX = MinimoInputBridge.GetAxis("Mouse X");
        float mouseY = MinimoInputBridge.GetAxis("Mouse Y");
        ragdollFreeCameraYaw += (mouseX * mouseXSensitivity + gamepadLook.x * gamepadLookSensitivity)
            * deltaTime
            * lookMultiplier;
        float verticalMouse = (invertY ? mouseY : -mouseY) * mouseYSensitivity;
        float verticalGamepad = (invertY ? gamepadLook.y : -gamepadLook.y) * gamepadLookSensitivity;
        ragdollFreeCameraPitch = Mathf.Clamp(
            ragdollFreeCameraPitch + (verticalMouse + verticalGamepad) * deltaTime * lookMultiplier,
            minPitch,
            maxPitch);

        float horizontalInput = 0f;
        if (MinimoInputBridge.GetKey(KeyCode.RightArrow) || MinimoInputBridge.GetKey(KeyCode.D))
        {
            horizontalInput += 1f;
        }
        if (MinimoInputBridge.GetKey(KeyCode.LeftArrow) || MinimoInputBridge.GetKey(KeyCode.A))
        {
            horizontalInput -= 1f;
        }
        horizontalInput += gamepadMove.x;

        float forwardInput = 0f;
        if (MinimoInputBridge.GetKey(KeyCode.UpArrow) || MinimoInputBridge.GetKey(KeyCode.W))
        {
            forwardInput += 1f;
        }
        if (MinimoInputBridge.GetKey(KeyCode.DownArrow) || MinimoInputBridge.GetKey(KeyCode.S))
        {
            forwardInput -= 1f;
        }
        forwardInput += gamepadMove.y;

        float verticalInput = 0f;
        if (MinimoInputBridge.GetKey(KeyCode.E))
        {
            verticalInput += 1f;
        }
        if (MinimoInputBridge.GetKey(KeyCode.Q))
        {
            verticalInput -= 1f;
        }
        verticalInput += ReadGamepadFreeCameraVerticalInput();

        Quaternion freeLookRotation = Quaternion.Euler(ragdollFreeCameraPitch, ragdollFreeCameraYaw, 0f);
        Vector3 moveDirection = freeLookRotation * new Vector3(horizontalInput, 0f, forwardInput) + Vector3.up * verticalInput;
        if (moveDirection.sqrMagnitude > 1f)
        {
            moveDirection.Normalize();
        }

        float speedMultiplier = 1f;
        if (MinimoInputBridge.GetKey(KeyCode.LeftShift) || MinimoInputBridge.GetKey(KeyCode.RightShift))
        {
            speedMultiplier *= Mathf.Max(1f, ragdollFreeCameraFastMultiplier);
        }
        if (MinimoInputBridge.GetKey(KeyCode.LeftControl) || MinimoInputBridge.GetKey(KeyCode.RightControl))
        {
            speedMultiplier *= Mathf.Clamp(ragdollFreeCameraSlowMultiplier, 0.05f, 1f);
        }

        float moveSpeed = Mathf.Max(0.1f, ragdollFreeCameraMoveSpeed) * speedMultiplier;
        ragdollFreeCameraPosition += moveDirection * moveSpeed * deltaTime;
        cameraTransform.SetPositionAndRotation(ragdollFreeCameraPosition, freeLookRotation);
    }

    private void ResolveRuntimeReferences()
    {
        runtimeAnimator = animatorOverride;
        if (runtimeAnimator == null)
        {
            runtimeAnimator = GetComponentInChildren<Animator>();
        }

        ApplyAnimatorDefaults();

        runtimeAudioSource = audioSourceOverride;
        if (runtimeAudioSource == null)
        {
            runtimeAudioSource = GetComponent<AudioSource>();
        }

        if (runtimeAudioSource == null && enableFeedback)
        {
            runtimeAudioSource = gameObject.AddComponent<AudioSource>();
            runtimeAudioSource.playOnAwake = false;
            runtimeAudioSource.spatialBlend = 0f;
        }
    }

    private void ApplyAnimatorDefaults()
    {
        animatorSpeedSmoothed = 0f;
        stopPrimedFromAnimatorSpeed = false;
        stopAnimationTimer = 0f;
        hadMovementInputLastFrame = false;
        hasAnimatorSamplePosition = false;
        lastAnimatorSamplePosition = Vector3.zero;
        teeterReentryLockUntilTime = float.NegativeInfinity;
        wallJumpCameraTurnRemaining = 0f;
        wallHangActive = false;
        ledgeHangActive = false;
        ledgeClimbActive = false;
        wallHangNormal = Vector3.back;
        wallJumpNoRotateActive = false;
        wallJumpForwardBackLockActive = false;
        wallJumpForcedDirection = Vector3.back;
        forceRollOnWallJumpLanding = false;
        lockFacingUntilGroundInputAfterWallJump = false;
        wallHangBlockedForCurrentJump = false;
        wallHangBlockedUntilFalling = false;
        rollAirReentryPending = false;
        rollAirHighFallTriggered = false;
        rollAirStartBottomY = float.NegativeInfinity;
        rollAirStartTime = float.NegativeInfinity;
        rollDirectionSteerLockedForCurrentRoll = false;
        rollStartedFromCrouch = false;
        slideLowClearanceLockActive = false;
        slideLowClearanceLockedSpeed = 0f;
        lastMoveCollisionFlags = CollisionFlags.None;
        rollCameraPivotLockActive = false;
        rollCameraPivotLockedY = 0f;
        rollCameraPivotReleaseTimer = 0f;
        lowClearanceCameraPivotInitialized = false;
        lowClearanceCameraPivotY = 0f;
        lowClearanceCameraBlend01 = 0f;
        rollAutoCrouchSuppressUntilTime = float.NegativeInfinity;
        jumpFallTrackingActive = false;
        jumpFallStartBottomY = float.NegativeInfinity;
        jumpFallHighestBottomY = float.NegativeInfinity;
        jumpFallStartTime = float.NegativeInfinity;
        doubleJumpDashRollOnLandingPending = false;
        ResetDoubleJumpApexTracking();
        jumpLandingCooldownUnlockTime = float.NegativeInfinity;
        landingHandledThisFrame = false;
        activeGroundPlatform = null;
        activeGroundPlatformLastPosition = Vector3.zero;
        activeGroundPlatformLastRotation = Quaternion.identity;
        hasActiveGroundPlatformPose = false;
        movingPlatformParentingActive = false;
        movingPlatformParentTransform = null;
        wallInteractionUnlockFeetY = float.NegativeInfinity;
        wallHangRegrabLockUntilTime = float.NegativeInfinity;
        hangJumpCornerSnapAssistUntilTime = float.NegativeInfinity;
        wallHangSlideEaseElapsed = 0f;
        ledgeHangClimbUnlockTime = float.NegativeInfinity;
        ledgeHangInputLockUntilTime = float.NegativeInfinity;
        ledgeHangLastValidContactTime = float.NegativeInfinity;
        ledgeHangLateralInputRuntime = 0f;
        ledgeClimbProgress = 0f;
        ledgeClimbStartPosition = Vector3.zero;
        ledgeClimbMidPosition = Vector3.zero;
        ledgeClimbTargetPosition = Vector3.zero;
        ledgeHangTopPoint = Vector3.zero;
        jumpMirrorRuntimeValue = false;
        forceJumpMirrorToggleFromHangJump = false;
        climbJumpAnimatorActive = false;
        forceFallAnimatorFromClimbJump = false;
        wallHangReleaseHighFallCheckActive = false;
        jumpAnimatorWasActive = false;
        backflipAnimatorHoldActive = false;
        backflipAnimatorWasActive = false;
        stopAnimatorWasActive = false;
        landingAnimatorPulseTimer = 0f;
        groundPoundHardFallActive = false;
        groundPoundHardLandActive = false;
        groundPoundStartedFromMovement = false;
        groundPoundBounceLockedUntilNextJump = false;
        backflipJumpActive = false;
        groundPoundHardLandReleaseTime = float.NegativeInfinity;
        dashGhostSpawnTimer = 0f;
        dashJumpCancelCooldownUntilTime = float.NegativeInfinity;
        dashMirrorRuntimeValue = false;
        ClearDashGhostInstances();
        ClearDashGhostMaterialCache();
        if (runtimeAnimator == null)
        {
            animatorParameterTypes.Clear();
            runtimeAnimFloatInitialized = false;
            runtimeAnimSpeedValue = 0f;
            runtimeAnimMoveSpeedValue = 0f;
            runtimeAnimVerticalSpeedValue = 0f;
            runtimeAnimatorPlaybackSpeed = Mathf.Max(0.01f, animatorNormalPlaybackSpeed);
            return;
        }

        runtimeAnimator.applyRootMotion = false;
        runtimeAnimator.updateMode = AnimatorUpdateMode.Normal;
        runtimeAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        runtimeAnimatorPlaybackSpeed = Mathf.Max(0.01f, animatorNormalPlaybackSpeed);
        runtimeAnimator.speed = runtimeAnimatorPlaybackSpeed;
        CacheAnimatorParameters();
        ApplyJumpMirrorAnimatorValue(false);
        ApplyDashMirrorAnimatorValue(false);
        runtimeAnimFloatInitialized = false;
        runtimeAnimSpeedValue = HasAnimatorParameter(animSpeedParam, AnimatorControllerParameterType.Float)
            ? runtimeAnimator.GetFloat(animSpeedParam)
            : 0f;
        runtimeAnimMoveSpeedValue = HasAnimatorParameter(animMoveSpeedParam, AnimatorControllerParameterType.Float)
            ? runtimeAnimator.GetFloat(animMoveSpeedParam)
            : 0f;
        runtimeAnimVerticalSpeedValue = HasAnimatorParameter(animVerticalSpeedParam, AnimatorControllerParameterType.Float)
            ? runtimeAnimator.GetFloat(animVerticalSpeedParam)
            : 0f;
        runtimeAnimFloatInitialized = true;
    }

    private void CacheAnimatorParameters()
    {
        animatorParameterTypes.Clear();
        if (!CanAccessAnimatorParameters())
        {
            return;
        }

        AnimatorControllerParameter[] parameters = runtimeAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (!string.IsNullOrEmpty(parameter.name))
            {
                animatorParameterTypes[parameter.name] = parameter.type;
            }
        }
    }

    private bool CanAccessAnimatorParameters()
    {
        if (runtimeAnimator == null || runtimeAnimator.runtimeAnimatorController == null)
        {
            return false;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            return false;
        }
#endif

        return true;
    }

    private bool HasAnimatorParameter(string parameter, AnimatorControllerParameterType expectedType)
    {
        if (!CanAccessAnimatorParameters() || string.IsNullOrWhiteSpace(parameter))
        {
            return false;
        }

        string resolvedParameter = parameter.Trim();
        if (!animatorParameterTypes.TryGetValue(resolvedParameter, out AnimatorControllerParameterType actualType))
        {
            // Animator controller runtime'da degismis olabilir; cache'i bir kez tazele.
            CacheAnimatorParameters();
            if (!animatorParameterTypes.TryGetValue(resolvedParameter, out actualType))
            {
                return false;
            }
        }

        return actualType == expectedType;
    }

    private void CacheStandingShape()
    {
        if (controller == null)
        {
            return;
        }

        standingHeight = StandingControllerHeight;
        standingCenter = StandingControllerCenter;
        standingRadius = StandingControllerRadius;
        crouchHeightResolved = CrouchControllerHeight;
    }

    private void ApplyLockedControllerStandingShape()
    {
        if (controller == null)
        {
            return;
        }

        ApplyControllerShape(
            StandingControllerHeight,
            StandingControllerCenter,
            StandingControllerRadius,
            StandingControllerStepOffset);
    }

    private void ApplyControllerShape(float height, Vector3 center, float radius, float stepOffset)
    {
        if (controller == null)
        {
            return;
        }

        float resolvedRadius = Mathf.Max(0.05f, radius);
        float minHeight = Mathf.Max(0.05f, resolvedRadius * 2f + 0.01f);
        float resolvedHeight = Mathf.Max(minHeight, height);
        float resolvedStepOffset = Mathf.Clamp(stepOffset, 0f, resolvedHeight);
        controller.radius = resolvedRadius;
        controller.height = resolvedHeight;
        controller.center = center;
        controller.stepOffset = resolvedStepOffset;
    }

    private void HandleDebugToggle()
    {
        bool togglePerformance = MinimoInputBridge.GetKeyDown(stateHudToggleKey);
        bool toggleControls = MinimoInputBridge.GetKeyDown(controlsOverlayToggleKey);

#if ENABLE_INPUT_SYSTEM
        if (enableGamepadInput && !IsDpadHorizontalAssignedToGameplay())
        {
            Gamepad gamepad = Gamepad.current;
            togglePerformance |= gamepad != null
                                 && gamepad.dpad.left != null
                                 && gamepad.dpad.left.wasPressedThisFrame;
            toggleControls |= gamepad != null
                              && gamepad.dpad.right != null
                              && gamepad.dpad.right.wasPressedThisFrame;
        }
#endif

        if (togglePerformance)
        {
            showStateHud = !showStateHud;
        }

        if (toggleControls)
        {
            showControlsOverlay = !showControlsOverlay;
        }

        if (MinimoInputBridge.GetKeyDown(debugToggleKey))
        {
            showDebugOverlay = !showDebugOverlay;
        }

        if (MinimoInputBridge.GetKeyDown(debugGizmoToggleKey))
        {
            showDebugGizmos = !showDebugGizmos;
        }
    }

    private void ResolveInputConflicts()
    {
        if (enableSprint
            && enableDash
            && sprintKey != KeyCode.None
            && dashKey == sprintKey)
        {
            dashKey = KeyCode.Q;
        }

        if (!enableGamepadInput)
        {
            return;
        }

        // Bu controller strict Xbox layout kullanir.
        ApplyStrictXboxGamepadBindings();
    }

    private void ConsumeLegacyTuningReferences()
    {
        // These fields remain serialized for scene and preset compatibility.
        _ = crouchHeight;
        _ = crouchTransitionSpeed;
        _ = groundedVisualSink;
        _ = crouchVisualSinkBonus;
    }

    private void ResetGamepadButtonCaches()
    {
        gamepadButtonCacheFrame = -1;
        gamepadButtonHeldCache.Clear();
        gamepadButtonDownCache.Clear();
        gamepadButtonUpCache.Clear();
        gamepadButtonPreviousHeld.Clear();
    }

    private void ReadInput()
    {
        if (wallJumpForwardBackLockActive && controller != null && controller.isGrounded)
        {
            wallJumpForwardBackLockActive = false;
        }

        rawMoveInput = ReadMovementInput();
        moveInput = rawMoveInput;
        if (moveInput.magnitude < movementInputDeadZone)
        {
            moveInput = Vector2.zero;
        }
        else if (wallJumpForwardBackLockActive)
        {
            // While wall jump lock is active, forward/back input is disabled and left/right input stays free.
            moveInput.y = 0f;
        }

        desiredMoveDirection = GetCameraRelativeDirection(moveInput);
        bool crouchPressed = enableCrouchSlide && (MinimoInputBridge.GetKeyDown(crouchKey) || IsGamepadButtonDown(gamepadCrouchButton));
        crouchPressedThisFrame = crouchPressed;
        bool crouchKeyboardHeld = enableCrouchSlide && MinimoInputBridge.GetKey(crouchKey);
        bool crouchGamepadHeld = enableCrouchSlide && IsGamepadButtonHeld(gamepadCrouchButton);
        crouchHeld = crouchKeyboardHeld || crouchGamepadHeld;
        if (enableCrouchSlide)
        {
            if (crouchToggleMode)
            {
                if (crouchPressed)
                {
                    crouchTarget = !crouchTarget;
                    if (!crouchTarget && !CanStandUp())
                    {
                        crouchTarget = true;
                    }
                }
            }
            else
            {
                crouchTarget = crouchHeld;
            }
        }
        else
        {
            crouchTarget = false;
        }

        if (enableCrouchSlide
            && isGrounded
            && !CanStandUp()
            && currentState != MovementState.Sliding
            && currentState != MovementState.SlopeSliding
            && currentState != MovementState.Rolling)
        {
            crouchTarget = true;
        }

        // Clear stale toggle-based crouch targets immediately after exiting roll.
        // During this window, keep the target only when an active crouch input exists.
        if (IsRollAutoCrouchSuppressed() && !crouchPressed && !crouchHeld)
        {
            crouchTarget = false;
        }

        bool sprintKeyboardHeld = enableSprint && sprintKey != KeyCode.None && MinimoInputBridge.GetKey(sprintKey);
        bool sprintGamepadButtonHeld = enableSprint && IsGamepadButtonHeld(gamepadSprintButton);
        float sprintGamepadTriggerAmount = ReadGamepadSprintTriggerAmount();
        bool sprintGamepadHeld = sprintGamepadButtonHeld || sprintGamepadTriggerAmount > 0.0001f;
        sprintHeld = sprintKeyboardHeld || sprintGamepadHeld;
        sprintInputAmount = enableSprint
            ? Mathf.Clamp01(Mathf.Max(sprintKeyboardHeld ? 1f : 0f, Mathf.Max(sprintGamepadButtonHeld ? 1f : 0f, sprintGamepadTriggerAmount)))
            : 0f;

        bool jumpPressed = IsKeyboardJumpDown() || IsGamepadButtonDown(gamepadJumpButton);
        if (jumpPressed)
        {
            lastJumpPressedTime = Time.time;
        }

        if (enableDash && (MinimoInputBridge.GetKeyDown(dashKey) || IsGamepadButtonDown(gamepadDashButton)))
        {
            lastDashPressedTime = Time.time;
        }

        if (enableGroundPound && (MinimoInputBridge.GetKeyDown(groundPoundKey) || IsGamepadButtonDown(gamepadGroundPoundButton)))
        {
            lastGroundPoundPressedTime = Time.time;
        }

        if (enableDiveRoll && (MinimoInputBridge.GetKeyDown(diveKey) || IsGamepadButtonDown(gamepadDiveButton)))
        {
            lastDivePressedTime = Time.time;
        }

        bool rollPressed = MinimoInputBridge.GetKeyDown(rollKey)
            || IsGamepadButtonDown(gamepadRollButton);
        if (enableDiveRoll && rollPressed)
        {
            bool hasRollMovementIntent = desiredMoveDirection.sqrMagnitude > movementInputDeadZone * movementInputDeadZone
                || planarVelocity.sqrMagnitude > Mathf.Max(0.01f, movementInputDeadZone * movementInputDeadZone);
            // Do not buffer roll input while airborne.
            // On the ground, buffer only with movement intent or during the landing dive-roll window.
            bool allowRollBuffer = ((isGrounded && hasRollMovementIntent) || awaitingDiveRoll)
                && currentState != MovementState.Rolling;
            if (allowRollBuffer)
            {
                lastRollPressedTime = Time.time;
                rollInputVersion++;
            }
            else if (!isGrounded)
            {
                // Air roll input should not affect landing or ground-pound flow.
                lastRollPressedTime = float.NegativeInfinity;
                consumedRollInputVersion = rollInputVersion;
            }
        }

        if (!isGrounded && currentState != MovementState.Rolling)
        {
            // Fully prevent stale roll buffer from carrying over while airborne.
            lastRollPressedTime = float.NegativeInfinity;
            consumedRollInputVersion = rollInputVersion;
        }

        if (enableCrouchSlide && crouchPressed)
        {
            lastSlidePressedTime = Time.time;
        }
    }

    private bool IsCrouchIntentActive()
    {
        if (!enableCrouchSlide)
        {
            return false;
        }

        // Toggle modda intent kaynagi sadece hedef durum olmalidir.
        // Otherwise, on the second press (uncrouch), the key is still held during that frame.
        // Keep crouchHeld=false so the target state does not accidentally return to crouch.
        if (crouchToggleMode)
        {
            return crouchTarget;
        }

        return crouchHeld || crouchTarget;
    }

    private bool IsRollAutoCrouchSuppressed()
    {
        return Time.time < rollAutoCrouchSuppressUntilTime;
    }

    private void ForceExitCrouchForAction()
    {
        if (!enableCrouchSlide || !CanStandUp())
        {
            return;
        }

        crouchTarget = false;
        if (currentState == MovementState.Crouching)
        {
            SetState(MovementState.Grounded);
        }
    }

    private void ForceEndDashOnJump()
    {
        if (currentState != MovementState.Dashing)
        {
            return;
        }

        // Dash ortasinda jump alindiginda dash tamamen iptal olsun:
        // Reset duration and cooldown reference, then apply a fixed 0.5s jump-cancel cooldown.
        dashTimer = 0f;
        lastDashTime = float.NegativeInfinity;
        dashGhostSpawnTimer = 0f;
        dashJumpCancelCooldownUntilTime = Time.time + DashJumpCancelCooldownDuration;
        doubleJumpDashRollOnLandingPending = false;
    }

    private void CancelSlideForAction()
    {
        if (currentState != MovementState.Sliding && currentState != MovementState.SlopeSliding)
        {
            return;
        }

        crouchTarget = false;
        slideTimer = 0f;
        slideExitLockTimer = 0f;
    }

    private void BeginJumpFallTracking()
    {
        if (controller == null)
        {
            return;
        }

        jumpFallTrackingActive = true;
        jumpFallStartBottomY = controller.bounds.min.y;
        jumpFallHighestBottomY = jumpFallStartBottomY;
        jumpFallStartTime = Time.time;
    }

    private void ClearJumpFallTracking()
    {
        jumpFallTrackingActive = false;
        jumpFallStartBottomY = float.NegativeInfinity;
        jumpFallHighestBottomY = float.NegativeInfinity;
        jumpFallStartTime = float.NegativeInfinity;
    }

    private void UpdateJumpFallPeakTracking()
    {
        if (!jumpFallTrackingActive || isGrounded)
        {
            return;
        }

        float currentBottomY = controller != null ? controller.bounds.min.y : transform.position.y;
        if (float.IsNegativeInfinity(jumpFallHighestBottomY))
        {
            jumpFallHighestBottomY = currentBottomY;
            return;
        }

        if (currentBottomY > jumpFallHighestBottomY)
        {
            jumpFallHighestBottomY = currentBottomY;
        }
    }

    private void ResetDoubleJumpApexTracking()
    {
        didPerformAirJumpSinceGrounded = false;
        doubleJumpApexTrackingActive = false;
        doubleJumpApexBottomY = float.NegativeInfinity;
    }

    private void BeginDoubleJumpApexTracking()
    {
        doubleJumpApexTrackingActive = true;
        doubleJumpApexBottomY = transform.position.y;
    }

    private void UpdateDoubleJumpApexTracking()
    {
        if (!doubleJumpApexTrackingActive || isGrounded)
        {
            return;
        }

        float currentBottomY = transform.position.y;
        if (float.IsNegativeInfinity(doubleJumpApexBottomY))
        {
            doubleJumpApexBottomY = currentBottomY;
            return;
        }

        if (currentBottomY > doubleJumpApexBottomY)
        {
            doubleJumpApexBottomY = currentBottomY;
        }
    }

    private void BeginRollAirTrackingIfNeeded()
    {
        if (!rollAirReentryPending)
        {
            rollAirReentryPending = true;
            rollAirHighFallTriggered = false;
            rollAirStartBottomY = controller != null ? controller.bounds.min.y : transform.position.y;
            rollAirStartTime = Time.time;
        }

        if (!rollAirHighFallTriggered && IsRollAirHighFallReached())
        {
            rollAirHighFallTriggered = true;
        }
    }

    private bool IsRollAirHighFallReached()
    {
        float currentBottomY = controller != null ? controller.bounds.min.y : transform.position.y;
        float fallHeight = Mathf.Max(0f, rollAirStartBottomY - currentBottomY);
        float fallAirTime = float.IsNegativeInfinity(rollAirStartTime)
            ? 0f
            : Mathf.Max(0f, Time.time - rollAirStartTime);
        bool highEnough = fallHeight >= GetResolvedHighFallRollHeightThreshold(fromJump: false);
        bool longEnough = fallAirTime >= GetResolvedHighFallRollAirTimeThreshold(fromJump: false);
        return highEnough || longEnough;
    }

    private void ClearRollAirTracking()
    {
        rollAirReentryPending = false;
        rollAirHighFallTriggered = false;
        rollAirStartBottomY = float.NegativeInfinity;
        rollAirStartTime = float.NegativeInfinity;
    }

    private void ProcessBufferedActions()
    {
        if (awaitingDiveRoll && Time.time > diveRollWindowEndTime)
        {
            awaitingDiveRoll = false;
            pendingDiveRollSpeed = 0f;
        }

        UpdateWallHangState();
        if (TryStartGroundPoundFromWallHangDiveInput())
        {
            return;
        }

        if (TryConsumeWallHangJump())
        {
            return;
        }

        if (wallHangActive)
        {
            CancelBufferedDashInput();
            return;
        }

        if (TryConsumeDiveRollWindow())
        {
            return;
        }

        if (TryStartGroundPoundFromBuffer())
        {
            return;
        }

        if (TryStartDiveFromBuffer())
        {
            return;
        }

        if (TryStartDashFromBuffer())
        {
            return;
        }

        if (TryStartGroundRollFromBuffer())
        {
            return;
        }

        TryConsumeBufferedJump();
        TryConsumeAirJump();
        TryStartSlideOrCrouch();
    }

    private void EvaluateTeeteringState()
    {
        if (!enableTeetering || controller == null)
        {
            if (currentState == MovementState.Teetering)
            {
                SetState(isGrounded ? MovementState.Grounded : MovementState.Falling);
            }

            return;
        }

        if (currentState == MovementState.Teetering)
        {
            if (!isGrounded)
            {
                SetState(MovementState.Falling);
                return;
            }

            if (HasTeeterMovementIntent() || !IsTeeterStationaryIdle())
            {
                teeterReentryLockUntilTime = Time.time + Mathf.Max(0f, teeterReentryLockDuration);
                SetState(MovementState.Grounded);
                return;
            }

            if (!IsEdgeOpenAhead(teeterEdgeDirection, out _)
                || !IsFacingTowardTeeterEdge(teeterEdgeDirection))
            {
                SetState(MovementState.Grounded);
            }

            return;
        }

        if (Time.time < teeterReentryLockUntilTime)
        {
            return;
        }

        if (!isGrounded || IsCrouchIntentActive())
        {
            return;
        }

        if (currentState == MovementState.Dashing
            || currentState == MovementState.GroundPound
            || currentState == MovementState.Dive
            || currentState == MovementState.Rolling
            || currentState == MovementState.Sliding
            || currentState == MovementState.SlopeSliding)
        {
            return;
        }

        if (HasTeeterMovementIntent() || !IsTeeterStationaryIdle())
        {
            return;
        }

        Vector3 primaryDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (primaryDirection.sqrMagnitude <= 0.0001f)
        {
            primaryDirection = Vector3.forward;
        }

        primaryDirection.Normalize();
        if (IsEdgeOpenAhead(primaryDirection, out Vector3 edgeDirection))
        {
            if (IsFacingTowardTeeterEdge(edgeDirection))
            {
                EnterTeetering(edgeDirection);
                return;
            }
        }

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forward.sqrMagnitude > 0.0001f
            && Vector3.Dot(primaryDirection.normalized, forward.normalized) < 0.995f
            && IsEdgeOpenAhead(forward.normalized, out edgeDirection))
        {
            if (IsFacingTowardTeeterEdge(edgeDirection))
            {
                EnterTeetering(edgeDirection);
                return;
            }
        }

        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up);
        if (right.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        right.Normalize();
        if (Mathf.Abs(moveInput.x) > movementInputDeadZone)
        {
            Vector3 sideDirection = moveInput.x > 0f ? right : -right;
            if (IsEdgeOpenAhead(sideDirection, out edgeDirection))
            {
                if (IsFacingTowardTeeterEdge(edgeDirection))
                {
                    EnterTeetering(edgeDirection);
                }
            }
            return;
        }

        if (IsEdgeOpenAhead(right, out edgeDirection) || IsEdgeOpenAhead(-right, out edgeDirection))
        {
            if (IsFacingTowardTeeterEdge(edgeDirection))
            {
                EnterTeetering(edgeDirection);
            }
        }
    }

    private bool IsFacingTowardTeeterEdge(Vector3 edgeDirection)
    {
        Vector3 facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        Vector3 edgePlanar = Vector3.ProjectOnPlane(edgeDirection, Vector3.up);
        if (facing.sqrMagnitude <= 0.0001f || edgePlanar.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        facing.Normalize();
        edgePlanar.Normalize();
        float minDot = Mathf.Clamp(teeterFacingEdgeAlignmentThreshold, -1f, 1f);
        return Vector3.Dot(facing, edgePlanar) >= minDot;
    }

    private bool HasTeeterMovementIntent()
    {
        float deadZone = movementInputDeadZone * movementInputDeadZone;
        return moveInput.sqrMagnitude > deadZone;
    }

    private bool IsTeeterStationaryIdle()
    {
        float idleSpeedThreshold = Mathf.Max(
            0.04f,
            Mathf.Min(
                Mathf.Max(0f, teeterMaxEntrySpeed),
                Mathf.Max(0.04f, planarStopSnapSpeed * 0.8f)));
        return planarVelocity.sqrMagnitude <= idleSpeedThreshold * idleSpeedThreshold;
    }

    private bool IsEdgeAnimatorActive()
    {
        return currentState == MovementState.Teetering
            && isGrounded
            && !HasTeeterMovementIntent()
            && IsTeeterStationaryIdle();
    }

    private void EnterTeetering(Vector3 edgeDirection)
    {
        teeterEdgeDirection = edgeDirection.normalized;
        Vector3 currentFacing = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (currentFacing.sqrMagnitude <= 0.0001f)
        {
            currentFacing = -Vector3.ProjectOnPlane(teeterEdgeDirection, Vector3.up);
        }

        teeterFacingDirection = currentFacing.sqrMagnitude > 0.0001f
            ? currentFacing.normalized
            : Vector3.forward;
        teeterReentryLockUntilTime = float.NegativeInfinity;
        debugTeeterEdgeDirection = teeterEdgeDirection;
        planarVelocity = Vector3.zero;
        verticalVelocity = groundedVerticalForce;
        if (!(enableCrouchSlide && crouchToggleMode && crouchTarget))
        {
            crouchTarget = false;
        }
        debugTeeterEnterPoint = transform.position + Vector3.up * 0.05f;
        debugTeeterEnterDirection = teeterEdgeDirection;
        debugTeeterEnterTime = Time.time;
        debugTeeterHadSnapBack = false;
        debugTeeterSnapTargetPoint = debugTeeterEnterPoint;

        if (teeterSnapBackDistance > 0f)
        {
            controller.Move(-teeterEdgeDirection * teeterSnapBackDistance);
            debugTeeterHadSnapBack = true;
            debugTeeterSnapTargetPoint = transform.position + Vector3.up * 0.05f;
        }

        if (Time.time - lastTeeterAudioTime >= teeterAudioCooldown)
        {
            PlayClip(teeterClip, 0.9f);
            lastTeeterAudioTime = Time.time;
        }

        SetState(MovementState.Teetering);
        TriggerAnimator(animTeeterParam);
    }

    private bool IsEdgeOpenAhead(Vector3 direction, out Vector3 edgeDirection)
    {
        edgeDirection = Vector3.zero;
        if (controller == null)
        {
            debugTeeterHasGroundAhead = false;
            return false;
        }

        direction = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            debugTeeterHasGroundAhead = false;
            return false;
        }

        direction.Normalize();

        Bounds bounds = controller.bounds;
        float probeRadius = Mathf.Clamp(Mathf.Min(groundProbeRadius, controller.radius) * 0.55f, 0.03f, controller.radius);
        Vector3 centerProbeOrigin = bounds.center + Vector3.up * groundProbeOffset;
        // Trigger teeter closer to the edge by tightening the forward probe distance.
        float teeterProbeForward = Mathf.Max(
            0f,
            teeterProbeForwardDistance * 0.65f + teeterEdgeEarlyDetectDistance * 0.15f);
        Vector3 edgeProbeOrigin = centerProbeOrigin + direction * teeterProbeForward;
        float probeDistance = bounds.extents.y + teeterProbeDepth;
        int mask = GetGroundMaskExcludingSelf();

        bool hasGroundBelow = Physics.SphereCast(
            centerProbeOrigin,
            probeRadius * 0.9f,
            Vector3.down,
            out RaycastHit belowHit,
            probeDistance,
            mask,
            QueryTriggerInteraction.Ignore);

        bool hasGroundAheadCenterRaw = Physics.SphereCast(
            edgeProbeOrigin,
            probeRadius,
            Vector3.down,
            out RaycastHit edgeHitCenter,
            probeDistance,
            mask,
            QueryTriggerInteraction.Ignore);
        bool hasGroundAheadCenter = IsSameTeeterSupportCollider(
            hasGroundBelow ? belowHit.collider : null,
            hasGroundAheadCenterRaw ? edgeHitCenter.collider : null);
        Vector3 lateral = Vector3.Cross(Vector3.up, direction);
        if (lateral.sqrMagnitude <= 0.0001f)
        {
            lateral = Vector3.right;
        }

        lateral.Normalize();
        float sideOffset = probeRadius * 0.65f;
        bool hasGroundAheadLeftRaw = Physics.SphereCast(
            edgeProbeOrigin + lateral * sideOffset,
            probeRadius * 0.9f,
            Vector3.down,
            out RaycastHit edgeHitLeft,
            probeDistance,
            mask,
            QueryTriggerInteraction.Ignore);
        bool hasGroundAheadLeft = IsSameTeeterSupportCollider(
            hasGroundBelow ? belowHit.collider : null,
            hasGroundAheadLeftRaw ? edgeHitLeft.collider : null);
        bool hasGroundAheadRightRaw = Physics.SphereCast(
            edgeProbeOrigin - lateral * sideOffset,
            probeRadius * 0.9f,
            Vector3.down,
            out RaycastHit edgeHitRight,
            probeDistance,
            mask,
            QueryTriggerInteraction.Ignore);
        bool hasGroundAheadRight = IsSameTeeterSupportCollider(
            hasGroundBelow ? belowHit.collider : null,
            hasGroundAheadRightRaw ? edgeHitRight.collider : null);

        int aheadGroundCount = (hasGroundAheadCenter ? 1 : 0) + (hasGroundAheadLeft ? 1 : 0) + (hasGroundAheadRight ? 1 : 0);
        bool hasGroundAhead = aheadGroundCount >= 2;
        RaycastHit edgeHit = hasGroundAheadCenter
            ? edgeHitCenter
            : hasGroundAheadLeft
                ? edgeHitLeft
                : edgeHitRight;
        Collider supportCollider = hasGroundBelow ? belowHit.collider : null;

        debugTeeterProbeOrigin = edgeProbeOrigin;
        debugTeeterProbeRadius = probeRadius;
        debugTeeterProbeDistance = probeDistance;
        debugTeeterEdgeDirection = direction;
        debugTeeterHasGroundAhead = hasGroundAhead;
        debugTeeterHitPoint = hasGroundAhead ? edgeHit.point : edgeProbeOrigin + Vector3.down * probeDistance;

        if (!hasGroundBelow || hasGroundAhead)
        {
            return false;
        }

        if (!IsTeeterEdgeSurroundingsClear(edgeProbeOrigin, direction, supportCollider))
        {
            return false;
        }

        Vector3 highDropRayOrigin = edgeProbeOrigin + Vector3.up * Mathf.Max(0.02f, controller.skinWidth + 0.01f);
        if (!IsDropHigherThanSingleJumpByRay(highDropRayOrigin, mask))
        {
            return false;
        }

        edgeDirection = direction;
        return true;
    }

    private static bool IsSameTeeterSupportCollider(Collider baseCollider, Collider candidateCollider)
    {
        if (baseCollider == null || candidateCollider == null)
        {
            return false;
        }

        if (ReferenceEquals(baseCollider, candidateCollider))
        {
            return true;
        }

        if (baseCollider.attachedRigidbody != null
            && baseCollider.attachedRigidbody == candidateCollider.attachedRigidbody)
        {
            return true;
        }

        Transform baseTransform = baseCollider.transform;
        Transform candidateTransform = candidateCollider.transform;
        if (baseTransform == null || candidateTransform == null)
        {
            return false;
        }

        if (candidateTransform == baseTransform
            || candidateTransform.IsChildOf(baseTransform)
            || baseTransform.IsChildOf(candidateTransform))
        {
            return true;
        }

        return false;
    }

    private bool IsTeeterEdgeSurroundingsClear(Vector3 edgeProbeOrigin, Vector3 edgeDirection, Collider supportCollider)
    {
        float radius = Mathf.Max(0f, teeterOpenSpaceRadius);
        if (radius <= 0.0001f)
        {
            return true;
        }

        Vector3 planarDirection = Vector3.ProjectOnPlane(edgeDirection, Vector3.up);
        if (planarDirection.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        planarDirection.Normalize();
        Vector3 firstProbeCenter =
            edgeProbeOrigin
            + planarDirection * Mathf.Max(0f, teeterOpenSpaceForwardOffset)
            + Vector3.up * Mathf.Max(0f, teeterOpenSpaceVerticalOffset);
        if (HasBlockingColliderAroundTeeterProbe(firstProbeCenter, radius, supportCollider))
        {
            return false;
        }

        Vector3 secondProbeCenter = firstProbeCenter + Vector3.up * Mathf.Max(0f, teeterOpenSpaceUpperVerticalOffset);
        if (HasBlockingColliderAroundTeeterProbe(secondProbeCenter, radius * 0.9f, supportCollider))
        {
            return false;
        }

        return true;
    }

    private bool HasBlockingColliderAroundTeeterProbe(Vector3 probeCenter, float probeRadius, Collider supportCollider)
    {
        float radius = Mathf.Max(0.01f, probeRadius);
        int overlapCount = Physics.OverlapSphereNonAlloc(
            probeCenter,
            radius,
            teeterOverlapBuffer,
            ~0,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlapCount; i++)
        {
            Collider candidate = teeterOverlapBuffer[i];
            if (candidate == null)
            {
                continue;
            }

            Transform candidateTransform = candidate.transform;
            if (candidateTransform == transform || candidateTransform.IsChildOf(transform))
            {
                continue;
            }

            if (IsSameTeeterSupportCollider(supportCollider, candidate))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool TryStartDashFromBuffer()
    {
        if (!enableDash)
        {
            CancelBufferedDashInput();
            return false;
        }

        if (!IsBuffered(lastDashPressedTime, dashBufferTime))
        {
            return false;
        }

        if (Time.time < dashJumpCancelCooldownUntilTime)
        {
            CancelBufferedDashInput();
            return false;
        }

        if (Time.time - lastDashTime < dashCooldown)
        {
            CancelBufferedDashInput();
            return false;
        }

        if (currentState == MovementState.GroundPound)
        {
            CancelBufferedDashInput();
            return false;
        }

        CancelSlideForAction();
        ForceExitCrouchForAction();

        Vector3 direction = ResolveDashDirectionFromCameraRelativeInput();
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = ResolvePlanarCameraForwardDirection();
            }
        }

        direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
        }

        bool dashHasMovementInput = moveInput.sqrMagnitude > movementInputDeadZone * movementInputDeadZone;
        float dashStandstillSpeedThreshold = Mathf.Max(0.01f, moveSpeed * 0.04f);
        float dashStartPlanarSpeed = Vector3.ProjectOnPlane(planarVelocity, Vector3.up).magnitude;
        dashStartedFromStandstill = isGrounded
            && !dashHasMovementInput
            && dashStartPlanarSpeed <= dashStandstillSpeedThreshold;

        dashDirection = direction;
        planarVelocity = dashDirection * dashSpeed;
        dashTimer = dashDuration;
        if (enableDashBlobEffect)
        {
            dashBlobPulseValue = Mathf.Max(dashBlobPulseValue, dashBlobPulse);
        }

        RecordDashDebug(dashDirection, dashSpeed);
        lastDashTime = Time.time;
        dashJumpCancelCooldownUntilTime = float.NegativeInfinity;
        lastDashPressedTime = float.NegativeInfinity;
        // Dash sonrasinda wall-jump geri-itme kilidi tekrar devreye girip
        // Do not pull the character backward.
        wallJumpForwardBackLockActive = false;
        wallJumpForcedDirection = Vector3.back;
        jumpHoldTimer = 0f;
        if (verticalVelocity < 0f)
        {
            verticalVelocity *= 0.3f;
        }

        SetState(MovementState.Dashing);
        bool dashStartedAfterDoubleJump = didPerformAirJumpSinceGrounded;
        doubleJumpDashRollOnLandingPending = dashStartedAfterDoubleJump;
        if (dashStartedAfterDoubleJump)
        {
            // Clear any remaining DoubleJump bool or trigger state in the Animator when entering dash after double jump.
            ResetAnimatorTrigger(animFrontflipTrigger);
        }

        ToggleDashMirrorOnDash();
        SpawnDashGhostSnapshot(dashDirection);
        dashGhostSpawnTimer = Mathf.Max(0.005f, dashGhostSpawnInterval / Mathf.Clamp(dashGhostDensity, 0.1f, 2f));
        ResetAnimatorTrigger(animDashParam);
        PlayClip(dashClip);
        AddCameraKick(dashCameraKick);
        AddCameraShake(cameraShakeMaxAmplitude * dashCameraShakeMultiplier);
        return true;
    }

    private Vector3 ResolveDashDirectionFromCameraRelativeInput()
    {
        if (cameraTransform == null)
        {
            return desiredMoveDirection;
        }

        Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
        if (cameraForward.sqrMagnitude <= 0.0001f)
        {
            cameraForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        if (cameraForward.sqrMagnitude <= 0.0001f)
        {
            cameraForward = Vector3.forward;
        }

        cameraForward.Normalize();
        Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraForward);
        if (cameraRight.sqrMagnitude <= 0.0001f)
        {
            cameraRight = Vector3.right;
        }

        cameraRight.Normalize();
        Vector2 input = moveInput;
        Vector3 direction = cameraForward * input.y + cameraRight * input.x;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
    }

    private Vector3 ResolvePlanarCameraForwardDirection()
    {
        if (cameraTransform != null)
        {
            Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
            if (cameraForward.sqrMagnitude > 0.0001f)
            {
                return cameraForward.normalized;
            }
        }

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }

    private bool TryStartGroundPoundFromWallHangDiveInput()
    {
        bool fromWallHang = wallHangActive || ledgeHangActive;
        if (!fromWallHang || !enableGroundPound || !enableDiveRoll || isGrounded)
        {
            return false;
        }

        if (!IsBuffered(lastDivePressedTime, diveBufferTime))
        {
            return false;
        }

        if (groundPoundBounceLockedUntilNextJump)
        {
            return false;
        }

        float requiredAirDistance = Mathf.Max(0.05f, minimumJumpHeight);
        if (!HasGroundPoundClearanceFromGround(requiredAirDistance))
        {
            return false;
        }

        bool hasDirectionalInput = desiredMoveDirection.sqrMagnitude > movementInputDeadZone * movementInputDeadZone;
        Vector3 planarCarryVelocity = Vector3.ProjectOnPlane(planarVelocity, Vector3.up);
        bool hasCarryMomentum = planarCarryVelocity.sqrMagnitude > 0.04f;
        groundPoundStartedFromMovement = hasDirectionalInput || hasCarryMomentum;

        groundPoundDelayTimer = groundPoundStartDelay;
        verticalVelocity = Mathf.Min(verticalVelocity, 0f);
        if (groundPoundStartedFromMovement)
        {
            planarVelocity *= 0.25f;
        }
        else
        {
            planarVelocity = Vector3.zero;
        }

        jumpHoldTimer = 0f;
        awaitingDiveRoll = false;
        pendingDiveRollSpeed = 0f;
        diveRollWindowEndTime = float.NegativeInfinity;
        rollAutoCrouchSuppressUntilTime = float.NegativeInfinity;
        lastDivePressedTime = float.NegativeInfinity;
        lastGroundPoundPressedTime = float.NegativeInfinity;

        ExitWallHang();
        wallJumpNoRotateActive = false;
        wallJumpForwardBackLockActive = false;
        wallHangReleaseHighFallCheckActive = false;

        RecordDiveDebug(Vector3.down, planarVelocity.magnitude, verticalVelocity);
        SetState(MovementState.GroundPound);
        BeginGroundPoundHardFallSequence();
        if (enableDashGhostTrail && enableGroundPoundGhostTrail)
        {
            SpawnDashGhostSnapshot(ResolveGhostTrailDirectionForState(isGroundPoundState: true));
            dashGhostSpawnTimer = Mathf.Max(0.005f, dashGhostSpawnInterval / Mathf.Clamp(dashGhostDensity, 0.1f, 2f));
        }

        TriggerAnimator(animGroundPoundParam);
        PlayClip(groundPoundStartClip);
        return true;
    }

    private void CancelBufferedDashInput()
    {
        lastDashPressedTime = float.NegativeInfinity;
    }

    private bool TryStartGroundPoundFromBuffer()
    {
        if (!enableGroundPound || isGrounded || !IsBuffered(lastGroundPoundPressedTime, groundPoundBufferTime))
        {
            return false;
        }

        if (groundPoundBounceLockedUntilNextJump)
        {
            return false;
        }

        if (!IsGroundPoundControlHeld())
        {
            return false;
        }

        if (currentState == MovementState.GroundPound || currentState == MovementState.Dive || currentState == MovementState.Rolling)
        {
            return false;
        }

        if (!HasReachedGroundPoundRequiredAirDistance())
        {
            return false;
        }

        bool hasDirectionalInput = desiredMoveDirection.sqrMagnitude > movementInputDeadZone * movementInputDeadZone;
        Vector3 planarCarryVelocity = Vector3.ProjectOnPlane(planarVelocity, Vector3.up);
        bool hasCarryMomentum = planarCarryVelocity.sqrMagnitude > 0.04f;
        groundPoundStartedFromMovement = hasDirectionalInput || hasCarryMomentum;

        groundPoundDelayTimer = groundPoundStartDelay;
        verticalVelocity = Mathf.Min(verticalVelocity, 0f);
        if (groundPoundStartedFromMovement)
        {
            planarVelocity *= 0.25f;
        }
        else
        {
            planarVelocity = Vector3.zero;
        }

        lastGroundPoundPressedTime = float.NegativeInfinity;
        jumpHoldTimer = 0f;
        SetState(MovementState.GroundPound);
        BeginGroundPoundHardFallSequence();
        if (enableDashGhostTrail && enableGroundPoundGhostTrail)
        {
            SpawnDashGhostSnapshot(ResolveGhostTrailDirectionForState(isGroundPoundState: true));
            dashGhostSpawnTimer = Mathf.Max(0.005f, dashGhostSpawnInterval / Mathf.Clamp(dashGhostDensity, 0.1f, 2f));
        }

        TriggerAnimator(animGroundPoundParam);
        PlayClip(groundPoundStartClip);
        return true;
    }

    private bool HasReachedGroundPoundRequiredAirDistance()
    {
        float requiredAirDistance = Mathf.Max(0.05f, minimumJumpHeight);
        float currentBottomY = controller != null ? controller.bounds.min.y : transform.position.y;

        bool startedFromJump = jumpConsumed
            && jumpFallTrackingActive
            && !float.IsNegativeInfinity(jumpFallStartBottomY);
        if (startedFromJump)
        {
            float highestBottomY = float.IsNegativeInfinity(jumpFallHighestBottomY)
                ? Mathf.Max(currentBottomY, jumpFallStartBottomY)
                : Mathf.Max(currentBottomY, jumpFallHighestBottomY);
            float jumpRise = Mathf.Max(0f, highestBottomY - jumpFallStartBottomY);
            return jumpRise >= requiredAirDistance;
        }

        bool startedFromFall = !jumpConsumed
            && trackingNonJumpFall
            && !float.IsNegativeInfinity(nonJumpFallStartBottomY);
        if (startedFromFall)
        {
            float fallDistance = Mathf.Max(0f, nonJumpFallStartBottomY - currentBottomY);
            return fallDistance >= requiredAirDistance;
        }

        return false;
    }

    private bool HasGroundPoundClearanceFromGround(float requiredAirDistance)
    {
        if (controller == null)
        {
            return false;
        }

        float requiredDistance = Mathf.Max(0.05f, requiredAirDistance);
        Bounds bounds = controller.bounds;
        float rayStartOffset = Mathf.Max(controller.skinWidth + 0.02f, 0.04f);
        Vector3 rayOrigin = bounds.center;
        rayOrigin.y = bounds.min.y + rayStartOffset;
        float rayDistance = requiredDistance + rayStartOffset;
        int mask = GetGroundMaskExcludingSelf();
        if (!TryRaycastIgnoringSelf(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance, mask, QueryTriggerInteraction.Ignore))
        {
            // If the ray finds no ground within this distance, the required height is already available.
            return true;
        }

        float feetToGround = Mathf.Max(0f, bounds.min.y - hit.point.y);
        return feetToGround >= requiredDistance;
    }

    private bool TryConsumeDiveRollWindow()
    {
        if (!enableDiveRoll || !awaitingDiveRoll || !isGrounded)
        {
            return false;
        }

        if (Time.time > diveRollWindowEndTime)
        {
            awaitingDiveRoll = false;
            pendingDiveRollSpeed = 0f;
            return false;
        }

        if (!HasBufferedRollInput())
        {
            return false;
        }

        return StartRoll(pendingDiveRollSpeed, true);
    }

    private bool TryStartDiveFromBuffer()
    {
        if (!enableDiveRoll || isGrounded || !IsBuffered(lastDivePressedTime, diveBufferTime))
        {
            return false;
        }

        if (!enableGroundPound || groundPoundBounceLockedUntilNextJump)
        {
            return false;
        }

        if (currentState == MovementState.Dashing || currentState == MovementState.GroundPound || currentState == MovementState.Rolling)
        {
            return false;
        }

        if (!HasReachedGroundPoundRequiredAirDistance())
        {
            return false;
        }

        bool hasDirectionalInput = desiredMoveDirection.sqrMagnitude > movementInputDeadZone * movementInputDeadZone;
        Vector3 planarCarryVelocity = Vector3.ProjectOnPlane(planarVelocity, Vector3.up);
        bool hasCarryMomentum = planarCarryVelocity.sqrMagnitude > 0.04f;
        groundPoundStartedFromMovement = hasDirectionalInput || hasCarryMomentum;

        groundPoundDelayTimer = groundPoundStartDelay;
        verticalVelocity = Mathf.Min(verticalVelocity, 0f);
        if (groundPoundStartedFromMovement)
        {
            planarVelocity *= 0.25f;
        }
        else
        {
            planarVelocity = Vector3.zero;
        }

        jumpHoldTimer = 0f;
        awaitingDiveRoll = false;
        pendingDiveRollSpeed = 0f;
        diveRollWindowEndTime = float.NegativeInfinity;
        lastDivePressedTime = float.NegativeInfinity;
        lastGroundPoundPressedTime = float.NegativeInfinity;
        ResetAnimatorTrigger(animDiveParam);
        RecordDiveDebug(Vector3.down, planarVelocity.magnitude, verticalVelocity);
        SetState(MovementState.GroundPound);
        BeginGroundPoundHardFallSequence();
        if (enableDashGhostTrail && enableGroundPoundGhostTrail)
        {
            SpawnDashGhostSnapshot(ResolveGhostTrailDirectionForState(isGroundPoundState: true));
            dashGhostSpawnTimer = Mathf.Max(0.005f, dashGhostSpawnInterval / Mathf.Clamp(dashGhostDensity, 0.1f, 2f));
        }

        TriggerAnimator(animGroundPoundParam);
        PlayClip(groundPoundStartClip);
        return true;
    }

    private bool StartRoll(float sourceSpeed, bool consumeInput, bool forceStart = false)
    {
        if (!enableDiveRoll && !forceStart)
        {
            return false;
        }

        rollStartedFromCrouch = currentState == MovementState.Crouching;
        ClearRollAirTracking();
        rollDirectionSteerLockedForCurrentRoll = false;

        bool airborneRollStart = currentState == MovementState.Jumping
            || currentState == MovementState.Falling
            || currentState == MovementState.Dive;
        float idleJumpLockThreshold = Mathf.Max(0f, idleJumpRollDirectionLockSpeedThreshold);
        bool hasDirectionalInput = desiredMoveDirection.sqrMagnitude > movementInputDeadZone * movementInputDeadZone;
        bool shouldLockDirectionForIdleJump = lockRollDirectionForIdleJumpLanding
            && airborneRollStart
            && planarVelocity.sqrMagnitude <= idleJumpLockThreshold * idleJumpLockThreshold
            && !hasDirectionalInput;
        if (shouldLockDirectionForIdleJump)
        {
            rollDirectionSteerLockedForCurrentRoll = true;
        }

        Vector3 direction;
        if (currentState == MovementState.Teetering)
        {
            direction = desiredMoveDirection.sqrMagnitude > 0.001f
                ? desiredMoveDirection
                : teeterEdgeDirection.sqrMagnitude > 0.001f
                    ? teeterEdgeDirection
                    : transform.forward;
        }
        else if (shouldLockDirectionForIdleJump)
        {
            direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }
        else if (lockFacingUntilGroundInputAfterWallJump)
        {
            direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }
        else
        {
            direction = planarVelocity.sqrMagnitude > 0.001f ? planarVelocity.normalized : diveDirection;
            if (desiredMoveDirection.sqrMagnitude > 0.001f)
            {
                direction = Vector3.Slerp(direction, desiredMoveDirection, 0.5f);
            }
        }

        direction = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
        }

        rollDirection = direction.normalized;
        float resolvedSourceSpeed = ResolveRollSourceSpeed(sourceSpeed);
        float startSpeed = Mathf.Max(rollBaseSpeed, resolvedSourceSpeed);
        float effectiveRollDuration = GetEffectiveRollDuration();
        planarVelocity = rollDirection * startSpeed;
        verticalVelocity = Mathf.Min(verticalVelocity, groundedVerticalForce);
        rollTimer = effectiveRollDuration;
        crouchTarget = false;
        // Clear ghost trail and slide-buffer effects when roll starts.
        ClearDashGhostInstances();
        dashGhostSpawnTimer = 0f;
        lastSlidePressedTime = float.NegativeInfinity;

        slideExitLockTimer = 0f;
        jumpHoldTimer = 0f;
        awaitingDiveRoll = false;
        pendingDiveRollSpeed = 0f;
        diveRollWindowEndTime = float.NegativeInfinity;
        if (consumeInput)
        {
            lastRollPressedTime = float.NegativeInfinity;
            consumedRollInputVersion = Mathf.Max(consumedRollInputVersion, rollInputVersion);
        }

        SetState(MovementState.Rolling);
        TriggerAnimator(animRollParam);
        PlayClip(slideClip, 0.85f);
        return true;
    }

    private float GetEffectiveRollDuration()
    {
        float durationScale = Mathf.Clamp(rollDurationMultiplier, 1f, 2f);
        return Mathf.Max(0.01f, rollDuration * durationScale);
    }

    private float ResolveRollSourceSpeed(float sourceSpeed)
    {
        float resolved = Mathf.Max(0f, sourceSpeed);
        if (!isGrounded || currentState != MovementState.Crouching)
        {
            return resolved;
        }

        // Raise crouch speed to the standing equivalent so roll speed and distance match a normal roll.
        float crouchMultiplier = Mathf.Clamp(crouchWalkSpeedMultiplier, 0.01f, 1f);
        float crouchTopSpeed = Mathf.Max(0.01f, moveSpeed * crouchMultiplier);
        if (resolved > crouchTopSpeed + 0.0001f)
        {
            return resolved;
        }

        float crouchNormalized = Mathf.Clamp01(resolved / crouchTopSpeed);
        float standingEquivalentSpeed = crouchNormalized * Mathf.Max(0.01f, moveSpeed);
        return Mathf.Max(resolved, standingEquivalentSpeed);
    }

    private bool TryStartGroundRollFromBuffer()
    {
        if (!enableDiveRoll || !isGrounded || !HasBufferedRollInput())
        {
            return false;
        }

        if (currentState == MovementState.Dashing
            || currentState == MovementState.GroundPound
            || currentState == MovementState.Dive
            || currentState == MovementState.Rolling
            || currentState == MovementState.Sliding
            || currentState == MovementState.SlopeSliding)
        {
            return false;
        }

        bool hasMovementIntent = desiredMoveDirection.sqrMagnitude > movementInputDeadZone * movementInputDeadZone
            || planarVelocity.magnitude > Mathf.Max(0.1f, movementInputDeadZone);
        if (!hasMovementIntent)
        {
            return false;
        }

        return StartRoll(planarVelocity.magnitude, true);
    }

    private void TryStartSlideOrCrouch()
    {
        bool crouchIntent = IsCrouchIntentActive();
        bool explicitCrouchInputActive = crouchPressedThisFrame || crouchHeld;
        if (!enableCrouchSlide)
        {
            crouchTarget = false;
            return;
        }

        // Separate roll and crouch flow by blocking automatic crouch transitions during the roll-exit window.
        if (IsRollAutoCrouchSuppressed() && !explicitCrouchInputActive)
        {
            crouchTarget = false;
            return;
        }

        if (!isGrounded)
        {
            if (!crouchToggleMode
                && !crouchHeld
                && currentState != MovementState.Sliding
                && currentState != MovementState.SlopeSliding
                && currentState != MovementState.Rolling)
            {
                crouchTarget = false;
            }
            return;
        }

        if (currentState == MovementState.GroundPound || currentState == MovementState.Rolling)
        {
            return;
        }

        if (crouchIntent)
        {
            crouchTarget = true;
            bool justLandedFromNonJumpDrop = !wasGrounded && trackingNonJumpFall && !jumpConsumed;
            bool slideBuffered = IsBuffered(lastSlidePressedTime, slideBufferTime);
            // Slide/slope-slide sadece C'ye yeni basistan gelen buffer ile baslasin.
            // Prevent acceleration or fall-driven speed gains from triggering slide while crouch state is preserved.
            bool allowSlideStartFromCrouch = !justLandedFromNonJumpDrop
                && slideBuffered
                && currentState != MovementState.Crouching;

            if (allowSlideStartFromCrouch
                && enableSlopeSlide
                && currentState != MovementState.SlopeSliding
                && CanEnterSlopeSlide())
            {
                StartSlopeSlide();
                return;
            }

            bool enoughSpeed = planarVelocity.magnitude >= slideStartMinSpeed;
            bool allowSlideFromCrouchInput = sprintHeld;
            if (allowSlideStartFromCrouch
                && slideBuffered
                && enoughSpeed
                && allowSlideFromCrouchInput
                && currentState != MovementState.Sliding
                && currentState != MovementState.SlopeSliding)
            {
                StartSlide();
                return;
            }

            if (currentState != MovementState.Sliding && currentState != MovementState.SlopeSliding)
            {
                SetState(MovementState.Crouching);
            }
        }
        else
        {
            if (currentState == MovementState.SlopeSliding)
            {
                return;
            }

            bool lowClearanceForStanding = IsLowClearanceForStanding();
            if (lowClearanceForStanding && !IsRollAutoCrouchSuppressed())
            {
                crouchTarget = true;
                if (isGrounded
                    && currentState != MovementState.Sliding
                    && currentState != MovementState.SlopeSliding
                    && currentState != MovementState.Rolling)
                {
                    SetState(MovementState.Crouching);
                }

                return;
            }

            if (currentState == MovementState.Crouching && CanStandUp())
            {
                crouchTarget = false;
                SetState(MovementState.Grounded);
            }

            if (currentState != MovementState.Sliding && currentState != MovementState.SlopeSliding)
            {
                crouchTarget = false;
            }
        }
    }

    private void StartSlide()
    {
        Vector3 direction = desiredMoveDirection;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = planarVelocity.sqrMagnitude > 0.01f ? planarVelocity.normalized : transform.forward;
        }

        direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
        }

        slideDirection = direction;
        slideSteerReferenceDirection = slideDirection;
        float startSpeed = Mathf.Min(Mathf.Max(planarVelocity.magnitude, slideStartMinSpeed) + slideInitialBoost, slideMaxSpeed);
        planarVelocity = slideDirection * startSpeed;
        slideLowClearanceLockActive = false;
        slideLowClearanceLockedSpeed = 0f;
        slideTimer = slideDuration;
        slideExitLockTimer = slideMinimumHoldTime;
        RecordSlideDebug(slideDirection, startSpeed, false);
        crouchTarget = true;
        lastSlidePressedTime = float.NegativeInfinity;
        SetState(MovementState.Sliding);
        if (enableDashGhostTrail && enableSlideGhostTrail)
        {
            SpawnDashGhostSnapshot(slideDirection);
            dashGhostSpawnTimer = Mathf.Max(0.005f, dashGhostSpawnInterval / Mathf.Clamp(dashGhostDensity, 0.1f, 2f));
        }

        TriggerAnimator(animSlideParam);
        PlayClip(slideClip);
    }

    private void StartSlideFromRollInLowClearance()
    {
        Vector3 direction = rollDirection.sqrMagnitude > 0.0001f ? rollDirection : planarVelocity;
        direction = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.forward;
        }

        direction.Normalize();
        slideDirection = direction;
        slideSteerReferenceDirection = slideDirection;

        float minimumSlideSpeed = Mathf.Max(0.01f, slideEndSpeed);
        float startSpeed = Mathf.Clamp(Mathf.Max(planarVelocity.magnitude, slideStartMinSpeed), minimumSlideSpeed, slideMaxSpeed);
        planarVelocity = slideDirection * startSpeed;
        slideLowClearanceLockActive = true;
        slideLowClearanceLockedSpeed = startSpeed;
        lastMoveCollisionFlags = CollisionFlags.None;
        slideTimer = slideDuration;
        slideExitLockTimer = slideMinimumHoldTime;
        RecordSlideDebug(slideDirection, startSpeed, false);
        crouchTarget = true;
        lastSlidePressedTime = float.NegativeInfinity;
        SetState(MovementState.Sliding);
        TriggerAnimator(animSlideParam);
        PlayClip(slideClip, 0.9f);
    }

    private bool IsLowClearanceForStanding()
    {
        if (controller == null)
        {
            return false;
        }

        return !CanStandUp();
    }

    private bool IsSlopeSlideSurfaceValid()
    {
        if (!enableSlopeSlide || !hasGroundNormal || controller == null)
        {
            return false;
        }

        if (groundSlopeAngle < slopeSlideMinAngle || groundSlopeAngle > controller.slopeLimit + 0.5f)
        {
            return false;
        }

        return groundDownhillDirection.sqrMagnitude > 0.0001f;
    }

    private bool CanEnterSlopeSlide()
    {
        if (!isGrounded || !IsSlopeSlideSurfaceValid())
        {
            return false;
        }

        if (planarVelocity.magnitude < slopeSlideEnterMinSpeed)
        {
            return false;
        }

        float downhillAlignment = Vector3.Dot(planarVelocity.normalized, groundDownhillDirection);
        return downhillAlignment > -0.1f;
    }

    private void StartSlopeSlide(bool preserveMomentum = false, bool latchCrouch = true)
    {
        Vector3 direction = planarVelocity.sqrMagnitude > 0.01f ? planarVelocity.normalized : groundDownhillDirection;
        direction = Vector3.ProjectOnPlane(direction, groundNormal).normalized;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = groundDownhillDirection.sqrMagnitude > 0.0001f ? groundDownhillDirection : transform.forward;
        }

        Vector3 downhillDirection = Vector3.ProjectOnPlane(groundDownhillDirection, groundNormal);
        if (downhillDirection.sqrMagnitude > 0.0001f)
        {
            downhillDirection.Normalize();
            // Redirect the start of an uphill slide downhill.
            if (Vector3.Dot(direction, downhillDirection) < -0.05f)
            {
                direction = downhillDirection;
            }
        }

        slideDirection = direction;
        slideSteerReferenceDirection = slideDirection;
        float entryBoost = preserveMomentum ? 0f : slideInitialBoost * 0.35f;
        float minStartSpeed = preserveMomentum ? 0f : slopeSlideEnterMinSpeed;
        float startSpeed = Mathf.Clamp(
            Mathf.Max(planarVelocity.magnitude, minStartSpeed) + entryBoost,
            minStartSpeed,
            slopeSlideMaxSpeed);
        planarVelocity = slideDirection * startSpeed;
        RecordSlideDebug(slideDirection, startSpeed, true);
        crouchTarget = latchCrouch;
        slideTimer = Mathf.Max(0.2f, slideDuration);
        slideExitLockTimer = slideMinimumHoldTime;
        slopeMomentumTimer = 0f;
        slopeMomentumSpeed = 0f;
        lastSlidePressedTime = float.NegativeInfinity;
        SetState(MovementState.SlopeSliding);
        if (enableDashGhostTrail && enableSlideGhostTrail)
        {
            SpawnDashGhostSnapshot(slideDirection);
            dashGhostSpawnTimer = Mathf.Max(0.005f, dashGhostSpawnInterval / Mathf.Clamp(dashGhostDensity, 0.1f, 2f));
        }

        TriggerAnimator(animSlideParam);
        PlayClip(slideClip, 0.95f);
    }

    private void ExitSlopeSlideToGround()
    {
        float carrySpeed = planarVelocity.magnitude * slopeMomentumRetention;
        if (carrySpeed > 0.01f && slopeMomentumCarryDuration > 0f)
        {
            slopeMomentumTimer = slopeMomentumCarryDuration;
            slopeMomentumSpeed = Mathf.Max(slopeMomentumSpeed, carrySpeed);
        }

        slideExitLockTimer = 0f;
        if (enableCrouchSlide && IsCrouchIntentActive())
        {
            crouchTarget = true;
            SetState(MovementState.Crouching);
        }
        else
        {
            crouchTarget = false;
            SetState(MovementState.Grounded);
        }
    }

    private static Vector3 ClampDirectionToSteerCone(
        Vector3 referenceDirection,
        Vector3 candidateDirection,
        Vector3 planeNormal,
        float maxAngleDegrees)
    {
        Vector3 resolvedPlaneNormal = planeNormal.sqrMagnitude > 0.0001f ? planeNormal.normalized : Vector3.up;
        Vector3 referencePlanar = Vector3.ProjectOnPlane(referenceDirection, resolvedPlaneNormal);
        Vector3 candidatePlanar = Vector3.ProjectOnPlane(candidateDirection, resolvedPlaneNormal);

        if (referencePlanar.sqrMagnitude <= 0.0001f)
        {
            referencePlanar = candidatePlanar;
        }

        if (candidatePlanar.sqrMagnitude <= 0.0001f)
        {
            candidatePlanar = referencePlanar;
        }

        if (referencePlanar.sqrMagnitude <= 0.0001f)
        {
            return Vector3.ProjectOnPlane(Vector3.forward, resolvedPlaneNormal).normalized;
        }

        referencePlanar.Normalize();
        candidatePlanar = candidatePlanar.sqrMagnitude > 0.0001f ? candidatePlanar.normalized : referencePlanar;

        float maxAngle = Mathf.Clamp(maxAngleDegrees, 0f, 89f);
        if (maxAngle <= 0.001f)
        {
            return referencePlanar;
        }

        float angle = Vector3.Angle(referencePlanar, candidatePlanar);
        if (angle <= maxAngle + 0.001f)
        {
            return candidatePlanar;
        }

        float steerT = maxAngle / Mathf.Max(angle, 0.0001f);
        return Vector3.Slerp(referencePlanar, candidatePlanar, steerT).normalized;
    }

    private Vector3 ResolveCameraForwardOnPlane(Vector3 planeNormal)
    {
        if (cameraTransform == null)
        {
            return Vector3.zero;
        }

        Vector3 resolvedPlaneNormal = planeNormal.sqrMagnitude > 0.0001f ? planeNormal.normalized : Vector3.up;
        Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, resolvedPlaneNormal);
        return cameraForward.sqrMagnitude > 0.0001f ? cameraForward.normalized : Vector3.zero;
    }

    private void TryConsumeBufferedJump()
    {
        bool jumpBuffered = IsBuffered(lastJumpPressedTime, jumpBufferTime);
        bool platformGroundAssistActive = CanUseMovingPlatformGroundAssist();
        bool coyoteValid = Time.time - lastGroundedTime <= coyoteTime || platformGroundAssistActive;
        bool isJumpingOutOfRoll = currentState == MovementState.Rolling;
        bool isJumpingOutOfSlide = currentState == MovementState.Sliding
            || currentState == MovementState.SlopeSliding;
        bool canBypassSlideJumpFilters = isJumpingOutOfSlide;
        bool jumpConsumedBlocksGroundJump = jumpConsumed && !canBypassSlideJumpFilters;
        bool actionLocked = currentState == MovementState.GroundPound
            || currentState == MovementState.Dive;
        if (!jumpBuffered || (!coyoteValid && !canBypassSlideJumpFilters) || jumpConsumedBlocksGroundJump || actionLocked)
        {
            return;
        }

        if (IsJumpLandingCooldownActive() && !platformGroundAssistActive && !canBypassSlideJumpFilters)
        {
            return;
        }

        if (!isJumpingOutOfSlide && !HasJumpHeadroom())
        {
            lastJumpPressedTime = float.NegativeInfinity;
            return;
        }

        float resolvedJumpHeight = Mathf.Max(0.1f, jumpHeight);
        if (isJumpingOutOfSlide)
        {
            CancelSlideForAction();
        }

        ForceExitCrouchForAction();
        ClearRollAirTracking();
        ClearGroundPoundRuntimeFlagsForJump();
        ForceEndDashOnJump();
        ResetDoubleJumpApexTracking();
        verticalVelocity = CalculateJumpVelocity(resolvedJumpHeight);
        jumpCutProtectedVelocity = ResolveMinimumJumpCutVelocity(resolvedJumpHeight);
        jumpHoldTimer = jumpHoldTime;
        jumpStretchPulse = 1f;
        jumpConsumed = true;
        doubleJumpDashRollOnLandingPending = false;
        DetachFromMovingPlatformForAirborneAction();
        if (isJumpingOutOfRoll)
        {
            // Roll'den jump'a geciste roll state/timer tamamen iptal edilir.
            rollTimer = 0f;
        }

        bool blockedFromStartWallContact = IsTouchingWallForJumpHangBlock();
        wallHangBlockedForCurrentJump = blockedFromStartWallContact;
        wallHangBlockedUntilFalling = blockedFromStartWallContact;
        ResetNonJumpFallTracking();
        autoRollFromMovingJump = false;
        isGrounded = false;
        slideExitLockTimer = 0f;
        lastJumpPressedTime = float.NegativeInfinity;
        lastGroundedTime = float.NegativeInfinity;
        awaitingDiveRoll = false;
        pendingDiveRollSpeed = 0f;
        diveRollWindowEndTime = float.NegativeInfinity;
        lockFacingUntilGroundInputAfterWallJump = false;
        backflipJumpActive = false;
        if (currentState == MovementState.SlopeSliding || slopeMomentumTimer > 0f)
        {
            planarVelocity *= slopeJumpSpeedMultiplier;
            slopeMomentumTimer = 0f;
            slopeMomentumSpeed = 0f;
        }

        BeginJumpFallTracking();
        SetState(MovementState.Jumping);
        SetFallAnimatorState(false);
        ToggleJumpMirrorOnJump();
        TriggerAnimator(animJumpTrigger);
        PlayClip(jumpClip);
        AddCameraKick(jumpCameraKick);
    }

    private void ToggleJumpMirrorOnJump()
    {
        if (runtimeAnimator == null || string.IsNullOrWhiteSpace(animJumpMirrorParam))
        {
            forceJumpMirrorToggleFromHangJump = false;
            return;
        }

        bool shouldToggle = enableJumpMirrorAlternation || forceJumpMirrorToggleFromHangJump;
        forceJumpMirrorToggleFromHangJump = false;
        if (!shouldToggle)
        {
            return;
        }

        jumpMirrorRuntimeValue = !jumpMirrorRuntimeValue;
        ApplyJumpMirrorAnimatorValue(jumpMirrorRuntimeValue);
    }

    private void ToggleDashMirrorOnDash()
    {
        if (runtimeAnimator == null || string.IsNullOrWhiteSpace(animDashMirrorParam))
        {
            return;
        }

        if (!enableDashMirrorAlternation)
        {
            return;
        }

        dashMirrorRuntimeValue = !dashMirrorRuntimeValue;
        ApplyDashMirrorAnimatorValue(dashMirrorRuntimeValue);
    }

    private void TryConsumeAirJump()
    {
        if (!enableDoubleJump || maxAirJumps <= 0 || isGrounded || wallHangActive)
        {
            return;
        }

        if (IsJumpLandingCooldownActive())
        {
            return;
        }

        bool jumpBuffered = IsBuffered(lastJumpPressedTime, jumpBufferTime);
        bool coyoteActive = Time.time - lastGroundedTime <= coyoteTime;
        bool actionLocked = currentState == MovementState.GroundPound
            || currentState == MovementState.Rolling
            || currentState == MovementState.Dive
            || currentState == MovementState.Teetering;

        if (!jumpBuffered || actionLocked || remainingAirJumps <= 0)
        {
            return;
        }

        if (coyoteActive && !jumpConsumed)
        {
            return;
        }

        if (!HasJumpHeadroom())
        {
            lastJumpPressedTime = float.NegativeInfinity;
            return;
        }

        remainingAirJumps--;
        didPerformAirJumpSinceGrounded = true;
        BeginDoubleJumpApexTracking();
        float resolvedJumpHeight = jumpHeight * Mathf.Max(0.25f, airJumpHeightMultiplier);
        // Air jump daima DoubleJump (frontflip) animasyonunu tetiklesin.
        LaunchAirJumpFrontflip(resolvedJumpHeight, true);
    }

    private bool ShouldUseClimbJumpAnimationForAirJump()
    {
        if (!enableWallJump || !enableWallHang || controller == null || isGrounded)
        {
            return false;
        }

        Vector3 approachDirection = ResolveWallHangApproachDirection();
        if (approachDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        float castDistance = Mathf.Max(0.05f, wallJumpProbeDistance + controller.skinWidth);
        if (!TryGetWallSurfaceNormal(approachDirection, castDistance, out Vector3 wallNormal, out float wallDistance))
        {
            return false;
        }

        float requiredContactDistance = Mathf.Max(
            0.02f,
            Mathf.Max(0f, wallHangContactDistance) + controller.skinWidth + 0.02f);
        if (wallDistance > requiredContactDistance)
        {
            return false;
        }

        return IsApproachDirectionValidForWallHangEntry(approachDirection, wallNormal);
    }

    private bool TryConsumeWallHangJump()
    {
        if (!wallHangActive || !enableWallJump || isGrounded)
        {
            return false;
        }

        if (IsJumpLandingCooldownActive())
        {
            return false;
        }

        if (ledgeHangActive && Time.time < ledgeHangInputLockUntilTime)
        {
            // Ledge yakalanirken basili kalan jump, kilit bitisinde tekrar ziplamayi tetiklemesin.
            lastJumpPressedTime = float.NegativeInfinity;
            return false;
        }

        if (ledgeClimbActive)
        {
            // Allow a jump transition during climbing when forward intent and jump are both present.
            if (!IsBuffered(lastJumpPressedTime, jumpBufferTime))
            {
                return false;
            }

            if (!HasForwardWallHangJumpIntent(wallHangNormal))
            {
                // If there is no forward intent, keep climbing and clear the jump buffer.
                lastJumpPressedTime = float.NegativeInfinity;
                return false;
            }

            if (!HasJumpHeadroom())
            {
                lastJumpPressedTime = float.NegativeInfinity;
                return true;
            }

            if (remainingAirJumps > 0)
            {
                remainingAirJumps--;
            }

            float climbJumpHeight = jumpHeight * Mathf.Max(0.25f, airJumpHeightMultiplier);
            forceJumpMirrorToggleFromHangJump = true;
            LaunchWallHangForwardJump(climbJumpHeight);
            return true;
        }

        if (!IsBuffered(lastJumpPressedTime, jumpBufferTime))
        {
            return false;
        }

        if (!HasJumpHeadroom())
        {
            lastJumpPressedTime = float.NegativeInfinity;
            return true;
        }

        if (remainingAirJumps > 0)
        {
            remainingAirJumps--;
        }

        float resolvedJumpHeight = jumpHeight * Mathf.Max(0.25f, airJumpHeightMultiplier);
        forceFallAnimatorFromClimbJump = false;
        forceJumpMirrorToggleFromHangJump = true;

        if (ledgeHangActive)
        {
            climbJumpAnimatorActive = false;
            bool ledgeReadyForClimb = Time.time >= ledgeHangClimbUnlockTime;
            bool hasForwardIntent = HasForwardWallHangJumpIntent(wallHangNormal);
            if (ledgeReadyForClimb && hasForwardIntent)
            {
                // Jump from a climb-ready ledge hold behaves like a normal ground jump.
                LaunchLedgeHangNormalJump(resolvedJumpHeight);
            }
            else
            {
                // Jump during early hang: classic backward wall-jump behavior.
                LaunchWallJump(wallHangNormal, resolvedJumpHeight);
            }
        }
        else if (HasForwardWallHangJumpIntent(wallHangNormal))
        {
            // Use climb-jump behavior from wall hang when forward intent is present.
            climbJumpAnimatorActive = true;
            LaunchWallHangForwardJump(resolvedJumpHeight);
        }
        else
        {
            // Use classic backward wall-jump or backflip for all other wall jump cases.
            climbJumpAnimatorActive = true;
            LaunchWallJump(wallHangNormal, resolvedJumpHeight);
        }

        return true;
    }

    private bool HasForwardWallHangJumpIntent(Vector3 wallNormal)
    {
        float minimumForwardInput = Mathf.Max(0.05f, movementInputDeadZone);
        if (!TryGetCharacterRelativeMovementIntent(out float forwardIntent, out _)
            || forwardIntent <= minimumForwardInput)
        {
            return false;
        }

        Vector3 towardWall = -Vector3.ProjectOnPlane(wallNormal, Vector3.up);
        if (towardWall.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        towardWall.Normalize();
        if (desiredMoveDirection.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        float towardDot = Vector3.Dot(desiredMoveDirection.normalized, towardWall);
        return towardDot >= 0.15f;
    }

    private bool TryGetCharacterRelativeMovementIntent(out float forwardIntent, out float rightIntent)
    {
        forwardIntent = 0f;
        rightIntent = 0f;

        Vector3 planarInputDirection = Vector3.ProjectOnPlane(desiredMoveDirection, Vector3.up);
        if (planarInputDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector3 characterForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (characterForward.sqrMagnitude <= 0.0001f)
        {
            characterForward = Vector3.forward;
        }

        characterForward.Normalize();
        Vector3 characterRight = Vector3.Cross(Vector3.up, characterForward);
        if (characterRight.sqrMagnitude <= 0.0001f)
        {
            characterRight = Vector3.right;
        }

        characterRight.Normalize();
        planarInputDirection.Normalize();
        forwardIntent = Mathf.Clamp(Vector3.Dot(planarInputDirection, characterForward), -1f, 1f);
        rightIntent = Mathf.Clamp(Vector3.Dot(planarInputDirection, characterRight), -1f, 1f);
        return true;
    }

    private bool TryGetCameraRelativeWallHangIntent(Vector3 wallNormal, out float towardWallIntent, out float lateralIntent)
    {
        towardWallIntent = 0f;
        lateralIntent = 0f;

        Vector3 planarInputDirection = Vector3.ProjectOnPlane(desiredMoveDirection, Vector3.up);
        if (planarInputDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector3 horizontalNormal = Vector3.ProjectOnPlane(wallNormal, Vector3.up);
        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            horizontalNormal = Vector3.ProjectOnPlane(wallHangNormal, Vector3.up);
        }

        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        planarInputDirection.Normalize();
        horizontalNormal.Normalize();
        Vector3 towardWall = -horizontalNormal;
        towardWallIntent = Mathf.Clamp(Vector3.Dot(planarInputDirection, towardWall), -1f, 1f);

        Vector3 ledgeRight = Vector3.Cross(horizontalNormal, Vector3.up);
        if (ledgeRight.sqrMagnitude > 0.0001f)
        {
            ledgeRight.Normalize();
            lateralIntent = Mathf.Clamp(Vector3.Dot(planarInputDirection, ledgeRight), -1f, 1f);
        }

        return true;
    }

    private void LaunchAirJumpFrontflip(float resolvedJumpHeight, bool playFrontflipAnimation)
    {
        ExitWallHang();
        ForceExitCrouchForAction();
        ClearRollAirTracking();
        ClearGroundPoundRuntimeFlagsForJump();
        ForceEndDashOnJump();
        verticalVelocity = CalculateJumpVelocity(resolvedJumpHeight);
        jumpCutProtectedVelocity = ResolveMinimumJumpCutVelocity(resolvedJumpHeight);
        jumpHoldTimer = jumpHoldTime;
        jumpStretchPulse = 1f;
        jumpConsumed = true;
        doubleJumpDashRollOnLandingPending = false;
        DetachFromMovingPlatformForAirborneAction();
        bool blockedFromStartWallContact = IsTouchingWallForJumpHangBlock();
        wallHangBlockedForCurrentJump = blockedFromStartWallContact;
        wallHangBlockedUntilFalling = blockedFromStartWallContact;
        ResetNonJumpFallTracking();
        autoRollFromMovingJump = true;
        isGrounded = false;
        slideExitLockTimer = 0f;
        lastJumpPressedTime = float.NegativeInfinity;
        lastGroundedTime = float.NegativeInfinity;
        awaitingDiveRoll = false;
        pendingDiveRollSpeed = 0f;
        diveRollWindowEndTime = float.NegativeInfinity;
        slopeMomentumTimer = 0f;
        slopeMomentumSpeed = 0f;
        wallJumpNoRotateActive = false;
        wallJumpForwardBackLockActive = false;
        wallJumpForcedDirection = Vector3.back;
        forceRollOnWallJumpLanding = false;
        wallJumpCameraTurnRemaining = 0f;
        lockFacingUntilGroundInputAfterWallJump = false;
        backflipJumpActive = false;
        backflipAnimatorHoldActive = false;
        climbJumpAnimatorActive = !playFrontflipAnimation;
        if (!playFrontflipAnimation)
        {
            ResetAnimatorTrigger(animFrontflipTrigger);
        }

        BeginJumpFallTracking();
        SetState(MovementState.Jumping);
        SetFallAnimatorState(false);
        ToggleJumpMirrorOnJump();
        TriggerAnimator(animJumpTrigger);
        if (playFrontflipAnimation)
        {
            TriggerAnimator(animFrontflipTrigger);
        }
        PlayClip(jumpClip, 0.95f);
        AddCameraKick(jumpCameraKick);
    }

    private void LaunchWallJump(Vector3 wallJumpNormal, float resolvedJumpHeight)
    {
        // Toggle JumpMirror first during a wall jump.
        ToggleJumpMirrorOnJump();
        ExitWallHang();
        ForceExitCrouchForAction();
        ClearRollAirTracking();
        ClearGroundPoundRuntimeFlagsForJump();
        wallHangRegrabLockUntilTime = Time.time + Mathf.Max(0f, ledgeHangRegrabLockTime);
        hangJumpCornerSnapAssistUntilTime = Time.time + Mathf.Max(0.4f, Mathf.Max(0f, ledgeHangRegrabLockTime) + 0.44f);
        verticalVelocity = CalculateJumpVelocity(resolvedJumpHeight);
        jumpCutProtectedVelocity = ResolveMinimumJumpCutVelocity(resolvedJumpHeight);
        jumpHoldTimer = jumpHoldTime;
        jumpStretchPulse = 1f;
        jumpConsumed = true;
        doubleJumpDashRollOnLandingPending = false;
        DetachFromMovingPlatformForAirborneAction();
        wallHangBlockedForCurrentJump = true;
        wallHangBlockedUntilFalling = false;
        ResetNonJumpFallTracking();
        autoRollFromMovingJump = false;
        isGrounded = false;
        slideExitLockTimer = 0f;
        lastJumpPressedTime = float.NegativeInfinity;
        lastGroundedTime = float.NegativeInfinity;
        awaitingDiveRoll = false;
        pendingDiveRollSpeed = 0f;
        diveRollWindowEndTime = float.NegativeInfinity;
        slopeMomentumTimer = 0f;
        slopeMomentumSpeed = 0f;

        Vector3 awayFromWall = Vector3.ProjectOnPlane(wallJumpNormal, Vector3.up);
        if (awayFromWall.sqrMagnitude <= 0.0001f)
        {
            awayFromWall = Vector3.ProjectOnPlane(-transform.forward, Vector3.up);
        }

        if (awayFromWall.sqrMagnitude <= 0.0001f)
        {
            awayFromWall = Vector3.back;
        }

        awayFromWall.Normalize();
        float wallJumpTravelSpeed = Mathf.Max(0.1f, Mathf.Min(moveSpeed, Mathf.Max(0f, wallJumpBackwardSpeed)));
        planarVelocity = awayFromWall * wallJumpTravelSpeed;
        wallJumpNoRotateActive = true;
        wallJumpForcedDirection = awayFromWall;
        wallJumpForwardBackLockActive = true;
        forceRollOnWallJumpLanding = false;
        wallJumpCameraTurnRemaining = 0f;
        lockFacingUntilGroundInputAfterWallJump = true;
        backflipJumpActive = true;
        backflipAnimatorHoldActive = true;

        BeginJumpFallTracking();
        SetState(MovementState.Jumping);
        SetFallAnimatorState(false);
        TriggerAnimator(animJumpTrigger);
        TriggerAnimator(animBackflipTrigger);
        PlayClip(jumpClip, 0.95f);
        AddCameraKick(jumpCameraKick);
    }

    private void LaunchLedgeHangNormalJump(float resolvedJumpHeight)
    {
        // Jumping from a ledge hold uses an exit behavior similar to a ground jump.
        ToggleJumpMirrorOnJump();
        ExitWallHang();
        ForceExitCrouchForAction();
        ClearRollAirTracking();
        ClearGroundPoundRuntimeFlagsForJump();
        wallHangRegrabLockUntilTime = Time.time + Mathf.Max(0f, ledgeHangRegrabLockTime);
        verticalVelocity = CalculateJumpVelocity(resolvedJumpHeight);
        jumpCutProtectedVelocity = ResolveMinimumJumpCutVelocity(resolvedJumpHeight);
        jumpHoldTimer = jumpHoldTime;
        jumpStretchPulse = 1f;
        jumpConsumed = true;
        doubleJumpDashRollOnLandingPending = false;
        DetachFromMovingPlatformForAirborneAction();
        bool blockedFromStartWallContact = IsTouchingWallForJumpHangBlock();
        wallHangBlockedForCurrentJump = blockedFromStartWallContact;
        wallHangBlockedUntilFalling = blockedFromStartWallContact;
        ResetNonJumpFallTracking();
        autoRollFromMovingJump = false;
        isGrounded = false;
        slideExitLockTimer = 0f;
        lastJumpPressedTime = float.NegativeInfinity;
        lastGroundedTime = float.NegativeInfinity;
        awaitingDiveRoll = false;
        pendingDiveRollSpeed = 0f;
        diveRollWindowEndTime = float.NegativeInfinity;
        slopeMomentumTimer = 0f;
        slopeMomentumSpeed = 0f;
        wallJumpNoRotateActive = false;
        wallJumpForwardBackLockActive = false;
        wallJumpForcedDirection = Vector3.back;
        forceRollOnWallJumpLanding = false;
        wallJumpCameraTurnRemaining = 0f;
        lockFacingUntilGroundInputAfterWallJump = false;
        backflipJumpActive = false;
        backflipAnimatorHoldActive = false;

        BeginJumpFallTracking();
        SetState(MovementState.Jumping);
        SetFallAnimatorState(false);
        TriggerAnimator(animJumpTrigger);
        PlayClip(jumpClip, 0.95f);
        AddCameraKick(jumpCameraKick);
    }

    private void LaunchWallHangForwardJump(float resolvedJumpHeight)
    {
        // Toggle JumpMirror before the jump starts during climb or hang jump.
        ToggleJumpMirrorOnJump();
        ExitWallHang();
        ForceExitCrouchForAction();
        ClearRollAirTracking();
        ClearGroundPoundRuntimeFlagsForJump();
        hangJumpCornerSnapAssistUntilTime = Time.time + Mathf.Max(0.4f, Mathf.Max(0f, ledgeHangRegrabLockTime) + 0.44f);
        verticalVelocity = CalculateJumpVelocity(resolvedJumpHeight);
        jumpCutProtectedVelocity = ResolveMinimumJumpCutVelocity(resolvedJumpHeight);
        jumpHoldTimer = jumpHoldTime;
        jumpStretchPulse = 1f;
        jumpConsumed = true;
        doubleJumpDashRollOnLandingPending = false;
        DetachFromMovingPlatformForAirborneAction();
        wallHangBlockedForCurrentJump = true;
        wallHangBlockedUntilFalling = true;
        ResetNonJumpFallTracking();
        autoRollFromMovingJump = false;
        isGrounded = false;
        slideExitLockTimer = 0f;
        lastJumpPressedTime = float.NegativeInfinity;
        lastGroundedTime = float.NegativeInfinity;
        awaitingDiveRoll = false;
        pendingDiveRollSpeed = 0f;
        diveRollWindowEndTime = float.NegativeInfinity;
        slopeMomentumTimer = 0f;
        slopeMomentumSpeed = 0f;
        wallJumpNoRotateActive = false;
        wallJumpForwardBackLockActive = false;
        wallJumpForcedDirection = Vector3.back;
        forceRollOnWallJumpLanding = false;
        wallJumpCameraTurnRemaining = 0f;
        lockFacingUntilGroundInputAfterWallJump = false;
        backflipJumpActive = false;
        backflipAnimatorHoldActive = false;

        Vector3 towardWall = ResolveWallHangApproachDirection();
        if (towardWall.sqrMagnitude <= 0.0001f)
        {
            towardWall = -Vector3.ProjectOnPlane(wallHangNormal, Vector3.up);
        }

        if (towardWall.sqrMagnitude > 0.0001f)
        {
            towardWall.Normalize();
            float launchPlanarSpeed = Mathf.Max(planarVelocity.magnitude, Mathf.Max(0.75f, moveSpeed * 0.45f));
            planarVelocity = towardWall * launchPlanarSpeed;
        }

        BeginJumpFallTracking();
        SetState(MovementState.Jumping);
        SetFallAnimatorState(false);
        TriggerAnimator(animJumpTrigger);
        PlayClip(jumpClip, 0.95f);
        AddCameraKick(jumpCameraKick);
    }

    private void UpdateWallHangState()
    {
        if (!enableWallJump || !enableWallHang || controller == null || IsWallHangActionLocked())
        {
            ExitWallHang();
            return;
        }

        if (Time.time < wallHangRegrabLockUntilTime)
        {
            if (wallHangActive)
            {
                ExitWallHang();
                wallJumpNoRotateActive = false;
                wallJumpForwardBackLockActive = false;
            }

            if (CanUseHangJumpCornerSnapAssist()
                && TryGetLedgeHangInfo(out Vector3 regrabLockedLedgeNormal, out Vector3 regrabLockedLedgeTopPoint)
                && ShouldForceLedgeSnapFromHangJump(regrabLockedLedgeNormal, regrabLockedLedgeTopPoint))
            {
                wallHangBlockedForCurrentJump = false;
                wallHangBlockedUntilFalling = false;
                EnterLedgeHang(regrabLockedLedgeNormal, regrabLockedLedgeTopPoint);
                return;
            }

            return;
        }

        if (!ledgeHangActive && !ledgeClimbActive && IsWallInteractionSuppressedByGroundProximity())
        {
            ExitWallHang();
            wallJumpNoRotateActive = false;
            wallJumpForwardBackLockActive = false;
            return;
        }

        if (wallJumpForwardBackLockActive)
        {
            return;
        }

        if (wallHangActive)
        {
            if (!ledgeClimbActive && !ledgeHangActive && !IsFacingTowardWallForWallHangEntry(wallHangNormal))
            {
                ExitWallHangToGroundOrFall();
                return;
            }

            bool ledgeInputLockActive = ledgeHangActive && Time.time < ledgeHangInputLockUntilTime;
            if (!ledgeClimbActive && !ledgeHangActive && !ledgeInputLockActive && ShouldReleaseWallHangNearGround())
            {
                ExitWallHangToGroundOrFall();
                return;
            }

            if (ledgeClimbActive)
            {
                ledgeHangLateralInputRuntime = 0f;
                UpdateLedgeClimbProgress();
                return;
            }

            if (ledgeHangActive)
            {
                float deadZoneSqr = movementInputDeadZone * movementInputDeadZone;
                bool hasDirectionalInput = desiredMoveDirection.sqrMagnitude > deadZoneSqr;
                if (hasDirectionalInput && !ledgeInputLockActive)
                {
                    float towardWallIntent = 0f;
                    bool hasWallRelativeIntent = TryGetCameraRelativeWallHangIntent(
                        wallHangNormal,
                        out towardWallIntent,
                        out _);
                    if (hasWallRelativeIntent
                        && towardWallIntent >= ledgeHangClimbForwardInputThreshold
                        && Time.time >= ledgeHangClimbUnlockTime)
                    {
                        // Ledge hang'den ileri input ile climb'a gec.
                        BeginLedgeClimb();
                        UpdateLedgeClimbProgress();
                        return;
                    }

                    if (hasWallRelativeIntent
                        && towardWallIntent <= Mathf.Clamp(ledgeHangReleaseBackInputThreshold, -1f, 0f))
                    {
                        ExitLedgeHangToWallSlide();
                        return;
                    }
                }

                if (ledgeHangActive)
                {
                    Vector3 currentLedgeNormal = wallHangNormal;
                    Vector3 currentLedgeTopPoint = ledgeHangTopPoint;
                    bool hasStableContact = false;
                    if (TryResolveLedgeTopPointOnWall(currentLedgeTopPoint, currentLedgeNormal, out _))
                    {
                        hasStableContact = true;
                        ledgeHangLastValidContactTime = Time.time;
                    }
                    else if (TryGetWallHangNormal(out _, true))
                    {
                        hasStableContact = true;
                        ledgeHangLastValidContactTime = Time.time;
                    }

                    if (!hasStableContact)
                    {
                        bool stabilizedByCachedLedge = false;
                        if (TryGetHandOriginsMidpoint(out Vector3 currentHandMidpoint))
                        {
                            Vector3 midpointPlanarOffset = currentHandMidpoint - currentLedgeTopPoint;
                            midpointPlanarOffset.y = 0f;
                            float planarTolerance = Mathf.Max(
                                0.24f,
                                (controller.radius + Mathf.Max(0f, ledgeGrabAssistDistance) + 0.1f));
                            float verticalTolerance = Mathf.Max(0.2f, controller.radius * 1.4f);
                            if (midpointPlanarOffset.sqrMagnitude <= planarTolerance * planarTolerance
                                && Mathf.Abs(currentHandMidpoint.y - currentLedgeTopPoint.y) <= verticalTolerance)
                            {
                                stabilizedByCachedLedge = true;
                                ledgeHangLastValidContactTime = Time.time;
                            }
                        }

                        if (!stabilizedByCachedLedge
                            && TryResolveLedgeTopPointOnWall(currentLedgeTopPoint, currentLedgeNormal, out Vector3 solvedTopPoint))
                        {
                            stabilizedByCachedLedge = true;
                            currentLedgeTopPoint = solvedTopPoint;
                            ledgeHangTopPoint = solvedTopPoint;
                            ledgeHangLastValidContactTime = Time.time;
                        }

                        if (!stabilizedByCachedLedge)
                        {
                            float grace = Mathf.Max(0.9f, ledgeHangContactLostGraceTime);
                            if (ledgeInputLockActive)
                            {
                                float remainingLock = Mathf.Max(0f, ledgeHangInputLockUntilTime - Time.time);
                                grace = Mathf.Max(grace, remainingLock + 0.02f);
                            }

                            bool contactExpired = float.IsNegativeInfinity(ledgeHangLastValidContactTime)
                                || Time.time - ledgeHangLastValidContactTime > grace;
                            if (contactExpired)
                            {
                                ExitWallHangToGroundOrFall();
                                return;
                            }
                        }
                    }

                    wallJumpNoRotateActive = true;
                    AlignCharacterToWallHangSurface();
                    ledgeHangLateralInputRuntime = MoveCharacterAlongLedge(
                        currentLedgeNormal,
                        Time.deltaTime,
                        out _,
                        out Vector3 resolvedLedgeNormal,
                        out Vector3 resolvedLedgeTopPoint);
                    if (resolvedLedgeNormal.sqrMagnitude > 0.0001f)
                    {
                        currentLedgeNormal = resolvedLedgeNormal;
                        wallHangNormal = resolvedLedgeNormal;
                        AlignCharacterToWallHangSurface();
                    }
                    if (Mathf.Abs(ledgeHangLateralInputRuntime) > 0.0001f)
                    {
                        currentLedgeTopPoint = resolvedLedgeTopPoint;
                        ledgeHangTopPoint = currentLedgeTopPoint;
                        SnapCharacterHandsMidpointToLedgeEdge(currentLedgeNormal, ref ledgeHangTopPoint);
                    }

                    jumpHoldTimer = 0f;
                    // Keep ledge hang stable and prevent downward sliding.
                    verticalVelocity = 0f;
                    if (currentState != MovementState.Falling)
                    {
                        SetState(MovementState.Falling);
                    }

                    return;
                }
            }

            ledgeHangLateralInputRuntime = 0f;
            if (ShouldReleaseWallHangFromInputDirection(wallHangNormal))
            {
                ExitWallHangToGroundOrFall();
                return;
            }

            if (!TryGetWallHangNormal(out Vector3 updatedWallNormal, true))
            {
                ExitWallHang();
                return;
            }

            if (enableLedgeHang
                && Time.time >= wallHangRegrabLockUntilTime
                && TryGetLedgeHangInfo(out Vector3 ledgeNormalWhileSliding, out Vector3 ledgeTopPointWhileSliding))
            {
                EnterLedgeHang(ledgeNormalWhileSliding, ledgeTopPointWhileSliding);
                return;
            }

            wallHangNormal = updatedWallNormal;
            wallJumpNoRotateActive = true;
            AlignCharacterToWallHangSurface();
            jumpHoldTimer = 0f;
            if (verticalVelocity > 0f)
            {
                verticalVelocity = 0f;
            }

            if (currentState != MovementState.Falling)
            {
                SetState(MovementState.Falling);
            }

            return;
        }

        if (currentState != MovementState.Jumping && currentState != MovementState.Falling)
        {
            return;
        }

        if (!jumpConsumed)
        {
            return;
        }

        if (wallHangBlockedForCurrentJump)
        {
            if (wallHangBlockedUntilFalling)
            {
                bool reachedFallingPhase = currentState == MovementState.Falling || verticalVelocity <= -0.01f;
                if (!reachedFallingPhase)
                {
                    // During ascend, allow ledge hang with tolerance if the character is still touching the edge.
                    Vector3 blockedApproachDirection = ResolveWallHangApproachDirection();
                    if (enableLedgeHang
                        && Time.time >= wallHangRegrabLockUntilTime
                        && TryGetLedgeHangInfo(out Vector3 blockedLedgeNormal, out Vector3 blockedLedgeTopPoint)
                        && (IsApproachDirectionValidForWallHangEntry(blockedApproachDirection, blockedLedgeNormal)
                            || ShouldForceLedgeSnapFromHangJump(blockedLedgeNormal, blockedLedgeTopPoint)))
                    {
                        wallHangBlockedForCurrentJump = false;
                        wallHangBlockedUntilFalling = false;
                        EnterLedgeHang(blockedLedgeNormal, blockedLedgeTopPoint);
                    }

                    return;
                }

                wallHangBlockedForCurrentJump = false;
                wallHangBlockedUntilFalling = false;
            }
            else
            {
                if (CanUseHangJumpCornerSnapAssist()
                    && Time.time >= wallHangRegrabLockUntilTime
                    && TryGetLedgeHangInfo(out Vector3 blockedLedgeNormal, out Vector3 blockedLedgeTopPoint)
                    && ShouldForceLedgeSnapFromHangJump(blockedLedgeNormal, blockedLedgeTopPoint))
                {
                    wallHangBlockedForCurrentJump = false;
                    wallHangBlockedUntilFalling = false;
                    EnterLedgeHang(blockedLedgeNormal, blockedLedgeTopPoint);
                    return;
                }

                return;
            }
        }

        Vector3 approachDirection = ResolveWallHangApproachDirection();
        if (enableLedgeHang
            && Time.time >= wallHangRegrabLockUntilTime
            && TryGetLedgeHangInfo(out Vector3 ledgeNormal, out Vector3 ledgeTopPoint)
            && (IsApproachDirectionValidForWallHangEntry(approachDirection, ledgeNormal)
                || ShouldForceLedgeSnapFromHangJump(ledgeNormal, ledgeTopPoint)))
        {
            EnterLedgeHang(ledgeNormal, ledgeTopPoint);
            return;
        }

        if (!TryGetWallHangNormal(out Vector3 detectedWallNormal))
        {
            return;
        }

        if (!IsApproachDirectionValidForWallHangEntry(approachDirection, detectedWallNormal))
        {
            return;
        }

        EnterWallHang(detectedWallNormal);
    }

    private void EnterWallHang(Vector3 wallNormal)
    {
        // Final safety: do not enter hang if facing and approach conditions are not satisfied.
        if (!wallHangActive)
        {
            Vector3 approachDirection = ResolveWallHangApproachDirection();
            if (!IsApproachDirectionValidForWallHangEntry(approachDirection, wallNormal))
            {
                return;
            }
        }

        Vector3 horizontalNormal = Vector3.ProjectOnPlane(wallNormal, Vector3.up);
        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            horizontalNormal = Vector3.ProjectOnPlane(-transform.forward, Vector3.up);
        }

        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            horizontalNormal = Vector3.back;
        }

        wallHangNormal = horizontalNormal.normalized;
        wallHangActive = true;
        ledgeHangActive = false;
        ledgeClimbActive = false;
        ledgeClimbProgress = 0f;
        ledgeClimbStartPosition = Vector3.zero;
        ledgeClimbMidPosition = Vector3.zero;
        ledgeClimbTargetPosition = Vector3.zero;
        ledgeHangTopPoint = Vector3.zero;
        ledgeHangClimbUnlockTime = float.NegativeInfinity;
        ledgeHangInputLockUntilTime = float.NegativeInfinity;
        wallJumpNoRotateActive = true;
        AlignCharacterToWallHangSurface();
        wallJumpForwardBackLockActive = false;
        wallJumpCameraTurnRemaining = 0f;
        forceRollOnWallJumpLanding = false;
        jumpHoldTimer = 0f;
        wallHangSlideEaseElapsed = 0f;
        climbJumpAnimatorActive = false;
        forceFallAnimatorFromClimbJump = false;
        wallHangReleaseHighFallCheckActive = false;
        ClearJumpFallTracking();
        verticalVelocity = Mathf.Min(verticalVelocity, 0f);
        lockFacingUntilGroundInputAfterWallJump = false;
        if (currentState != MovementState.Falling)
        {
            SetState(MovementState.Falling);
        }
    }

    private void EnterLedgeHang(Vector3 wallNormal, Vector3 ledgeTopPoint)
    {
        Vector3 horizontalNormal = Vector3.ProjectOnPlane(wallNormal, Vector3.up);
        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            horizontalNormal = Vector3.ProjectOnPlane(wallHangNormal, Vector3.up);
        }

        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            horizontalNormal = Vector3.ProjectOnPlane(-transform.forward, Vector3.up);
        }

        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            horizontalNormal = Vector3.back;
        }

        horizontalNormal.Normalize();
        wallHangNormal = horizontalNormal;
        wallHangActive = true;
        ledgeHangActive = true;
        ledgeClimbActive = false;
        ledgeHangLateralInputRuntime = 0f;
        ledgeClimbProgress = 0f;
        ledgeClimbStartPosition = transform.position;
        ledgeClimbMidPosition = transform.position;
        ledgeClimbTargetPosition = transform.position;
        ledgeHangTopPoint = ledgeTopPoint;
        wallJumpNoRotateActive = true;
        AlignCharacterToWallHangSurface();
        wallJumpForwardBackLockActive = false;
        wallJumpCameraTurnRemaining = 0f;
        forceRollOnWallJumpLanding = false;
        jumpHoldTimer = 0f;
        wallHangSlideEaseElapsed = 0f;
        climbJumpAnimatorActive = false;
        forceFallAnimatorFromClimbJump = false;
        wallHangReleaseHighFallCheckActive = false;
        ClearJumpFallTracking();
        lockFacingUntilGroundInputAfterWallJump = false;

        float entryLockDuration = Mathf.Max(0f, ledgeHangEntryInputLockDuration);
        ledgeHangInputLockUntilTime = Time.time + entryLockDuration;
        // Kose yakalamada cok uzun beklemeden, kilit biter bitmez ileri input varsa climb'a gec.
        float climbUnlockDelay = entryLockDuration <= 0f
            ? Mathf.Max(0f, ledgeHangHoldBeforeClimbDuration)
            : Mathf.Min(Mathf.Max(0f, ledgeHangHoldBeforeClimbDuration), entryLockDuration);
        ledgeHangClimbUnlockTime = Time.time + climbUnlockDelay;
        ledgeHangLastValidContactTime = Time.time;
        lastJumpPressedTime = float.NegativeInfinity;
        SnapCharacterHandsMidpointToLedgeEdge(wallHangNormal, ref ledgeHangTopPoint);
        planarVelocity = Vector3.zero;
        verticalVelocity = 0f;
        if (currentState != MovementState.Falling)
        {
            SetState(MovementState.Falling);
        }
    }

    private void ExitLedgeHangToWallSlide()
    {
        if (!wallHangActive)
        {
            return;
        }

        wallHangRegrabLockUntilTime = Time.time + Mathf.Max(0f, ledgeHangRegrabLockTime);
        ExitWallHangToGroundOrFall(-0.15f, true);
    }

    private void BeginLedgeClimb()
    {
        if (!ledgeHangActive)
        {
            return;
        }

        // Tirmanma gercek edge tutusundan baslasin; probe anlik kacarsa mevcut tutunma verisini kullan.
        bool hasConfirmedLedge = TryGetLedgeHangInfo(out Vector3 confirmedWallNormal, out Vector3 confirmedLedgeTopPoint);
        if (!hasConfirmedLedge)
        {
            confirmedWallNormal = wallHangNormal;
            confirmedLedgeTopPoint = ledgeHangTopPoint;
            if (confirmedWallNormal.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            if (TryResolveLedgeTopPointOnWall(confirmedLedgeTopPoint, confirmedWallNormal, out Vector3 resolvedCachedTopPoint))
            {
                confirmedLedgeTopPoint = resolvedCachedTopPoint;
            }
            else if (!TryGetHandOriginsMidpoint(out confirmedLedgeTopPoint))
            {
                return;
            }
        }

        if (Vector3.Dot(
                Vector3.ProjectOnPlane(confirmedWallNormal, Vector3.up).normalized,
                Vector3.ProjectOnPlane(wallHangNormal, Vector3.up).normalized) < 0.25f)
        {
            return;
        }

        // If an obstacle or vertex exists above the edge, do not start climbing; return to wall-hang slide.
        if (!IsLedgeTopClimbClearForEntry(confirmedLedgeTopPoint, confirmedWallNormal))
        {
            ExitLedgeHangToWallSlide();
            return;
        }

        wallHangNormal = confirmedWallNormal;
        ledgeHangTopPoint = confirmedLedgeTopPoint;
        AlignCharacterToWallHangSurface();
        MoveCharacterTowardsLedgeAnchor(wallHangNormal, ledgeHangTopPoint, Time.deltaTime, true);

        Vector3 towardWall = -Vector3.ProjectOnPlane(wallHangNormal, Vector3.up);
        if (towardWall.sqrMagnitude <= 0.0001f)
        {
            towardWall = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        if (towardWall.sqrMagnitude <= 0.0001f)
        {
            towardWall = Vector3.forward;
        }

        towardWall.Normalize();
        ledgeHangActive = false;
        ledgeClimbActive = true;
        TrySetAnimatorBoolDirect(animClimbParam, true);
        ledgeHangClimbUnlockTime = float.NegativeInfinity;
        ledgeHangInputLockUntilTime = float.NegativeInfinity;
        ledgeHangLastValidContactTime = float.NegativeInfinity;
        ledgeClimbProgress = 0f;
        ledgeClimbStartPosition = transform.position;
        ledgeClimbTargetPosition = ResolveLedgeClimbTargetPosition(ledgeHangTopPoint, towardWall);
        float midY = Mathf.Lerp(ledgeClimbStartPosition.y, ledgeClimbTargetPosition.y, 0.82f);
        ledgeClimbMidPosition = new Vector3(
            ledgeClimbStartPosition.x,
            midY,
            ledgeClimbStartPosition.z);
        jumpHoldTimer = 0f;
        planarVelocity = Vector3.zero;
        verticalVelocity = 0f;
    }

    private void UpdateLedgeClimbProgress()
    {
        if (!ledgeClimbActive || controller == null)
        {
            return;
        }

        float duration = Mathf.Max(0.05f, ledgeHangClimbDuration);
        ledgeClimbProgress = Mathf.Clamp01(ledgeClimbProgress + Time.deltaTime / duration);
        const float holdEdgePortion = 0.62f;
        Vector3 desiredPosition;
        if (ledgeClimbProgress <= holdEdgePortion)
        {
            float phase01 = Mathf.Clamp01(ledgeClimbProgress / holdEdgePortion);
            float eased = phase01 * phase01 * (3f - 2f * phase01);
            desiredPosition = Vector3.Lerp(ledgeClimbStartPosition, ledgeClimbMidPosition, eased);
        }
        else
        {
            float phase01 = Mathf.Clamp01((ledgeClimbProgress - holdEdgePortion) / (1f - holdEdgePortion));
            float eased = phase01 * phase01 * (3f - 2f * phase01);
            desiredPosition = Vector3.Lerp(ledgeClimbMidPosition, ledgeClimbTargetPosition, eased);
        }

        Vector3 delta = desiredPosition - transform.position;
        if (delta.sqrMagnitude > 0.0000001f)
        {
            controller.Move(delta);
        }

        wallJumpNoRotateActive = true;
        jumpHoldTimer = 0f;
        planarVelocity = Vector3.zero;
        verticalVelocity = 0f;

        if (ledgeClimbProgress < 0.9999f)
        {
            return;
        }

        ledgeClimbActive = false;
        ledgeHangActive = false;
        wallHangActive = false;
        ledgeClimbProgress = 0f;
        ledgeClimbStartPosition = Vector3.zero;
        ledgeClimbMidPosition = Vector3.zero;
        ledgeClimbTargetPosition = Vector3.zero;
        ledgeHangTopPoint = Vector3.zero;
        ledgeHangClimbUnlockTime = float.NegativeInfinity;
        ledgeHangInputLockUntilTime = float.NegativeInfinity;
        ledgeHangLastValidContactTime = float.NegativeInfinity;
        wallHangSlideEaseElapsed = 0f;
        wallHangRegrabLockUntilTime = Time.time + Mathf.Max(Mathf.Max(0f, ledgeHangRegrabLockTime), Mathf.Max(0f, wallPostClimbDetachLockTime));
        wallJumpNoRotateActive = false;
        wallJumpForwardBackLockActive = false;
        wallJumpForcedDirection = Vector3.back;
        forceRollOnWallJumpLanding = false;
        lockFacingUntilGroundInputAfterWallJump = false;
        jumpConsumed = false;
        isGrounded = true;
        lastGroundedTime = Time.time;
        ResetAirJumps();
        verticalVelocity = groundedVerticalForce;
        jumpHoldTimer = 0f;
        slideExitLockTimer = 0f;
        SetState(enableCrouchSlide && IsCrouchIntentActive()
            ? MovementState.Crouching
            : MovementState.Grounded);
    }

    private Vector3 ResolveLedgeClimbTargetPosition(Vector3 ledgeTopPoint, Vector3 towardWall)
    {
        Vector3 fallbackTarget = transform.position
            + Vector3.up * Mathf.Max(0f, ledgeHangClimbUpDistance)
            + towardWall * Mathf.Max(0f, ledgeHangClimbForwardDistance);
        if (controller == null)
        {
            return fallbackTarget;
        }

        Bounds bounds = controller.bounds;
        float feetOffsetFromTransform = bounds.min.y - transform.position.y;
        float desiredFeetY = ledgeTopPoint.y + Mathf.Max(controller.skinWidth + 0.02f, 0.04f);
        float desiredTransformY = desiredFeetY - feetOffsetFromTransform;
        float minimumTransformY = transform.position.y + Mathf.Max(0f, ledgeHangClimbUpDistance);
        desiredTransformY = Mathf.Max(desiredTransformY, minimumTransformY);

        float desiredForwardDistance = Mathf.Max(
            Mathf.Max(0f, ledgeHangClimbForwardDistance),
            controller.radius + controller.skinWidth + 0.08f);
        desiredForwardDistance += Mathf.Max(0f, ledgeClimbStepForwardBonus);
        Vector3 desiredPosition = ledgeTopPoint + towardWall * desiredForwardDistance;
        desiredPosition.y = desiredTransformY;

        return desiredPosition;
    }

    private void MoveCharacterTowardsLedgeAnchor(Vector3 wallNormal, Vector3 ledgeTopPoint, float deltaTime, bool snapInstantly)
    {
        if (controller == null || !TryGetHandOriginsMidpoint(out Vector3 handMidpoint))
        {
            return;
        }

        Vector3 horizontalNormal = Vector3.ProjectOnPlane(wallNormal, Vector3.up);
        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            horizontalNormal = Vector3.ProjectOnPlane(wallHangNormal, Vector3.up);
        }

        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            horizontalNormal = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            horizontalNormal = Vector3.forward;
        }

        horizontalNormal.Normalize();
        if (!TryResolveLedgeHangAnchorPoint(wallNormal, ledgeTopPoint, out Vector3 anchorPoint, out Vector3 resolvedHorizontalNormal))
        {
            return;
        }

        horizontalNormal = resolvedHorizontalNormal;

        Vector3 targetDelta = anchorPoint - handMidpoint;
        if (!snapInstantly)
        {
            // During continuous alignment, operate only toward the wall (forward) and on the upward axis.
            // Let player input alone drive left-right shimmy along the ledge.
            Vector3 towardWall = -horizontalNormal;
            if (towardWall.sqrMagnitude > 0.0001f)
            {
                towardWall.Normalize();
                targetDelta = Vector3.Project(targetDelta, towardWall) + Vector3.Project(targetDelta, Vector3.up);
            }
            else
            {
                targetDelta = Vector3.Project(targetDelta, Vector3.up);
            }
        }

        float deltaMagnitude = targetDelta.magnitude;
        float tolerance = Mathf.Max(0f, ledgeHangAnchorAlignTolerance);
        if (deltaMagnitude <= tolerance + 0.0001f)
        {
            return;
        }

        if (snapInstantly)
        {
            controller.Move(targetDelta);
            return;
        }

        float alignSpeed = Mathf.Max(0f, ledgeHangAnchorAlignSpeed);
        float maxStep = Mathf.Max(0f, alignSpeed * Mathf.Max(0f, deltaTime));
        Vector3 stepDelta;
        if (maxStep <= 0.0001f || deltaMagnitude <= maxStep)
        {
            stepDelta = targetDelta;
        }
        else
        {
            stepDelta = targetDelta / deltaMagnitude * maxStep;
        }

        controller.Move(stepDelta);
    }

    private void SnapCharacterHandsMidpointToLedgeEdge(Vector3 wallNormal, ref Vector3 ledgeTopPoint)
    {
        if (controller == null)
        {
            return;
        }

        float stopThreshold = 0.001f;
        int maxIterations = 4;
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            if (!TryGetHandOriginsMidpoint(out Vector3 handMidpoint))
            {
                break;
            }

            if (TryResolveLedgeTopPointNearestToHandMidpoint(wallNormal, ledgeTopPoint, out Vector3 refinedTopPoint))
            {
                ledgeTopPoint = refinedTopPoint;
            }

            if (!TryResolveLedgeHangAnchorPoint(wallNormal, ledgeTopPoint, out Vector3 anchorPoint, out _))
            {
                break;
            }

            Vector3 snapDelta = anchorPoint - handMidpoint;
            if (snapDelta.sqrMagnitude <= stopThreshold * stopThreshold)
            {
                break;
            }

            controller.Move(snapDelta);
        }
    }

    private float ResolveLedgeHangRuntimeVerticalBias()
    {
        float runtimeVerticalBias = ledgeHangHandMidpointVerticalBias;
        if (Mathf.Abs(runtimeVerticalBias) <= 0.0001f && controller != null)
        {
            // Preserve the downward bias automatically if the new field is still zero in the scene.
            runtimeVerticalBias = -Mathf.Clamp(controller.radius * 0.72f, 0.22f, 0.45f);
        }

        return runtimeVerticalBias + ledgeHangVerticalCalibrationOffset;
    }

    private bool TryResolveLedgeHangAnchorPoint(
        Vector3 wallNormal,
        Vector3 ledgeTopPoint,
        out Vector3 anchorPoint,
        out Vector3 resolvedHorizontalNormal)
    {
        anchorPoint = ledgeTopPoint;
        resolvedHorizontalNormal = Vector3.zero;
        if (controller == null)
        {
            return false;
        }

        Vector3 horizontalNormal = Vector3.ProjectOnPlane(wallNormal, Vector3.up);
        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            horizontalNormal = Vector3.ProjectOnPlane(wallHangNormal, Vector3.up);
        }

        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            horizontalNormal = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            horizontalNormal = Vector3.forward;
        }

        horizontalNormal.Normalize();
        resolvedHorizontalNormal = horizontalNormal;

        if (ledgeHangSnapHandsMidpointToEdge)
        {
            anchorPoint = ledgeTopPoint;
            return true;
        }

        float targetHandDistance = Mathf.Clamp(ledgeHangHandSurfaceTargetOffset, -0.05f, 0.05f);
        anchorPoint = ledgeTopPoint + horizontalNormal * targetHandDistance;
        anchorPoint += horizontalNormal * ledgeHangWallProximityOffset;
        anchorPoint.y += ResolveLedgeHangRuntimeVerticalBias();
        if (ledgeHangUseAnchorOffsets)
        {
            anchorPoint.y += ledgeHangAnchorVerticalOffset;
            anchorPoint += horizontalNormal * Mathf.Max(0f, ledgeHangAnchorOutwardOffset);
        }

        if (TryGetHandDistanceExtentsToPlane(ledgeTopPoint, horizontalNormal, out _, out float farthestHandDistance))
        {
            float toleranceToSurface = Mathf.Max(0f, ledgeHangDualHandSurfaceTolerance);
            float requiredTowardWallShift = farthestHandDistance - targetHandDistance - toleranceToSurface;
            if (requiredTowardWallShift > 0.0001f)
            {
                float maxExtraPush = Mathf.Max(0f, ledgeHangDualHandMaxExtraPush);
                float clampedShift = maxExtraPush > 0.0001f
                    ? Mathf.Min(requiredTowardWallShift, maxExtraPush)
                    : requiredTowardWallShift;
                anchorPoint -= horizontalNormal * clampedShift;
            }
        }

        return true;
    }

    private bool IsLedgeHangCandidateWithinEntrySnapLimits(Vector3 wallNormal, Vector3 ledgeTopPoint)
    {
        if (ledgeHangActive || ledgeClimbActive || controller == null)
        {
            return true;
        }

        if (!TryGetHandOriginsMidpoint(out Vector3 handMidpoint))
        {
            return false;
        }

        Vector3 horizontalNormal = Vector3.ProjectOnPlane(wallNormal, Vector3.up);
        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            horizontalNormal = Vector3.ProjectOnPlane(wallHangNormal, Vector3.up);
        }

        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            horizontalNormal = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            horizontalNormal = Vector3.forward;
        }

        horizontalNormal.Normalize();

        float targetHandDistance = Mathf.Clamp(ledgeHangHandSurfaceTargetOffset, -0.05f, 0.05f);
        Vector3 anchorPoint = ledgeTopPoint + horizontalNormal * targetHandDistance;
        anchorPoint += horizontalNormal * ledgeHangWallProximityOffset;
        if (ledgeHangUseAnchorOffsets)
        {
            anchorPoint += horizontalNormal * Mathf.Max(0f, ledgeHangAnchorOutwardOffset);
        }

        Vector3 deltaToAnchor = anchorPoint - handMidpoint;
        float totalDistance = deltaToAnchor.magnitude;
        float horizontalDistance = Vector3.ProjectOnPlane(deltaToAnchor, Vector3.up).magnitude;
        float verticalDistance = Mathf.Abs(deltaToAnchor.y);
        if (totalDistance > Mathf.Max(0.05f, ledgeHangMaxEntrySnapDistance))
        {
            return false;
        }

        if (horizontalDistance > Mathf.Max(0.05f, ledgeHangMaxEntryHorizontalDistance))
        {
            return false;
        }

        if (verticalDistance > Mathf.Max(0.05f, ledgeHangMaxEntryVerticalDistance))
        {
            return false;
        }

        if (handMidpoint.y > ledgeTopPoint.y + Mathf.Max(0f, ledgeHangMaxHandAboveEdge))
        {
            return false;
        }

        if (TryGetHandDistanceExtentsToPlane(ledgeTopPoint, horizontalNormal, out _, out float farthestHandDistanceToWall))
        {
            if (farthestHandDistanceToWall > Mathf.Max(0.05f, ledgeHangMaxEntryWallDistance))
            {
                return false;
            }
        }

        return true;
    }

    private float ComputeLedgeMidpointCandidateScore(Vector3 wallNormal, Vector3 ledgeTopPoint)
    {
        if (!TryGetHandOriginsMidpoint(out Vector3 handMidpoint))
        {
            return float.PositiveInfinity;
        }

        if (TryResolveLedgeHangAnchorPoint(wallNormal, ledgeTopPoint, out Vector3 anchorPoint, out _))
        {
            Vector3 anchorDelta = anchorPoint - handMidpoint;
            return anchorDelta.sqrMagnitude;
        }

        Vector3 delta = ledgeTopPoint - handMidpoint;
        Vector3 planarDelta = Vector3.ProjectOnPlane(delta, Vector3.up);
        return planarDelta.sqrMagnitude + Mathf.Abs(delta.y) * 0.25f;
    }

    private float MoveCharacterAlongLedge(
        Vector3 wallNormal,
        float deltaTime,
        out Vector3 appliedDelta,
        out Vector3 resolvedWallNormal,
        out Vector3 resolvedLedgeTopPoint)
    {
        appliedDelta = Vector3.zero;
        resolvedLedgeTopPoint = ledgeHangTopPoint;
        resolvedWallNormal = wallNormal;
        if (controller == null)
        {
            return 0f;
        }

        float threshold = Mathf.Clamp01(ledgeHangLateralInputThreshold);
        float characterRightIntent = 0f;
        if (TryGetCharacterRelativeMovementIntent(out _, out float rightIntent))
        {
            characterRightIntent = rightIntent;
        }

        float lateralInput = Mathf.Abs(characterRightIntent) >= threshold
            ? Mathf.Clamp(characterRightIntent, -1f, 1f)
            : 0f;
        if (Mathf.Abs(lateralInput) <= 0.0001f)
        {
            return 0f;
        }

        Vector3 horizontalNormal = Vector3.ProjectOnPlane(wallNormal, Vector3.up);
        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            horizontalNormal = Vector3.ProjectOnPlane(wallHangNormal, Vector3.up);
        }

        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            horizontalNormal = Vector3.ProjectOnPlane(-transform.forward, Vector3.up);
        }

        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            return 0f;
        }

        horizontalNormal.Normalize();
        resolvedWallNormal = horizontalNormal;
        float speed = Mathf.Max(0f, ledgeHangLateralMoveSpeed);
        if (speed <= 0.0001f || deltaTime <= 0f)
        {
            return 0f;
        }

        float intendedDistance = Mathf.Abs(lateralInput) * speed * deltaTime;
        if (intendedDistance <= 0.0001f)
        {
            return 0f;
        }

        Vector3 currentWallNormal = horizontalNormal;
        Vector3 currentTopPoint = ledgeHangTopPoint;
        Vector3 accumulatedDelta = Vector3.zero;
        float remainingDistance = intendedDistance;
        bool transitionedCorner = false;
        const int maxCornerTransitionsPerStep = 2;
        for (int transitionStep = 0;
             transitionStep < maxCornerTransitionsPerStep && remainingDistance > 0.0001f;
             transitionStep++)
        {
            Vector3 desiredAlongEdge = ResolveLedgeDirectionForLateralInput(currentWallNormal, lateralInput);
            if (desiredAlongEdge.sqrMagnitude <= 0.0001f)
            {
                break;
            }

            float limitedDistance = LimitLedgeMovementNearCorner(
                currentWallNormal,
                desiredAlongEdge,
                remainingDistance,
                currentTopPoint);
            if (limitedDistance > 0.0001f)
            {
                float movedDistance = MoveAlongValidatedLedgeSegment(
                    currentWallNormal,
                    desiredAlongEdge,
                    currentTopPoint,
                    limitedDistance,
                    out Vector3 segmentDelta,
                    out Vector3 endTopPoint);
                if (movedDistance > 0.0001f)
                {
                    accumulatedDelta += segmentDelta;
                    currentTopPoint = endTopPoint;
                    remainingDistance = Mathf.Max(0f, remainingDistance - movedDistance);
                }
                else
                {
                    break;
                }
            }

            if (remainingDistance <= 0.0001f)
            {
                break;
            }

            if (!TryResolveCornerTransition(
                    currentWallNormal,
                    desiredAlongEdge,
                    currentTopPoint,
                    remainingDistance,
                    out Vector3 cornerWallNormal,
                    out Vector3 cornerTopPoint))
            {
                break;
            }

            currentWallNormal = cornerWallNormal;
            currentTopPoint = cornerTopPoint;
            transitionedCorner = true;
        }

        appliedDelta = accumulatedDelta;
        appliedDelta.y = 0f;
        resolvedWallNormal = currentWallNormal;
        resolvedLedgeTopPoint = currentTopPoint;
        if (TryResolveLedgeTopPointOnWall(resolvedLedgeTopPoint, resolvedWallNormal, out Vector3 solvedTopPoint))
        {
            resolvedLedgeTopPoint = solvedTopPoint;
        }

        return (appliedDelta.sqrMagnitude > 0.0000001f || transitionedCorner) ? lateralInput : 0f;
    }

    private static Vector3 ResolveLedgeDirectionForLateralInput(Vector3 wallNormal, float lateralInput)
    {
        Vector3 ledgeRight = Vector3.Cross(wallNormal, Vector3.up);
        if (ledgeRight.sqrMagnitude <= 0.0001f || Mathf.Abs(lateralInput) <= 0.0001f)
        {
            return Vector3.zero;
        }

        ledgeRight.Normalize();
        return ledgeRight * Mathf.Sign(lateralInput);
    }

    private float LimitLedgeMovementNearCorner(
        Vector3 wallNormal,
        Vector3 edgeDirection,
        float requestedDistance,
        Vector3 referenceTopPoint)
    {
        if (requestedDistance <= 0.0001f)
        {
            return 0f;
        }

        float stopDistance = Mathf.Max(0f, ledgeHangCornerStopDistance);
        if (stopDistance <= 0.0001f || controller == null || edgeDirection.sqrMagnitude <= 0.0001f)
        {
            return requestedDistance;
        }

        edgeDirection.Normalize();
        float scanDistance = requestedDistance + stopDistance + Mathf.Max(0.08f, controller.radius * 0.6f);
        if (!TryGetDistanceToLedgeCorner(
                wallNormal,
                edgeDirection,
                scanDistance,
                referenceTopPoint,
                out float cornerDistance))
        {
            return requestedDistance;
        }

        float handToCornerDistance = cornerDistance;
        if (TryGetHandOriginsMidpoint(out Vector3 handMidpoint))
        {
            float handAlongEdgeOffset = Vector3.Dot(handMidpoint - referenceTopPoint, edgeDirection);
            handToCornerDistance = cornerDistance - handAlongEdgeOffset;
        }

        float handCenterStopBuffer = ResolveHandCenterCornerStopBuffer();
        float veryCloseStopDistance = Mathf.Max(
            handCenterStopBuffer,
            Mathf.Min(stopDistance, Mathf.Max(0.004f, controller.skinWidth * 0.35f)));
        float allowedDistance = Mathf.Max(0f, handToCornerDistance - veryCloseStopDistance);
        return Mathf.Min(requestedDistance, allowedDistance);
    }

    private float ResolveHandCenterCornerStopBuffer()
    {
        Transform leftHand = ResolveLedgeHandTransform(true);
        Transform rightHand = ResolveLedgeHandTransform(false);
        float maxHandRadius = 0f;
        if (TryGetHandTransformProbeSphere(leftHand, leftHandLedgeProbeRadius, out _, out float leftRadius))
        {
            maxHandRadius = Mathf.Max(maxHandRadius, leftRadius);
        }

        if (TryGetHandTransformProbeSphere(rightHand, rightHandLedgeProbeRadius, out _, out float rightRadius))
        {
            maxHandRadius = Mathf.Max(maxHandRadius, rightRadius);
        }

        if (maxHandRadius <= 0.0001f)
        {
            maxHandRadius = controller != null ? Mathf.Max(0.01f, controller.radius * 0.08f) : 0.01f;
        }

        // "Neredeyse degme" davranisi: yaricaptan cok daha kucuk bir tampon.
        return Mathf.Max(0.002f, maxHandRadius * 0.12f);
    }

    private float ResolveHandCenterWallInsetOffset()
    {
        Transform leftHand = ResolveLedgeHandTransform(true);
        Transform rightHand = ResolveLedgeHandTransform(false);
        float maxHandRadius = 0f;
        if (TryGetHandTransformProbeSphere(leftHand, leftHandLedgeProbeRadius, out _, out float leftRadius))
        {
            maxHandRadius = Mathf.Max(maxHandRadius, leftRadius);
        }

        if (TryGetHandTransformProbeSphere(rightHand, rightHandLedgeProbeRadius, out _, out float rightRadius))
        {
            maxHandRadius = Mathf.Max(maxHandRadius, rightRadius);
        }

        if (maxHandRadius <= 0.0001f)
        {
            return 0f;
        }

        // Keep the hand center closer to the edge or wall surface.
        return -Mathf.Clamp(maxHandRadius * 0.82f, 0.01f, 0.1f);
    }

    private bool TryGetDistanceToLedgeCorner(
        Vector3 wallNormal,
        Vector3 edgeDirection,
        float maxDistance,
        Vector3 referenceTopPoint,
        out float cornerDistance)
    {
        cornerDistance = float.PositiveInfinity;
        if (controller == null || maxDistance <= 0.0001f || edgeDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        edgeDirection.Normalize();
        float step = Mathf.Clamp(controller.radius * 0.2f, 0.005f, 0.04f);
        bool sawValidSegment = false;
        int invalidStreak = 0;
        for (float distance = step; distance <= maxDistance + 0.0001f; distance += step)
        {
            Vector3 sampleEdgePoint = referenceTopPoint + edgeDirection * distance;
            if (IsLedgeContinuationValid(sampleEdgePoint, wallNormal, referenceTopPoint))
            {
                sawValidSegment = true;
                invalidStreak = 0;
                continue;
            }

            if (!sawValidSegment)
            {
                // Baslangicta olasi probe jitter'inda hareketi kilitleme.
                continue;
            }

            invalidStreak++;
            if (invalidStreak >= 2)
            {
                cornerDistance = Mathf.Max(0f, distance - step * invalidStreak);
                return true;
            }
        }

        return false;
    }

    private float MoveAlongValidatedLedgeSegment(
        Vector3 wallNormal,
        Vector3 edgeDirection,
        Vector3 startTopPoint,
        float requestedDistance,
        out Vector3 segmentDelta,
        out Vector3 endTopPoint)
    {
        segmentDelta = Vector3.zero;
        endTopPoint = startTopPoint;
        if (controller == null || requestedDistance <= 0.0001f || edgeDirection.sqrMagnitude <= 0.0001f)
        {
            return 0f;
        }

        edgeDirection.Normalize();
        float remaining = requestedDistance;
        float movedDistance = 0f;
        float step = Mathf.Max(0.02f, Mathf.Min(0.07f, controller.radius * 0.32f));
        int safety = 0;
        while (remaining > 0.0001f && safety++ < 128)
        {
            float moveStep = Mathf.Min(step, remaining);
            Vector3 candidateTopPoint = endTopPoint + edgeDirection * moveStep;
            if (!IsLedgeContinuationValid(candidateTopPoint, wallNormal, endTopPoint))
            {
                break;
            }

            Vector3 before = transform.position;
            controller.Move(edgeDirection * moveStep);
            Vector3 actualMove = transform.position - before;
            actualMove.y = 0f;
            float movedAlongEdge = Vector3.Dot(actualMove, edgeDirection);
            if (movedAlongEdge <= moveStep * 0.15f)
            {
                break;
            }

            segmentDelta += actualMove;
            movedDistance += movedAlongEdge;
            endTopPoint += edgeDirection * movedAlongEdge;
            remaining -= movedAlongEdge;
        }

        return movedDistance;
    }

    private bool TryResolveCornerTransition(
        Vector3 currentWallNormal,
        Vector3 desiredAlongEdge,
        Vector3 cornerReferenceTopPoint,
        float remainingDistance,
        out Vector3 cornerWallNormal,
        out Vector3 cornerTopPoint)
    {
        cornerWallNormal = Vector3.zero;
        cornerTopPoint = cornerReferenceTopPoint;
        if (controller == null || desiredAlongEdge.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector3 normalizedDesiredAlongEdge = desiredAlongEdge.normalized;
        float probeDistance = Mathf.Max(
            0.08f,
            Mathf.Max(0f, remainingDistance)
            +
            Mathf.Max(wallJumpProbeDistance, wallHangContactDistance)
            + controller.skinWidth
            + Mathf.Max(0f, ledgeGrabAssistDistance)
            + Mathf.Max(0f, ledgeHangCornerStopDistance)
            + Mathf.Max(0f, ledgeHangCornerTransitionAssistDistance));

        bool foundCandidate = false;
        float bestAlignmentScore = float.PositiveInfinity;
        Vector3 bestNormal = Vector3.zero;
        Vector3 bestTopPoint = cornerReferenceTopPoint;

        if (TryGetWallSurfaceNormal(normalizedDesiredAlongEdge, probeDistance, out Vector3 forwardProbeNormal, out _)
            && TryEvaluateCornerTransitionCandidate(
                currentWallNormal,
                forwardProbeNormal,
                cornerReferenceTopPoint,
                out Vector3 forwardTopPoint,
                out float forwardAlignmentScore))
        {
            foundCandidate = true;
            bestAlignmentScore = forwardAlignmentScore;
            bestNormal = forwardProbeNormal;
            bestTopPoint = forwardTopPoint;
        }

        if (TryGetWallSurfaceNormal(-normalizedDesiredAlongEdge, probeDistance, out Vector3 backwardProbeNormal, out _)
            && TryEvaluateCornerTransitionCandidate(
                currentWallNormal,
                backwardProbeNormal,
                cornerReferenceTopPoint,
                out Vector3 backwardTopPoint,
                out float backwardAlignmentScore))
        {
            if (!foundCandidate || backwardAlignmentScore < bestAlignmentScore)
            {
                foundCandidate = true;
                bestAlignmentScore = backwardAlignmentScore;
                bestNormal = backwardProbeNormal;
                bestTopPoint = backwardTopPoint;
            }
        }

        if (!foundCandidate)
        {
            return false;
        }

        cornerWallNormal = bestNormal;
        cornerTopPoint = bestTopPoint;
        return true;
    }

    private bool TryEvaluateCornerTransitionCandidate(
        Vector3 currentWallNormal,
        Vector3 candidateNormal,
        Vector3 cornerReferenceTopPoint,
        out Vector3 candidateTopPoint,
        out float normalAlignmentScore)
    {
        candidateTopPoint = cornerReferenceTopPoint;
        float normalSimilarity = Vector3.Dot(candidateNormal, currentWallNormal);
        normalAlignmentScore = Mathf.Abs(normalSimilarity);
        if (normalSimilarity >= 0.95f)
        {
            return false;
        }

        if (!TryResolveLedgeTopPointOnWall(cornerReferenceTopPoint, candidateNormal, out candidateTopPoint))
        {
            return false;
        }

        return IsLedgeContinuationValid(candidateTopPoint, candidateNormal, candidateTopPoint);
    }

    private bool TryResolveLedgeTopPointOnWall(Vector3 referenceTopPoint, Vector3 wallNormal, out Vector3 topPoint)
    {
        topPoint = referenceTopPoint;
        if (controller == null)
        {
            return false;
        }

        Vector3 horizontalNormal = Vector3.ProjectOnPlane(wallNormal, Vector3.up);
        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        horizontalNormal.Normalize();
        int mask = GetClimbMaskExcludingSelf();
        float probeUpDistance = Mathf.Max(0.05f, ledgeTopProbeUpDistance * 0.8f);
        float probeInset = Mathf.Max(0f, ledgeTopProbeForwardInset);
        float probeDownDistance = Mathf.Max(0.15f, ledgeTopProbeDownDistance);
        Vector3 topProbeOrigin = referenceTopPoint + Vector3.up * probeUpDistance - horizontalNormal * probeInset;
        if (!Physics.Raycast(topProbeOrigin, Vector3.down, out RaycastHit topHit, probeDownDistance, mask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (topHit.collider == null
            || topHit.collider.transform == transform
            || topHit.collider.transform.IsChildOf(transform))
        {
            return false;
        }

        if (topHit.normal.y < Mathf.Clamp01(ledgeTopSurfaceMinUpDot - 0.12f))
        {
            return false;
        }

        float verticalTolerance = Mathf.Max(0.2f, controller.radius * 1.4f);
        if (Mathf.Abs(topHit.point.y - referenceTopPoint.y) > verticalTolerance)
        {
            return false;
        }

        topPoint = topHit.point;
        return true;
    }

    private bool TryResolveLedgeTopPointNearestToHandMidpoint(
        Vector3 wallNormal,
        Vector3 fallbackTopPoint,
        out Vector3 resolvedTopPoint)
    {
        resolvedTopPoint = fallbackTopPoint;
        if (controller == null || !TryGetHandOriginsMidpoint(out Vector3 handMidpoint))
        {
            return TryResolveLedgeTopPointOnWall(fallbackTopPoint, wallNormal, out resolvedTopPoint);
        }

        Vector3 horizontalNormal = Vector3.ProjectOnPlane(wallNormal, Vector3.up);
        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            horizontalNormal = Vector3.ProjectOnPlane(wallHangNormal, Vector3.up);
        }

        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            horizontalNormal = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            return TryResolveLedgeTopPointOnWall(fallbackTopPoint, wallNormal, out resolvedTopPoint);
        }

        horizontalNormal.Normalize();
        Vector3 edgeDirection = Vector3.Cross(horizontalNormal, Vector3.up);
        if (edgeDirection.sqrMagnitude <= 0.0001f)
        {
            return TryResolveLedgeTopPointOnWall(fallbackTopPoint, wallNormal, out resolvedTopPoint);
        }

        edgeDirection.Normalize();
        float alongEdgeOffset = Vector3.Dot(handMidpoint - fallbackTopPoint, edgeDirection);
        Vector3 projectedReference = fallbackTopPoint + edgeDirection * alongEdgeOffset;
        float sampleStep = Mathf.Max(0.02f, controller.radius * 0.22f);
        float maxSweep = Mathf.Max(
            sampleStep * 3f,
            Mathf.Max(0.08f, ledgeHangCornerProbeLateralOffset + controller.radius * 0.35f));
        int sampleCountPerSide = Mathf.Clamp(Mathf.CeilToInt(maxSweep / sampleStep), 1, 5);

        bool hasBest = false;
        float bestScore = float.PositiveInfinity;
        Vector3 bestPoint = fallbackTopPoint;
        for (int sampleIndex = -sampleCountPerSide; sampleIndex <= sampleCountPerSide; sampleIndex++)
        {
            Vector3 candidateReference = projectedReference + edgeDirection * (sampleStep * sampleIndex);
            if (!TryResolveLedgeTopPointOnWall(candidateReference, wallNormal, out Vector3 candidateTopPoint))
            {
                continue;
            }

            Vector3 deltaToMidpoint = handMidpoint - candidateTopPoint;
            float score = deltaToMidpoint.sqrMagnitude;
            if (!hasBest || score < bestScore)
            {
                hasBest = true;
                bestScore = score;
                bestPoint = candidateTopPoint;
            }
        }

        if (!hasBest)
        {
            return TryResolveLedgeTopPointOnWall(fallbackTopPoint, wallNormal, out resolvedTopPoint);
        }

        resolvedTopPoint = bestPoint;
        return true;
    }

    private bool IsLedgeContinuationValid(Vector3 sampleEdgePoint, Vector3 wallNormal, Vector3 referenceTopPoint)
    {
        if (controller == null)
        {
            return false;
        }

        Vector3 horizontalNormal = Vector3.ProjectOnPlane(wallNormal, Vector3.up);
        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        horizontalNormal.Normalize();
        int mask = GetClimbMaskExcludingSelf();
        float probeUpDistance = Mathf.Max(0.05f, ledgeTopProbeUpDistance * 0.75f);
        float probeInset = Mathf.Max(0f, ledgeTopProbeForwardInset);
        float probeDownDistance = Mathf.Max(0.15f, ledgeTopProbeDownDistance);
        Vector3 topProbeOrigin = sampleEdgePoint + Vector3.up * probeUpDistance - horizontalNormal * probeInset;
        bool hasTopSurfaceEvidence = false;
        if (Physics.Raycast(topProbeOrigin, Vector3.down, out RaycastHit topHit, probeDownDistance, mask, QueryTriggerInteraction.Ignore))
        {
            if (topHit.collider != null
                && topHit.collider.transform != transform
                && !topHit.collider.transform.IsChildOf(transform)
                && topHit.normal.y >= Mathf.Clamp01(ledgeTopSurfaceMinUpDot - 0.12f))
            {
                float verticalTolerance = Mathf.Max(0.14f, controller.radius * 1.2f);
                if (Mathf.Abs(topHit.point.y - referenceTopPoint.y) <= verticalTolerance)
                {
                    hasTopSurfaceEvidence = true;
                }
            }
        }

        float outwardProbeOffset = Mathf.Max(0.03f, Mathf.Max(0f, wallHangContactDistance) + controller.skinWidth + 0.015f);
        Vector3 wallProbeOrigin = sampleEdgePoint + Vector3.up * 0.03f + horizontalNormal * outwardProbeOffset;
        float wallProbeDistance = outwardProbeOffset + Mathf.Max(0.08f, wallJumpProbeDistance);
        if (!Physics.Raycast(wallProbeOrigin, -horizontalNormal, out RaycastHit wallHit, wallProbeDistance, mask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (wallHit.collider == null
            || wallHit.collider.transform == transform
            || wallHit.collider.transform.IsChildOf(transform))
        {
            return false;
        }

        float maxWallUpDot = Mathf.Clamp01(wallJumpMaxSurfaceUpDot) + 0.08f;
        if (Mathf.Abs(wallHit.normal.y) > maxWallUpDot)
        {
            return false;
        }

        Vector3 wallHitHorizontal = Vector3.ProjectOnPlane(wallHit.normal, Vector3.up);
        if (wallHitHorizontal.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        wallHitHorizontal.Normalize();
        float requiredAlignment = hasTopSurfaceEvidence ? 0.5f : 0.4f;
        return Vector3.Dot(wallHitHorizontal, horizontalNormal) >= requiredAlignment;
    }

    private bool TryGetHandOriginsMidpoint(out Vector3 midpoint)
    {
        midpoint = Vector3.zero;
        Transform leftHand = ResolveLedgeHandTransform(true);
        Transform rightHand = ResolveLedgeHandTransform(false);
        bool hasLeft = TryGetHandTransformOrigin(leftHand, out Vector3 leftOrigin);
        bool hasRight = TryGetHandTransformOrigin(rightHand, out Vector3 rightOrigin);
        bool requiresDualHandMidpoint = leftHand != null && rightHand != null;
        if (requiresDualHandMidpoint && (!hasLeft || !hasRight))
        {
            return false;
        }

        if (hasLeft && hasRight)
        {
            midpoint = (leftOrigin + rightOrigin) * 0.5f;
            return true;
        }

        if (hasLeft)
        {
            midpoint = leftOrigin;
            return true;
        }

        if (hasRight)
        {
            midpoint = rightOrigin;
            return true;
        }

        return false;
    }

    private bool TryGetHandDistanceExtentsToPlane(
        Vector3 planePoint,
        Vector3 planeNormal,
        out float minDistance,
        out float maxDistance)
    {
        minDistance = 0f;
        maxDistance = 0f;
        if (planeNormal.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector3 normalizedPlaneNormal = planeNormal.normalized;
        bool hasAnyHand = false;
        Transform leftHand = ResolveLedgeHandTransform(true);
        Transform rightHand = ResolveLedgeHandTransform(false);

        if (TryGetHandTransformOrigin(leftHand, out Vector3 leftOrigin))
        {
            float leftDistance = Vector3.Dot(leftOrigin - planePoint, normalizedPlaneNormal);
            minDistance = leftDistance;
            maxDistance = leftDistance;
            hasAnyHand = true;
        }

        if (TryGetHandTransformOrigin(rightHand, out Vector3 rightOrigin))
        {
            float rightDistance = Vector3.Dot(rightOrigin - planePoint, normalizedPlaneNormal);
            if (!hasAnyHand)
            {
                minDistance = rightDistance;
                maxDistance = rightDistance;
                hasAnyHand = true;
            }
            else
            {
                minDistance = Mathf.Min(minDistance, rightDistance);
                maxDistance = Mathf.Max(maxDistance, rightDistance);
            }
        }

        return hasAnyHand;
    }

    private static bool TryGetHandTransformOrigin(Transform handTransform, out Vector3 origin)
    {
        origin = Vector3.zero;
        if (handTransform == null)
        {
            return false;
        }

        origin = handTransform.position;
        return true;
    }

    private Transform ResolveLedgeHandTransform(bool leftHand)
    {
        Transform explicitHand = leftHand ? leftHandLedgeTransform : rightHandLedgeTransform;
        if (explicitHand != null)
        {
            return explicitHand;
        }

        if (runtimeAnimator == null)
        {
            return null;
        }

        HumanBodyBones handBone = leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
        return runtimeAnimator.GetBoneTransform(handBone);
    }

    private void ExitWallHang()
    {
        wallHangActive = false;
        ledgeHangActive = false;
        ledgeClimbActive = false;
        ledgeHangLateralInputRuntime = 0f;
        ledgeClimbProgress = 0f;
        ledgeClimbStartPosition = Vector3.zero;
        ledgeClimbMidPosition = Vector3.zero;
        ledgeClimbTargetPosition = Vector3.zero;
        ledgeHangTopPoint = Vector3.zero;
        ledgeHangClimbUnlockTime = float.NegativeInfinity;
        ledgeHangInputLockUntilTime = float.NegativeInfinity;
        ledgeHangLastValidContactTime = float.NegativeInfinity;
        wallHangSlideEaseElapsed = 0f;
        backflipAnimatorHoldActive = false;
        ClearHangAnimatorBoolsImmediate();
    }

    private void ExitWallHangToGroundOrFall(
        float minimumDownwardSpeed = -0.25f,
        bool armHighFallCheckForReleaseDrop = false)
    {
        ExitWallHang();
        wallJumpNoRotateActive = false;
        wallJumpForwardBackLockActive = false;
        climbJumpAnimatorActive = false;
        forceFallAnimatorFromClimbJump = false;

        bool groundedNow = isGrounded || (controller != null && controller.isGrounded);
        if (groundedNow)
        {
            isGrounded = true;
            wallHangReleaseHighFallCheckActive = false;
            if (verticalVelocity < 0f)
            {
                verticalVelocity = groundedVerticalForce;
            }

            SetState(MovementState.Grounded);
            return;
        }

        if (verticalVelocity > minimumDownwardSpeed)
        {
            verticalVelocity = minimumDownwardSpeed;
        }

        wallHangReleaseHighFallCheckActive = armHighFallCheckForReleaseDrop;
        SetState(MovementState.Falling);
    }

    private void AlignCharacterToWallHangSurface()
    {
        Vector3 towardWall = -Vector3.ProjectOnPlane(wallHangNormal, Vector3.up);
        if (towardWall.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        towardWall.Normalize();
        transform.rotation = Quaternion.LookRotation(towardWall, Vector3.up);
    }

    private bool ShouldReleaseWallHangNearGround()
    {
        if (!wallHangActive || controller == null)
        {
            return false;
        }

        if (!TryGetGroundDistanceBelowFeet(out float feetToGround))
        {
            return false;
        }

        float releaseDistance = Mathf.Max(0f, wallHangReleaseGroundDistance);
        return releaseDistance > 0.0001f && feetToGround <= releaseDistance;
    }

    private bool ShouldReleaseWallHangFromInputDirection(Vector3 wallNormal)
    {
        Vector3 inputDirection = Vector3.ProjectOnPlane(desiredMoveDirection, Vector3.up);
        float deadZoneSqr = movementInputDeadZone * movementInputDeadZone;
        if (inputDirection.sqrMagnitude <= deadZoneSqr)
        {
            return false;
        }

        Vector3 towardWall = -Vector3.ProjectOnPlane(wallNormal, Vector3.up);
        if (towardWall.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        inputDirection.Normalize();
        towardWall.Normalize();
        float maxAllowedAngle = Mathf.Clamp(wallHangReleaseInputAngle, 0f, 180f);
        float minAllowedDot = Mathf.Cos(maxAllowedAngle * Mathf.Deg2Rad);
        return Vector3.Dot(inputDirection, towardWall) < minAllowedDot;
    }

    private bool IsGroundContactBlockingWallHang()
    {
        if (controller == null)
        {
            return false;
        }

        if (isGrounded || controller.isGrounded)
        {
            return true;
        }

        if (!TryGetGroundDistanceBelowFeet(out float feetToGround))
        {
            return false;
        }

        float releaseDistance = Mathf.Max(0f, wallHangReleaseGroundDistance);
        return releaseDistance > 0.0001f && feetToGround <= releaseDistance;
    }

    private bool IsWallInteractionSuppressedByGroundProximity()
    {
        if (controller == null)
        {
            return false;
        }

        Bounds bounds = controller.bounds;
        float currentFeetY = bounds.min.y;
        float unlockMargin = Mathf.Max(controller.skinWidth + 0.02f, 0.05f);
        if (isGrounded || controller.isGrounded)
        {
            wallInteractionUnlockFeetY = currentFeetY;
            return true;
        }

        if (!float.IsNegativeInfinity(wallInteractionUnlockFeetY))
        {
            if (currentFeetY <= wallInteractionUnlockFeetY + unlockMargin)
            {
                return true;
            }

            wallInteractionUnlockFeetY = float.NegativeInfinity;
        }

        if (!TryGetGroundDistanceBelowFeet(out float feetToGround))
        {
            return false;
        }

        float releaseDistance = Mathf.Max(0f, wallHangReleaseGroundDistance);
        if (releaseDistance <= 0.0001f || feetToGround > releaseDistance)
        {
            return false;
        }

        wallInteractionUnlockFeetY = currentFeetY;
        return true;
    }

    private bool TryGetGroundDistanceBelowFeet(out float feetToGround)
    {
        feetToGround = float.PositiveInfinity;
        if (controller == null)
        {
            return false;
        }

        Bounds bounds = controller.bounds;
        float rayStartOffset = Mathf.Max(controller.skinWidth + 0.02f, 0.04f);
        Vector3 rayOrigin = bounds.center;
        rayOrigin.y = bounds.min.y + rayStartOffset;
        int mask = GetGroundMaskExcludingSelf();
        float maxProbeDistance = Mathf.Max(0.2f, Mathf.Max(0f, wallHangReleaseGroundDistance) + rayStartOffset + 0.1f);
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxProbeDistance, mask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        feetToGround = Mathf.Max(0f, bounds.min.y - hit.point.y);
        return true;
    }

    private bool IsWallHangActionLocked()
    {
        return currentState == MovementState.Dashing
            || currentState == MovementState.GroundPound
            || currentState == MovementState.Dive
            || currentState == MovementState.Rolling
            || currentState == MovementState.Sliding
            || currentState == MovementState.SlopeSliding
            || currentState == MovementState.Teetering;
    }

    private bool TryGetWallJumpNormal(out Vector3 wallNormal)
    {
        return TryGetWallJumpNormal(out wallNormal, out _);
    }

    private bool TryGetWallJumpNormal(out Vector3 wallNormal, out float wallDistance)
    {
        wallNormal = Vector3.zero;
        wallDistance = float.PositiveInfinity;
        if (!enableWallJump || controller == null || isGrounded)
        {
            return false;
        }

        if (IsWallInteractionSuppressedByGroundProximity())
        {
            return false;
        }

        Vector3 probeDirection = ResolveWallJumpProbeDirection();
        if (probeDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        float castDistance = Mathf.Max(0.05f, wallJumpProbeDistance + controller.skinWidth);
        if (!TryGetWallSurfaceNormal(probeDirection, castDistance, out wallNormal, out wallDistance))
        {
            return false;
        }

        return true;
    }

    private bool TryGetWallHangNormal(out Vector3 wallNormal)
    {
        return TryGetWallHangNormal(out wallNormal, false);
    }

    private bool TryGetWallHangNormal(out Vector3 wallNormal, bool allowFallbackProbeDirection)
    {
        wallNormal = Vector3.zero;
        if (!enableWallJump || controller == null || isGrounded)
        {
            return false;
        }

        if (IsWallInteractionSuppressedByGroundProximity())
        {
            return false;
        }

        Vector3 probeDirection = ResolveWallHangApproachDirection();
        if (allowFallbackProbeDirection)
        {
            Vector3 towardCurrentWall = -Vector3.ProjectOnPlane(wallHangNormal, Vector3.up);
            if (probeDirection.sqrMagnitude <= 0.0001f)
            {
                probeDirection = towardCurrentWall;
            }
            else if (towardCurrentWall.sqrMagnitude > 0.0001f)
            {
                towardCurrentWall.Normalize();
                if (Vector3.Dot(probeDirection.normalized, towardCurrentWall) < 0.1f)
                {
                    probeDirection = towardCurrentWall;
                }
            }
        }

        if (probeDirection.sqrMagnitude <= 0.0001f && allowFallbackProbeDirection)
        {
            probeDirection = ResolveWallJumpProbeDirection();
        }

        if (probeDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        float castDistance = Mathf.Max(0.05f, wallJumpProbeDistance + controller.skinWidth);
        if (!TryGetWallSurfaceNormal(probeDirection, castDistance, out Vector3 candidate, out float hitDistance))
        {
            return false;
        }

        float maxDistance = Mathf.Max(0.01f, Mathf.Max(0f, wallHangContactDistance) + controller.skinWidth);
        if (hitDistance > maxDistance)
        {
            return false;
        }

        wallNormal = candidate;
        return true;
    }

    private bool TryGetWallSurfaceNormal(
        Vector3 probeDirection,
        float castDistance,
        out Vector3 wallNormal,
        out float wallDistance)
    {
        wallNormal = Vector3.zero;
        wallDistance = float.PositiveInfinity;
        if (controller == null)
        {
            return false;
        }

        Vector3 planarDirection = Vector3.ProjectOnPlane(probeDirection, Vector3.up);
        if (planarDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        planarDirection.Normalize();
        Bounds bounds = controller.bounds;
        float castRadius = Mathf.Clamp(controller.radius * 0.8f, 0.05f, controller.radius * 0.98f);
        Vector3 castOrigin = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z);
        int mask = GetClimbMaskExcludingSelf();
        if (!Physics.SphereCast(
                castOrigin,
                castRadius,
                planarDirection,
                out RaycastHit hit,
                Mathf.Max(0.01f, castDistance),
                mask,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        wallDistance = hit.distance;
        if (Mathf.Abs(hit.normal.y) > Mathf.Clamp01(wallJumpMaxSurfaceUpDot))
        {
            return false;
        }

        Vector3 horizontalNormal = Vector3.ProjectOnPlane(hit.normal, Vector3.up);
        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        horizontalNormal.Normalize();
        if (Vector3.Dot(horizontalNormal, planarDirection) > -0.1f)
        {
            return false;
        }

        wallNormal = horizontalNormal;
        return true;
    }

    private bool TryGetLedgeHangInfo(out Vector3 wallNormal, out Vector3 ledgeTopPoint)
    {
        wallNormal = Vector3.zero;
        ledgeTopPoint = Vector3.zero;
        if (!enableLedgeHang || controller == null)
        {
            return false;
        }

        if (IsWallInteractionSuppressedByGroundProximity())
        {
            return false;
        }

        // Do not force clearance at ledge-hang entry; it is checked again when climb starts.
        Transform leftHand = ResolveLedgeHandTransform(true);
        Transform rightHand = ResolveLedgeHandTransform(false);
        bool hasHandCandidate = false;
        float bestCandidateScore = float.PositiveInfinity;
        if (TryGetLedgeHangInfoFromHand(leftHand, leftHandLedgeProbeRadius, out Vector3 leftWallNormal, out Vector3 leftTopPoint))
        {
            if (TryResolveLedgeTopPointNearestToHandMidpoint(leftWallNormal, leftTopPoint, out Vector3 refinedLeftTopPoint))
            {
                leftTopPoint = refinedLeftTopPoint;
            }

            if (IsLedgeHangCandidateWithinEntrySnapLimits(leftWallNormal, leftTopPoint))
            {
                float leftScore = ComputeLedgeMidpointCandidateScore(leftWallNormal, leftTopPoint);
                if (!hasHandCandidate || leftScore < bestCandidateScore)
                {
                    hasHandCandidate = true;
                    bestCandidateScore = leftScore;
                    wallNormal = leftWallNormal;
                    ledgeTopPoint = leftTopPoint;
                }
            }
        }

        if (TryGetLedgeHangInfoFromHand(rightHand, rightHandLedgeProbeRadius, out Vector3 rightWallNormal, out Vector3 rightTopPoint))
        {
            if (TryResolveLedgeTopPointNearestToHandMidpoint(rightWallNormal, rightTopPoint, out Vector3 refinedRightTopPoint))
            {
                rightTopPoint = refinedRightTopPoint;
            }

            if (IsLedgeHangCandidateWithinEntrySnapLimits(rightWallNormal, rightTopPoint))
            {
                float rightScore = ComputeLedgeMidpointCandidateScore(rightWallNormal, rightTopPoint);
                if (!hasHandCandidate || rightScore < bestCandidateScore)
                {
                    hasHandCandidate = true;
                    bestCandidateScore = rightScore;
                    wallNormal = rightWallNormal;
                    ledgeTopPoint = rightTopPoint;
                }
            }
        }

        if (hasHandCandidate)
        {
            return true;
        }

        if (!ledgeHangActive && TryGetLedgeHangInfoFromBodyForCornerAssist(out wallNormal, out ledgeTopPoint))
        {
            if (IsLedgeHangCandidateWithinEntrySnapLimits(wallNormal, ledgeTopPoint))
            {
                return true;
            }
        }

        wallNormal = Vector3.zero;
        ledgeTopPoint = Vector3.zero;
        return false;
    }

    private bool IsLedgeTopClimbClearForEntry(Vector3 ledgeTopPoint, Vector3 wallNormal)
    {
        if (controller == null)
        {
            return true;
        }

        Vector3 horizontalNormal = Vector3.ProjectOnPlane(wallNormal, Vector3.up);
        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        horizontalNormal.Normalize();
        Vector3 towardWall = -horizontalNormal;
        int mask = GetClimbMaskExcludingSelf();
        float probeRadius = Mathf.Clamp(controller.radius * 0.22f, 0.03f, 0.12f);
        float forwardInset = Mathf.Clamp(controller.radius * 0.38f, 0.08f, 0.45f);
        float baseHeight = Mathf.Max(controller.skinWidth + probeRadius + 0.08f, 0.14f);
        float clearanceHeight = Mathf.Max(0.45f, controller.height * 0.78f);
        Vector3 probeBase = ledgeTopPoint + towardWall * forwardInset;
        Vector3 capsuleBottom = probeBase + Vector3.up * baseHeight;
        Vector3 capsuleTop = probeBase + Vector3.up * (baseHeight + clearanceHeight);

        return !HasBlockingColliderInLedgeClimbCapsule(
            capsuleBottom,
            capsuleTop,
            probeRadius,
            mask);
    }

    private bool HasBlockingColliderInLedgeClimbCapsule(Vector3 bottom, Vector3 top, float radius, int mask)
    {
        int hitCount = Physics.OverlapCapsuleNonAlloc(
            bottom,
            top,
            Mathf.Max(0.01f, radius),
            ledgeOverlapBuffer,
            mask,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider candidate = ledgeOverlapBuffer[i];
            if (candidate == null)
            {
                continue;
            }

            Transform candidateTransform = candidate.transform;
            if (candidateTransform == transform || candidateTransform.IsChildOf(transform))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool TryGetLedgeHangInfoFromHand(
        Transform handTransform,
        float handProbeRadius,
        out Vector3 wallNormal,
        out Vector3 ledgeTopPoint)
    {
        wallNormal = Vector3.zero;
        ledgeTopPoint = Vector3.zero;
        if (handTransform == null || controller == null)
        {
            return false;
        }

        if (!TryGetHandTransformProbeSphere(handTransform, handProbeRadius, out Vector3 handCenter, out float handRadius))
        {
            return false;
        }

        int mask = GetClimbMaskExcludingSelf();
        float assistDistance = Mathf.Max(Mathf.Max(0f, ledgeGrabAssistDistance), 0.26f);
        float cornerAssistDistance = Mathf.Clamp(Mathf.Max(Mathf.Max(0f, ledgeHangCornerTransitionAssistDistance), 0.18f), 0f, 1.25f);
        float overlapRadius = Mathf.Max(
            0.01f,
            handRadius
            + Mathf.Max(0f, ledgeHandContactPadding)
            + assistDistance
            + cornerAssistDistance * 0.55f);
        int hitCount = Physics.OverlapSphereNonAlloc(
            handCenter,
            overlapRadius,
            ledgeOverlapBuffer,
            mask,
            QueryTriggerInteraction.Ignore);
        if (hitCount <= 0)
        {
            return false;
        }

        float maxWallUpDot = Mathf.Clamp01(wallJumpMaxSurfaceUpDot);
        float minTopUpDot = Mathf.Clamp01(ledgeTopSurfaceMinUpDot - assistDistance * 0.25f);
        Bounds characterBounds = controller.bounds;
        float minEdgeHeight = Mathf.Max(0f, ledgeHangMinEdgeHeight - assistDistance * 1.35f);
        float maxEdgeHeight = Mathf.Max(
            minEdgeHeight,
            ledgeHangMaxEdgeHeight + assistDistance * 2.4f + 0.32f);
        float maxHorizontalGap = Mathf.Max(
            0f,
            ledgeTopMaxHorizontalGap
            + assistDistance * 1.8f
            + cornerAssistDistance * 1.2f
            + 0.18f);
        float probeUpDistance = Mathf.Max(
            0.01f,
            ledgeTopProbeUpDistance
            + assistDistance * 0.55f
            + cornerAssistDistance * 0.3f
            + 0.06f);
        float probeInset = Mathf.Max(0f, ledgeTopProbeForwardInset);
        float probeDownDistance = Mathf.Max(
            0.05f,
            ledgeTopProbeDownDistance
            + assistDistance * 1.9f
            + cornerAssistDistance * 0.75f
            + 0.35f);

        for (int i = 0; i < hitCount; i++)
        {
            Collider candidate = ledgeOverlapBuffer[i];
            if (candidate == null
                || candidate.transform == transform
                || candidate.transform.IsChildOf(transform))
            {
                continue;
            }

            Vector3 closest = candidate.ClosestPoint(handCenter);
            Vector3 toHand = handCenter - closest;
            float contactDistance = toHand.magnitude;
            if (contactDistance > overlapRadius + 0.0001f)
            {
                continue;
            }

            Vector3 candidateNormal = contactDistance > 0.0001f
                ? toHand / contactDistance
                : -Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (Mathf.Abs(candidateNormal.y) > maxWallUpDot)
            {
                continue;
            }

            Vector3 horizontalNormal = Vector3.ProjectOnPlane(candidateNormal, Vector3.up);
            if (horizontalNormal.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            horizontalNormal.Normalize();
            Vector3 edgeDirection = Vector3.Cross(horizontalNormal, Vector3.up);
            bool hasEdgeDirection = edgeDirection.sqrMagnitude > 0.0001f;
            if (hasEdgeDirection)
            {
                edgeDirection.Normalize();
            }

            float cornerProbeOffset = hasEdgeDirection
                ? Mathf.Max(
                    0f,
                    ledgeHangCornerProbeLateralOffset
                    + assistDistance * 0.2f
                    + cornerAssistDistance * 0.55f)
                : 0f;
            int probeVariantCount = cornerProbeOffset > 0.0001f ? 7 : 1;
            bool hasBestTopHit = false;
            Vector3 bestTopPoint = Vector3.zero;
            float bestHorizontalGap = float.PositiveInfinity;
            for (int probeVariant = 0; probeVariant < probeVariantCount; probeVariant++)
            {
                float lateralOffset = 0f;
                if (probeVariant == 1)
                {
                    lateralOffset = cornerProbeOffset;
                }
                else if (probeVariant == 2)
                {
                    lateralOffset = -cornerProbeOffset;
                }
                else if (probeVariant == 3)
                {
                    lateralOffset = cornerProbeOffset * 2f;
                }
                else if (probeVariant == 4)
                {
                    lateralOffset = -cornerProbeOffset * 2f;
                }
                else if (probeVariant == 5)
                {
                    lateralOffset = cornerProbeOffset * 3f;
                }
                else if (probeVariant == 6)
                {
                    lateralOffset = -cornerProbeOffset * 3f;
                }

                Vector3 topProbeOrigin = closest + Vector3.up * probeUpDistance - horizontalNormal * probeInset;
                if (hasEdgeDirection && Mathf.Abs(lateralOffset) > 0.0001f)
                {
                    topProbeOrigin += edgeDirection * lateralOffset;
                }

                if (!Physics.Raycast(topProbeOrigin, Vector3.down, out RaycastHit topHit, probeDownDistance, mask, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (topHit.collider == null
                    || topHit.collider.transform == transform
                    || topHit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (topHit.normal.y < minTopUpDot)
                {
                    continue;
                }

                float edgeHeight = topHit.point.y - characterBounds.min.y;
                if (edgeHeight < minEdgeHeight || edgeHeight > maxEdgeHeight)
                {
                    continue;
                }

                // Project the top hit point back onto the wall plane to get the real edge line.
                Vector3 edgeTopPoint = topHit.point - horizontalNormal * Vector3.Dot(topHit.point - closest, horizontalNormal);
                Vector3 topToWall = topHit.point - edgeTopPoint;
                float wallProximity = topToWall.magnitude;
                float maxTopToWallDistance = Mathf.Max(
                    0.04f,
                    probeInset
                    + assistDistance * 0.45f
                    + cornerAssistDistance * 0.25f);
                if (wallProximity > maxTopToWallDistance + 0.0001f)
                {
                    continue;
                }

                Vector3 handToTop = handCenter - edgeTopPoint;
                handToTop.y = 0f;
                float horizontalGap = handToTop.magnitude;
                if (horizontalGap > maxHorizontalGap)
                {
                    continue;
                }

                if (!hasBestTopHit || horizontalGap < bestHorizontalGap)
                {
                    hasBestTopHit = true;
                    bestHorizontalGap = horizontalGap;
                    bestTopPoint = edgeTopPoint;
                }
            }

            if (!hasBestTopHit)
            {
                continue;
            }

            wallNormal = horizontalNormal;
            ledgeTopPoint = bestTopPoint;
            return true;
        }

        return false;
    }

    private bool TryGetLedgeHangInfoFromBodyForCornerAssist(out Vector3 wallNormal, out Vector3 ledgeTopPoint)
    {
        wallNormal = Vector3.zero;
        ledgeTopPoint = Vector3.zero;
        bool allowGeneralAirAssist = !isGrounded
            && (currentState == MovementState.Jumping
                || currentState == MovementState.Falling
                || wallHangActive
                || ledgeHangActive);
        if ((!CanUseHangJumpCornerSnapAssist() && !allowGeneralAirAssist) || controller == null)
        {
            return false;
        }

        float assistDistance = Mathf.Max(Mathf.Max(0f, ledgeGrabAssistDistance), 0.24f)
            + Mathf.Max(Mathf.Max(0f, ledgeHangCornerTransitionAssistDistance), 0.16f);
        float castDistance = Mathf.Max(
            0.08f,
            Mathf.Max(0f, wallJumpProbeDistance)
            + controller.skinWidth
            + Mathf.Max(0f, wallHangContactDistance)
            + assistDistance
            + controller.radius * 1.9f);

        float bestScore = float.PositiveInfinity;
        bool foundCandidate = false;
        Vector3 bestWallNormal = Vector3.zero;
        Vector3 bestLedgeTopPoint = Vector3.zero;

        Vector3 towardCurrentWall = -Vector3.ProjectOnPlane(wallHangNormal, Vector3.up);
        TryUpdateCornerAssistLedgeCandidate(
            towardCurrentWall,
            castDistance,
            assistDistance,
            ref foundCandidate,
            ref bestScore,
            ref bestWallNormal,
            ref bestLedgeTopPoint);

        Vector3 approachDirection = ResolveWallHangApproachDirection();
        TryUpdateCornerAssistLedgeCandidate(
            approachDirection,
            castDistance,
            assistDistance,
            ref foundCandidate,
            ref bestScore,
            ref bestWallNormal,
            ref bestLedgeTopPoint);

        Vector3 velocityDirection = Vector3.ProjectOnPlane(planarVelocity, Vector3.up);
        TryUpdateCornerAssistLedgeCandidate(
            velocityDirection,
            castDistance,
            assistDistance,
            ref foundCandidate,
            ref bestScore,
            ref bestWallNormal,
            ref bestLedgeTopPoint);

        Vector3 forwardDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        TryUpdateCornerAssistLedgeCandidate(
            forwardDirection,
            castDistance,
            assistDistance,
            ref foundCandidate,
            ref bestScore,
            ref bestWallNormal,
            ref bestLedgeTopPoint);
        TryUpdateCornerAssistLedgeCandidate(
            -forwardDirection,
            castDistance,
            assistDistance,
            ref foundCandidate,
            ref bestScore,
            ref bestWallNormal,
            ref bestLedgeTopPoint);

        Vector3 rightDirection = Vector3.ProjectOnPlane(transform.right, Vector3.up);
        TryUpdateCornerAssistLedgeCandidate(
            rightDirection,
            castDistance,
            assistDistance,
            ref foundCandidate,
            ref bestScore,
            ref bestWallNormal,
            ref bestLedgeTopPoint);
        TryUpdateCornerAssistLedgeCandidate(
            -rightDirection,
            castDistance,
            assistDistance,
            ref foundCandidate,
            ref bestScore,
            ref bestWallNormal,
            ref bestLedgeTopPoint);

        if (!foundCandidate)
        {
            return false;
        }

        wallNormal = bestWallNormal;
        ledgeTopPoint = bestLedgeTopPoint;
        return true;
    }

    private void TryUpdateCornerAssistLedgeCandidate(
        Vector3 probeDirection,
        float castDistance,
        float assistDistance,
        ref bool foundCandidate,
        ref float bestScore,
        ref Vector3 bestWallNormal,
        ref Vector3 bestLedgeTopPoint)
    {
        if (probeDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (!TryGetWallSurfaceNormal(probeDirection, castDistance, out Vector3 candidateWallNormal, out float wallDistance))
        {
            return;
        }

        if (!TryResolveCornerAssistLedgeTopPoint(
                candidateWallNormal,
                wallDistance,
                assistDistance,
                out Vector3 candidateTopPoint,
                out float horizontalGap))
        {
            return;
        }

        Vector3 planarVelocityDirection = Vector3.ProjectOnPlane(planarVelocity, Vector3.up);
        Vector3 towardWall = -Vector3.ProjectOnPlane(candidateWallNormal, Vector3.up);
        float awayPenalty = 0f;
        if (planarVelocityDirection.sqrMagnitude > 0.0001f && towardWall.sqrMagnitude > 0.0001f)
        {
            awayPenalty = Mathf.Clamp01(-Vector3.Dot(planarVelocityDirection.normalized, towardWall.normalized));
        }

        float score = wallDistance + horizontalGap * 0.55f + awayPenalty * 0.12f;
        if (!foundCandidate || score < bestScore)
        {
            foundCandidate = true;
            bestScore = score;
            bestWallNormal = candidateWallNormal;
            bestLedgeTopPoint = candidateTopPoint;
        }
    }

    private bool TryResolveCornerAssistLedgeTopPoint(
        Vector3 wallNormal,
        float wallDistance,
        float assistDistance,
        out Vector3 ledgeTopPoint,
        out float horizontalGap)
    {
        ledgeTopPoint = Vector3.zero;
        horizontalGap = float.PositiveInfinity;
        if (controller == null)
        {
            return false;
        }

        Vector3 horizontalNormal = Vector3.ProjectOnPlane(wallNormal, Vector3.up);
        if (horizontalNormal.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        horizontalNormal.Normalize();
        Vector3 edgeDirection = Vector3.Cross(horizontalNormal, Vector3.up);
        if (edgeDirection.sqrMagnitude > 0.0001f)
        {
            edgeDirection.Normalize();
        }

        Bounds characterBounds = controller.bounds;
        int mask = GetClimbMaskExcludingSelf();
        float minTopUpDot = Mathf.Clamp01(ledgeTopSurfaceMinUpDot - 0.2f - assistDistance * 0.08f);
        float minEdgeHeight = Mathf.Max(0f, ledgeHangMinEdgeHeight - assistDistance * 2f);
        float maxEdgeHeight = Mathf.Max(minEdgeHeight, ledgeHangMaxEdgeHeight + assistDistance * 2.4f);
        float maxHorizontalGap = Mathf.Max(0f, ledgeTopMaxHorizontalGap + assistDistance + controller.radius * 1.85f);
        float probeUpDistance = Mathf.Max(0.05f, ledgeTopProbeUpDistance + assistDistance * 0.6f + controller.radius * 0.2f);
        float probeInset = Mathf.Max(0f, ledgeTopProbeForwardInset - assistDistance * 0.2f);
        float probeDownDistance = Mathf.Max(0.2f, ledgeTopProbeDownDistance + assistDistance * 1.4f + controller.radius * 0.8f);
        float cornerProbeOffset = Mathf.Max(0f, ledgeHangCornerProbeLateralOffset + assistDistance + controller.radius * 0.28f);
        float wallOffset = Mathf.Clamp(
            wallDistance + controller.radius * 0.78f,
            0.06f,
            controller.radius + Mathf.Max(0f, wallJumpProbeDistance) + assistDistance + 0.45f);

        Vector3 baseProbePoint = characterBounds.center
            + Vector3.up * (characterBounds.extents.y * 0.44f)
            - horizontalNormal * wallOffset;

        bool hasBestTopHit = false;
        float bestGap = float.PositiveInfinity;
        Vector3 bestTopPoint = Vector3.zero;

        for (int variant = 0; variant < 9; variant++)
        {
            float lateralOffset = 0f;
            float verticalOffset = 0f;
            if (variant == 1)
            {
                lateralOffset = cornerProbeOffset;
            }
            else if (variant == 2)
            {
                lateralOffset = -cornerProbeOffset;
            }
            else if (variant == 3)
            {
                lateralOffset = cornerProbeOffset * 2f;
            }
            else if (variant == 4)
            {
                lateralOffset = -cornerProbeOffset * 2f;
            }
            else if (variant == 5)
            {
                verticalOffset = controller.radius * 0.32f;
            }
            else if (variant == 6)
            {
                verticalOffset = controller.radius * 0.32f;
                lateralOffset = cornerProbeOffset;
            }
            else if (variant == 7)
            {
                verticalOffset = controller.radius * 0.32f;
                lateralOffset = -cornerProbeOffset;
            }
            else if (variant == 8)
            {
                verticalOffset = -controller.radius * 0.2f;
            }

            Vector3 topProbeOrigin = baseProbePoint + Vector3.up * verticalOffset;
            if (edgeDirection.sqrMagnitude > 0.0001f && Mathf.Abs(lateralOffset) > 0.0001f)
            {
                topProbeOrigin += edgeDirection * lateralOffset;
            }

            topProbeOrigin += Vector3.up * probeUpDistance - horizontalNormal * probeInset;
            if (!Physics.Raycast(topProbeOrigin, Vector3.down, out RaycastHit topHit, probeDownDistance, mask, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            if (topHit.collider == null
                || topHit.collider.transform == transform
                || topHit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (topHit.normal.y < minTopUpDot)
            {
                continue;
            }

            float edgeHeight = topHit.point.y - characterBounds.min.y;
            if (edgeHeight < minEdgeHeight || edgeHeight > maxEdgeHeight)
            {
                continue;
            }

            Vector3 characterToTop = topHit.point - characterBounds.center;
            Vector3 planarCharacterToTop = new Vector3(characterToTop.x, 0f, characterToTop.z);
            float planarGap = planarCharacterToTop.magnitude;
            if (planarGap > maxHorizontalGap)
            {
                continue;
            }

            float wallFacingAmount = Vector3.Dot(planarCharacterToTop.normalized, -horizontalNormal);
            if (planarGap > 0.0001f && wallFacingAmount < -0.35f)
            {
                continue;
            }

            if (!hasBestTopHit || planarGap < bestGap)
            {
                hasBestTopHit = true;
                bestGap = planarGap;
                bestTopPoint = topHit.point;
            }
        }

        if (!hasBestTopHit)
        {
            return false;
        }

        ledgeTopPoint = bestTopPoint;
        horizontalGap = bestGap;
        return true;
    }

    private static bool TryGetHandTransformProbeSphere(
        Transform handTransform,
        float configuredRadius,
        out Vector3 center,
        out float radius)
    {
        center = Vector3.zero;
        radius = 0f;
        if (handTransform == null)
        {
            return false;
        }

        center = handTransform.position;
        float maxScale = Mathf.Max(
            Mathf.Abs(handTransform.lossyScale.x),
            Mathf.Abs(handTransform.lossyScale.y),
            Mathf.Abs(handTransform.lossyScale.z));
        radius = Mathf.Max(0.005f, configuredRadius) * Mathf.Max(0.0001f, maxScale);
        return radius > 0.0001f;
    }

    private Vector3 ResolveWallJumpProbeDirection()
    {
        if (desiredMoveDirection.sqrMagnitude > 0.0001f)
        {
            return desiredMoveDirection.normalized;
        }

        Vector3 planar = Vector3.ProjectOnPlane(planarVelocity, Vector3.up);
        if (planar.sqrMagnitude > 0.0001f)
        {
            return planar.normalized;
        }

        Vector3 forwardPlanar = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        return forwardPlanar.sqrMagnitude > 0.0001f ? forwardPlanar.normalized : Vector3.forward;
    }

    private Vector3 ResolveWallHangApproachDirection()
    {
        if (desiredMoveDirection.sqrMagnitude > 0.0001f)
        {
            return desiredMoveDirection.normalized;
        }

        Vector3 planarVelocityDirection = Vector3.ProjectOnPlane(planarVelocity, Vector3.up);
        if (planarVelocityDirection.sqrMagnitude > 0.0016f)
        {
            return planarVelocityDirection.normalized;
        }

        return Vector3.zero;
    }

    private bool CanUseHangJumpCornerSnapAssist()
    {
        if (!enableLedgeHang || controller == null || isGrounded || !jumpConsumed)
        {
            return false;
        }

        if (currentState != MovementState.Jumping && currentState != MovementState.Falling)
        {
            return false;
        }

        return Time.time <= hangJumpCornerSnapAssistUntilTime;
    }

    private bool ShouldForceLedgeSnapFromHangJump(Vector3 ledgeNormal, Vector3 ledgeTopPoint)
    {
        if (!CanUseHangJumpCornerSnapAssist())
        {
            return false;
        }

        // Even with corner assist, wall hang entry still requires facing and approaching the wall.
        Vector3 approachDirection = ResolveWallHangApproachDirection();
        if (!IsApproachDirectionValidForWallHangEntry(approachDirection, ledgeNormal))
        {
            return false;
        }

        Vector3 previousWallNormal = Vector3.ProjectOnPlane(wallHangNormal, Vector3.up);
        Vector3 candidateWallNormal = Vector3.ProjectOnPlane(ledgeNormal, Vector3.up);
        if (candidateWallNormal.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        candidateWallNormal.Normalize();
        if (previousWallNormal.sqrMagnitude > 0.0001f)
        {
            previousWallNormal.Normalize();
            float cornerDot = Vector3.Dot(previousWallNormal, candidateWallNormal);
            if (cornerDot > 0.98f)
            {
                return false;
            }
        }

        float assistDistance = Mathf.Max(0f, ledgeGrabAssistDistance) + Mathf.Max(0f, ledgeHangCornerTransitionAssistDistance);
        float maxHorizontalSnapDistance = Mathf.Max(
            0.12f,
            Mathf.Max(0f, ledgeTopMaxHorizontalGap) + assistDistance + controller.radius * 0.55f);

        Vector3 planarDelta = ledgeTopPoint - controller.bounds.center;
        planarDelta.y = 0f;
        return planarDelta.sqrMagnitude <= maxHorizontalSnapDistance * maxHorizontalSnapDistance;
    }

    private static bool IsApproachDirectionTowardWall(Vector3 approachDirection, Vector3 wallNormal)
    {
        if (approachDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector3 towardWall = -Vector3.ProjectOnPlane(wallNormal, Vector3.up);
        if (towardWall.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        towardWall.Normalize();
        return Vector3.Dot(approachDirection.normalized, towardWall) >= 0.15f;
    }

    private bool IsFacingTowardWallForWallHangEntry(Vector3 wallNormal)
    {
        Vector3 facingPlanar = ResolveWallHangFacingDirection();
        if (facingPlanar.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector3 towardWall = -Vector3.ProjectOnPlane(wallNormal, Vector3.up);
        if (towardWall.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        towardWall.Normalize();
        facingPlanar.Normalize();

        // 0 degrees means fully facing the wall.
        // Sadece -tolerans ile +tolerans aci araliginda tutunmaya izin ver.
        // This angle difference is calculated directly between character direction and the held wall normal.
        float maxFacingAngle = Mathf.Clamp(wallHangFacingAngleTolerance, 0f, 180f);
        float signedFacingAngle = Vector3.SignedAngle(towardWall, facingPlanar, Vector3.up);
        return Mathf.Abs(signedFacingAngle) <= maxFacingAngle;
    }

    private bool IsApproachDirectionValidForWallHangEntry(Vector3 approachDirection, Vector3 wallNormal)
    {
        Vector3 towardWall = -Vector3.ProjectOnPlane(wallNormal, Vector3.up);
        if (towardWall.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        towardWall.Normalize();
        if (!IsFacingTowardWallForWallHangEntry(wallNormal))
        {
            return false;
        }

        Vector3 resolvedApproach = approachDirection;
        if (resolvedApproach.sqrMagnitude <= 0.0001f)
        {
            Vector3 planarVelocityDirection = Vector3.ProjectOnPlane(planarVelocity, Vector3.up);
            if (planarVelocityDirection.sqrMagnitude > 0.0001f)
            {
                resolvedApproach = planarVelocityDirection.normalized;
            }
            else
            {
                return false;
            }
        }

        // Keep wall approach direction strict: movement must be nearly straight toward the wall.
        const float minApproachDot = 0.97f;
        return Vector3.Dot(resolvedApproach.normalized, towardWall) >= minApproachDot;
    }

    private Vector3 ResolveWallHangFacingDirection()
    {
        Transform facingSource = runtimeVisualRoot;
        if (facingSource == null || facingSource == transform)
        {
            facingSource = visualRoot;
        }

        if (facingSource == null || facingSource == transform)
        {
            facingSource = transform;
        }

        Vector3 facing = Vector3.ProjectOnPlane(facingSource.forward, Vector3.up);
        if (facing.sqrMagnitude <= 0.0001f && facingSource != transform)
        {
            facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        return facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector3.zero;
    }

    private bool IsTouchingWallForJumpHangBlock()
    {
        if (!enableWallJump || !enableWallHang || controller == null)
        {
            return false;
        }

        float nearContactDistance = Mathf.Max(
            0.02f,
            Mathf.Max(0f, wallHangContactDistance) + controller.skinWidth + 0.02f);

        Vector3 inputDirection = ResolveWallHangApproachDirection();
        if (inputDirection.sqrMagnitude > 0.0001f
            && TryGetWallSurfaceNormal(inputDirection, nearContactDistance, out _, out _))
        {
            return true;
        }

        Vector3 planarVelocityDirection = Vector3.ProjectOnPlane(planarVelocity, Vector3.up);
        if (planarVelocityDirection.sqrMagnitude > 0.0001f
            && TryGetWallSurfaceNormal(planarVelocityDirection, nearContactDistance, out _, out _))
        {
            return true;
        }

        Vector3 forwardDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forwardDirection.sqrMagnitude > 0.0001f
            && TryGetWallSurfaceNormal(forwardDirection, nearContactDistance, out _, out _))
        {
            return true;
        }

        Vector3 backwardDirection = -forwardDirection;
        if (backwardDirection.sqrMagnitude > 0.0001f
            && TryGetWallSurfaceNormal(backwardDirection, nearContactDistance, out _, out _))
        {
            return true;
        }

        Vector3 rightDirection = Vector3.ProjectOnPlane(transform.right, Vector3.up);
        if (rightDirection.sqrMagnitude > 0.0001f
            && TryGetWallSurfaceNormal(rightDirection, nearContactDistance, out _, out _))
        {
            return true;
        }

        Vector3 leftDirection = -rightDirection;
        return leftDirection.sqrMagnitude > 0.0001f
            && TryGetWallSurfaceNormal(leftDirection, nearContactDistance, out _, out _);
    }

    private void ApplyJumpCut()
    {
        if ((IsKeyboardJumpUp() || IsGamepadButtonUp(gamepadJumpButton)) && verticalVelocity > 0f)
        {
            float minimumProtectedVelocity = Mathf.Min(jumpCutProtectedVelocity, verticalVelocity);
            verticalVelocity = Mathf.Max(verticalVelocity * jumpCutMultiplier, minimumProtectedVelocity);
        }
    }

    private bool ShouldAutoRollFromJumpLanding()
    {
        float deadZoneSqr = movementInputDeadZone * movementInputDeadZone;
        bool hasMoveInput = moveInput.sqrMagnitude > deadZoneSqr;
        float minimumSpeed = Mathf.Max(0.35f, movementInputDeadZone * 2f);
        bool hasCarrySpeed = planarVelocity.sqrMagnitude > minimumSpeed * minimumSpeed;
        return hasMoveInput || hasCarrySpeed;
    }

    private bool HasRawDirectionalInput()
    {
        float deadZoneSqr = movementInputDeadZone * movementInputDeadZone;
        return rawMoveInput.sqrMagnitude > deadZoneSqr;
    }

    private void BeginNonJumpFallTracking()
    {
        if (controller == null)
        {
            return;
        }

        trackingNonJumpFall = true;
        nonJumpHighFallReached = false;
        nonJumpFallStartBottomY = controller.bounds.min.y;
        nonJumpFallStartTime = Time.time;
    }

    private void ResetNonJumpFallTracking()
    {
        trackingNonJumpFall = false;
        nonJumpHighFallReached = false;
        nonJumpFallStartBottomY = float.NegativeInfinity;
        nonJumpFallStartTime = float.NegativeInfinity;
    }

    private float GetTrackedNonJumpFallHeight()
    {
        if (!trackingNonJumpFall || controller == null)
        {
            return 0f;
        }

        return Mathf.Max(0f, nonJumpFallStartBottomY - controller.bounds.min.y);
    }

    private float GetTrackedNonJumpFallDuration()
    {
        if (!trackingNonJumpFall || float.IsNegativeInfinity(nonJumpFallStartTime))
        {
            return 0f;
        }

        return Mathf.Max(0f, Time.time - nonJumpFallStartTime);
    }

    private bool ShouldSuppressJumpAnimatorForShortNonJumpFall()
    {
        if (isGrounded || jumpConsumed)
        {
            return false;
        }

        bool shortFallSensitiveState = currentState == MovementState.Falling
            || currentState == MovementState.Dashing;
        if (!shortFallSensitiveState)
        {
            return false;
        }

        float requiredDistance = Mathf.Max(0.05f, minimumJumpHeight);
        if (!trackingNonJumpFall || float.IsNegativeInfinity(nonJumpFallStartBottomY) || controller == null)
        {
            // If a non-jump fall has just started, do not enable the Jump bool until the threshold is passed.
            return true;
        }

        float fallDistance = Mathf.Max(0f, nonJumpFallStartBottomY - controller.bounds.min.y);
        return fallDistance < requiredDistance;
    }

    private float GetDoubleJumpEquivalentHeight()
    {
        if (!enableDoubleJump || maxAirJumps <= 0)
        {
            return 0f;
        }

        float firstJumpHeight = Mathf.Max(0.1f, jumpHeight);
        float airJumpHeight = firstJumpHeight * Mathf.Max(0.25f, airJumpHeightMultiplier);
        return firstJumpHeight + airJumpHeight;
    }

    private float GetResolvedHighFallRollHeightThreshold(bool fromJump)
    {
        float doubleJumpHeight = GetDoubleJumpEquivalentHeight();
        float fallbackHeightThreshold = Mathf.Max(Mathf.Max(highFallMinHeightForFallAnim, 2f), 2.8f);
        float tolerance = Mathf.Clamp(highFallDoubleJumpHeightTolerance, 0f, 0.2f);
        float resolvedThreshold;
        if (doubleJumpHeight > 0f)
        {
            resolvedThreshold = Mathf.Max(fallbackHeightThreshold, doubleJumpHeight - tolerance);
        }
        else
        {
            resolvedThreshold = fallbackHeightThreshold;
        }

        if (fromJump)
        {
            // For jump-based landings, raise the threshold further to prevent a normal jump from triggering roll.
            // Increase the threshold additionally.
            float jumpHeightFloor = Mathf.Max(
                Mathf.Max(highFallMinHeightForFallAnim + 0.8f, jumpHeight * 1.55f),
                3.4f);
            resolvedThreshold = Mathf.Max(resolvedThreshold, jumpHeightFloor);
        }

        return resolvedThreshold;
    }

    private float GetResolvedHighFallRollAirTimeThreshold(bool fromJump)
    {
        float resolved = Mathf.Max(Mathf.Max(0f, highFallMinAirTimeForRoll), 0.95f);
        if (fromJump)
        {
            // Jump inislerinde kisa hava sï¿½resiyle roll tetiklenmesin.
            resolved = Mathf.Max(resolved, 1.05f);
        }

        return resolved;
    }

    private bool ShouldRollFromHighFallLanding()
    {
        if (!trackingNonJumpFall || jumpConsumed)
        {
            return false;
        }

        float fallHeight = GetTrackedNonJumpFallHeight();
        float fallAirTime = GetTrackedNonJumpFallDuration();
        float rollHeightThreshold = GetResolvedHighFallRollHeightThreshold(fromJump: false);
        float rollAirTimeThreshold = GetResolvedHighFallRollAirTimeThreshold(fromJump: false);

        bool highEnough = nonJumpHighFallReached || fallHeight >= rollHeightThreshold;
        bool longEnough = fallAirTime >= rollAirTimeThreshold;
        return highEnough || longEnough;
    }

    private bool ShouldRollFromHighJumpLanding()
    {
        if (!jumpFallTrackingActive || controller == null)
        {
            return false;
        }

        float fallHeight = Mathf.Max(0f, jumpFallStartBottomY - controller.bounds.min.y);
        float fallAirTime = float.IsNegativeInfinity(jumpFallStartTime)
            ? 0f
            : Mathf.Max(0f, Time.time - jumpFallStartTime);
        float rollHeightThreshold = GetResolvedHighFallRollHeightThreshold(fromJump: true);
        float rollAirTimeThreshold = GetResolvedHighFallRollAirTimeThreshold(fromJump: true);
        bool highEnough = fallHeight >= rollHeightThreshold;
        bool longEnough = fallAirTime >= rollAirTimeThreshold;
        return highEnough && longEnough;
    }

    private void UpdatePlanarVelocity(float deltaTime)
    {
        bool crouchIntent = IsCrouchIntentActive();
        bool lowClearanceForStanding = isGrounded
            && enableCrouchSlide
            && !IsRollAutoCrouchSuppressed()
            && IsLowClearanceForStanding();
        if (lowClearanceForStanding
            && currentState != MovementState.Sliding
            && currentState != MovementState.SlopeSliding
            && currentState != MovementState.Rolling
            && currentState != MovementState.Dashing
            && currentState != MovementState.GroundPound
            && currentState != MovementState.Dive)
        {
            crouchTarget = true;
            crouchIntent = true;
            if (currentState != MovementState.Crouching)
            {
                SetState(MovementState.Crouching);
            }
        }

        if (isGrounded
            && currentState == MovementState.Crouching
            && sprintHeld
            && moveInput.sqrMagnitude > movementInputDeadZone * movementInputDeadZone
            && CanStandUp())
        {
            crouchTarget = false;
            crouchIntent = false;
            SetState(MovementState.Grounded);
        }

        if (wallHangActive)
        {
            if (ledgeHangActive || ledgeClimbActive)
            {
                planarVelocity = Vector3.zero;
                return;
            }

            Vector3 towardWall = -Vector3.ProjectOnPlane(wallHangNormal, Vector3.up);
            if (towardWall.sqrMagnitude > 0.0001f)
            {
                towardWall.Normalize();
                Vector3 targetPlanar = towardWall * Mathf.Max(0f, wallHangStickSpeed);
                planarVelocity = Vector3.MoveTowards(
                    planarVelocity,
                    targetPlanar,
                    Mathf.Max(0f, wallHangHorizontalDamping) * deltaTime);
            }
            else
            {
                planarVelocity = Vector3.MoveTowards(
                    planarVelocity,
                    Vector3.zero,
                    Mathf.Max(0f, wallHangHorizontalDamping) * deltaTime);
            }

            return;
        }

        // State-first velocity update for action abilities.
        if (currentState == MovementState.Dashing)
        {
            dashTimer -= deltaTime;
            float dashSteerSharpness = Mathf.Max(0f, dashCameraSteerSharpness);
            if (dashSteerSharpness > 0.0001f)
            {
                Vector3 steerDirection = ResolveDashDirectionFromCameraRelativeInput();
                if (steerDirection.sqrMagnitude > 0.0001f)
                {
                    float steerBlend = 1f - Mathf.Exp(-dashSteerSharpness * deltaTime);
                    dashDirection = Vector3.Slerp(dashDirection, steerDirection.normalized, steerBlend);
                }
            }

            dashDirection = Vector3.ProjectOnPlane(dashDirection, Vector3.up);
            if (dashDirection.sqrMagnitude <= 0.0001f)
            {
                dashDirection = ResolvePlanarCameraForwardDirection();
            }
            else
            {
                dashDirection.Normalize();
            }

            planarVelocity = dashDirection * dashSpeed;
            if (dashTimer <= 0f)
            {
                planarVelocity *= dashExitSpeedMultiplier;
                if (doubleJumpDashRollOnLandingPending && !isGrounded)
                {
                    // Return the character to the normal falling flow immediately when exiting dash after a double jump.
                    jumpHoldTimer = 0f;
                    jumpCutProtectedVelocity = 0f;
                    float minDownwardSpeed = Mathf.Max(0.05f, fallAnimationVerticalSpeedThreshold + 0.02f);
                    verticalVelocity = Mathf.Min(verticalVelocity, -minDownwardSpeed);
                }

                SetState(isGrounded ? MovementState.Grounded : MovementState.Falling);
            }

            return;
        }

        if (currentState == MovementState.GroundPound)
        {
            planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, groundPoundHorizontalDamping * deltaTime);
            return;
        }

        if (currentState == MovementState.Teetering)
        {
            if (!isGrounded)
            {
                SetState(MovementState.Falling);
                return;
            }

            bool hasMovementIntent = HasTeeterMovementIntent();
            bool stationaryIdle = IsTeeterStationaryIdle();
            bool shouldStayTeetering = enableTeetering
                && !hasMovementIntent
                && stationaryIdle
                && IsEdgeOpenAhead(teeterEdgeDirection, out _)
                && IsFacingTowardTeeterEdge(teeterEdgeDirection);
            if (shouldStayTeetering)
            {
                planarVelocity = Vector3.zero;
                return;
            }

            if (hasMovementIntent || !stationaryIdle)
            {
                teeterReentryLockUntilTime = Time.time + Mathf.Max(0f, teeterReentryLockDuration);
            }

            SetState(MovementState.Grounded);
        }

        if (currentState == MovementState.Dive)
        {
            diveTimer -= deltaTime;
            if (desiredMoveDirection.sqrMagnitude > 0.0001f)
            {
                float steerT = 1f - Mathf.Exp(-diveSteerControl * deltaTime);
                diveDirection = Vector3.Slerp(diveDirection, desiredMoveDirection.normalized, steerT);
                diveDirection = Vector3.ProjectOnPlane(diveDirection, Vector3.up).normalized;
                if (diveDirection.sqrMagnitude <= 0.0001f)
                {
                    diveDirection = transform.forward;
                }
            }

            float speed = Mathf.Max(diveForwardSpeed, planarVelocity.magnitude - groundDeceleration * 0.15f * deltaTime);
            planarVelocity = diveDirection * speed;
            if (diveTimer <= 0f && verticalVelocity < 0f)
            {
                SetState(MovementState.Falling);
            }

            return;
        }

        if (currentState == MovementState.Rolling)
        {
            float effectiveRollDuration = GetEffectiveRollDuration();
            if (!isGrounded)
            {
                BeginRollAirTrackingIfNeeded();
                rollTimer = Mathf.Max(0f, rollTimer - deltaTime);
                float airDecel = Mathf.Max(0f, rollDeceleration * 0.2f);
                float carrySpeed = Mathf.Max(0f, planarVelocity.magnitude - airDecel * deltaTime);
                planarVelocity = rollDirection * carrySpeed;
                return;
            }

            rollTimer -= deltaTime;
            if (!rollDirectionSteerLockedForCurrentRoll
                && !lockFacingUntilGroundInputAfterWallJump
                && desiredMoveDirection.sqrMagnitude > 0.0001f)
            {
                float steerT = 1f - Mathf.Exp(-rollSteerControl * deltaTime);
                rollDirection = Vector3.Slerp(rollDirection, desiredMoveDirection.normalized, steerT);
                rollDirection = Vector3.ProjectOnPlane(rollDirection, Vector3.up).normalized;
                if (rollDirection.sqrMagnitude <= 0.0001f)
                {
                    rollDirection = transform.forward;
                }
            }

            float speed = Mathf.Max(0f, planarVelocity.magnitude - rollDeceleration * deltaTime);
            planarVelocity = rollDirection * speed;
            float rollMinimumHold = Mathf.Max(0f, Mathf.Min(effectiveRollDuration, slideMinimumHoldTime));
            bool allowRollExitBySpeed = rollTimer <= Mathf.Max(0f, effectiveRollDuration - rollMinimumHold);
            bool shouldEndRoll = rollTimer <= 0f || (allowRollExitBySpeed && speed <= slideEndSpeed);
            if (shouldEndRoll)
            {
                rollDirectionSteerLockedForCurrentRoll = false;
                MovementState postRollState = ResolvePostRollState(crouchIntent);
                crouchTarget = postRollState == MovementState.Crouching;
                rollAutoCrouchSuppressUntilTime = postRollState == MovementState.Crouching
                    ? float.NegativeInfinity
                    : Time.time + RollAutoCrouchSuppressDuration;
                SetState(postRollState);
                rollStartedFromCrouch = false;
            }

            return;
        }

        if (currentState == MovementState.SlopeSliding)
        {
            slideTimer -= deltaTime;
            slideExitLockTimer = Mathf.Max(0f, slideExitLockTimer - deltaTime);
            if (!isGrounded)
            {
                slideExitLockTimer = 0f;
                SetState(MovementState.Falling);
                return;
            }

            Vector3 downhill = groundDownhillDirection.sqrMagnitude > 0.0001f ? groundDownhillDirection : slideDirection;
            if (desiredMoveDirection.sqrMagnitude > 0.0001f)
            {
                float boostedSteerSharpness = Mathf.Max(0f, slopeSlideSteerControl) * 3.6f;
                float steerT = 1f - Mathf.Exp(-boostedSteerSharpness * deltaTime);
                downhill = Vector3.Slerp(downhill, desiredMoveDirection.normalized, steerT);
            }

            downhill = Vector3.ProjectOnPlane(downhill, groundNormal).normalized;
            if (downhill.sqrMagnitude <= 0.0001f)
            {
                downhill = slideDirection.sqrMagnitude > 0.0001f ? slideDirection : transform.forward;
            }

            Vector3 slopeCameraForward = ResolveCameraForwardOnPlane(groundNormal);
            if (slopeCameraForward.sqrMagnitude > 0.0001f)
            {
                float boostedCameraDrift = Mathf.Max(0f, slopeSlideCameraSteerDrift) * 4.2f;
                float camDriftT = 1f - Mathf.Exp(-boostedCameraDrift * deltaTime);
                downhill = Vector3.Slerp(downhill, slopeCameraForward, camDriftT);
            }

            downhill = Vector3.ProjectOnPlane(downhill, groundNormal).normalized;
            if (downhill.sqrMagnitude <= 0.0001f)
            {
                downhill = slideDirection.sqrMagnitude > 0.0001f ? slideDirection : transform.forward;
            }

            float boostedMaxSteerAngle = Mathf.Clamp(
                Mathf.Max(0f, slopeSlideMaxSteerAngle) * 2.2f + 6f,
                0f,
                70f);
            slideDirection = ClampDirectionToSteerCone(
                slideSteerReferenceDirection,
                downhill,
                groundNormal,
                boostedMaxSteerAngle);

            float slope01 = Mathf.InverseLerp(slopeSlideMinAngle, Mathf.Max(slopeSlideMinAngle + 0.01f, controller.slopeLimit), groundSlopeAngle);
            float downhillAccel = slopeSlideDownhillAcceleration * slope01;
            Vector3 slopeVelocity = Vector3.ProjectOnPlane(planarVelocity, groundNormal);
            slopeVelocity += groundDownhillDirection * downhillAccel * deltaTime;
            if (desiredMoveDirection.sqrMagnitude > 0.0001f)
            {
                Vector3 downhillPlanar = Vector3.ProjectOnPlane(groundDownhillDirection, groundNormal);
                Vector3 slopeRight = Vector3.Cross(groundNormal, downhillPlanar);
                if (slopeRight.sqrMagnitude <= 0.0001f)
                {
                    slopeRight = Vector3.Cross(groundNormal, slideDirection);
                }

                if (slopeRight.sqrMagnitude > 0.0001f)
                {
                    slopeRight.Normalize();
                    float lateralIntent = Mathf.Clamp(Vector3.Dot(desiredMoveDirection.normalized, slopeRight), -1f, 1f);
                    float lateralAcceleration = Mathf.Max(0f, slopeSlideDownhillAcceleration) * 2.4f;
                    slopeVelocity += slopeRight * (lateralIntent * lateralAcceleration * deltaTime);
                }
            }

            if (slopeVelocity.sqrMagnitude > 0.0001f)
            {
                Vector3 slopeVelocityDirection = slopeVelocity.normalized;
                float followVelocityT = 1f - Mathf.Exp(-Mathf.Max(0f, slopeSlideSteerControl) * 2.8f * deltaTime);
                slideDirection = Vector3.Slerp(slideDirection, slopeVelocityDirection, followVelocityT);
                slideDirection = Vector3.ProjectOnPlane(slideDirection, groundNormal).normalized;
                if (slideDirection.sqrMagnitude <= 0.0001f)
                {
                    slideDirection = slopeVelocityDirection;
                }
            }

            float speed = Mathf.Max(0f, slopeVelocity.magnitude - slopeSlideFriction * deltaTime);
            speed = Mathf.Min(speed, slopeSlideMaxSpeed);
            planarVelocity = slideDirection * speed;
            bool flatSurface = !hasGroundNormal
                || groundSlopeAngle <= slopeSlideExitFlatAngle
                || groundDownhillDirection.sqrMagnitude <= 0.0001f;
            if (flatSurface)
            {
                ExitSlopeSlideToGround();
            }

            return;
        }

        if (currentState == MovementState.Sliding)
        {
            bool lowClearanceWhileSliding = IsLowClearanceForStanding();
            if (!lowClearanceWhileSliding && slideLowClearanceLockActive)
            {
                slideLowClearanceLockActive = false;
                slideLowClearanceLockedSpeed = 0f;
            }
            else if (lowClearanceWhileSliding && lockSlideSpeedInLowClearance && !slideLowClearanceLockActive)
            {
                slideLowClearanceLockActive = true;
                float minimumSlideSpeed = Mathf.Max(0.01f, slideEndSpeed);
                slideLowClearanceLockedSpeed = Mathf.Clamp(
                    Mathf.Max(planarVelocity.magnitude, minimumSlideSpeed),
                    minimumSlideSpeed,
                    slideMaxSpeed);
                lastMoveCollisionFlags = CollisionFlags.None;
            }

            if (slideLowClearanceLockActive && (lastMoveCollisionFlags & CollisionFlags.Sides) != 0)
            {
                // If the character hits a wall in a tight space, end slide and stay crouched.
                slideLowClearanceLockActive = false;
                slideLowClearanceLockedSpeed = 0f;
                slideTimer = 0f;
                slideExitLockTimer = 0f;
                planarVelocity = Vector3.zero;
                crouchTarget = true;
                SetState(MovementState.Crouching);
                return;
            }

            if (!slideLowClearanceLockActive && isGrounded && IsSlopeSlideSurfaceValid())
            {
                StartSlopeSlide(true);
                return;
            }

            slideTimer -= deltaTime;
            slideExitLockTimer = Mathf.Max(0f, slideExitLockTimer - deltaTime);
            Vector3 steerDirection = slideDirection.sqrMagnitude > 0.0001f ? slideDirection : transform.forward;
            if (desiredMoveDirection.sqrMagnitude > 0.0001f)
            {
                float steerT = 1f - Mathf.Exp(-slideSteerControl * deltaTime);
                steerDirection = Vector3.Slerp(steerDirection, desiredMoveDirection.normalized, steerT);
                steerDirection = Vector3.ProjectOnPlane(steerDirection, Vector3.up).normalized;
                if (steerDirection.sqrMagnitude <= 0.0001f)
                {
                    steerDirection = transform.forward;
                }
            }

            Vector3 slideCameraForward = ResolveCameraForwardOnPlane(Vector3.up);
            if (slideCameraForward.sqrMagnitude > 0.0001f)
            {
                float camDriftT = 1f - Mathf.Exp(-Mathf.Max(0f, slideCameraSteerDrift) * deltaTime);
                steerDirection = Vector3.Slerp(steerDirection, slideCameraForward, camDriftT);
            }

            steerDirection = Vector3.ProjectOnPlane(steerDirection, Vector3.up).normalized;
            if (steerDirection.sqrMagnitude <= 0.0001f)
            {
                steerDirection = slideDirection.sqrMagnitude > 0.0001f ? slideDirection : transform.forward;
            }

            slideDirection = ClampDirectionToSteerCone(
                slideSteerReferenceDirection,
                steerDirection,
                Vector3.up,
                slideMaxSteerAngle);

            float speed = slideLowClearanceLockActive
                ? slideLowClearanceLockedSpeed
                : Mathf.Max(0f, planarVelocity.magnitude - slideDeceleration * deltaTime);
            planarVelocity = slideDirection * speed;
            bool allowSlideExit = slideExitLockTimer <= 0f;
            bool shouldEndSlide = !isGrounded
                || (!slideLowClearanceLockActive && allowSlideExit && (slideTimer <= 0f || speed <= slideEndSpeed || !crouchIntent));
            if (shouldEndSlide)
            {
                slideExitLockTimer = 0f;
                if (isGrounded)
                {
                    SetState(crouchIntent ? MovementState.Crouching : MovementState.Grounded);
                }
                else
                {
                    SetState(MovementState.Falling);
                }
            }

            return;
        }

        if (isGrounded && slopeMomentumTimer > 0f)
        {
            slopeMomentumTimer = Mathf.Max(0f, slopeMomentumTimer - deltaTime);
            slopeMomentumSpeed = Mathf.MoveTowards(slopeMomentumSpeed, 0f, slopeMomentumDecay * deltaTime);
            if (slopeMomentumTimer <= 0f || slopeMomentumSpeed <= 0.01f)
            {
                slopeMomentumTimer = 0f;
                slopeMomentumSpeed = 0f;
            }
        }

        float inputMagnitude = moveInput.magnitude;
        bool hasInput = inputMagnitude > movementInputDeadZone;
        float speedMultiplier = currentState == MovementState.Crouching ? crouchWalkSpeedMultiplier : 1f;
        float sprintAmount = Mathf.Clamp01(sprintInputAmount);
        bool sprintActive = enableSprint
            && sprintAmount > 0.0001f
            && hasInput
            && isGrounded
            && currentState != MovementState.Crouching;
        if (sprintActive)
        {
            speedMultiplier *= Mathf.Lerp(1f, sprintSpeedMultiplier, sprintAmount);
        }

        float targetSpeed = inputMagnitude * moveSpeed * speedMultiplier;
        if (isGrounded && slopeMomentumTimer > 0f)
        {
            targetSpeed = Mathf.Max(targetSpeed, slopeMomentumSpeed);
        }

        bool useSlopeAssist = IsSlopeAssistActive();
        float slopeFactor = 1f;
        if (hasInput && useSlopeAssist && groundDownhillDirection.sqrMagnitude > 0.0001f)
        {
            float downhillAlignment = Vector3.Dot(desiredMoveDirection, groundDownhillDirection);
            float directionalSlopeFactor = downhillAlignment >= 0f
                ? Mathf.Lerp(1f, 1f + downhillSpeedBoost, downhillAlignment)
                : Mathf.Lerp(1f, 1f - uphillSpeedPenalty, -downhillAlignment);

            slopeFactor = Mathf.Lerp(1f, directionalSlopeFactor, slopeSpeedInfluence);
        }

        lastSlopeSpeedFactor = slopeFactor;
        Vector3 desiredVelocity = desiredMoveDirection * (targetSpeed * slopeFactor);
        if (useSlopeAssist && hasInput)
        {
            desiredVelocity = Vector3.ProjectOnPlane(desiredVelocity, groundNormal);
        }

        if (isGrounded)
        {
            if (!hasInput)
            {
                float snapSpeed = Mathf.Max(0.1f, planarStopSnapSpeed);
                bool isStopAnimationActive = currentState == MovementState.Grounded && stopAnimationTimer > 0f;
                if (isStopAnimationActive)
                {
                    // Preserve character momentum while the stop animation is playing.
                    runStopSlideTimer = 0f;
                    return;
                }

                if (enableRunStopSlide
                    && currentState == MovementState.Grounded
                    && planarVelocity.magnitude >= runStopSlideMinSpeed)
                {
                    runStopSlideTimer = Mathf.Max(runStopSlideTimer, runStopSlideDuration);
                }

                if (runStopSlideTimer > 0f)
                {
                    runStopSlideTimer = Mathf.Max(0f, runStopSlideTimer - deltaTime);
                }

                float deceleration = slopeMomentumTimer > 0f ? groundDeceleration * 0.35f : groundDeceleration;
                if (runStopSlideTimer > 0f)
                {
                    deceleration *= runStopSlideDecelerationMultiplier;
                }

                planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, deceleration * deltaTime);
                if (useSlopeAssist && groundDownhillDirection.sqrMagnitude > 0.0001f)
                {
                    float downhillComponent = Vector3.Dot(planarVelocity, groundDownhillDirection);
                    if (downhillComponent > 0f)
                    {
                        planarVelocity -= groundDownhillDirection * Mathf.Min(downhillComponent, slopeAntiSlideDeceleration * deltaTime);
                    }
                }

                if (planarVelocity.sqrMagnitude <= snapSpeed * snapSpeed)
                {
                    planarVelocity = Vector3.zero;
                }

                return;
            }

            runStopSlideTimer = 0f;

            float alignment = planarVelocity.sqrMagnitude > movementInputDeadZone * movementInputDeadZone
                ? Vector3.Dot(planarVelocity.normalized, desiredMoveDirection)
                : 1f;

            float acceleration = alignment < 0f
                ? turnAcceleration * reverseDirectionAccelerationMultiplier
                : groundAcceleration;

            if (alignment < reversePivotThreshold)
            {
                planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, pivotBrakeDeceleration * deltaTime);
            }

            planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVelocity, acceleration * deltaTime);
            if (useSlopeAssist)
            {
                Vector3 slopeAligned = Vector3.ProjectOnPlane(planarVelocity, groundNormal);
                float alignT = 1f - Mathf.Exp(-slopeAlignmentSharpness * deltaTime);
                planarVelocity = Vector3.Lerp(planarVelocity, slopeAligned, alignT);
            }

            return;
        }

        lastSlopeSpeedFactor = 1f;
        if (currentState != MovementState.Jumping && currentState != MovementState.Falling)
        {
            slopeMomentumTimer = 0f;
            slopeMomentumSpeed = 0f;
        }

        if (hasInput)
        {
            float alignment = (planarVelocity.sqrMagnitude > 0.0001f && desiredVelocity.sqrMagnitude > 0.0001f)
                ? Vector3.Dot(planarVelocity.normalized, desiredVelocity.normalized)
                : 1f;
            float steeringBoost = Mathf.Lerp(1f, 1.85f, Mathf.InverseLerp(0.25f, -1f, alignment));
            float airAccelerationStep = airAcceleration * airControl * steeringBoost;
            planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVelocity, airAccelerationStep * deltaTime);
        }

        if (wallJumpForwardBackLockActive && (currentState == MovementState.Jumping || currentState == MovementState.Falling))
        {
            Vector3 awayDirection = Vector3.ProjectOnPlane(wallJumpForcedDirection, Vector3.up);
            if (awayDirection.sqrMagnitude <= 0.0001f)
            {
                awayDirection = Vector3.ProjectOnPlane(-transform.forward, Vector3.up);
            }

            if (awayDirection.sqrMagnitude > 0.0001f)
            {
                awayDirection.Normalize();
                float forcedAwaySpeed = Mathf.Max(0.1f, Mathf.Min(moveSpeed, Mathf.Max(0f, wallJumpBackwardSpeed)));
                float currentAwaySpeed = Vector3.Dot(planarVelocity, awayDirection);
                if (currentAwaySpeed < forcedAwaySpeed)
                {
                    planarVelocity += awayDirection * (forcedAwaySpeed - currentAwaySpeed);
                }
            }
        }
    }

    private void ApplyGravity(float deltaTime)
    {
        if (currentState == MovementState.Dashing)
        {
            apexAssistActive = false;
            verticalVelocity += gravity * dashGravityMultiplier * deltaTime;
            verticalVelocity = Mathf.Max(verticalVelocity, terminalVelocity * 0.5f);
            return;
        }

        if (currentState == MovementState.GroundPound)
        {
            apexAssistActive = false;
            if (groundPoundDelayTimer > 0f)
            {
                groundPoundDelayTimer -= deltaTime;
                verticalVelocity = Mathf.MoveTowards(verticalVelocity, 0f, groundPoundAcceleration * deltaTime);
                return;
            }

            verticalVelocity = Mathf.MoveTowards(verticalVelocity, -groundPoundSpeed, groundPoundAcceleration * deltaTime);
            return;
        }

        if (currentState == MovementState.Dive)
        {
            apexAssistActive = false;
            verticalVelocity += gravity * diveGravityMultiplier * deltaTime;
            verticalVelocity = Mathf.Max(verticalVelocity, terminalVelocity);
            return;
        }

        if (currentState == MovementState.Teetering)
        {
            apexAssistActive = false;
            verticalVelocity = teeterLockForwardWalk ? 0f : groundedVerticalForce;
            jumpHoldTimer = 0f;
            return;
        }

        if (wallHangActive)
        {
            apexAssistActive = false;
            jumpHoldTimer = 0f;
            if (ledgeHangActive || ledgeClimbActive)
            {
                verticalVelocity = 0f;
                return;
            }

            float targetDownSpeed = -Mathf.Max(0f, wallHangSlideSpeed);
            float baseSlideAcceleration = Mathf.Abs(gravity) * Mathf.Max(0.1f, fallGravityMultiplier);
            float slideAcceleration = baseSlideAcceleration;
            float easeDuration = Mathf.Max(0f, wallHangSlideEaseInDuration);
            if (easeDuration > 0.0001f)
            {
                wallHangSlideEaseElapsed = Mathf.Min(easeDuration, wallHangSlideEaseElapsed + Mathf.Max(0f, deltaTime));
                float normalizedEaseTime = Mathf.Clamp01(wallHangSlideEaseElapsed / easeDuration);
                float easeInCirc = EvaluateEaseInCirc01(normalizedEaseTime);
                float startAccelerationScale = Mathf.Clamp01(wallHangSlideEaseInStartAccelerationScale);
                slideAcceleration = baseSlideAcceleration * Mathf.Lerp(startAccelerationScale, 1f, easeInCirc);
            }
            else
            {
                wallHangSlideEaseElapsed = 0f;
            }

            verticalVelocity = Mathf.MoveTowards(verticalVelocity, targetDownSpeed, slideAcceleration * deltaTime);
            return;
        }

        if (suppressGravityWhenParentedToPlatform
            && CanUseMovingPlatformGroundAssist())
        {
            apexAssistActive = false;
            jumpHoldTimer = 0f;
            verticalVelocity = 0f;
            return;
        }

        if (isGrounded && verticalVelocity <= 0f)
        {
            apexAssistActive = false;
            verticalVelocity = groundedVerticalForce;
            jumpHoldTimer = 0f;
            return;
        }

        if (jumpHoldTimer > 0f && (IsKeyboardJumpHeld() || IsGamepadButtonHeld(gamepadJumpButton)) && verticalVelocity > 0f)
        {
            verticalVelocity += jumpHoldForce * deltaTime;
            jumpHoldTimer -= deltaTime;
        }
        else
        {
            jumpHoldTimer = 0f;
        }

        float gravityMultiplier = 1f;
        if (verticalVelocity < 0f)
        {
            gravityMultiplier = IsGroundPoundControlHeld() ? fastFallGravityMultiplier : fallGravityMultiplier;
        }

        apexAssistActive = !isGrounded
            && currentState != MovementState.GroundPound
            && Mathf.Abs(verticalVelocity) <= apexVerticalSpeedThreshold;
        if (apexAssistActive)
        {
            gravityMultiplier *= apexGravityMultiplier;
        }

        verticalVelocity += gravity * gravityMultiplier * deltaTime;
        verticalVelocity = Mathf.Max(verticalVelocity, terminalVelocity);
    }

    private bool IsGroundPoundControlHeld()
    {
        if (!enableGroundPound)
        {
            return false;
        }

        bool keyboardHeld = groundPoundKey != KeyCode.None && MinimoInputBridge.GetKey(groundPoundKey);
        bool gamepadHeld = enableGamepadInput && IsGamepadButtonHeld(gamepadGroundPoundButton);
        return keyboardHeld || gamepadHeld;
    }

    private bool IsJumpLandingCooldownActive()
    {
        return Time.time < jumpLandingCooldownUnlockTime;
    }

    private void StartJumpLandingCooldown()
    {
        jumpLandingCooldownUnlockTime = Time.time + Mathf.Max(0f, jumpLandingCooldownDuration);
    }

    private void ClearGroundPoundRuntimeFlagsForJump()
    {
        groundPoundHardFallActive = false;
        groundPoundHardLandActive = false;
        groundPoundHardLandReleaseTime = float.NegativeInfinity;
        groundPoundStartedFromMovement = false;
        groundPoundDelayTimer = 0f;
        groundPoundBounceLockedUntilNextJump = false;
    }

    private void BeginGroundPoundHardFallSequence()
    {
        groundPoundHardFallActive = true;
        groundPoundHardLandActive = false;
        groundPoundHardLandReleaseTime = float.NegativeInfinity;
    }

    private void TriggerGroundPoundHardLanding(float impactSpeed)
    {
        groundPoundHardFallActive = false;
        groundPoundHardLandActive = true;
        groundPoundBounceLockedUntilNextJump = true;
        groundPoundHardLandReleaseTime = Time.time + Mathf.Max(0f, groundPoundHardLandHoldTime);

        if (groundPoundStartedFromMovement)
        {
            Vector3 backward = Vector3.ProjectOnPlane(-transform.forward, Vector3.up);
            if (backward.sqrMagnitude <= 0.0001f)
            {
                backward = Vector3.ProjectOnPlane(-planarVelocity, Vector3.up);
            }

            if (backward.sqrMagnitude <= 0.0001f)
            {
                backward = Vector3.back;
            }

            backward.Normalize();
            float backwardSpeed = Mathf.Max(0f, groundPoundHardBounceBackSpeed);
            if (backwardSpeed > 0.0001f)
            {
                planarVelocity = backward * backwardSpeed;
            }
        }
        else
        {
            planarVelocity = Vector3.zero;
        }

        float impact01 = Mathf.InverseLerp(landingImpactMinSpeed, landingImpactMaxSpeed, Mathf.Max(0f, impactSpeed));
        float upwardVelocity = Mathf.Max(0f, groundPoundHardBounceUpVelocity)
            + Mathf.Max(0f, groundPoundHardBounceUpImpactBonus) * impact01;
        upwardVelocity *= 0.75f;
        if (upwardVelocity > 0.0001f)
        {
            verticalVelocity = Mathf.Max(verticalVelocity, upwardVelocity);
        }
    }

    private void UpdateGroundPoundHardLandingState()
    {
        if (groundPoundHardFallActive && isGrounded && currentState != MovementState.GroundPound)
        {
            groundPoundHardFallActive = false;
        }

        if (!groundPoundHardLandActive)
        {
            return;
        }

        if (Time.time < groundPoundHardLandReleaseTime)
        {
            return;
        }

        groundPoundHardLandActive = false;
        if (!isGrounded)
        {
            // HardLand bittigi anda direkt fall moduna gec.
            groundPoundHardFallActive = true;
            SetState(MovementState.Falling);
            return;
        }

        groundPoundHardFallActive = false;
    }

    private void ApplyMovingPlatformMotion(float deltaTime)
    {
        bool isParentedToPlatform = IsParentedToMovingPlatform();
        if (isParentedToPlatform && disablePlatformCompensationWhenParented)
        {
            if (activeGroundPlatform != null)
            {
                activeGroundPlatformLastPosition = activeGroundPlatform.position;
                activeGroundPlatformLastRotation = activeGroundPlatform.rotation;
                hasActiveGroundPlatformPose = true;
            }

            return;
        }

        if (isParentedToPlatform)
        {
            if (activeGroundPlatform != null)
            {
                activeGroundPlatformLastPosition = activeGroundPlatform.position;
                activeGroundPlatformLastRotation = activeGroundPlatform.rotation;
                hasActiveGroundPlatformPose = true;
            }

            return;
        }

        if (!enableMovingPlatformCompensation
            || controller == null
            || !isGrounded
            || activeGroundPlatform == null
            || !hasActiveGroundPlatformPose)
        {
            return;
        }

        Vector3 currentPlatformPosition = activeGroundPlatform.position;
        Quaternion currentPlatformRotation = activeGroundPlatform.rotation;
        Vector3 platformDelta = currentPlatformPosition - activeGroundPlatformLastPosition;

        float rotationInfluence = Mathf.Clamp01(movingPlatformRotationInfluence);
        if (rotationInfluence > 0.0001f)
        {
            Quaternion rotationDelta = currentPlatformRotation * Quaternion.Inverse(activeGroundPlatformLastRotation);
            Vector3 fromPlatform = transform.position - activeGroundPlatformLastPosition;
            Vector3 rotatedFromPlatform = rotationDelta * fromPlatform;
            Vector3 rotationMove = rotatedFromPlatform - fromPlatform;
            platformDelta += rotationMove * rotationInfluence;
        }

        float maxDelta = Mathf.Max(0f, movingPlatformMaxDeltaPerFrame);
        if (maxDelta > 0.0001f)
        {
            float deltaMagnitude = platformDelta.magnitude;
            if (deltaMagnitude > maxDelta)
            {
                platformDelta = platformDelta / deltaMagnitude * maxDelta;
            }
        }

        if (platformDelta.sqrMagnitude > 0.0000001f)
        {
            controller.Move(platformDelta);
        }

        activeGroundPlatformLastPosition = currentPlatformPosition;
        activeGroundPlatformLastRotation = currentPlatformRotation;
    }

    private void UpdateGroundPlatformReference(Collider groundCollider)
    {
        if (!enableMovingPlatformCompensation || groundCollider == null)
        {
            ClearGroundPlatformReference();
            return;
        }

        Transform candidate = groundCollider.transform;
        if (candidate == null || candidate == transform || candidate.IsChildOf(transform))
        {
            ClearGroundPlatformReference();
            return;
        }

        if (activeGroundPlatform != candidate)
        {
            activeGroundPlatform = candidate;
            activeGroundPlatformLastPosition = candidate.position;
            activeGroundPlatformLastRotation = candidate.rotation;
            hasActiveGroundPlatformPose = true;
            return;
        }

        if (!hasActiveGroundPlatformPose)
        {
            activeGroundPlatformLastPosition = candidate.position;
            activeGroundPlatformLastRotation = candidate.rotation;
            hasActiveGroundPlatformPose = true;
        }
    }

    private void ClearGroundPlatformReference()
    {
        activeGroundPlatform = null;
        hasActiveGroundPlatformPose = false;
        activeGroundPlatformLastPosition = Vector3.zero;
        activeGroundPlatformLastRotation = Quaternion.identity;
    }

    private void MoveCharacter(float deltaTime, float verticalBeforeMove)
    {
        if (isGrounded)
        {
            TryStepUp(deltaTime);
        }
        else
        {
            ClearStepDebugData();
        }

        Vector3 verticalAxis = ResolveVerticalMotionUpAxis();
        Vector3 velocity = planarVelocity + verticalAxis * verticalVelocity;
        CollisionFlags collisionFlags = controller.Move(velocity * deltaTime);
        lastMoveCollisionFlags = collisionFlags;

        if ((collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
            jumpHoldTimer = 0f;
            jumpCutProtectedVelocity = 0f;
        }

        if ((collisionFlags & CollisionFlags.Below) != 0)
        {
            if (hasPendingJumpingPlatformBounce)
            {
                ConsumePendingJumpingPlatformBounce();
                return;
            }

            bool landedFromAir = !wasGrounded
                || jumpConsumed
                || currentState == MovementState.Jumping
                || currentState == MovementState.Falling
                || currentState == MovementState.GroundPound
                || currentState == MovementState.Dive;
            if (landedFromAir)
            {
                HandleLanding(verticalBeforeMove);
                landingHandledThisFrame = true;
                ResetNonJumpFallTracking();
                StartJumpLandingCooldown();
            }

            if (verticalVelocity < 0f)
            {
                verticalVelocity = groundedVerticalForce;
            }

            isGrounded = true;
            lastGroundedTime = Time.time;
            jumpConsumed = false;
            jumpHoldTimer = 0f;
            jumpCutProtectedVelocity = 0f;
            ResetAirJumps();
        }

        if (hasPendingJumpingPlatformBounce)
        {
            ConsumePendingJumpingPlatformBounce();
        }
    }

    private void TryHandleLandingFallback(float verticalBeforeMove)
    {
        if (landingHandledThisFrame || controller == null)
        {
            return;
        }

        bool groundedNow = isGrounded || controller.isGrounded;
        if (!groundedNow)
        {
            return;
        }

        bool stuckInAirStateWhileGrounded = (currentState == MovementState.Falling || currentState == MovementState.Jumping)
            && verticalBeforeMove <= 0.01f;
        if (wasGrounded && !stuckInAirStateWhileGrounded)
        {
            return;
        }

        bool airborneLikeState = jumpConsumed
            || currentState == MovementState.Jumping
            || currentState == MovementState.Falling
            || currentState == MovementState.GroundPound
            || currentState == MovementState.Dive
            || verticalBeforeMove < -0.01f;
        if (!airborneLikeState)
        {
            return;
        }

        HandleLanding(verticalBeforeMove);
        landingHandledThisFrame = true;
        ResetNonJumpFallTracking();
        StartJumpLandingCooldown();
        if (verticalVelocity < 0f)
        {
            verticalVelocity = groundedVerticalForce;
        }

        isGrounded = true;
        lastGroundedTime = Time.time;
        jumpConsumed = false;
        jumpHoldTimer = 0f;
        jumpCutProtectedVelocity = 0f;
        ResetAirJumps();
    }

    private Vector3 ResolveVerticalMotionUpAxis()
    {
        Transform axisReference = null;
        if (IsParentedToMovingPlatform() && movingPlatformParentTransform != null)
        {
            axisReference = movingPlatformParentTransform;
        }
        else if (transform.parent != null)
        {
            axisReference = transform.parent;
        }

        if (axisReference != null)
        {
            Vector3 parentUp = axisReference.up;
            if (parentUp.sqrMagnitude > 0.0001f)
            {
                return parentUp.normalized;
            }
        }

        return Vector3.up;
    }

    private Vector2 ReadMovementInput()
    {
        Vector2 keyboardInput = ReadKeyboardMoveInput();
        Vector2 gamepadInput = ReadGamepadMoveInput();
        Vector2 chosenInput = gamepadInput.sqrMagnitude > keyboardInput.sqrMagnitude ? gamepadInput : keyboardInput;
        return Vector2.ClampMagnitude(chosenInput, 1f);
    }

    private static Vector2 ReadKeyboardMoveInput()
    {
        float horizontal = 0f;
        if (MinimoInputBridge.GetKey(KeyCode.A))
        {
            horizontal -= 1f;
        }

        if (MinimoInputBridge.GetKey(KeyCode.D))
        {
            horizontal += 1f;
        }

        float vertical = 0f;
        if (MinimoInputBridge.GetKey(KeyCode.S))
        {
            vertical -= 1f;
        }

        if (MinimoInputBridge.GetKey(KeyCode.W))
        {
            vertical += 1f;
        }

        return new Vector2(horizontal, vertical);
    }

    private void TryStepUp(float deltaTime)
    {
        ClearStepDebugData();
        if (!enableStepUp || controller == null || !isGrounded)
        {
            return;
        }

        if (currentState == MovementState.Dashing
            || currentState == MovementState.GroundPound
            || currentState == MovementState.Dive
            || currentState == MovementState.Rolling
            || currentState == MovementState.SlopeSliding
            || currentState == MovementState.Teetering)
        {
            return;
        }

        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(planarVelocity, Vector3.up);
        if (horizontalVelocity.sqrMagnitude <= 0.01f)
        {
            return;
        }

        Bounds bounds = controller.bounds;
        float stepHeight = Mathf.Clamp(maxStepHeight, 0f, Mathf.Max(0.05f, controller.height * 0.5f));
        if (stepHeight <= 0.001f)
        {
            return;
        }

        Vector3 moveDirection = horizontalVelocity.normalized;
        float dynamicDistance = horizontalVelocity.magnitude * deltaTime + controller.skinWidth + 0.05f;
        float checkDistance = Mathf.Max(stepCheckDistance, dynamicDistance);
        float castRadius = Mathf.Clamp(controller.radius * 0.45f, 0.04f, controller.radius);
        int mask = GetGroundMaskExcludingSelf();

        float lowerHeight = Mathf.Max(stepSurfaceProbeHeight, castRadius + 0.02f);
        Vector3 lowerOrigin = new Vector3(bounds.center.x, bounds.min.y + lowerHeight, bounds.center.z);
        Vector3 upperOrigin = lowerOrigin + Vector3.up * stepHeight;
        debugStepLowerOrigin = lowerOrigin;
        debugStepUpperOrigin = upperOrigin;
        debugStepDirection = moveDirection;
        debugStepProbeDistance = checkDistance;
        debugStepProbeRadius = castRadius;

        bool lowerBlocked = Physics.SphereCast(
            lowerOrigin,
            castRadius,
            moveDirection,
            out RaycastHit lowerHit,
            checkDistance,
            mask,
            QueryTriggerInteraction.Ignore);
        bool upperBlocked = Physics.SphereCast(
            upperOrigin,
            castRadius,
            moveDirection,
            out _,
            checkDistance,
            mask,
            QueryTriggerInteraction.Ignore);
        debugStepLowerBlocked = lowerBlocked;
        debugStepUpperBlocked = upperBlocked;
        if (!lowerBlocked || upperBlocked)
        {
            return;
        }

        Vector3 probeOrigin = upperOrigin + moveDirection * (lowerHit.distance + stepForwardOffset);
        float probeDistance = stepHeight + lowerHeight + 0.25f;
        debugStepProbeOrigin = probeOrigin;
        if (!Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit topHit, probeDistance, mask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        float slopeLimit = controller.slopeLimit + 0.5f;
        float stepSurfaceAngle = Vector3.Angle(topHit.normal, Vector3.up);
        if (stepSurfaceAngle > slopeLimit)
        {
            return;
        }

        float stepUpAmount = topHit.point.y - bounds.min.y;
        if (stepUpAmount <= 0.001f || stepUpAmount > stepHeight + 0.05f)
        {
            return;
        }

        controller.Move(Vector3.up * stepUpAmount);
        debugStepCandidateFound = true;
        debugStepHitPoint = topHit.point;
        hasGroundNormal = true;
        groundNormal = topHit.normal.sqrMagnitude > 0.0001f ? topHit.normal.normalized : Vector3.up;
        groundHitPoint = topHit.point;
        groundSlopeAngle = Vector3.Angle(groundNormal, Vector3.up);
        groundDownhillDirection = CalculateDownhillDirection(groundNormal);
    }

    private void ClearStepDebugData()
    {
        debugStepLowerBlocked = false;
        debugStepUpperBlocked = false;
        debugStepCandidateFound = false;
    }

    private void RecordDashDebug(Vector3 direction, float speed)
    {
        debugDashDirection = ResolveDebugDirection(direction);
        debugDashStartPoint = transform.position + Vector3.up * 0.08f;
        debugDashStartSpeed = Mathf.Max(0f, speed);
        float distance = debugDashStartSpeed * Mathf.Max(0.01f, dashDuration);
        debugDashEndPoint = debugDashStartPoint + debugDashDirection * distance;
        debugDashStartTime = Time.time;
    }

    private void RecordDiveDebug(Vector3 direction, float startSpeed, float startVerticalSpeed)
    {
        debugDiveDirection = ResolveDebugDirection(direction);
        debugDiveStartPoint = transform.position + Vector3.up * 0.08f;
        debugDiveStartSpeed = Mathf.Max(0f, startSpeed);
        debugDiveStartVerticalSpeed = startVerticalSpeed;
        float duration = Mathf.Max(0.01f, diveDuration);
        float horizontalDistance = debugDiveStartSpeed * duration;
        float gravityAccel = gravity * diveGravityMultiplier;
        float verticalOffset = startVerticalSpeed * duration + 0.5f * gravityAccel * duration * duration;
        debugDivePredictedEndPoint = debugDiveStartPoint + debugDiveDirection * horizontalDistance + Vector3.up * verticalOffset;
        debugDiveStartTime = Time.time;
    }

    private void RecordSlideDebug(Vector3 direction, float startSpeed, bool slopeSlide)
    {
        debugSlideDirection = ResolveDebugDirection(direction);
        debugSlideStartPoint = transform.position + Vector3.up * 0.05f;
        debugSlideStartSpeed = Mathf.Max(0f, startSpeed);
        debugSlideWasSlope = slopeSlide;
        float deceleration = slopeSlide ? Mathf.Max(0.01f, slopeSlideFriction) : Mathf.Max(0.01f, slideDeceleration);
        float maxDuration = slopeSlide ? Mathf.Max(0.2f, slideDuration) : Mathf.Max(0.1f, slideDuration);
        float distance = EstimateStopDistance(debugSlideStartSpeed, deceleration, maxDuration);
        debugSlidePredictedEndPoint = debugSlideStartPoint + debugSlideDirection * distance;
        debugSlideStartTime = Time.time;
    }

    private Vector3 ResolveDebugDirection(Vector3 direction)
    {
        Vector3 flattened = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (flattened.sqrMagnitude <= 0.0001f)
        {
            flattened = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        return flattened.sqrMagnitude > 0.0001f ? flattened.normalized : Vector3.forward;
    }

    private static float EstimateStopDistance(float speed, float deceleration, float maxDuration)
    {
        float safeDecel = Mathf.Max(0.01f, deceleration);
        float timeToStop = speed / safeDecel;
        float evalTime = Mathf.Min(Mathf.Max(0.01f, maxDuration), timeToStop);
        float distance = speed * evalTime - 0.5f * safeDecel * evalTime * evalTime;
        return Mathf.Max(0f, distance);
    }

    private void HandleLanding(float verticalBeforeMove)
    {
        jumpCutProtectedVelocity = 0f;
        ResetAnimatorTrigger(animJumpTrigger);
        ResetAnimatorTrigger(animFrontflipTrigger);
        ResetAnimatorTrigger(animBackflipTrigger);

        float landingBottomY = transform.position.y;
        bool forceImmediateRollFromDoubleJumpDash = doubleJumpDashRollOnLandingPending;
        doubleJumpDashRollOnLandingPending = false;
        bool doubleJumpLanding = didPerformAirJumpSinceGrounded;
        bool hasDoubleJumpApex = doubleJumpLanding
            && doubleJumpApexTrackingActive
            && !float.IsNegativeInfinity(doubleJumpApexBottomY);
        float doubleJumpFallDistance = hasDoubleJumpApex
            ? Mathf.Max(0f, doubleJumpApexBottomY - landingBottomY)
            : 0f;
        float singleJumpHeightThreshold = Mathf.Max(0.1f, jumpHeight);
        bool shouldRollFromDoubleJumpHeight = hasDoubleJumpApex
            && doubleJumpFallDistance >= singleJumpHeightThreshold;
        bool hasLandingMovementIntent = HasRawDirectionalInput();
        bool forceLandingPulseForIdleDoubleJump = doubleJumpLanding
            && !hasLandingMovementIntent
            && !forceImmediateRollFromDoubleJumpDash;
        bool allowRollFromDoubleJumpRule = shouldRollFromDoubleJumpHeight
            && !forceLandingPulseForIdleDoubleJump;
        bool suppressRollAndLandingForLowDoubleJump = doubleJumpLanding && !shouldRollFromDoubleJumpHeight;
        bool suppressRollForDoubleJump = suppressRollAndLandingForLowDoubleJump
            || forceLandingPulseForIdleDoubleJump;

        float impactSpeed = -verticalBeforeMove;
        debugLastLandingImpactSpeed = impactSpeed;
        debugLastLandingPoint = groundHitPoint;
        debugLastLandingTime = Time.time;
        bool landedFromGroundPound = currentState == MovementState.GroundPound;
        bool landedFromDive = currentState == MovementState.Dive;
        bool landedFromBackflipJump = backflipJumpActive
            && (currentState == MovementState.Jumping || currentState == MovementState.Falling);
        bool startedRollOnLanding = false;
        bool startedSlopeSlideOnLanding = false;
        bool resumedRollAfterAir = currentState == MovementState.Rolling && rollAirReentryPending;
        bool shouldRollFromWallJumpMovement = !suppressRollForDoubleJump
            && !landedFromBackflipJump
            && wallJumpForwardBackLockActive
            && (currentState == MovementState.Jumping || currentState == MovementState.Falling)
            && hasLandingMovementIntent;
        bool forceWallJumpRollOnLanding = !suppressRollForDoubleJump
            && !landedFromBackflipJump
            && forceRollOnWallJumpLanding
            && (currentState == MovementState.Jumping || currentState == MovementState.Falling);
        bool shouldRollFromHeldLandingInput = !suppressRollForDoubleJump
            && !landedFromBackflipJump
            && (currentState == MovementState.Jumping || currentState == MovementState.Falling)
            && IsRollInputHeld();
        bool shouldAutoRollOnLanding = !suppressRollForDoubleJump
            && !landedFromBackflipJump
            && autoRollFromMovingJump
            && !didPerformAirJumpSinceGrounded
            && (currentState == MovementState.Jumping || currentState == MovementState.Falling)
            && ShouldAutoRollFromJumpLanding();
        bool highFallRollThresholdReached = ShouldRollFromHighFallLanding();
        bool highJumpRollThresholdReached = ShouldRollFromHighJumpLanding();
        bool highDropForLandingPulse = highFallRollThresholdReached || highJumpRollThresholdReached;
        bool shouldSpawnLandingParticle = jumpConsumed || highFallRollThresholdReached;
        if (shouldSpawnLandingParticle)
        {
            SpawnLandingParticle();
        }

        autoRollFromMovingJump = false;
        forceRollOnWallJumpLanding = false;
        backflipJumpActive = false;
        backflipAnimatorHoldActive = false;
        if (landedFromGroundPound)
        {
            awaitingDiveRoll = false;
            pendingDiveRollSpeed = 0f;
            diveRollWindowEndTime = float.NegativeInfinity;
        }
        else if (forceImmediateRollFromDoubleJumpDash)
        {
            awaitingDiveRoll = false;
            pendingDiveRollSpeed = 0f;
            diveRollWindowEndTime = float.NegativeInfinity;
            float rollSourceSpeed = Mathf.Clamp(
                Mathf.Max(rollBaseSpeed, planarVelocity.magnitude + impactSpeed * rollMomentumConversion),
                rollBaseSpeed,
                6f);
            startedRollOnLanding = StartRoll(rollSourceSpeed, false, true);
            if (!startedRollOnLanding)
            {
                SetAnimatorBoolOrTrigger(animRollParam, true);
                startedRollOnLanding = true;
            }
        }
        else if (resumedRollAfterAir && !suppressRollForDoubleJump)
        {
            awaitingDiveRoll = false;
            pendingDiveRollSpeed = 0f;
            diveRollWindowEndTime = float.NegativeInfinity;
            float effectiveRollDuration = GetEffectiveRollDuration();
            float landingResumeRollTimerCap = Mathf.Clamp(
                effectiveRollDuration * rollLandingResumeDurationRatio,
                0.1f,
                Mathf.Min(effectiveRollDuration, rollLandingResumeMaxDuration));
            if (rollAirHighFallTriggered)
            {
                float rollSourceSpeed = Mathf.Clamp(
                    Mathf.Max(rollBaseSpeed, planarVelocity.magnitude + impactSpeed * rollMomentumConversion),
                    rollBaseSpeed,
                    6f);
                startedRollOnLanding = StartRoll(rollSourceSpeed, false, true);
                rollTimer = Mathf.Min(rollTimer, landingResumeRollTimerCap);
            }
            else
            {
                // Preserve the roll state on short falls and avoid unnecessary timer resets at landing.
                rollTimer = Mathf.Min(rollTimer, landingResumeRollTimerCap);
                startedRollOnLanding = true;
            }
        }
        else if (shouldRollFromWallJumpMovement)
        {
            awaitingDiveRoll = false;
            pendingDiveRollSpeed = 0f;
            diveRollWindowEndTime = float.NegativeInfinity;
            float rollSourceSpeed = Mathf.Clamp(
                Mathf.Max(rollBaseSpeed, planarVelocity.magnitude),
                rollBaseSpeed,
                5f);
            startedRollOnLanding = StartRoll(rollSourceSpeed, false);
        }
        else if (forceWallJumpRollOnLanding)
        {
            awaitingDiveRoll = false;
            pendingDiveRollSpeed = 0f;
            diveRollWindowEndTime = float.NegativeInfinity;
            float rollSourceSpeed = Mathf.Max(rollBaseSpeed, planarVelocity.magnitude);
            startedRollOnLanding = StartRoll(rollSourceSpeed, false, true);
        }
        else if (shouldRollFromHeldLandingInput)
        {
            awaitingDiveRoll = false;
            pendingDiveRollSpeed = 0f;
            diveRollWindowEndTime = float.NegativeInfinity;
            float rollSourceSpeed = Mathf.Clamp(
                Mathf.Max(rollBaseSpeed, planarVelocity.magnitude + impactSpeed * rollMomentumConversion),
                rollBaseSpeed,
                5.5f);
            startedRollOnLanding = StartRoll(rollSourceSpeed, false, true);
        }
        else if (currentState == MovementState.Dive && !suppressRollForDoubleJump)
        {
            float diveImpact = Mathf.Max(impactSpeed, diveDownwardSpeed * 0.65f);
            float rollSourceSpeed = Mathf.Max(rollBaseSpeed, planarVelocity.magnitude + diveImpact * rollMomentumConversion);
            bool canRoll = diveImpact >= rollMinImpactSpeed;
            if (canRoll && HasBufferedRollInput())
            {
                startedRollOnLanding = StartRoll(rollSourceSpeed, true);
            }
            if (!startedRollOnLanding)
            {
                awaitingDiveRoll = canRoll;
                pendingDiveRollSpeed = rollSourceSpeed;
                diveRollWindowEndTime = canRoll ? Time.time + rollWindowTime : float.NegativeInfinity;
                planarVelocity *= diveMissRecoveryMultiplier;
                if (currentState == MovementState.Dive)
                {
                    SetState(enableCrouchSlide && IsCrouchIntentActive()
                        ? MovementState.Crouching
                        : MovementState.Grounded);
                }
            }
        }
        else
        {
            awaitingDiveRoll = false;
            pendingDiveRollSpeed = 0f;
            diveRollWindowEndTime = float.NegativeInfinity;
            bool shouldRollFromHighFall = !suppressRollForDoubleJump
                && hasLandingMovementIntent
                && highFallRollThresholdReached;
            bool shouldRollFromHighJump = !suppressRollForDoubleJump
                && hasLandingMovementIntent
                && highJumpRollThresholdReached;

            if (!landedFromBackflipJump && allowRollFromDoubleJumpRule)
            {
                float rollSourceSpeed = Mathf.Clamp(
                    Mathf.Max(rollBaseSpeed, planarVelocity.magnitude + impactSpeed * rollMomentumConversion),
                    rollBaseSpeed,
                    5.5f);

                startedRollOnLanding = StartRoll(rollSourceSpeed, false);
            }
            else if (!landedFromBackflipJump && (shouldRollFromHighFall || shouldRollFromHighJump))
            {
                float rollSourceSpeed = Mathf.Clamp(
                    Mathf.Max(rollBaseSpeed, planarVelocity.magnitude + impactSpeed * rollMomentumConversion),
                    rollBaseSpeed,
                    5.5f);

                startedRollOnLanding = StartRoll(rollSourceSpeed, false);
            }
            else if (!landedFromBackflipJump && shouldAutoRollOnLanding)
            {
                float rollSourceSpeed = Mathf.Clamp(
                    Mathf.Max(rollBaseSpeed, planarVelocity.magnitude),
                    rollBaseSpeed,
                    5f);

                startedRollOnLanding = StartRoll(rollSourceSpeed, false);
            }
        }

        if (landedFromGroundPound && IsSlopeSlideSurfaceValid())
        {
            StartSlopeSlide(true, false);
            startedSlopeSlideOnLanding = currentState == MovementState.SlopeSliding;
            if (startedSlopeSlideOnLanding)
            {
                groundPoundHardFallActive = false;
                groundPoundHardLandActive = false;
                groundPoundHardLandReleaseTime = float.NegativeInfinity;
                groundPoundBounceLockedUntilNextJump = true;
            }
        }

        if (landedFromGroundPound && !startedSlopeSlideOnLanding)
        {
            TriggerGroundPoundHardLanding(impactSpeed);
        }

        bool shouldTriggerLandingPulse = !startedRollOnLanding
            && !startedSlopeSlideOnLanding
            && !hasLandingMovementIntent
            && !landedFromGroundPound
            && !landedFromDive
            && highDropForLandingPulse;
        if (shouldTriggerLandingPulse)
        {
            landingAnimatorPulseTimer = LandingAnimatorPulseDuration;
        }
        else
        {
            // Do not enable the Landing bool on moving, low-height, or other action-based landings.
            landingAnimatorPulseTimer = 0f;
        }

        ClearRollAirTracking();
        ClearJumpFallTracking();
        ResetDoubleJumpApexTracking();

        if (suppressRollAndLandingForLowDoubleJump)
        {
            // Do not play extra impact FX or shake on short falls after a double jump.
            return;
        }

        if (impactSpeed < landingImpactStartSpeed)
        {
            return;
        }

        float impact01 = Mathf.InverseLerp(landingImpactMinSpeed, landingImpactMaxSpeed, impactSpeed);
        AddCameraShake(cameraShakeMaxAmplitude * impact01 * 0.5f * landingCameraShakeMultiplier);
        float totalSquash = landSquash * impact01;
        if (landedFromGroundPound)
        {
            totalSquash += groundPoundLandSquashBonus;
        }

        landSquashAmount = Mathf.Max(landSquashAmount, totalSquash);

        if (landedFromGroundPound)
        {
            PlayClip(groundPoundLandClip);
            AddCameraKick(groundPoundLandCameraKick);
        }
        else
        {
            PlayClip(landClip);
            AddCameraKick(landCameraKick * Mathf.Max(0.35f, impact01));
        }
    }

    private void RefreshMovementState()
    {
        if (currentState == MovementState.Dashing && dashTimer > 0f)
        {
            return;
        }

        if (currentState == MovementState.GroundPound && !isGrounded)
        {
            return;
        }

        if (currentState == MovementState.Dive && !isGrounded)
        {
            return;
        }

        if (currentState == MovementState.Teetering && isGrounded)
        {
            return;
        }

        if (currentState == MovementState.Rolling && isGrounded && rollTimer > 0f)
        {
            return;
        }

        if (currentState == MovementState.Rolling && !isGrounded && rollAirReentryPending)
        {
            return;
        }

        if (currentState == MovementState.Crouching && !isGrounded)
        {
            // Preserve state on short or medium falls from crouch; switch to normal fall on very high drops.
            bool keepCrouchDuringDrop = IsCrouchIntentActive()
                && !jumpConsumed
                && !ShouldRollFromHighFallLanding();
            if (keepCrouchDuringDrop)
            {
                return;
            }
        }

        if (currentState == MovementState.SlopeSliding && isGrounded)
        {
            return;
        }

        if (currentState == MovementState.Sliding
            && isGrounded
            && (slideTimer > 0f || slideExitLockTimer > 0f))
        {
            return;
        }

        if (groundPoundHardFallActive
            && !groundPoundHardLandActive
            && !isGrounded
            && currentState != MovementState.GroundPound)
        {
            SetState(MovementState.Falling);
            return;
        }

        if (isGrounded)
        {
            bool explicitCrouchInputActive = crouchPressedThisFrame || crouchHeld;
            bool allowCrouchIntentByState = !IsRollAutoCrouchSuppressed() || explicitCrouchInputActive;
            bool lowClearanceForStanding = enableCrouchSlide
                && !IsRollAutoCrouchSuppressed()
                && IsLowClearanceForStanding();
            if (enableCrouchSlide && ((allowCrouchIntentByState && IsCrouchIntentActive()) || lowClearanceForStanding))
            {
                crouchTarget = true;
                SetState(MovementState.Crouching);
            }
            else
            {
                SetState(MovementState.Grounded);
            }

            return;
        }

        SetState(verticalVelocity > 0.1f ? MovementState.Jumping : MovementState.Falling);
    }

    private MovementState ResolvePostRollState(bool _)
    {
        // If roll started from crouch, return to crouch.
        if (enableCrouchSlide && rollStartedFromCrouch)
        {
            return MovementState.Crouching;
        }

        // Otherwise finish roll exit standing on the ground.
        return MovementState.Grounded;
    }

    private void SetState(MovementState nextState)
    {
        if (currentState == nextState)
        {
            return;
        }

        if (nextState != MovementState.Falling)
        {
            wallHangReleaseHighFallCheckActive = false;
        }

        if (nextState != MovementState.Rolling)
        {
            ClearRollAirTracking();
        }

        if (nextState != MovementState.Dashing)
        {
            dashStartedFromStandstill = false;
        }

        if (nextState != MovementState.Sliding)
        {
            slideLowClearanceLockActive = false;
            slideLowClearanceLockedSpeed = 0f;
        }

        if (nextState == MovementState.Falling)
        {
            // Preserve ClimbJump during the fall after jumping from hang, without forcing Fall.
            forceFallAnimatorFromClimbJump = false;
        }
        else
        {
            forceFallAnimatorFromClimbJump = false;
            if (nextState != MovementState.Jumping)
            {
                climbJumpAnimatorActive = false;
            }
        }

        currentState = nextState;
    }

    private void UpdateClimbJumpAnimatorWallContactState()
    {
        if (!climbJumpAnimatorActive)
        {
            return;
        }

        if (controller == null || isGrounded)
        {
            climbJumpAnimatorActive = false;
            return;
        }

        if (currentState != MovementState.Jumping && currentState != MovementState.Falling)
        {
            climbJumpAnimatorActive = false;
            return;
        }

        if (!TryGetWallHangNormal(out _, true))
        {
            climbJumpAnimatorActive = false;
        }
    }

    private void UpdateWallJumpToRegularJumpTransition()
    {
        if (isGrounded
            || wallHangActive
            || ledgeHangActive
            || ledgeClimbActive
            || currentState != MovementState.Jumping
            || verticalVelocity <= 0.05f)
        {
            return;
        }

        bool wallJumpStyleActive = wallJumpForwardBackLockActive
            || wallJumpNoRotateActive
            || lockFacingUntilGroundInputAfterWallJump
            || backflipJumpActive;
        if (!wallJumpStyleActive)
        {
            return;
        }

        if (backflipJumpActive)
        {
            // Keep the facing lock in the air during backward wall-jump or backflip.
            // Keep the character locked to the facing direction while holding the wall.
            return;
        }

        if (IsTouchingWallForJumpHangBlock())
        {
            return;
        }

        if (CanUseHangJumpCornerSnapAssist()
            && TryGetLedgeHangInfo(out Vector3 cornerLedgeNormal, out Vector3 cornerLedgeTopPoint)
            && ShouldForceLedgeSnapFromHangJump(cornerLedgeNormal, cornerLedgeTopPoint))
        {
            wallHangBlockedForCurrentJump = false;
            wallHangBlockedUntilFalling = false;
            EnterLedgeHang(cornerLedgeNormal, cornerLedgeTopPoint);
            return;
        }

        wallJumpNoRotateActive = false;
        wallJumpForwardBackLockActive = false;
        wallJumpForcedDirection = Vector3.back;
        lockFacingUntilGroundInputAfterWallJump = false;
        backflipJumpActive = false;
        forceRollOnWallJumpLanding = false;
        wallJumpCameraTurnRemaining = 0f;
        wallHangBlockedForCurrentJump = false;
        wallHangBlockedUntilFalling = false;
        hangJumpCornerSnapAssistUntilTime = float.NegativeInfinity;
        TriggerAnimator(animJumpTrigger);
    }

    private int GetGroundMaskExcludingSelf()
    {
        int mask = ResolveGroundMaskValue();
        return mask & ~(1 << gameObject.layer);
    }

    private int GetStandUpMaskExcludingSelf()
    {
        int configured = standUpBlockMask.value;
        int mask = configured == 0 ? ~0 : configured;
        return mask & ~(1 << gameObject.layer);
    }

    private int ResolveGroundMaskValue()
    {
        int configured = groundMask.value;

        // Treat inspector defaults (~0 / 0) as "auto". If a Ground layer exists, prefer it.
        bool useAutoMask = configured == 0 || configured == ~0;
        if (useAutoMask)
        {
            if (cachedGroundLayerIndex == int.MinValue)
            {
                cachedGroundLayerIndex = LayerMask.NameToLayer(DefaultGroundLayerName);
            }

            if (cachedGroundLayerIndex >= 0)
            {
                return 1 << cachedGroundLayerIndex;
            }
        }

        // Fallback: keep user-provided mask, or all layers if explicitly empty.
        return configured != 0 ? configured : ~0;
    }

    private int GetClimbMaskExcludingSelf()
    {
        int mask = customClimbMask.value;
        return mask & ~(1 << gameObject.layer);
    }

    private bool TrySphereCastIgnoringSelf(
        Vector3 origin,
        float radius,
        Vector3 direction,
        out RaycastHit closestHit,
        float maxDistance,
        int layerMask,
        QueryTriggerInteraction queryTriggerInteraction)
    {
        Array.Clear(physicsHitBuffer, 0, physicsHitBuffer.Length);
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            radius,
            direction,
            physicsHitBuffer,
            maxDistance,
            layerMask,
            queryTriggerInteraction);
        return TrySelectClosestNonSelfHit(hitCount, out closestHit);
    }

    private bool TryRaycastIgnoringSelf(
        Vector3 origin,
        Vector3 direction,
        out RaycastHit closestHit,
        float maxDistance,
        int layerMask,
        QueryTriggerInteraction queryTriggerInteraction)
    {
        Array.Clear(physicsHitBuffer, 0, physicsHitBuffer.Length);
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            physicsHitBuffer,
            maxDistance,
            layerMask,
            queryTriggerInteraction);
        return TrySelectClosestNonSelfHit(hitCount, out closestHit);
    }

    private bool TrySelectClosestNonSelfHit(int hitCount, out RaycastHit closestHit)
    {
        closestHit = default;
        bool found = false;
        float bestDistance = float.PositiveInfinity;
        int iterateCount = Mathf.Min(hitCount, physicsHitBuffer.Length);
        for (int i = 0; i < iterateCount; i++)
        {
            RaycastHit hit = physicsHitBuffer[i];
            if (hit.collider == null || IsOwnCollider(hit.collider))
            {
                continue;
            }

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                closestHit = hit;
                found = true;
            }
        }

        return found;
    }

    private bool IsOwnCollider(Collider candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (candidate == controller)
        {
            return true;
        }

        Transform candidateTransform = candidate.transform;
        if (IsOwnTransform(candidateTransform))
        {
            return true;
        }

        Rigidbody attachedRigidbody = candidate.attachedRigidbody;
        return attachedRigidbody != null && IsOwnTransform(attachedRigidbody.transform);
    }

    private bool IsOwnTransform(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (candidate == transform || candidate.IsChildOf(transform))
        {
            return true;
        }

        return runtimeVisualRoot != null
            && (candidate == runtimeVisualRoot || candidate.IsChildOf(runtimeVisualRoot));
    }

    private void UpdateGroundState()
    {
        if (controller == null)
        {
            return;
        }

        Bounds controllerBounds = controller.bounds;
        Vector3 probeOrigin = controllerBounds.center + Vector3.up * groundProbeOffset;
        float probeDistance = controllerBounds.extents.y + groundProbeDistance;
        float probeRadius = Mathf.Clamp(groundProbeRadius, 0.05f, controllerBounds.extents.x * 0.95f);
        bool ignoreProbeWhileAscending = verticalVelocity > ignoreGroundProbeUpwardSpeed;
        int mask = GetGroundMaskExcludingSelf();

        bool groundedByProbe = TrySphereCastIgnoringSelf(
            probeOrigin,
            probeRadius,
            Vector3.down,
            out RaycastHit probeHit,
            probeDistance,
            mask,
            QueryTriggerInteraction.Ignore);

        bool groundedByController = controller.isGrounded;
        float probeGapToFeet = float.PositiveInfinity;
        if (groundedByProbe)
        {
            probeGapToFeet = Mathf.Max(0f, controllerBounds.min.y - probeHit.point.y);
        }

        bool hasGroundMovementIntent = desiredMoveDirection.sqrMagnitude > movementInputDeadZone * movementInputDeadZone
            || Vector3.ProjectOnPlane(planarVelocity, Vector3.up).sqrMagnitude > 0.04f;
        float uphillProbeAssist01 = 0f;
        float probeCloseTolerance = Mathf.Max(controller.skinWidth + 0.02f, GroundProbeSnapDistance);
        bool canUseUphillProbeAssist = groundedByProbe
            && hasGroundMovementIntent
            && TryGetUphillProbeAssistStrength(probeHit.normal, out uphillProbeAssist01);
        if (canUseUphillProbeAssist)
        {
            float extraProbeTolerance = Mathf.Max(0.06f, groundProbeDistance * 0.75f) * uphillProbeAssist01;
            probeCloseTolerance += extraProbeTolerance;
        }

        bool probeCloseToFeet = groundedByProbe
            && probeGapToFeet <= probeCloseTolerance;
        bool canUseProbeGround = groundedByProbe
            && !ignoreProbeWhileAscending
            && (groundedByController || verticalVelocity <= 0.05f)
            && probeCloseToFeet;

        isGrounded = groundedByController || canUseProbeGround;
        bool canUsePlatformGroundAssist = CanUseMovingPlatformGroundAssist();
        if (!isGrounded && canUsePlatformGroundAssist)
        {
            isGrounded = true;
        }

        if (!isGrounded
            && !jumpConsumed
            && groundedByProbe
            && hasGroundMovementIntent
            && canUseUphillProbeAssist
            && verticalVelocity <= Mathf.Max(0.2f, ignoreGroundProbeUpwardSpeed)
            && Time.time - lastGroundedTime <= Mathf.Lerp(0.05f, 0.2f, uphillProbeAssist01))
        {
            float hysteresisExtraGap = Mathf.Lerp(
                0.03f,
                Mathf.Max(0.08f, controller.skinWidth + groundProbeDistance * 0.35f),
                uphillProbeAssist01);
            if (probeGapToFeet <= probeCloseTolerance + hysteresisExtraGap)
            {
                // Prevent Grounded/Falling jitter from momentary probe loss on uphill slopes.
                isGrounded = true;
            }
        }

        hasGroundNormal = false;
        groundNormal = Vector3.up;
        groundSlopeAngle = 0f;
        groundDownhillDirection = Vector3.zero;
        groundHitPoint = controllerBounds.center - Vector3.up * controllerBounds.extents.y;

        bool leftGroundWithoutJump = !isGrounded && wasGrounded && !jumpConsumed;
        if (leftGroundWithoutJump)
        {
            BeginNonJumpFallTracking();
        }
        else if (!isGrounded && !trackingNonJumpFall && !jumpConsumed)
        {
            // Fallback: start non-jump fall tracking even if controller-based transition moments are missed.
            BeginNonJumpFallTracking();
        }

        if (!isGrounded && trackingNonJumpFall && !jumpConsumed)
        {
            float fallHeight = GetTrackedNonJumpFallHeight();
            if (fallHeight >= GetResolvedHighFallRollHeightThreshold(fromJump: false))
            {
                nonJumpHighFallReached = true;
            }
        }
        else if (isGrounded && wasGrounded)
        {
            ResetNonJumpFallTracking();
        }

        if (isGrounded)
        {
            RaycastHit normalHit = default;
            Collider groundedCollider = null;
            if (canUseProbeGround)
            {
                normalHit = probeHit;
                hasGroundNormal = true;
            }
            else
            {
                Vector3 rayOrigin = controllerBounds.center + Vector3.up * 0.2f;
                float rayDistance = controllerBounds.extents.y + groundProbeDistance + maxStepHeight + 0.3f;
                if (TryRaycastIgnoringSelf(rayOrigin, Vector3.down, out normalHit, rayDistance, mask, QueryTriggerInteraction.Ignore))
                {
                    hasGroundNormal = true;
                }
            }

            if (hasGroundNormal)
            {
                groundNormal = normalHit.normal.sqrMagnitude > 0.0001f ? normalHit.normal.normalized : Vector3.up;
                groundHitPoint = normalHit.point;
                groundSlopeAngle = Vector3.Angle(groundNormal, Vector3.up);
                groundDownhillDirection = CalculateDownhillDirection(groundNormal);
                groundedCollider = normalHit.collider;
            }

            UpdateGroundPlatformReference(groundedCollider);
        }

        if (isGrounded)
        {
            wallHangReleaseHighFallCheckActive = false;
            bool keepGroundSuppressedForLedge = ledgeClimbActive
                || (ledgeHangActive && !ShouldReleaseWallHangNearGround());
            if (keepGroundSuppressedForLedge)
            {
                // Do not auto-release due to position or ground contact after ledge hang starts.
                isGrounded = false;
                ClearGroundPlatformReference();
                apexAssistActive = false;
                lastSlopeSpeedFactor = 1f;
                runStopSlideTimer = 0f;
                stopPrimedFromAnimatorSpeed = false;
                stopAnimationTimer = 0f;
            }
            else
            {
                ExitWallHang();
                wallJumpNoRotateActive = false;
                if (groundedByController)
                {
                    wallJumpForwardBackLockActive = false;
                }

                lastGroundedTime = Time.time;
                ResetAirJumps();
                wallHangBlockedForCurrentJump = false;
                wallHangBlockedUntilFalling = false;
                hangJumpCornerSnapAssistUntilTime = float.NegativeInfinity;
                if (verticalVelocity < 0f && currentState != MovementState.GroundPound && currentState != MovementState.Dive)
                {
                    verticalVelocity = groundedVerticalForce;
                }
            }
        }
        else
        {
            ClearGroundPlatformReference();
            apexAssistActive = false;
            lastSlopeSpeedFactor = 1f;
            runStopSlideTimer = 0f;
            stopPrimedFromAnimatorSpeed = false;
            stopAnimationTimer = 0f;
        }
    }

    private bool IsSlopeAssistActive()
    {
        if (!isGrounded || !hasGroundNormal || controller == null)
        {
            return false;
        }

        if (groundSlopeAngle <= 0.01f)
        {
            return false;
        }

        return groundSlopeAngle <= controller.slopeLimit + 0.5f;
    }

    private bool TryGetUphillProbeAssistStrength(Vector3 probeNormal, out float assist01)
    {
        assist01 = 0f;
        if (controller == null)
        {
            return false;
        }

        if (currentState != MovementState.Grounded
            && currentState != MovementState.Crouching
            && currentState != MovementState.Falling)
        {
            return false;
        }

        if (currentState == MovementState.SlopeSliding
            || currentState == MovementState.Sliding
            || currentState == MovementState.Rolling
            || currentState == MovementState.Dashing
            || currentState == MovementState.GroundPound
            || currentState == MovementState.Dive
            || wallHangActive
            || ledgeHangActive
            || ledgeClimbActive)
        {
            return false;
        }

        Vector3 normalizedNormal = probeNormal;
        if (normalizedNormal.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        normalizedNormal.Normalize();
        float slopeAngle = Vector3.Angle(normalizedNormal, Vector3.up);
        if (slopeAngle <= 0.01f || slopeAngle > controller.slopeLimit + 0.5f)
        {
            return false;
        }

        Vector3 downhill = CalculateDownhillDirection(normalizedNormal);
        if (downhill.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        downhill.Normalize();
        Vector3 movementDirection = Vector3.ProjectOnPlane(desiredMoveDirection, Vector3.up);
        if (movementDirection.sqrMagnitude <= movementInputDeadZone * movementInputDeadZone)
        {
            movementDirection = Vector3.ProjectOnPlane(planarVelocity, Vector3.up);
        }

        if (movementDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        movementDirection.Normalize();
        float uphillIntent = -Vector3.Dot(movementDirection, downhill);
        if (uphillIntent <= 0.05f)
        {
            return false;
        }

        float slope01 = Mathf.Clamp01(Mathf.InverseLerp(0f, Mathf.Max(1f, controller.slopeLimit), slopeAngle));
        float uphill01 = Mathf.Clamp01(Mathf.InverseLerp(0.05f, 1f, uphillIntent));
        assist01 = Mathf.Clamp01(Mathf.Max(slope01, uphill01));
        return true;
    }

    private bool CanUseMovingPlatformGroundAssist()
    {
        return IsParentedToMovingPlatform()
            && currentState != MovementState.Jumping
            && currentState != MovementState.Falling
            && currentState != MovementState.GroundPound
            && currentState != MovementState.Dive
            && !wallHangActive
            && !ledgeHangActive
            && !ledgeClimbActive;
    }

    private static Vector3 CalculateDownhillDirection(Vector3 normal)
    {
        Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, normal);
        if (downhill.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        return downhill.normalized;
    }

    private float GetCurrentFallSpeed()
    {
        return !isGrounded && verticalVelocity < 0f ? -verticalVelocity : 0f;
    }

    private float GetRequiredHighDropHeightForAirAnimations()
    {
        float singleJumpMaxHeight = Mathf.Max(0.1f, jumpHeight);
        return Mathf.Max(0.5f, singleJumpMaxHeight + 0.05f);
    }

    private bool IsDropHigherThanSingleJumpByRay(Vector3 rayOrigin, int mask)
    {
        float requiredDropHeight = GetRequiredHighDropHeightForAirAnimations();
        return IsDropHigherThanHeightByRay(rayOrigin, mask, requiredDropHeight);
    }

    private bool IsDropHigherThanHeightByRay(Vector3 rayOrigin, int mask, float requiredDropHeight)
    {
        if (controller == null)
        {
            return false;
        }

        float resolvedRequiredHeight = Mathf.Max(0.05f, requiredDropHeight);
        float rayDistance = controller.bounds.extents.y + resolvedRequiredHeight;
        return !TryRaycastIgnoringSelf(
            rayOrigin,
            Vector3.down,
            out _,
            rayDistance,
            mask,
            QueryTriggerInteraction.Ignore);
    }

    private bool IsFallAnimatorStateActive()
    {
        float minimumFallSpeed = Mathf.Max(0f, fallAnimationVerticalSpeedThreshold);
        bool groundedNow = isGrounded || (controller != null && controller.isGrounded);
        if (controller == null || groundedNow)
        {
            return false;
        }

        bool airborneRollReentry = currentState == MovementState.Rolling && rollAirReentryPending;
        bool fallingState = currentState == MovementState.Falling;
        if (!fallingState && !airborneRollReentry)
        {
            return false;
        }

        if (fallingState && verticalVelocity > -minimumFallSpeed)
        {
            return false;
        }

        if (airborneRollReentry)
        {
            // During airborne roll re-entry, Fall must stay active until ground contact.
            return verticalVelocity <= 0.01f;
        }

        if (climbJumpAnimatorActive)
        {
            return false;
        }

        if (forceFallAnimatorFromClimbJump)
        {
            return true;
        }

        bool jumpFallHeightGateActive = jumpFallTrackingActive
            && !float.IsNegativeInfinity(jumpFallStartBottomY);
        if (jumpFallHeightGateActive)
        {
            // After a jump, enable Fall only if the character drops below the jump start height.
            // Fall stays disabled when returning to the same height within tolerance.
            float currentBottomY = controller.bounds.min.y;
            return currentBottomY < jumpFallStartBottomY - JumpFallStartHeightTolerance;
        }

        Bounds controllerBounds = controller.bounds;
        Vector3 highDropRayOrigin = controllerBounds.center + Vector3.up * groundProbeOffset;
        int mask = GetGroundMaskExcludingSelf();
        if (wallHangReleaseHighFallCheckActive)
        {
            float requiredHighDropHeight = Mathf.Max(0.5f, highFallMinHeightForFallAnim);
            if (!IsDropHigherThanHeightByRay(highDropRayOrigin, mask, requiredHighDropHeight))
            {
                return false;
            }

            return !ShouldSuppressFallAnimatorForNearbyLanding();
        }

        if (!IsDropHigherThanSingleJumpByRay(highDropRayOrigin, mask))
        {
            return false;
        }

        return !ShouldSuppressFallAnimatorForNearbyLanding();
    }

    private bool ShouldSuppressFallAnimatorForNearbyLanding()
    {
        if (controller == null)
        {
            return false;
        }

        Bounds controllerBounds = controller.bounds;
        float nearbyLandingDistance = Mathf.Max(
            Mathf.Max(0.08f, maxStepHeight + controller.skinWidth + 0.05f),
            Mathf.Max(0.08f, groundProbeDistance + GroundProbeSnapDistance));
        float castDistance = controllerBounds.extents.y + nearbyLandingDistance;
        float castRadius = Mathf.Clamp(
            Mathf.Min(Mathf.Max(0.05f, groundProbeRadius), controller.radius),
            0.05f,
            controllerBounds.extents.x * 0.95f);
        Vector3 castOrigin = controllerBounds.center + Vector3.up * groundProbeOffset;

        if (!TrySphereCastIgnoringSelf(
                castOrigin,
                castRadius,
                Vector3.down,
                out RaycastHit hit,
                castDistance,
                GetGroundMaskExcludingSelf(),
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        float gapToFeet = Mathf.Max(0f, controllerBounds.min.y - hit.point.y);
        return gapToFeet <= nearbyLandingDistance;
    }

    private void ClampJumpSettings()
    {
        jumpHeight = Mathf.Max(0.1f, jumpHeight);
        minimumJumpHeight = Mathf.Clamp(minimumJumpHeight, 0.05f, jumpHeight);
        maxAirJumps = enableDoubleJump ? Mathf.Max(1, maxAirJumps) : Mathf.Max(0, maxAirJumps);
        jumpCeilingCheckDistance = Mathf.Max(0f, jumpCeilingCheckDistance);
        jumpLandingCooldownDuration = Mathf.Max(0f, jumpLandingCooldownDuration);
        wallJumpProbeDistance = Mathf.Max(0.05f, wallJumpProbeDistance);
        wallJumpBackwardSpeed = Mathf.Max(0f, wallJumpBackwardSpeed);
        wallJumpMaxSurfaceUpDot = Mathf.Clamp01(wallJumpMaxSurfaceUpDot);
        wallHangSlideSpeed = Mathf.Max(0f, wallHangSlideSpeed);
        wallHangSlideEaseInDuration = Mathf.Max(0f, wallHangSlideEaseInDuration);
        wallHangSlideEaseInStartAccelerationScale = Mathf.Clamp01(wallHangSlideEaseInStartAccelerationScale);
        wallHangHorizontalDamping = Mathf.Max(0f, wallHangHorizontalDamping);
        wallHangStickSpeed = Mathf.Max(0f, wallHangStickSpeed);
        wallHangReleaseGroundDistance = Mathf.Max(0f, wallHangReleaseGroundDistance);
        wallHangContactDistance = Mathf.Max(0f, wallHangContactDistance);
        wallHangFacingAngleTolerance = Mathf.Clamp(wallHangFacingAngleTolerance, 0f, 180f);
        wallHangReleaseInputAngle = Mathf.Clamp(wallHangReleaseInputAngle, 0f, 180f);
        slideMaxSteerAngle = Mathf.Clamp(slideMaxSteerAngle, 0f, 45f);
        slopeSlideMaxSteerAngle = Mathf.Clamp(slopeSlideMaxSteerAngle, 0f, 45f);
        slideCameraSteerDrift = Mathf.Max(0f, slideCameraSteerDrift);
        slopeSlideCameraSteerDrift = Mathf.Max(0f, slopeSlideCameraSteerDrift);
        teeterMaxEntrySpeed = Mathf.Max(0f, teeterMaxEntrySpeed);
        teeterProbeForwardDistance = Mathf.Max(0f, teeterProbeForwardDistance);
        teeterEdgeEarlyDetectDistance = Mathf.Max(0f, teeterEdgeEarlyDetectDistance);
        teeterProbeDepth = Mathf.Max(0.05f, teeterProbeDepth);
        teeterSnapBackDistance = Mathf.Max(0f, teeterSnapBackDistance);
        teeterHoldBackSpeed = Mathf.Max(0f, teeterHoldBackSpeed);
        teeterFallInputThreshold = Mathf.Clamp01(teeterFallInputThreshold);
        teeterRecoverInputThreshold = Mathf.Clamp01(teeterRecoverInputThreshold);
        teeterLateralExitInputThreshold = Mathf.Clamp01(teeterLateralExitInputThreshold);
        teeterFacingEdgeAlignmentThreshold = Mathf.Clamp(teeterFacingEdgeAlignmentThreshold, -1f, 1f);
        teeterOpenSpaceRadius = Mathf.Max(0f, teeterOpenSpaceRadius);
        teeterOpenSpaceForwardOffset = Mathf.Max(0f, teeterOpenSpaceForwardOffset);
        teeterOpenSpaceVerticalOffset = Mathf.Max(0f, teeterOpenSpaceVerticalOffset);
        teeterOpenSpaceUpperVerticalOffset = Mathf.Max(0f, teeterOpenSpaceUpperVerticalOffset);
        teeterReentryLockDuration = Mathf.Max(0f, teeterReentryLockDuration);
        teeterAudioCooldown = Mathf.Max(0f, teeterAudioCooldown);
        teeterNoInputEntrySpeedMultiplier = Mathf.Max(1f, teeterNoInputEntrySpeedMultiplier);
        stopAnimationEndNormalizedTime = Mathf.Clamp01(stopAnimationEndNormalizedTime);
        stopAnimationLayerIndex = Mathf.Max(0, stopAnimationLayerIndex);
        leftHandLedgeProbeRadius = Mathf.Max(0.005f, leftHandLedgeProbeRadius);
        rightHandLedgeProbeRadius = Mathf.Max(0.005f, rightHandLedgeProbeRadius);
        ledgeHandContactPadding = Mathf.Max(0f, ledgeHandContactPadding);
        ledgeGrabAssistDistance = Mathf.Max(0f, ledgeGrabAssistDistance);
        ledgeTopProbeUpDistance = Mathf.Max(0.01f, ledgeTopProbeUpDistance);
        ledgeTopProbeForwardInset = Mathf.Max(0f, ledgeTopProbeForwardInset);
        ledgeTopProbeDownDistance = Mathf.Max(0.05f, ledgeTopProbeDownDistance);
        ledgeTopSurfaceMinUpDot = Mathf.Clamp01(ledgeTopSurfaceMinUpDot);
        ledgeTopMaxHorizontalGap = Mathf.Max(0f, ledgeTopMaxHorizontalGap);
        ledgeHangMinEdgeHeight = Mathf.Max(0f, ledgeHangMinEdgeHeight);
        ledgeHangMaxEdgeHeight = Mathf.Max(ledgeHangMinEdgeHeight, ledgeHangMaxEdgeHeight);
        ledgeHangClimbForwardInputThreshold = Mathf.Clamp01(ledgeHangClimbForwardInputThreshold);
        ledgeHangLateralMoveSpeed = Mathf.Max(0f, ledgeHangLateralMoveSpeed);
        ledgeHangLateralInputThreshold = Mathf.Clamp01(ledgeHangLateralInputThreshold);
        ledgeHangCornerStopDistance = Mathf.Max(0f, ledgeHangCornerStopDistance);
        ledgeHangSideAnimationThreshold = Mathf.Clamp01(ledgeHangSideAnimationThreshold);
        ledgeHangHoldBeforeClimbDuration = Mathf.Max(0f, ledgeHangHoldBeforeClimbDuration);
        ledgeHangEntryInputLockDuration = Mathf.Max(0f, ledgeHangEntryInputLockDuration);
        ledgeHangReleaseBackInputThreshold = Mathf.Clamp(ledgeHangReleaseBackInputThreshold, -1f, 0f);
        ledgeHangClimbDuration = Mathf.Max(0.05f, ledgeHangClimbDuration);
        ledgeHangClimbUpDistance = Mathf.Max(0f, ledgeHangClimbUpDistance);
        ledgeHangClimbForwardDistance = Mathf.Max(0f, ledgeHangClimbForwardDistance);
        ledgeClimbStepForwardBonus = Mathf.Max(0f, ledgeClimbStepForwardBonus);
        ledgeHangWallProximityOffset = Mathf.Clamp(ledgeHangWallProximityOffset, -0.2f, 0.2f);
        ledgeHangHandSurfaceTargetOffset = Mathf.Clamp(ledgeHangHandSurfaceTargetOffset, -0.05f, 0.05f);
        ledgeHangHandMidpointVerticalBias = Mathf.Clamp(ledgeHangHandMidpointVerticalBias, -1f, 1f);
        ledgeHangVerticalCalibrationOffset = Mathf.Clamp(ledgeHangVerticalCalibrationOffset, -1f, 1f);
        ledgeHangDualHandSurfaceTolerance = Mathf.Max(0f, ledgeHangDualHandSurfaceTolerance);
        ledgeHangDualHandMaxExtraPush = Mathf.Max(0f, ledgeHangDualHandMaxExtraPush);
        ledgeHangAnchorOutwardOffset = Mathf.Max(0f, ledgeHangAnchorOutwardOffset);
        ledgeHangAnchorAlignSpeed = Mathf.Max(0f, ledgeHangAnchorAlignSpeed);
        ledgeHangAnchorAlignTolerance = Mathf.Max(0f, ledgeHangAnchorAlignTolerance);
        wallPostClimbDetachLockTime = Mathf.Max(0f, wallPostClimbDetachLockTime);
        ledgeHangContactLostGraceTime = Mathf.Max(0f, ledgeHangContactLostGraceTime);
        ledgeHangRegrabLockTime = Mathf.Max(0f, ledgeHangRegrabLockTime);
        ledgeHangCornerTransitionAssistDistance = Mathf.Max(0f, ledgeHangCornerTransitionAssistDistance);
        ledgeHangCornerProbeLateralOffset = Mathf.Max(0f, ledgeHangCornerProbeLateralOffset);
        ledgeHangMaxEntrySnapDistance = Mathf.Max(0.05f, ledgeHangMaxEntrySnapDistance);
        ledgeHangMaxEntryHorizontalDistance = Mathf.Max(0.05f, ledgeHangMaxEntryHorizontalDistance);
        ledgeHangMaxEntryVerticalDistance = Mathf.Max(0.05f, ledgeHangMaxEntryVerticalDistance);
        ledgeHangMaxHandAboveEdge = Mathf.Max(0f, ledgeHangMaxHandAboveEdge);
        ledgeHangMaxEntryWallDistance = Mathf.Max(0.05f, ledgeHangMaxEntryWallDistance);
        groundPoundHardBounceBackSpeed = Mathf.Max(0f, groundPoundHardBounceBackSpeed);
        groundPoundHardBounceUpVelocity = Mathf.Max(0f, groundPoundHardBounceUpVelocity);
        groundPoundHardBounceUpImpactBonus = Mathf.Max(0f, groundPoundHardBounceUpImpactBonus);
        groundPoundHardLandHoldTime = Mathf.Max(0f, groundPoundHardLandHoldTime);
        movingPlatformRotationInfluence = Mathf.Clamp01(movingPlatformRotationInfluence);
        movingPlatformMaxDeltaPerFrame = Mathf.Max(0f, movingPlatformMaxDeltaPerFrame);
        stopAnimationHoldTime = Mathf.Max(0.01f, stopAnimationHoldTime);
        stopArmAnimatorSpeedThreshold = Mathf.Clamp01(stopArmAnimatorSpeedThreshold);
        stopTriggerAnimatorSpeedThreshold = Mathf.Clamp(stopTriggerAnimatorSpeedThreshold, 0f, stopArmAnimatorSpeedThreshold);
        stopSprintPrimeMinPlanarSpeed = Mathf.Max(0f, stopSprintPrimeMinPlanarSpeed);
        stopSprintReleaseTriggerSpeed01 = Mathf.Clamp01(stopSprintReleaseTriggerSpeed01);
        highFallMinHeightForFallAnim = Mathf.Max(0f, highFallMinHeightForFallAnim);
        highFallDoubleJumpHeightTolerance = Mathf.Clamp(highFallDoubleJumpHeightTolerance, 0f, 0.2f);
        highFallMinAirTimeForRoll = Mathf.Max(0f, highFallMinAirTimeForRoll);
        rollLandingResumeDurationRatio = Mathf.Clamp(rollLandingResumeDurationRatio, 0.25f, 1f);
        rollLandingResumeMaxDuration = Mathf.Max(0.1f, rollLandingResumeMaxDuration);
        minimumStandClearanceHeight = Mathf.Max(0.1f, minimumStandClearanceHeight);
        rollCameraPivotReleaseDuration = Mathf.Max(0f, rollCameraPivotReleaseDuration);
        rollCameraPivotReleaseSharpness = Mathf.Max(0f, rollCameraPivotReleaseSharpness);
        rollCameraPivotStabilizationDeadZone = Mathf.Max(0f, rollCameraPivotStabilizationDeadZone);
        rollCameraPivotStabilizationSharpness = Mathf.Max(0f, rollCameraPivotStabilizationSharpness);
        rollCameraPivotStabilizationMaxLag = Mathf.Max(0f, rollCameraPivotStabilizationMaxLag);
        lowClearanceCameraPivotSharpness = Mathf.Max(0f, lowClearanceCameraPivotSharpness);
        lowClearanceCameraBlendSpeed = Mathf.Max(0f, lowClearanceCameraBlendSpeed);
        idleJumpRollDirectionLockSpeedThreshold = Mathf.Max(0f, idleJumpRollDirectionLockSpeedThreshold);
        crouchWalkSpeedMultiplier = Mathf.Clamp(crouchWalkSpeedMultiplier, 0.05f, 1f);
        fallAnimationVerticalSpeedThreshold = Mathf.Max(0f, fallAnimationVerticalSpeedThreshold);
        gamepadSprintDeadZone = Mathf.Clamp01(gamepadSprintDeadZone);
        gamepadMoveDeadZone = Mathf.Clamp01(gamepadMoveDeadZone);
        gamepadLookDeadZone = Mathf.Clamp01(gamepadLookDeadZone);
        gamepadLookSensitivity = Mathf.Max(0f, gamepadLookSensitivity);
        jumpStretch = Mathf.Max(0f, jumpStretch);
        jumpStretchDecaySpeed = Mathf.Max(0f, jumpStretchDecaySpeed);
        jumpBlobHorizontalCompression = Mathf.Max(0f, jumpBlobHorizontalCompression);
        jumpBlobVerticalStretch = Mathf.Max(0f, jumpBlobVerticalStretch);
        jumpBlobVerticalVelocityInfluence = Mathf.Max(0f, jumpBlobVerticalVelocityInfluence);
        animSpeedLinearRateScale = Mathf.Clamp(animSpeedLinearRateScale, 0.05f, 1f);
        dashCameraSteerSharpness = Mathf.Max(0f, dashCameraSteerSharpness);
        dashGhostSpawnInterval = Mathf.Max(0.005f, dashGhostSpawnInterval);
        dashGhostDensity = Mathf.Clamp(dashGhostDensity, 0.1f, 2f);
        dashGhostMaxActiveCount = Mathf.Max(1, dashGhostMaxActiveCount);
        dashGhostLifetime = Mathf.Max(0.01f, dashGhostLifetime);
        dashGhostStartOpacity = Mathf.Clamp01(dashGhostStartOpacity);
        dashGhostScale = Mathf.Max(0.01f, dashGhostScale);
        dashGhostBackOffset = Mathf.Max(0f, dashGhostBackOffset);
        dashGhostMinPlanarSpeed = Mathf.Max(0f, dashGhostMinPlanarSpeed);
        groundPoundGhostMinFallSpeed = Mathf.Max(0f, groundPoundGhostMinFallSpeed);
    }

    private float CalculateJumpVelocity(float targetJumpHeight)
    {
        float safeGravity = Mathf.Min(gravity, -0.01f);
        float resolvedHeight = Mathf.Max(0.05f, targetJumpHeight);
        return Mathf.Sqrt(resolvedHeight * -2f * safeGravity);
    }

    private float ResolveMinimumJumpCutVelocity(float launchedJumpHeight)
    {
        float resolvedMinimumJumpHeight = Mathf.Clamp(minimumJumpHeight, 0.05f, Mathf.Max(0.05f, launchedJumpHeight));
        return CalculateJumpVelocity(resolvedMinimumJumpHeight);
    }

    private float ResolveRequiredJumpHeadroom()
    {
        return Mathf.Max(Mathf.Max(0f, jumpCeilingCheckDistance), Mathf.Max(0.05f, minimumJumpHeight));
    }

    private bool TryGetJumpHeadroomInfo(out float clearance, out Vector3 headPoint, out Vector3 ceilingPoint, out float requiredHeadroom)
    {
        clearance = float.PositiveInfinity;
        headPoint = transform.position;
        ceilingPoint = transform.position;
        requiredHeadroom = ResolveRequiredJumpHeadroom();

        if (controller == null)
        {
            return false;
        }

        float checkDistance = Mathf.Max(0f, requiredHeadroom);
        Vector3 worldCenter = transform.TransformPoint(controller.center);
        float radius = Mathf.Max(0.05f, controller.radius - 0.01f);
        float halfHeight = Mathf.Max(controller.height * 0.5f, radius);
        Vector3 topSphereCenter = worldCenter + Vector3.up * (halfHeight - radius);
        headPoint = topSphereCenter + Vector3.up * radius;
        ceilingPoint = headPoint + Vector3.up * checkDistance;

        if (checkDistance <= 0.0001f)
        {
            return false;
        }

        int mask = jumpHeadroomMask.value & ~(1 << gameObject.layer);
        if (!TrySphereCastIgnoringSelf(topSphereCenter, radius, Vector3.up, out RaycastHit hit, checkDistance, mask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        clearance = Mathf.Max(0f, hit.distance);
        ceilingPoint = hit.point;
        return true;
    }

    private bool HasJumpHeadroom()
    {
        if (controller == null)
        {
            return true;
        }

        if (!CanStandUp())
        {
            return false;
        }

        float requiredHeadroom = ResolveRequiredJumpHeadroom();
        if (requiredHeadroom <= 0.0001f)
        {
            return true;
        }

        return !TryGetJumpHeadroomInfo(out float clearance, out _, out _, out _)
            || clearance + 0.001f >= requiredHeadroom;
    }

    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        if (cameraTransform == null)
        {
            return new Vector3(input.x, 0f, input.y);
        }

        Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 cameraRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        Vector3 direction = cameraForward * input.y + cameraRight * input.x;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero;
    }

    private void UpdateCharacterFacing(float deltaTime)
    {
        bool hasDirectionalInput = desiredMoveDirection.sqrMagnitude > movementInputDeadZone * movementInputDeadZone;
        if ((currentState == MovementState.GroundPound || groundPoundHardLandActive || groundPoundHardFallActive)
            && !groundPoundStartedFromMovement)
        {
            return;
        }

        if (wallJumpNoRotateActive && !isGrounded)
        {
            return;
        }

        if (lockFacingUntilGroundInputAfterWallJump)
        {
            if (currentState == MovementState.Rolling)
            {
                lockFacingUntilGroundInputAfterWallJump = false;
            }
        }

        if (lockFacingUntilGroundInputAfterWallJump)
        {
            if (!isGrounded)
            {
                // Keep the lock active while airborne; it prevents sudden angle changes on landing.
                return;
            }
            else if (!hasDirectionalInput)
            {
                return;
            }
            else
            {
                lockFacingUntilGroundInputAfterWallJump = false;
            }
        }

        Vector3 lookDirection = planarVelocity;
        if (currentState == MovementState.Dashing)
        {
            lookDirection = dashDirection * dashSpeed;
        }
        else if (currentState == MovementState.Dive)
        {
            lookDirection = diveDirection * planarVelocity.magnitude;
        }
        else if (currentState == MovementState.Rolling)
        {
            lookDirection = rollDirection * planarVelocity.magnitude;
        }
        else if (currentState == MovementState.Teetering)
        {
            lookDirection = teeterFacingDirection.sqrMagnitude > 0.001f
                ? teeterFacingDirection
                : transform.forward;
        }
        else if (currentState == MovementState.SlopeSliding)
        {
            lookDirection = slideDirection * planarVelocity.magnitude;
        }
        else if (currentState == MovementState.Sliding)
        {
            lookDirection = slideDirection * planarVelocity.magnitude;
        }
        else if (hasDirectionalInput)
        {
            // On ground, reading look direction from input decouples it from movement and speeds up pivoting.
            lookDirection = desiredMoveDirection;
        }

        if (lookDirection.sqrMagnitude <= facingSpeedThreshold * facingSpeedThreshold)
        {
            return;
        }

        lookDirection = Vector3.ProjectOnPlane(lookDirection, Vector3.up);
        if (lookDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float sharpness = isGrounded ? rotationSharpness : rotationSharpness * airRotationMultiplier;
        if (isGrounded && hasDirectionalInput && Vector3.Dot(transform.forward, desiredMoveDirection) < reversePivotThreshold)
        {
            sharpness *= snappyReverseTurnMultiplier;
        }

        float rotateT = 1f - Mathf.Exp(-sharpness * deltaTime);
        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotateT);
    }

    private void InitializeVisualRoot()
    {
        if (!enableCharacterFeel)
        {
            return;
        }

        runtimeVisualRoot = visualRoot;
        if (runtimeVisualRoot == null)
        {
            Transform existingVisual = transform.Find("CharacterVisual");
            if (existingVisual == null)
            {
                existingVisual = FindNestedReusableCharacterVisualRoot(transform, transform);
            }

            if (existingVisual != null)
            {
                runtimeVisualRoot = existingVisual;
            }
        }

        if (runtimeVisualRoot == null)
        {
            runtimeVisualRoot = ResolveReusableSceneCharacterVisualRoot();
            if (runtimeVisualRoot != null)
            {
                runtimeVisualRoot.SetParent(transform, false);
                runtimeVisualRoot.name = "CharacterVisual";

                Animator sceneAnimator = runtimeVisualRoot.GetComponentInChildren<Animator>(true);
                if (sceneAnimator != null)
                {
                    ConfigureAnimator(sceneAnimator, true);
                    sceneAnimator.applyRootMotion = false;
                    sceneAnimator.updateMode = AnimatorUpdateMode.Normal;
                    sceneAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    runtimeAnimatorPlaybackSpeed = Mathf.Max(0.01f, animatorNormalPlaybackSpeed);
                    sceneAnimator.speed = runtimeAnimatorPlaybackSpeed;
                }
            }
        }

        if (runtimeVisualRoot == null)
        {
            if (enableCharacterFeel)
            {
                MeshFilter sourceFilter = GetComponent<MeshFilter>();
                MeshRenderer sourceRenderer = GetComponent<MeshRenderer>();
                if (sourceFilter != null && sourceRenderer != null && sourceFilter.sharedMesh != null)
                {
                    GameObject visualObject = new GameObject("CharacterVisual");
                    visualObject.transform.SetParent(transform, false);
                    MeshFilter visualFilter = visualObject.AddComponent<MeshFilter>();
                    visualFilter.sharedMesh = sourceFilter.sharedMesh;
                    MeshRenderer visualRenderer = visualObject.AddComponent<MeshRenderer>();
                    visualRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
                    sourceRenderer.enabled = false;
                    runtimeVisualRoot = visualObject.transform;
                }
            }
        }

        if (runtimeVisualRoot == null || runtimeVisualRoot == transform)
        {
            enableCharacterFeel = false;
            return;
        }

        visualBaseLocalPosition = LockedVisualRootLocalPosition;
        runtimeVisualRoot.localPosition = ResolveVisualRootLocalPositionWithGroundSink();
        visualBaseScale = runtimeVisualRoot.localScale;
        visualBaseRotation = runtimeVisualRoot.localRotation;
    }

    private static Transform FindNestedReusableCharacterVisualRoot(Transform current, Transform characterRoot)
    {
        if (current == null || characterRoot == null)
        {
            return null;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            Transform child = current.GetChild(i);
            if (string.Equals(child.name, "CharacterVisual", System.StringComparison.OrdinalIgnoreCase)
                && IsReusableSceneCharacterVisualRoot(child, characterRoot))
            {
                return child;
            }

            Transform nested = FindNestedReusableCharacterVisualRoot(child, characterRoot);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private Transform ResolveReusableSceneCharacterVisualRoot()
    {
        UnityEngine.SceneManagement.Scene scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null)
            {
                continue;
            }

            Transform match = FindReusableSceneCharacterVisualRoot(root.transform, transform);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static Transform FindReusableSceneCharacterVisualRoot(Transform current, Transform characterRoot)
    {
        if (current == null || characterRoot == null)
        {
            return null;
        }

        if (current == characterRoot || current.IsChildOf(characterRoot))
        {
            return null;
        }

        if (string.Equals(current.name, "CharacterVisual", System.StringComparison.OrdinalIgnoreCase)
            && IsReusableSceneCharacterVisualRoot(current, characterRoot))
        {
            return current;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            Transform match = FindReusableSceneCharacterVisualRoot(current.GetChild(i), characterRoot);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static bool IsReusableSceneCharacterVisualRoot(Transform candidate, Transform characterRoot)
    {
        CharacterController owningController = candidate.GetComponentInParent<CharacterController>();
        if (owningController != null
            && owningController.transform != characterRoot
            && !owningController.transform.IsChildOf(candidate))
        {
            return false;
        }

        MinimoTpsController owningTpsController = candidate.GetComponentInParent<MinimoTpsController>();
        if (owningTpsController != null
            && owningTpsController.transform != characterRoot
            && !owningTpsController.transform.IsChildOf(candidate))
        {
            return false;
        }

        return candidate.GetComponentInChildren<Animator>(true) != null
            || candidate.GetComponentInChildren<Renderer>(true) != null
            || candidate.GetComponentInChildren<MinimoCharacterCustomizer>(true) != null;
    }

    private void ForceLockVisualRootLocalPosition()
    {
        Transform targetVisual = runtimeVisualRoot;
        if (targetVisual == null)
        {
            targetVisual = visualRoot;
            if (targetVisual == null)
            {
                targetVisual = transform.Find("CharacterVisual");
            }

            if (targetVisual != null && targetVisual != transform)
            {
                runtimeVisualRoot = targetVisual;
            }
        }

        if (targetVisual == null || targetVisual == transform)
        {
            return;
        }

        visualBaseLocalPosition = LockedVisualRootLocalPosition;
        targetVisual.localPosition = ResolveVisualRootLocalPositionWithGroundSink();
    }

    private void UpdateCharacterVisuals(float deltaTime, float verticalBeforeMove)
    {
        if (!enableCharacterFeel)
        {
            return;
        }

        if (runtimeVisualRoot == null)
        {
            InitializeVisualRoot();
            if (runtimeVisualRoot == null)
            {
                return;
            }
        }

        landSquashAmount = Mathf.MoveTowards(landSquashAmount, 0f, landRecoverSpeed * deltaTime);
        jumpStretchPulse = Mathf.MoveTowards(jumpStretchPulse, 0f, jumpStretchDecaySpeed * deltaTime);
        dashBlobPulseValue = Mathf.MoveTowards(dashBlobPulseValue, 0f, dashBlobDecaySpeed * deltaTime);

        float speed01 = Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(0.01f, moveSpeed));
        Vector3 localVelocity = transform.InverseTransformDirection(planarVelocity);
        Vector3 localAcceleration = transform.InverseTransformDirection(planarAcceleration);
        _ = maxForwardLean;
        _ = accelerationLean;
        _ = airForwardLeanMultiplier;

        float sideLean = Mathf.Clamp(localVelocity.x / Mathf.Max(0.01f, moveSpeed), -1f, 1f) * maxSideLean;
        sideLean -= Mathf.Clamp(localAcceleration.x / Mathf.Max(0.01f, moveSpeed * 4f), -1f, 1f) * turnLean;

        if (!isGrounded)
        {
            sideLean *= airSideLeanMultiplier;
        }

        // Disable forward-backward script pitch and keep only lateral lean.
        Quaternion targetRotation = visualBaseRotation * Quaternion.Euler(0f, 0f, -sideLean);

        float directionalStretch = speed01 * moveStretch;
        float accelStretch = Mathf.Clamp(localAcceleration.z / Mathf.Max(0.01f, moveSpeed * 4f), -1f, 1f) * accelerationStretch;
        float brakeAmount = Mathf.Clamp(-localAcceleration.z / Mathf.Max(0.01f, moveSpeed * 4f), 0f, 1f) * brakeSquash;
        Vector3 targetScale = visualBaseScale + new Vector3(
            -directionalStretch * 0.45f + brakeAmount * 0.15f,
            -directionalStretch * 0.2f + accelStretch - brakeAmount * 0.45f,
            directionalStretch * 0.65f - accelStretch * 0.2f);

        if (enableDashBlobEffect && dashBlobPulseValue > 0f)
        {
            float dashAmount = dashBlobPulseValue * (currentState == MovementState.Dashing ? 1f : 0.72f);
            targetScale += new Vector3(
                -dashAmount * dashBlobHorizontalCompression,
                -dashAmount * dashBlobVerticalCompression,
                dashAmount * dashBlobForwardStretch);
        }

        if (!isGrounded)
        {
            float jumpAmount = 0f;
            float fallAmount = 0f;
            bool inJump = currentState == MovementState.Jumping && currentState != MovementState.GroundPound;
            if (enableJumpBlobEffect && inJump && verticalBeforeMove >= 0f)
            {
                float upward01 = Mathf.Clamp01(verticalBeforeMove / 10f);
                jumpAmount = jumpStretchPulse * jumpStretch + upward01 * jumpStretch * jumpBlobVerticalVelocityInfluence;
            }
            else
            {
                float fall01 = Mathf.Clamp01(-verticalBeforeMove / Mathf.Max(0.01f, fallShapeMaxSpeed));
                if (currentState == MovementState.GroundPound)
                {
                    fall01 = 1f;
                }

                fallAmount = fall01 * fallShapeAmount;
            }

            targetScale += new Vector3(
                -jumpAmount * jumpBlobHorizontalCompression + fallAmount * 0.42f,
                jumpAmount * jumpBlobVerticalStretch - fallAmount * 0.85f,
                -jumpAmount * jumpBlobHorizontalCompression + fallAmount * 0.42f);
        }
        else
        {
            targetScale += new Vector3(
                landSquashAmount * 0.45f,
                -landSquashAmount,
                landSquashAmount * 0.45f);
        }

        if (currentState == MovementState.Crouching
            || currentState == MovementState.Sliding
            || currentState == MovementState.SlopeSliding
            || currentState == MovementState.Rolling)
        {
            targetScale += new Vector3(0.03f, -0.08f, 0.03f);
        }

        targetScale.x = Mathf.Max(visualBaseScale.x * minVisualScaleRatio.x, targetScale.x);
        targetScale.y = Mathf.Max(visualBaseScale.y * minVisualScaleRatio.y, targetScale.y);
        targetScale.z = Mathf.Max(visualBaseScale.z * minVisualScaleRatio.z, targetScale.z);

        float blend = 1f - Mathf.Exp(-visualResponse * deltaTime);
        runtimeVisualRoot.localRotation = Quaternion.Slerp(runtimeVisualRoot.localRotation, targetRotation, blend);
        runtimeVisualRoot.localScale = Vector3.Lerp(runtimeVisualRoot.localScale, targetScale, blend);

        // The visual child local position is updated with controlled sink during runtime.
        runtimeVisualRoot.localPosition = ResolveVisualRootLocalPositionWithGroundSink();
    }

    private Vector3 ResolveVisualRootLocalPositionWithGroundSink()
    {
        Vector3 position = visualBaseLocalPosition;
        if (!isGrounded || ragdollRuntimeActive)
        {
            return position;
        }

        float visualSink = Mathf.Max(0f, groundedVisualSink);
        if (currentState == MovementState.Crouching
            || currentState == MovementState.Sliding
            || currentState == MovementState.SlopeSliding
            || currentState == MovementState.Rolling
            || currentState == MovementState.GroundPound)
        {
            visualSink += Mathf.Max(0f, crouchVisualSinkBonus);
        }

        return position + Vector3.down * visualSink;
    }


    private void UpdateDashGhostTrail(float deltaTime)
    {
        UpdateDashGhostInstances();

        if (!enableDashGhostTrail)
        {
            return;
        }

        if (currentState == MovementState.Rolling)
        {
            // Never create ghost visuals during roll.
            ClearDashGhostInstances();
            dashGhostSpawnTimer = 0f;
            return;
        }

        bool isDashState = currentState == MovementState.Dashing;
        bool isGroundPoundState = currentState == MovementState.GroundPound && enableGroundPoundGhostTrail;
        bool isSlideState = enableSlideGhostTrail
            && (currentState == MovementState.Sliding || currentState == MovementState.SlopeSliding);
        if (!isDashState && !isGroundPoundState && !isSlideState)
        {
            return;
        }

        float minSpeed = Mathf.Max(0f, dashGhostMinPlanarSpeed);
        bool hasEnoughSpeed = planarVelocity.sqrMagnitude >= minSpeed * minSpeed;
        if (isGroundPoundState)
        {
            hasEnoughSpeed = hasEnoughSpeed || -verticalVelocity >= Mathf.Max(0f, groundPoundGhostMinFallSpeed);
        }

        if (!hasEnoughSpeed)
        {
            return;
        }

        Vector3 trailDirection = ResolveGhostTrailDirectionForState(isGroundPoundState, isSlideState);
        dashGhostSpawnTimer -= deltaTime;
        float density = Mathf.Clamp(dashGhostDensity, 0.1f, 2f);
        float spawnInterval = Mathf.Max(0.005f, dashGhostSpawnInterval / density);
        while (dashGhostSpawnTimer <= 0f)
        {
            SpawnDashGhostSnapshot(trailDirection);
            dashGhostSpawnTimer += spawnInterval;
        }
    }

    private void UpdateDashGhostInstances()
    {
        if (activeDashGhosts.Count <= 0)
        {
            return;
        }

        float now = Time.time;
        for (int i = activeDashGhosts.Count - 1; i >= 0; i--)
        {
            DashGhostInstance ghost = activeDashGhosts[i];
            if (ghost == null || ghost.root == null)
            {
                activeDashGhosts.RemoveAt(i);
                continue;
            }

            float safeLifetime = Mathf.Max(0.01f, ghost.lifetime);
            float age = now - ghost.startTime;
            if (age >= safeLifetime)
            {
                ReleaseDashGhostInstance(ghost);
                activeDashGhosts.RemoveAt(i);
                continue;
            }

            float remaining01 = 1f - Mathf.Clamp01(age / safeLifetime);
            float opacity = ghost.startOpacity * remaining01;
            ApplyDashGhostOpacity(ghost, opacity);
        }
    }

    private Vector3 ResolveGhostTrailDirectionForState(bool isGroundPoundState, bool isSlideState = false)
    {
        if (isSlideState)
        {
            Vector3 slidePlanarDirection = Vector3.ProjectOnPlane(slideDirection, Vector3.up);
            if (slidePlanarDirection.sqrMagnitude > 0.0001f)
            {
                return slidePlanarDirection.normalized;
            }

            Vector3 velocityPlanarDirection = Vector3.ProjectOnPlane(planarVelocity, Vector3.up);
            if (velocityPlanarDirection.sqrMagnitude > 0.0001f)
            {
                return velocityPlanarDirection.normalized;
            }

            Vector3 facingPlanarDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (facingPlanarDirection.sqrMagnitude > 0.0001f)
            {
                return facingPlanarDirection.normalized;
            }

            return dashDirection;
        }

        if (!isGroundPoundState)
        {
            return dashDirection;
        }

        Vector3 planarDirection = Vector3.ProjectOnPlane(planarVelocity, Vector3.up);
        if (planarDirection.sqrMagnitude > 0.0001f)
        {
            return planarDirection.normalized;
        }

        Vector3 facingDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (facingDirection.sqrMagnitude > 0.0001f)
        {
            return facingDirection.normalized;
        }

        return dashDirection;
    }

    private void SpawnDashGhostSnapshot()
    {
        SpawnDashGhostSnapshot(dashDirection);
    }

    private void SpawnDashGhostSnapshot(Vector3 trailDirection)
    {
        if (!enableDashGhostTrail || dashGhostLifetime <= 0f)
        {
            return;
        }

        Transform sourceRoot = runtimeVisualRoot != null ? runtimeVisualRoot : transform;
        if (sourceRoot == null)
        {
            return;
        }

        if (!EnsureDashGhostPoolInitialized(sourceRoot))
        {
            return;
        }

        Vector3 backOffset = Vector3.zero;
        if (dashGhostBackOffset > 0f && trailDirection.sqrMagnitude > 0.0001f)
        {
            backOffset = -trailDirection.normalized * dashGhostBackOffset;
        }

        DashGhostInstance ghost = AcquireDashGhostInstanceFromPool();
        if (ghost == null || ghost.root == null)
        {
            return;
        }

        if (!PrepareDashGhostInstance(ghost, backOffset))
        {
            // Runtime'da visual cocuklar degismisse source cache'i bir kez yenileyip tekrar dene.
            RebuildDashGhostPool(sourceRoot);
            ghost = AcquireDashGhostInstanceFromPool();
            if (ghost == null || ghost.root == null || !PrepareDashGhostInstance(ghost, backOffset))
            {
                ReleaseDashGhostInstance(ghost);
                return;
            }
        }

        ghost.startTime = Time.time;
        ghost.lifetime = Mathf.Max(0.01f, dashGhostLifetime);
        ghost.startOpacity = Mathf.Clamp01(dashGhostStartOpacity);
        activeDashGhosts.Add(ghost);
        ApplyDashGhostOpacity(ghost, ghost.startOpacity);
        TrimDashGhostCount();
    }

    private bool EnsureDashGhostPoolInitialized(Transform sourceRoot)
    {
        if (sourceRoot == null)
        {
            return false;
        }

        bool sourceChanged = dashGhostPoolSourceRoot != sourceRoot;
        bool poolInvalid = IsDashGhostPoolInvalid();
        if (sourceChanged || poolInvalid)
        {
            RebuildDashGhostPool(sourceRoot);
        }
        else
        {
            EnsureDashGhostPoolCapacity();
        }

        return dashGhostPool.Count > 0 && dashGhostSourceEntries.Length > 0;
    }

    private bool IsDashGhostPoolInvalid()
    {
        if (dashGhostPoolRoot == null || dashGhostPool.Count <= 0 || dashGhostSourceEntries.Length <= 0)
        {
            return true;
        }

        for (int i = 0; i < dashGhostPool.Count; i++)
        {
            DashGhostInstance ghost = dashGhostPool[i];
            if (ghost == null || ghost.root == null || ghost.pieces == null || ghost.renderers == null)
            {
                return true;
            }
        }

        return false;
    }

    private void RebuildDashGhostPool(Transform sourceRoot)
    {
        DestroyDashGhostPool();
        dashGhostPoolSourceRoot = sourceRoot;
        CacheDashGhostSourceEntries(sourceRoot);
        if (dashGhostSourceEntries.Length <= 0)
        {
            return;
        }

        EnsureDashGhostPoolCapacity();
    }

    private void CacheDashGhostSourceEntries(Transform sourceRoot)
    {
        if (sourceRoot == null)
        {
            dashGhostSourceEntries = Array.Empty<DashGhostSourceEntry>();
            return;
        }

        List<DashGhostSourceEntry> entries = new List<DashGhostSourceEntry>();
        MeshRenderer[] meshRenderers = sourceRoot.GetComponentsInChildren<MeshRenderer>(false);
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            MeshRenderer sourceRenderer = meshRenderers[i];
            if (sourceRenderer == null)
            {
                continue;
            }

            MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
            {
                continue;
            }

            entries.Add(new DashGhostSourceEntry
            {
                sourceRenderer = sourceRenderer,
                sourceMeshFilter = sourceFilter,
                sourceSkinnedRenderer = null,
                isSkinned = false
            });
        }

        SkinnedMeshRenderer[] skinnedRenderers = sourceRoot.GetComponentsInChildren<SkinnedMeshRenderer>(false);
        for (int i = 0; i < skinnedRenderers.Length; i++)
        {
            SkinnedMeshRenderer sourceRenderer = skinnedRenderers[i];
            if (sourceRenderer == null)
            {
                continue;
            }

            entries.Add(new DashGhostSourceEntry
            {
                sourceRenderer = sourceRenderer,
                sourceMeshFilter = null,
                sourceSkinnedRenderer = sourceRenderer,
                isSkinned = true
            });
        }

        dashGhostSourceEntries = entries.Count > 0 ? entries.ToArray() : Array.Empty<DashGhostSourceEntry>();
    }

    private Transform GetOrCreateDashGhostPoolRoot()
    {
        if (dashGhostPoolRoot != null)
        {
            return dashGhostPoolRoot;
        }

        GameObject poolRoot = new GameObject("DashGhostPool");
        poolRoot.layer = gameObject.layer;
        poolRoot.hideFlags = DashGhostHideFlags;
        dashGhostPoolRoot = poolRoot.transform;
        return dashGhostPoolRoot;
    }

    private void EnsureDashGhostPoolCapacity()
    {
        if (dashGhostSourceEntries.Length <= 0)
        {
            return;
        }

        int requiredCount = Mathf.Max(1, dashGhostMaxActiveCount + 1);
        Transform poolRoot = GetOrCreateDashGhostPoolRoot();
        while (dashGhostPool.Count < requiredCount)
        {
            DashGhostInstance ghost = CreateDashGhostPooledInstance(poolRoot);
            if (ghost == null || ghost.root == null)
            {
                break;
            }

            dashGhostPool.Add(ghost);
        }
    }

    private DashGhostInstance CreateDashGhostPooledInstance(Transform poolRoot)
    {
        if (poolRoot == null || dashGhostSourceEntries.Length <= 0)
        {
            return null;
        }

        GameObject ghostRoot = new GameObject("DashGhost");
        ghostRoot.layer = gameObject.layer;
        ghostRoot.hideFlags = DashGhostHideFlags;
        ghostRoot.transform.SetParent(poolRoot, false);

        List<DashGhostPiece> pieces = new List<DashGhostPiece>(dashGhostSourceEntries.Length);
        List<Renderer> renderers = new List<Renderer>(dashGhostSourceEntries.Length);
        for (int i = 0; i < dashGhostSourceEntries.Length; i++)
        {
            DashGhostSourceEntry sourceEntry = dashGhostSourceEntries[i];
            if (sourceEntry == null)
            {
                continue;
            }

            GameObject piece = new GameObject(sourceEntry.isSkinned ? "SkinnedGhost" : "MeshGhost");
            piece.layer = gameObject.layer;
            piece.hideFlags = DashGhostHideFlags;
            piece.transform.SetParent(ghostRoot.transform, false);

            MeshFilter pieceFilter = piece.AddComponent<MeshFilter>();
            MeshRenderer pieceRenderer = piece.AddComponent<MeshRenderer>();
            Material[] sourceMaterials = sourceEntry.sourceRenderer != null
                ? sourceEntry.sourceRenderer.sharedMaterials
                : Array.Empty<Material>();
            ApplyDashGhostRendererSettings(pieceRenderer, sourceMaterials);
            pieceRenderer.enabled = false;

            DashGhostPiece ghostPiece = new DashGhostPiece()
            {
                transform = piece.transform,
                meshFilter = pieceFilter,
                meshRenderer = pieceRenderer,
                bakedMesh = null,
                sourceEntry = sourceEntry,
                materialCount = ResolveDashGhostMaterialCount(sourceMaterials),
                sourceMaterialCount = sourceMaterials != null ? sourceMaterials.Length : 0
            };

            if (sourceEntry.isSkinned)
            {
                Mesh bakedMesh = new Mesh()
                {
                    name = "DashGhostBakedMesh"
                };
                bakedMesh.MarkDynamic();
                ghostPiece.bakedMesh = bakedMesh;
                pieceFilter.sharedMesh = bakedMesh;
            }
            else if (sourceEntry.sourceMeshFilter != null)
            {
                pieceFilter.sharedMesh = sourceEntry.sourceMeshFilter.sharedMesh;
            }

            pieces.Add(ghostPiece);
            renderers.Add(pieceRenderer);
        }

        ghostRoot.SetActive(false);
        return new DashGhostInstance
        {
            root = ghostRoot,
            pieces = pieces.ToArray(),
            renderers = renderers.ToArray(),
            startTime = 0f,
            lifetime = Mathf.Max(0.01f, dashGhostLifetime),
            startOpacity = Mathf.Clamp01(dashGhostStartOpacity)
        };
    }

    private DashGhostInstance AcquireDashGhostInstanceFromPool()
    {
        EnsureDashGhostPoolCapacity();
        int poolCount = dashGhostPool.Count;
        if (poolCount <= 0)
        {
            return null;
        }

        for (int i = 0; i < poolCount; i++)
        {
            int index = (nextDashGhostPoolIndex + i) % poolCount;
            DashGhostInstance candidate = dashGhostPool[index];
            if (candidate == null || candidate.root == null || candidate.root.activeSelf)
            {
                continue;
            }

            nextDashGhostPoolIndex = (index + 1) % poolCount;
            return candidate;
        }

        if (activeDashGhosts.Count > 0)
        {
            DashGhostInstance recycled = activeDashGhosts[0];
            ReleaseDashGhostInstance(recycled);
            activeDashGhosts.RemoveAt(0);
            return recycled;
        }

        return dashGhostPool[0];
    }

    private bool PrepareDashGhostInstance(DashGhostInstance ghost, Vector3 backOffset)
    {
        if (ghost == null || ghost.root == null || ghost.pieces == null || ghost.pieces.Length <= 0)
        {
            return false;
        }

        bool hasVisiblePiece = false;
        float resolvedScale = Mathf.Max(0.01f, dashGhostScale);
        for (int i = 0; i < ghost.pieces.Length; i++)
        {
            DashGhostPiece piece = ghost.pieces[i];
            DashGhostSourceEntry sourceEntry = piece?.sourceEntry;
            MeshRenderer pieceRenderer = piece?.meshRenderer;
            MeshFilter pieceFilter = piece?.meshFilter;
            if (piece == null || sourceEntry == null || pieceRenderer == null || pieceFilter == null)
            {
                continue;
            }

            Renderer sourceRenderer = sourceEntry.sourceRenderer;
            if (sourceRenderer == null || !sourceRenderer.enabled)
            {
                pieceRenderer.enabled = false;
                continue;
            }

            if (sourceEntry.isSkinned)
            {
                SkinnedMeshRenderer sourceSkinned = sourceEntry.sourceSkinnedRenderer;
                if (sourceSkinned == null || piece.bakedMesh == null)
                {
                    pieceRenderer.enabled = false;
                    continue;
                }

                sourceSkinned.BakeMesh(piece.bakedMesh);
                if (piece.bakedMesh.vertexCount <= 0)
                {
                    pieceRenderer.enabled = false;
                    continue;
                }
            }
            else
            {
                MeshFilter sourceFilter = sourceEntry.sourceMeshFilter;
                if (sourceFilter == null || sourceFilter.sharedMesh == null)
                {
                    pieceRenderer.enabled = false;
                    continue;
                }

                if (pieceFilter.sharedMesh != sourceFilter.sharedMesh)
                {
                    pieceFilter.sharedMesh = sourceFilter.sharedMesh;
                }
            }

            Transform sourceTransform = sourceRenderer.transform;
            piece.transform.SetPositionAndRotation(sourceTransform.position + backOffset, sourceTransform.rotation);
            piece.transform.localScale = sourceTransform.lossyScale * resolvedScale;
            pieceRenderer.enabled = true;
            ApplyDashGhostMaterialPropertyBlocks(
                pieceRenderer,
                sourceRenderer,
                piece.materialCount,
                piece.sourceMaterialCount,
                Mathf.Clamp01(dashGhostStartOpacity),
                true);
            hasVisiblePiece = true;
        }

        ghost.root.SetActive(hasVisiblePiece);
        return hasVisiblePiece;
    }

    private void ReleaseDashGhostInstance(DashGhostInstance ghost)
    {
        if (ghost == null)
        {
            return;
        }

        if (ghost.pieces != null)
        {
            for (int i = 0; i < ghost.pieces.Length; i++)
            {
                DashGhostPiece piece = ghost.pieces[i];
                if (piece != null && piece.meshRenderer != null)
                {
                    piece.meshRenderer.enabled = false;
                }
            }
        }

        if (ghost.root != null)
        {
            ghost.root.SetActive(false);
        }
    }

    private void ApplyDashGhostOpacity(DashGhostInstance ghost, float opacity)
    {
        if (ghost == null)
        {
            return;
        }

        float clampedOpacity = Mathf.Clamp01(opacity);
        if (ghost.pieces != null && ghost.pieces.Length > 0)
        {
            for (int i = 0; i < ghost.pieces.Length; i++)
            {
                DashGhostPiece piece = ghost.pieces[i];
                Renderer sourceRenderer = piece?.sourceEntry?.sourceRenderer;
                MeshRenderer renderer = piece?.meshRenderer;
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                ApplyDashGhostMaterialPropertyBlocks(
                    renderer,
                    sourceRenderer,
                    piece.materialCount,
                    piece.sourceMaterialCount,
                    clampedOpacity,
                    false);
            }

            return;
        }

        if (ghost.renderers == null || ghost.renderers.Length <= 0)
        {
            return;
        }

        for (int i = 0; i < ghost.renderers.Length; i++)
        {
            Renderer renderer = ghost.renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            Material[] sharedMaterials = renderer.sharedMaterials;
            int materialCount = sharedMaterials != null ? sharedMaterials.Length : 0;
            ApplyDashGhostMaterialPropertyBlocks(renderer, null, materialCount, 0, clampedOpacity, false);
        }
    }

    private void ApplyDashGhostMaterialPropertyBlocks(
        Renderer ghostRenderer,
        Renderer sourceRenderer,
        int materialCount,
        int sourceMaterialCount,
        float opacity,
        bool copySourceColors)
    {
        if (ghostRenderer == null)
        {
            return;
        }

        dashGhostPropertyBlock ??= new MaterialPropertyBlock();
        if (materialCount <= 0)
        {
            if (copySourceColors)
            {
                dashGhostPropertyBlock.Clear();
            }
            else
            {
                ghostRenderer.GetPropertyBlock(dashGhostPropertyBlock);
            }

            dashGhostPropertyBlock.SetFloat(OpacityShaderId, opacity);
            ghostRenderer.SetPropertyBlock(dashGhostPropertyBlock);
            return;
        }

        for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
        {
            if (copySourceColors)
            {
                dashGhostPropertyBlock.Clear();
                CopyDashGhostSourceColorsToPropertyBlock(
                    sourceRenderer,
                    materialIndex,
                    sourceMaterialCount,
                    dashGhostPropertyBlock);
            }
            else
            {
                ghostRenderer.GetPropertyBlock(dashGhostPropertyBlock, materialIndex);
            }

            dashGhostPropertyBlock.SetFloat(OpacityShaderId, opacity);
            ghostRenderer.SetPropertyBlock(dashGhostPropertyBlock, materialIndex);
        }
    }

    private bool CopyDashGhostSourceColorsToPropertyBlock(
        Renderer sourceRenderer,
        int materialIndex,
        int sourceMaterialCount,
        MaterialPropertyBlock targetBlock)
    {
        if (sourceRenderer == null || targetBlock == null)
        {
            return false;
        }

        dashGhostSourcePropertyBlock ??= new MaterialPropertyBlock();
        if (materialIndex >= 0 && materialIndex < sourceMaterialCount)
        {
            dashGhostSourcePropertyBlock.Clear();
            sourceRenderer.GetPropertyBlock(dashGhostSourcePropertyBlock, materialIndex);
            bool copied = CopyDashGhostColorProperties(dashGhostSourcePropertyBlock, targetBlock);
            if (copied)
            {
                return true;
            }
        }

        dashGhostSourcePropertyBlock.Clear();
        sourceRenderer.GetPropertyBlock(dashGhostSourcePropertyBlock);
        return CopyDashGhostColorProperties(dashGhostSourcePropertyBlock, targetBlock);
    }

    private bool CopyDashGhostColorProperties(
        MaterialPropertyBlock sourceBlock,
        MaterialPropertyBlock targetBlock)
    {
        if (sourceBlock == null || sourceBlock.isEmpty || targetBlock == null)
        {
            return false;
        }

        bool copied = false;
        for (int i = 0; i < DashGhostColorShaderIds.Length; i++)
        {
            int propertyId = DashGhostColorShaderIds[i];
            if (!sourceBlock.HasColor(propertyId))
            {
                continue;
            }

            targetBlock.SetColor(propertyId, MultiplyGhostColorRgb(sourceBlock.GetColor(propertyId), dashGhostTint));
            copied = true;
        }

        return copied;
    }

    private void TrimDashGhostCount()
    {
        int maxCount = Mathf.Max(1, dashGhostMaxActiveCount);
        while (activeDashGhosts.Count > maxCount)
        {
            DashGhostInstance oldest = activeDashGhosts[0];
            ReleaseDashGhostInstance(oldest);
            activeDashGhosts.RemoveAt(0);
        }
    }

    private static int ResolveDashGhostMaterialCount(Material[] sourceMaterials)
    {
        return sourceMaterials != null && sourceMaterials.Length > 0 ? sourceMaterials.Length : 1;
    }

    private void ApplyDashGhostRendererSettings(MeshRenderer ghostRenderer, Material[] sourceMaterials)
    {
        if (ghostRenderer == null)
        {
            return;
        }

        int materialCount = ResolveDashGhostMaterialCount(sourceMaterials);
        Material[] ghostMaterials = new Material[materialCount];
        for (int i = 0; i < materialCount; i++)
        {
            Material sourceMaterial = sourceMaterials != null && i < sourceMaterials.Length
                ? sourceMaterials[i]
                : null;
            ghostMaterials[i] = ResolveDashGhostMaterial(sourceMaterial);
        }

        if (ghostMaterials.Length > 0)
        {
            ghostRenderer.sharedMaterials = ghostMaterials;
        }
        else
        {
            ghostRenderer.sharedMaterials = Array.Empty<Material>();
        }

        ghostRenderer.shadowCastingMode = ShadowCastingMode.Off;
        ghostRenderer.receiveShadows = false;
        ghostRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        ghostRenderer.lightProbeUsage = LightProbeUsage.Off;
        ghostRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private Material ResolveDashGhostMaterial(Material sourceMaterial)
    {
        if (dashGhostMaterial != null)
        {
            return dashGhostMaterial;
        }

        if (sourceMaterial == null)
        {
            return null;
        }

        if (dashGhostMaterialCache.TryGetValue(sourceMaterial, out Material cachedGhostMaterial) && cachedGhostMaterial != null)
        {
            return cachedGhostMaterial;
        }

        Material ghostMaterial = new Material(sourceMaterial)
        {
            name = $"{sourceMaterial.name}_DashGhost"
        };

        ConfigureMaterialForTransparentGhost(ghostMaterial);
        ApplyGhostTintToMaterial(ghostMaterial, sourceMaterial, dashGhostTint);
        dashGhostMaterialCache[sourceMaterial] = ghostMaterial;
        return ghostMaterial;
    }

    private static void ConfigureMaterialForTransparentGhost(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(OpacityShaderId))
        {
            material.SetFloat(OpacityShaderId, 1f);
            return;
        }

        material.renderQueue = (int)RenderQueue.Transparent;
        if (material.HasProperty(SurfaceShaderId))
        {
            material.SetFloat(SurfaceShaderId, 1f);
        }

        if (material.HasProperty(BlendShaderId))
        {
            material.SetFloat(BlendShaderId, 0f);
        }

        if (material.HasProperty(SrcBlendShaderId))
        {
            material.SetFloat(SrcBlendShaderId, (float)BlendMode.SrcAlpha);
        }

        if (material.HasProperty(DstBlendShaderId))
        {
            material.SetFloat(DstBlendShaderId, (float)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty(ZWriteShaderId))
        {
            material.SetFloat(ZWriteShaderId, 0f);
        }

        if (material.HasProperty(AlphaClipShaderId))
        {
            material.SetFloat(AlphaClipShaderId, 0f);
        }

        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    private static void ApplyGhostTintToMaterial(Material ghostMaterial, Material sourceMaterial, Color tint)
    {
        if (ghostMaterial == null)
        {
            return;
        }

        if (ghostMaterial.HasProperty(BaseColorShaderId))
        {
            Color sourceBaseColor = sourceMaterial != null && sourceMaterial.HasProperty(BaseColorShaderId)
                ? sourceMaterial.GetColor(BaseColorShaderId)
                : Color.white;
            ghostMaterial.SetColor(BaseColorShaderId, MultiplyGhostColorRgb(sourceBaseColor, tint));
        }

        if (ghostMaterial.HasProperty(ColorShaderId))
        {
            Color sourceColor = sourceMaterial != null && sourceMaterial.HasProperty(ColorShaderId)
                ? sourceMaterial.GetColor(ColorShaderId)
                : sourceMaterial != null && sourceMaterial.HasProperty(BaseColorShaderId)
                    ? sourceMaterial.GetColor(BaseColorShaderId)
                    : Color.white;
            ghostMaterial.SetColor(ColorShaderId, MultiplyGhostColorRgb(sourceColor, tint));
        }
    }

    private static Color MultiplyGhostColorRgb(Color sourceColor, Color tint)
    {
        return new Color(
            sourceColor.r * tint.r,
            sourceColor.g * tint.g,
            sourceColor.b * tint.b,
            sourceColor.a);
    }

    private void ClearDashGhostInstances()
    {
        if (activeDashGhosts.Count <= 0)
        {
            return;
        }

        for (int i = activeDashGhosts.Count - 1; i >= 0; i--)
        {
            DashGhostInstance ghost = activeDashGhosts[i];
            ReleaseDashGhostInstance(ghost);
        }

        activeDashGhosts.Clear();
    }

    private void DestroyDashGhostPool()
    {
        ClearDashGhostInstances();
        nextDashGhostPoolIndex = 0;

        for (int i = 0; i < dashGhostPool.Count; i++)
        {
            DashGhostInstance ghost = dashGhostPool[i];
            if (ghost?.pieces != null)
            {
                for (int j = 0; j < ghost.pieces.Length; j++)
                {
                    DashGhostPiece piece = ghost.pieces[j];
                    if (piece != null && piece.bakedMesh != null)
                    {
                        Destroy(piece.bakedMesh);
                    }
                }
            }

            if (ghost != null && ghost.root != null)
            {
                Destroy(ghost.root);
            }
        }

        dashGhostPool.Clear();
        dashGhostSourceEntries = Array.Empty<DashGhostSourceEntry>();
        dashGhostPoolSourceRoot = null;

        if (dashGhostPoolRoot != null)
        {
            Destroy(dashGhostPoolRoot.gameObject);
            dashGhostPoolRoot = null;
        }
    }

    private void ClearDashGhostMaterialCache()
    {
        dashGhostPropertyBlock = null;
        dashGhostSourcePropertyBlock = null;
        DestroyDashGhostPool();

        if (dashGhostMaterialCache.Count <= 0)
        {
            return;
        }

        foreach (KeyValuePair<Material, Material> pair in dashGhostMaterialCache)
        {
            if (pair.Value != null)
            {
                Destroy(pair.Value);
            }
        }

        dashGhostMaterialCache.Clear();
    }

    private void UpdateZoomInput(float deltaTime)
    {
        float wheel = MinimoInputBridge.MouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.0001f)
        {
            int levelIndex = GetNearestZoomLevelIndex(targetZoomDistance);
            levelIndex += wheel > 0f ? -1 : 1;
            targetZoomDistance = GetZoomLevelDistance(levelIndex);
        }

        float zoomLerp = 1f - Mathf.Exp(-zoomSharpness * deltaTime);
        currentZoomDistance = Mathf.Lerp(currentZoomDistance, targetZoomDistance, zoomLerp);
    }

    private int GetNearestZoomLevelIndex(float distance)
    {
        int lastLevelIndex = Mathf.Max(1, zoomLevelCount - 1);
        float normalizedDistance = Mathf.InverseLerp(minZoomDistance, maxZoomDistance, distance);
        return Mathf.Clamp(Mathf.RoundToInt(normalizedDistance * lastLevelIndex), 0, lastLevelIndex);
    }

    private float GetNearestZoomLevelDistance(float distance)
    {
        return GetZoomLevelDistance(GetNearestZoomLevelIndex(distance));
    }

    private float GetZoomLevelDistance(int levelIndex)
    {
        int lastLevelIndex = Mathf.Max(1, zoomLevelCount - 1);
        int clampedLevelIndex = Mathf.Clamp(levelIndex, 0, lastLevelIndex);
        return Mathf.Lerp(minZoomDistance, maxZoomDistance, clampedLevelIndex / (float)lastLevelIndex);
    }

    private void UpdateCameraRotation(float deltaTime)
    {
        float mouseX = MinimoInputBridge.GetAxis("Mouse X");
        float mouseY = MinimoInputBridge.GetAxis("Mouse Y");
        Vector2 gamepadLook = ReadGamepadLookInput();
        yaw += (mouseX * mouseXSensitivity + gamepadLook.x * gamepadLookSensitivity) * deltaTime;
        float verticalMouse = (invertY ? mouseY : -mouseY) * mouseYSensitivity;
        float verticalGamepad = (invertY ? gamepadLook.y : -gamepadLook.y) * gamepadLookSensitivity;
        pitch = Mathf.Clamp(pitch + (verticalMouse + verticalGamepad) * deltaTime, minPitch, maxPitch);

        if (Mathf.Abs(wallJumpCameraTurnRemaining) > 0.001f)
        {
            float turnStep = Mathf.Min(Mathf.Abs(wallJumpCameraTurnRemaining), Mathf.Max(0f, wallJumpCameraTurnSpeed) * deltaTime);
            float signedStep = Mathf.Sign(wallJumpCameraTurnRemaining) * turnStep;
            yaw = Mathf.Repeat(yaw + signedStep, 360f);
            wallJumpCameraTurnRemaining -= signedStep;
            if (Mathf.Abs(wallJumpCameraTurnRemaining) <= 0.001f)
            {
                wallJumpCameraTurnRemaining = 0f;
            }
        }

        if (wallJumpNoRotateActive && !isGrounded)
        {
            return;
        }

        if (!enableCameraAssist)
        {
            return;
        }

        Vector3 assistDirection = planarVelocity.sqrMagnitude > 0.16f ? planarVelocity : desiredMoveDirection;
        assistDirection = Vector3.ProjectOnPlane(assistDirection, Vector3.up);
        if (assistDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float targetYaw = Mathf.Atan2(assistDirection.x, assistDirection.z) * Mathf.Rad2Deg;
        float assistT = (1f - Mathf.Exp(-cameraAssistYawSharpness * deltaTime)) * cameraAssistYawWeight;
        yaw = Mathf.LerpAngle(yaw, targetYaw, assistT);
    }

    private void UpdateCameraMotionEffects(float deltaTime)
    {
        if (!enableCameraMotionFx || cameraTransform == null)
        {
            currentCameraBankAngle = Mathf.Lerp(currentCameraBankAngle, 0f, 1f - Mathf.Exp(-cameraBankSharpness * deltaTime));
            cameraShakeAmplitude = Mathf.MoveTowards(cameraShakeAmplitude, 0f, cameraShakeDamping * deltaTime);
            cameraShakeOffset = Vector2.zero;
            return;
        }

        Vector3 planarRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        float moveSpeed01 = Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(0.01f, moveSpeed));
        float lateralSpeed = planarVelocity.sqrMagnitude > 0.0001f
            ? Vector3.Dot(planarVelocity.normalized, planarRight) * moveSpeed01
            : 0f;

        float lateralAcceleration = planarAcceleration.sqrMagnitude > 0.0001f
            ? Vector3.Dot(planarAcceleration.normalized, planarRight) * Mathf.Clamp01(planarAcceleration.magnitude / Mathf.Max(0.01f, groundAcceleration))
            : 0f;

        float bank01 = lateralSpeed * cameraBankVelocityInfluence + lateralAcceleration * cameraBankAccelerationInfluence;
        bank01 = Mathf.Clamp(bank01, -1f, 1f);
        targetCameraBankAngle = -bank01 * maxCameraBankAngle;
        if (currentState == MovementState.Teetering)
        {
            targetCameraBankAngle += Mathf.Sin(Time.time * teeterCameraSwayFrequency) * teeterCameraSwayAngle;
        }

        float bankBlend = 1f - Mathf.Exp(-cameraBankSharpness * deltaTime);
        currentCameraBankAngle = Mathf.Lerp(currentCameraBankAngle, targetCameraBankAngle, bankBlend);

        cameraShakeAmplitude = Mathf.MoveTowards(cameraShakeAmplitude, 0f, cameraShakeDamping * deltaTime);
        if (cameraShakeAmplitude <= 0.0001f)
        {
            cameraShakeOffset = Vector2.zero;
            return;
        }

        float noiseTime = Time.time * cameraShakeFrequency;
        float noiseX = Mathf.PerlinNoise(noiseTime, 0.47f) * 2f - 1f;
        float noiseY = Mathf.PerlinNoise(0.91f, noiseTime) * 2f - 1f;
        cameraShakeOffset = new Vector2(noiseX, noiseY) * cameraShakeAmplitude;
    }

    private void UpdateCameraPosition(float deltaTime)
    {
        Vector3 lookAheadTarget = Vector3.zero;
        if (enableCameraAssist && cameraLookAheadDistance > 0f)
        {
            Vector3 referenceDirection = desiredMoveDirection.sqrMagnitude > 0.0001f ? desiredMoveDirection : planarVelocity.normalized;
            if (referenceDirection.sqrMagnitude > 0.0001f)
            {
                lookAheadTarget = referenceDirection * cameraLookAheadDistance;
            }
        }

        float lookAheadBlend = 1f - Mathf.Exp(-cameraLookAheadSharpness * deltaTime);
        cameraLookAhead = Vector3.Lerp(cameraLookAhead, lookAheadTarget, lookAheadBlend);

        Vector3 pivotBase = GetCameraPivot();
        if (currentState == MovementState.Rolling)
        {
            rollCameraPivotReleaseTimer = 0f;
            if (!rollCameraPivotLockActive)
            {
                rollCameraPivotLockActive = true;
                float rollEntryPivotY = pivotBase.y;
                if (lowClearanceCameraPivotInitialized && lowClearanceCameraBlend01 > 0.0001f)
                {
                    rollEntryPivotY = Mathf.Lerp(
                        rollEntryPivotY,
                        lowClearanceCameraPivotY,
                        Mathf.Clamp01(lowClearanceCameraBlend01));
                }

                rollCameraPivotLockedY = rollEntryPivotY;
            }

            float targetY = pivotBase.y;
            float deadZone = Mathf.Max(0f, rollCameraPivotStabilizationDeadZone);
            float deltaToTarget = targetY - rollCameraPivotLockedY;
            if (Mathf.Abs(deltaToTarget) <= deadZone)
            {
                targetY = rollCameraPivotLockedY;
            }
            else
            {
                targetY -= Mathf.Sign(deltaToTarget) * deadZone;
            }

            float stabilizeSharpness = Mathf.Max(0.01f, rollCameraPivotStabilizationSharpness);
            float stabilizeBlend = 1f - Mathf.Exp(-stabilizeSharpness * deltaTime);
            rollCameraPivotLockedY = Mathf.Lerp(rollCameraPivotLockedY, targetY, stabilizeBlend);

            float maxLag = Mathf.Max(0f, rollCameraPivotStabilizationMaxLag);
            if (maxLag > 0.0001f)
            {
                rollCameraPivotLockedY = Mathf.Clamp(
                    rollCameraPivotLockedY,
                    pivotBase.y - maxLag,
                    pivotBase.y + maxLag);
            }

            pivotBase.y = rollCameraPivotLockedY;
        }
        else
        {
            if (rollCameraPivotLockActive)
            {
                rollCameraPivotLockActive = false;
                rollCameraPivotReleaseTimer = Mathf.Max(0f, rollCameraPivotReleaseDuration);
            }

            if (rollCameraPivotReleaseTimer > 0f)
            {
                float releaseBlend = 1f - Mathf.Exp(-Mathf.Max(0.01f, rollCameraPivotReleaseSharpness) * deltaTime);
                rollCameraPivotLockedY = Mathf.Lerp(rollCameraPivotLockedY, pivotBase.y, releaseBlend);
                pivotBase.y = rollCameraPivotLockedY;
                rollCameraPivotReleaseTimer = Mathf.Max(0f, rollCameraPivotReleaseTimer - deltaTime);
            }
        }

        float effectivePitch = pitch;
        float effectiveZoomDistance = currentZoomDistance;
        float lowClearanceBottomY = 0f;
        float lowClearanceTopY = 0f;
        bool lowClearanceTargetActive = false;
        if (enableLowClearanceCameraMode && currentState != MovementState.Rolling)
        {
            lowClearanceTargetActive = TryGetLowStandClearanceInfo(
                minimumStandClearanceHeight,
                out lowClearanceBottomY,
                out lowClearanceTopY,
                out _);
        }

        if (lowClearanceTargetActive)
        {
            float targetPivotY = (lowClearanceBottomY + lowClearanceTopY) * 0.5f;
            if (!lowClearanceCameraPivotInitialized)
            {
                lowClearanceCameraPivotInitialized = true;
                lowClearanceCameraPivotY = pivotBase.y;
            }

            float pivotStep = Mathf.Max(0f, lowClearanceCameraPivotSharpness) * deltaTime;
            lowClearanceCameraPivotY = Mathf.MoveTowards(lowClearanceCameraPivotY, targetPivotY, pivotStep);
        }

        float clearanceBlendTarget = lowClearanceTargetActive ? 1f : 0f;
        float clearanceBlendStep = Mathf.Max(0f, lowClearanceCameraBlendSpeed) * deltaTime;
        lowClearanceCameraBlend01 = Mathf.MoveTowards(lowClearanceCameraBlend01, clearanceBlendTarget, clearanceBlendStep);
        if (!lowClearanceTargetActive && lowClearanceCameraBlend01 <= 0.0001f)
        {
            lowClearanceCameraPivotInitialized = false;
        }

        bool lowClearanceCameraActive = currentState != MovementState.Rolling
            && lowClearanceCameraBlend01 > 0.0001f
            && lowClearanceCameraPivotInitialized;
        if (lowClearanceCameraActive)
        {
            float clearanceBlend01 = Mathf.Clamp01(lowClearanceCameraBlend01);
            pivotBase.y = Mathf.Lerp(pivotBase.y, lowClearanceCameraPivotY, clearanceBlend01);

            // Prevent the camera from unnecessarily pulling backward when pitch is reset.
            float pitchDistanceScale = Mathf.Clamp01(Mathf.Cos(Mathf.Abs(pitch) * Mathf.Deg2Rad));
            float normalizedDistance = currentZoomDistance * pitchDistanceScale;
            float lowClearanceDistance = Mathf.Clamp(normalizedDistance, minZoomDistance, currentZoomDistance);
            effectiveZoomDistance = Mathf.Lerp(currentZoomDistance, lowClearanceDistance, clearanceBlend01);
            effectivePitch = Mathf.Lerp(pitch, 0f, clearanceBlend01);
        }

        Vector3 pivot = pivotBase + cameraLookAhead;
        Quaternion cameraRotation = Quaternion.Euler(effectivePitch, yaw, 0f);
        Vector3 desiredOffset = cameraRotation * new Vector3(shoulderOffset, 0f, -effectiveZoomDistance);
        Vector3 desiredPosition = pivot + desiredOffset;
        Vector3 resolvedPosition = ResolveCameraCollision(pivot, desiredPosition);

        Vector3 shakeWorldOffset = cameraRotation * new Vector3(cameraShakeOffset.x, cameraShakeOffset.y, 0f);
        Vector3 finalPosition = resolvedPosition + Vector3.up * cameraKick + shakeWorldOffset;
        if (cameraPositionSmooth <= 0f)
        {
            cameraTransform.position = finalPosition;
        }
        else
        {
            float smooth = 1f - Mathf.Exp(-(1f / cameraPositionSmooth) * deltaTime);
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, finalPosition, smooth);
        }

        Quaternion lookRotation = Quaternion.LookRotation(pivot - cameraTransform.position, Vector3.up);
        Quaternion bankRotation = Quaternion.AngleAxis(lowClearanceCameraActive ? 0f : currentCameraBankAngle, Vector3.forward);
        cameraTransform.rotation = lookRotation * bankRotation;
    }

    private void UpdateDynamicCameraEffects(float deltaTime)
    {
        if (cameraTransform == null)
        {
            return;
        }

        Camera runtimeCamera = cameraTransform.GetComponent<Camera>();
        if (runtimeCamera == null)
        {
            return;
        }

        float dashTarget01 = 0f;
        if (enableDashCameraBoost && currentState == MovementState.Dashing)
        {
            float dashSpeed01 = Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(0.1f, dashSpeed));
            dashTarget01 = Mathf.Clamp01(Mathf.Max(dashFovFloor01, dashSpeed01));
        }

        float dashSharpness = dashTarget01 >= dashCameraBoost01 ? dashFovBoostSharpness : dashFovRecoverSharpness;
        float dashBlend = 1f - Mathf.Exp(-Mathf.Max(0.01f, dashSharpness) * deltaTime);
        dashCameraBoost01 = Mathf.Lerp(dashCameraBoost01, dashTarget01, dashBlend);
        UpdateDashMotionBlurWeight(dashCameraBoost01, deltaTime);

        float speed01 = Mathf.Clamp01(planarVelocity.magnitude / Mathf.Max(0.1f, fovSpeedReference));
        bool airDashBoost = currentState == MovementState.Dashing && !isGrounded;
        bool slopeSlideBoost = currentState == MovementState.SlopeSliding
            && groundDownhillDirection.sqrMagnitude > 0.0001f
            && Vector3.Dot(planarVelocity.normalized, groundDownhillDirection) > 0f;
        float speedContribution = enableSpeedFov ? speed01 * speedFovWeight : 0f;
        float legacyDashContribution = airDashBoost ? speed01 * airDashFovWeight : 0f;
        float dashContribution = Mathf.Max(legacyDashContribution, dashCameraBoost01 * dashFovWeight);
        float slopeContribution = enableSpeedFov && slopeSlideBoost ? speed01 * slopeSlideFovWeight : 0f;
        float boost01 = Mathf.Clamp01(Mathf.Max(speedContribution, Mathf.Max(dashContribution, slopeContribution)));
        bool allowFovBoost = enableSpeedFov || enableDashCameraBoost;
        targetCameraFov = allowFovBoost ? Mathf.Lerp(baseCameraFov, maxBoostCameraFov, boost01) : baseCameraFov;
        float sharpness = targetCameraFov >= currentCameraFov ? fovBoostSharpness : fovRecoverSharpness;
        float fovBlend = 1f - Mathf.Exp(-Mathf.Max(0.01f, sharpness) * deltaTime);
        currentCameraFov = Mathf.Lerp(currentCameraFov, targetCameraFov, fovBlend);
        runtimeCamera.fieldOfView = currentCameraFov;

        float windTarget = enableWindOverlay ? boost01 : 0f;
        float windBlend = 1f - Mathf.Exp(-Mathf.Max(0.01f, windOverlaySharpness) * deltaTime);
        windOverlayIntensity = Mathf.Lerp(windOverlayIntensity, windTarget, windBlend);
    }

    private void UpdateDashMotionBlurWeight(float dashBoost01, float deltaTime)
    {
        float targetWeight = dashMotionBlurVolume == null
            ? 0f
            : Mathf.Clamp01(dashBoost01) * Mathf.Clamp01(dashMotionBlurMaxWeight);
        float sharpness = targetWeight >= dashMotionBlurWeight ? dashMotionBlurBoostSharpness : dashMotionBlurRecoverSharpness;
        float blend = 1f - Mathf.Exp(-Mathf.Max(0.01f, sharpness) * deltaTime);
        dashMotionBlurWeight = Mathf.Lerp(dashMotionBlurWeight, targetWeight, blend);
        if (dashMotionBlurVolume != null)
        {
            SetDashMotionBlurVolumeWeight(dashMotionBlurWeight);
        }
    }

    private bool IsDpadHorizontalAssignedToGameplay()
    {
        string normalizedMoveAxis = string.IsNullOrWhiteSpace(gamepadMoveXAxis)
            ? string.Empty
            : gamepadMoveXAxis.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        return normalizedMoveAxis == "dpadx";
    }

    private void SetDashMotionBlurVolumeWeight(float weight)
    {
        if (dashMotionBlurVolume == null)
        {
            return;
        }

        Type volumeType = dashMotionBlurVolume.GetType();
        System.Reflection.PropertyInfo property = volumeType.GetProperty("weight");
        if (property != null && property.CanWrite && property.PropertyType == typeof(float))
        {
            property.SetValue(dashMotionBlurVolume, weight, null);
            return;
        }

        System.Reflection.FieldInfo field = volumeType.GetField("weight");
        if (field != null && field.FieldType == typeof(float))
        {
            field.SetValue(dashMotionBlurVolume, weight);
        }
    }

    private Vector3 ResolveCameraCollision(Vector3 pivot, Vector3 desiredPosition)
    {
        Vector3 toCamera = desiredPosition - pivot;
        float distance = toCamera.magnitude;
        if (distance <= 0.001f)
        {
            return desiredPosition;
        }

        Vector3 direction = toCamera / distance;
        float probeRadius = Mathf.Max(0f, collisionProbeRadius);
        // Start the cast slightly toward the camera direction instead of exactly at the pivot center.
        // This prevents side wall contacts around the pivot from creating false blocks during states like roll.
        float castStartOffset = Mathf.Min(probeRadius, Mathf.Max(0f, distance - 0.0005f));
        float castDistance = Mathf.Max(0f, distance - castStartOffset);
        Vector3 castOrigin = pivot + direction * castStartOffset;
        RaycastHit[] hits = Physics.SphereCastAll(
            castOrigin,
            probeRadius,
            direction,
            castDistance,
            cameraCollisionMask,
            QueryTriggerInteraction.Ignore);

        float nearestHit = castDistance;
        bool blocked = false;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null)
            {
                continue;
            }

            Transform hitTransform = hit.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
            {
                continue;
            }

            // Filter side or surface contacts at cast start with near-zero distance.
            // Gercek bloklar genelde kameraya gidis yonune bakan bir normale sahiptir.
            bool startsInsideOrTouching = hit.distance <= 0.0005f;
            float facingDot = Vector3.Dot(-hit.normal, direction);
            if (startsInsideOrTouching && facingDot < 0.2f)
            {
                continue;
            }

            if (hit.distance < nearestHit)
            {
                nearestHit = hit.distance;
                blocked = true;
            }
        }

        if (!blocked)
        {
            return desiredPosition;
        }

        float safeDistance = castStartOffset + nearestHit - collisionBuffer;
        safeDistance = Mathf.Clamp(safeDistance, cameraCollisionMinDistance, distance);
        return pivot + direction * safeDistance;
    }

    private void UpdateCrouchShape(float deltaTime)
    {
        _ = deltaTime;
        if (controller == null)
        {
            return;
        }

        bool isRollingState = currentState == MovementState.Rolling;
        bool isSlidingState = currentState == MovementState.Sliding
            || currentState == MovementState.SlopeSliding;
        bool shouldUseCrouchShape = currentState == MovementState.Crouching || crouchTarget;
        if (!isRollingState && !isSlidingState && !shouldUseCrouchShape && !CanStandUp())
        {
            shouldUseCrouchShape = true;
        }

        if (isRollingState)
        {
            ApplyControllerShape(
                RollSlideControllerHeight,
                RollControllerCenter,
                RollSlideControllerRadius,
                RollSlideControllerStepOffset);
            return;
        }

        if (isSlidingState)
        {
            ApplyControllerShape(
                RollSlideControllerHeight,
                SlideControllerCenter,
                RollSlideControllerRadius,
                RollSlideControllerStepOffset);
            return;
        }

        if (shouldUseCrouchShape)
        {
            ApplyControllerShape(
                crouchHeightResolved,
                CrouchControllerCenter,
                CrouchControllerRadius,
                CrouchControllerStepOffset);
            return;
        }

        ApplyControllerShape(
            StandingControllerHeight,
            StandingControllerCenter,
            StandingControllerRadius,
            StandingControllerStepOffset);
    }

    private bool CanStandUp()
    {
        if (controller == null)
        {
            return true;
        }

        if (TryGetLowStandClearanceInfo(minimumStandClearanceHeight, out _, out _, out _))
        {
            return false;
        }

        if (controller.height >= standingHeight - 0.01f && controller.radius >= standingRadius - 0.01f)
        {
            return true;
        }

        int mask = GetStandUpMaskExcludingSelf();
        BuildCapsuleWorldData(
            standingCenter,
            standingHeight,
            standingRadius,
            out Vector3 standingBottom,
            out Vector3 standingTop,
            out float standingCapsuleRadius);
        int standingOverlapCount = CollectCapsuleBlockingOverlaps(
            standingBottom,
            standingTop,
            standingCapsuleRadius,
            mask,
            standUpTargetOverlapBuffer);
        return standingOverlapCount <= 0;
    }

    private bool TryGetLowStandClearanceInfo(
        float requiredHeight,
        out float bottomY,
        out float topY,
        out float measuredHeight)
    {
        bottomY = 0f;
        topY = 0f;
        measuredHeight = float.PositiveInfinity;
        if (controller == null)
        {
            return false;
        }

        BuildCapsuleWorldData(
            standingCenter,
            standingHeight,
            standingRadius,
            out Vector3 standingBottom,
            out _,
            out float standingCapsuleRadius);
        float diameter = standingCapsuleRadius * 2f;
        float safeRequiredHeight = Mathf.Max(requiredHeight, diameter + 0.001f);
        float castDistance = Mathf.Max(0f, safeRequiredHeight - diameter + 0.02f);

        RaycastHit[] hits = Physics.SphereCastAll(
            standingBottom,
            standingCapsuleRadius,
            Vector3.up,
            castDistance,
            GetStandUpMaskExcludingSelf(),
            QueryTriggerInteraction.Ignore);

        bool found = false;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null)
            {
                continue;
            }

            if (IsOwnCollider(hit.collider))
            {
                continue;
            }

            float facingDot = Vector3.Dot(-hit.normal, Vector3.up);
            bool startsInsideOrTouching = hit.distance <= 0.0005f;
            if (startsInsideOrTouching && facingDot < 0.2f)
            {
                continue;
            }

            if (facingDot < 0.05f)
            {
                continue;
            }

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                found = true;
            }
        }

        if (!found)
        {
            return false;
        }

        measuredHeight = bestDistance + diameter;
        if (measuredHeight + 0.001f >= safeRequiredHeight)
        {
            return false;
        }

        bottomY = standingBottom.y - standingCapsuleRadius;
        topY = bottomY + measuredHeight;
        return true;
    }

    private int CollectCapsuleBlockingOverlaps(
        Vector3 capsuleBottom,
        Vector3 capsuleTop,
        float capsuleRadius,
        int mask,
        Collider[] overlapBuffer)
    {
        Array.Clear(overlapBuffer, 0, overlapBuffer.Length);
        int rawHitCount = Physics.OverlapCapsuleNonAlloc(
            capsuleBottom,
            capsuleTop,
            Mathf.Max(0.01f, capsuleRadius),
            overlapBuffer,
            mask,
            QueryTriggerInteraction.Ignore);
        int compactCount = 0;
        int iterateCount = Mathf.Min(rawHitCount, overlapBuffer.Length);
        for (int i = 0; i < iterateCount; i++)
        {
            Collider candidate = overlapBuffer[i];
            if (candidate == null)
            {
                continue;
            }

            if (IsOwnCollider(candidate))
            {
                continue;
            }

            overlapBuffer[compactCount] = candidate;
            compactCount++;
        }

        return compactCount;
    }

    private void BuildCapsuleWorldData(
        Vector3 localCenter,
        float height,
        float radius,
        out Vector3 capsuleBottom,
        out Vector3 capsuleTop,
        out float capsuleRadius)
    {
        capsuleRadius = Mathf.Max(0.01f, radius);
        float halfHeight = Mathf.Max(height * 0.5f, capsuleRadius);
        float axisOffset = halfHeight - capsuleRadius;
        Vector3 worldCenter = transform.TransformPoint(localCenter);
        capsuleBottom = worldCenter - Vector3.up * axisOffset;
        capsuleTop = worldCenter + Vector3.up * axisOffset;
    }

    private static string ResolveAnimatorParameterOrDefault(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static bool IsLandingAnimatorParamConflicting(
        string candidate,
        string fallParam,
        string diveParam,
        string groundPoundParam)
    {
        return string.Equals(candidate, fallParam, StringComparison.Ordinal)
            || string.Equals(candidate, diveParam, StringComparison.Ordinal)
            || string.Equals(candidate, groundPoundParam, StringComparison.Ordinal);
    }

    private string ResolveLandingAnimatorFallbackName(
        string configured,
        string fallParam,
        string diveParam,
        string groundPoundParam)
    {
        const string preferredFallback = "Land";
        if (!IsLandingAnimatorParamConflicting(preferredFallback, fallParam, diveParam, groundPoundParam)
            && HasAnimatorParameter(preferredFallback, AnimatorControllerParameterType.Bool))
        {
            return preferredFallback;
        }

        const string legacyFallback = "Landing";
        if (!IsLandingAnimatorParamConflicting(legacyFallback, fallParam, diveParam, groundPoundParam)
            && HasAnimatorParameter(legacyFallback, AnimatorControllerParameterType.Bool))
        {
            return legacyFallback;
        }

        // Last resort: use an existing bool parameter even if the preferred name conflicts.
        if (HasAnimatorParameter(preferredFallback, AnimatorControllerParameterType.Bool))
        {
            return preferredFallback;
        }

        if (HasAnimatorParameter(legacyFallback, AnimatorControllerParameterType.Bool))
        {
            return legacyFallback;
        }

        if (!IsLandingAnimatorParamConflicting(preferredFallback, fallParam, diveParam, groundPoundParam))
        {
            return preferredFallback;
        }

        if (!IsLandingAnimatorParamConflicting(legacyFallback, fallParam, diveParam, groundPoundParam))
        {
            return legacyFallback;
        }

        return configured;
    }

    private string ResolveLandingAnimatorParameterName()
    {
        string configured = ResolveAnimatorParameterOrDefault(animLandParam, "Land");
        string fallParam = ResolveAnimatorParameterOrDefault(animFallParam, "Fall");
        string diveParam = ResolveAnimatorParameterOrDefault(animDiveParam, "Dive");
        string groundPoundParam = ResolveAnimatorParameterOrDefault(animGroundPoundParam, "GroundPound");

        bool conflictsWithActionParam = IsLandingAnimatorParamConflicting(
            configured,
            fallParam,
            diveParam,
            groundPoundParam);

        if (!conflictsWithActionParam && HasAnimatorParameter(configured, AnimatorControllerParameterType.Bool))
        {
            return configured;
        }

        string fallback = ResolveLandingAnimatorFallbackName(configured, fallParam, diveParam, groundPoundParam);
        if (!string.Equals(fallback, configured, StringComparison.Ordinal))
        {
            // Keep the serialized setting aligned with runtime resolution so the same warning
            // condition does not reappear on every validate/start cycle.
            animLandParam = fallback;
        }

        return fallback;
    }

    private float GetAnimatorDisplacementPlanarSpeed(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return 0f;
        }

        Vector3 currentPosition = transform.position;
        if (!hasAnimatorSamplePosition)
        {
            hasAnimatorSamplePosition = true;
            lastAnimatorSamplePosition = currentPosition;
            return 0f;
        }

        Vector3 displacement = currentPosition - lastAnimatorSamplePosition;
        lastAnimatorSamplePosition = currentPosition;

        Vector3 upAxis = ResolveVerticalMotionUpAxis();
        if (upAxis.sqrMagnitude <= 0.0001f)
        {
            upAxis = Vector3.up;
        }

        Vector3 planarDisplacement = Vector3.ProjectOnPlane(displacement, upAxis);
        float sampledSpeed = planarDisplacement.magnitude / deltaTime;
        if (float.IsNaN(sampledSpeed) || float.IsInfinity(sampledSpeed) || sampledSpeed <= 0f)
        {
            return 0f;
        }

        float maxExpectedMoveSpeed = Mathf.Max(0.1f, moveSpeed * Mathf.Max(1f, sprintSpeedMultiplier));
        float maxExpectedDashSpeed = Mathf.Max(maxExpectedMoveSpeed, dashSpeed);
        float maxExpectedSampleSpeed = Mathf.Max(1f, maxExpectedDashSpeed * 1.25f);
        return Mathf.Clamp(sampledSpeed, 0f, maxExpectedSampleSpeed);
    }

    private void UpdateAnimatorParameters()
    {
        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        float displacementPlanarSpeed = GetAnimatorDisplacementPlanarSpeed(deltaTime);
        if (runtimeAnimator == null)
        {
            hadMovementInputLastFrame = moveInput.sqrMagnitude > movementInputDeadZone * movementInputDeadZone;
            return;
        }

        bool hangAnimatorActive = IsHangAnimatorStateValid();
        if (!hangAnimatorActive && ShouldForceHangAnimatorDuringWallTraversal())
        {
            hangAnimatorActive = true;
        }
        if (backflipAnimatorHoldActive
            && (isGrounded || (controller != null && controller.isGrounded)))
        {
            backflipAnimatorHoldActive = false;
        }

        bool climbJumpAnimatorFlag = climbJumpAnimatorActive;
        bool climbAnimatorActive = ledgeClimbActive;
        bool dashAnimatorActive = currentState == MovementState.Dashing;
        bool airDashAnimatorActive = dashAnimatorActive && !isGrounded;
        bool jumpAnimatorActive = !isGrounded
            && (jumpConsumed || currentState == MovementState.Jumping || airDashAnimatorActive);
        if (jumpAnimatorActive && ShouldSuppressJumpAnimatorForShortNonJumpFall())
        {
            jumpAnimatorActive = false;
        }
        bool teeterAnimatorActive = IsEdgeAnimatorActive();
        bool ledgeShimmyAnimatorActive = hangAnimatorActive && ledgeHangActive;
        float shimmyThreshold = Mathf.Max(0.0001f, Mathf.Clamp01(ledgeHangSideAnimationThreshold));
        bool hangLeftAnimatorActive = ledgeShimmyAnimatorActive && ledgeHangLateralInputRuntime <= -shimmyThreshold;
        bool hangRightAnimatorActive = ledgeShimmyAnimatorActive && ledgeHangLateralInputRuntime >= shimmyThreshold;
        bool groundedNow = isGrounded || (controller != null && controller.isGrounded);
        string resolvedLandParam = ResolveLandingAnimatorParameterName();
        bool rollAnimatorActive = currentState == MovementState.Rolling;
        if (!useAnimatorParameters)
        {
            bool landingPulseActiveNoParams = landingAnimatorPulseTimer > 0f && groundedNow;
            TrySetAnimatorBoolDirect(animHangParam, hangAnimatorActive);
            TrySetAnimatorBoolDirect(animHangLeftParam, hangLeftAnimatorActive);
            TrySetAnimatorBoolDirect(animHangRightParam, hangRightAnimatorActive);
            TrySetAnimatorBoolDirect(animClimbParam, climbAnimatorActive);
            TrySetAnimatorBoolDirect(animClimbJumpParam, climbJumpAnimatorFlag);
            TrySetAnimatorBoolDirect(animBackflipTrigger, backflipAnimatorHoldActive);
            TrySetAnimatorBoolDirect(animSlideParam, currentState == MovementState.Sliding || currentState == MovementState.SlopeSliding);
            TrySetAnimatorBoolDirect(animDashParam, dashAnimatorActive);
            TrySetAnimatorBoolDirect(animHardFallParam, groundPoundHardFallActive && !groundPoundHardLandActive);
            TrySetAnimatorBoolDirect(animHardLandParam, groundPoundHardLandActive);
            TrySetAnimatorBoolDirect(resolvedLandParam, landingPulseActiveNoParams);
            TrySetAnimatorBoolDirect(animRollParam, rollAnimatorActive);
            TrySetAnimatorBoolDirect(animTeeterParam, teeterAnimatorActive);
            UpdateFallAnimatorStateByMovementState();
            ForceClearAnimatorActionBoolsOnStableGround();
            ForceGroundedAnimatorFalseStates();
            if (landingPulseActiveNoParams)
            {
                TrySetAnimatorBoolDirect(resolvedLandParam, true);
            }
            landingAnimatorPulseTimer = Mathf.Max(0f, landingAnimatorPulseTimer - deltaTime);
            hadMovementInputLastFrame = moveInput.sqrMagnitude > movementInputDeadZone * movementInputDeadZone;
            return;
        }

        bool noMovementInput = moveInput.sqrMagnitude <= movementInputDeadZone * movementInputDeadZone;
        bool hasMovementInput = !noMovementInput;
        float animatorInputDeadZone = Mathf.Max(0f, movementInputDeadZone * 0.35f);
        bool hasAnimatorMovementInput = moveInput.sqrMagnitude > animatorInputDeadZone * animatorInputDeadZone;
        bool noAnimatorMovementInput = !hasAnimatorMovementInput;
        float planarSpeed = planarVelocity.magnitude;
        bool canUseDisplacementSpeedFallback = groundedNow
            && !wallHangActive
            && !ledgeHangActive
            && !ledgeClimbActive
            && currentState != MovementState.Dashing
            && currentState != MovementState.GroundPound;
        if (canUseDisplacementSpeedFallback)
        {
            planarSpeed = Mathf.Max(planarSpeed, displacementPlanarSpeed);
        }

        float stopReleasePlanarSpeed = Mathf.Max(0.1f, Mathf.Max(planarStopSnapSpeed * 1.5f, moveSpeed * 0.04f));
        bool movingWhileStopCouldBeHeld = planarSpeed > stopReleasePlanarSpeed
            || (canUseDisplacementSpeedFallback && displacementPlanarSpeed > stopReleasePlanarSpeed);
        if (hasMovementInput || !isGrounded || currentState != MovementState.Grounded || movingWhileStopCouldBeHeld)
        {
            stopAnimationTimer = 0f;
        }
        else if (!hasMovementInput && hadMovementInputLastFrame && stopPrimedFromAnimatorSpeed && sprintHeld)
        {
            stopAnimationTimer = Mathf.Max(stopAnimationTimer, stopAnimationHoldTime);
            stopPrimedFromAnimatorSpeed = false;
        }

        if (stopPrimedFromAnimatorSpeed && movingWhileStopCouldBeHeld)
        {
            stopPrimedFromAnimatorSpeed = false;
        }

        float zeroEpsilon = Mathf.Max(0f, animatorSpeedZeroEpsilon);
        if (isGrounded && noMovementInput && planarSpeed <= Mathf.Max(zeroEpsilon, planarStopSnapSpeed))
        {
            planarSpeed = 0f;
        }
        else if (planarSpeed <= zeroEpsilon)
        {
            planarSpeed = 0f;
        }

        bool hardStop = isGrounded && noAnimatorMovementInput && planarSpeed <= Mathf.Max(zeroEpsilon, planarStopSnapSpeed);

        float speed01 = ComputeAnimatorSpeed01(planarSpeed, noMovementInput);
        bool dashActive = dashAnimatorActive;
        bool forceRunSpeedForStandstillDash = dashActive && dashStartedFromStandstill;
        if (dashActive)
        {
            speed01 = Mathf.Clamp01(Mathf.Max(1f, animDashSpeedValue));
        }
        else if (isGrounded && hasAnimatorMovementInput)
        {
            float inputMagnitude = Mathf.Clamp01(moveInput.magnitude);
            float inputDrivenSpeed01 = inputMagnitude * 0.5f;
            if (enableSprint && sprintHeld)
            {
                inputDrivenSpeed01 = Mathf.Lerp(0.5f, 1f, inputMagnitude);
            }

            if (currentState == MovementState.Crouching)
            {
                inputDrivenSpeed01 *= 0.5f;
            }

            speed01 = Mathf.Max(speed01, inputDrivenSpeed01);
        }

        InitializeRuntimeAnimatorFloatState(speed01, planarSpeed, verticalVelocity);

        float linearBoost = Mathf.Max(1f, animSpeedSmoothness);
        // Update the Speed parameter with a linear but adjustable smoother transition.
        float speedLinearScale = Mathf.Clamp(animSpeedLinearRateScale, 0.05f, 1f);
        float speedLinearRiseRate = Mathf.Max(0f, animSpeedLinearRise) * speedLinearScale;
        float speedLinearFallRate = Mathf.Max(0f, animSpeedLinearFall) * speedLinearScale;
        bool linearStopActive = stopAnimationTimer > 0f && isGrounded && noMovementInput;
        bool moveStartBoostActive = hasAnimatorMovementInput
            && runtimeAnimSpeedValue <= Mathf.Max(zeroEpsilon, 0.001f)
            && speed01 > Mathf.Max(zeroEpsilon, 0.001f);
        if (moveStartBoostActive)
        {
            speedLinearRiseRate = Mathf.Max(speedLinearRiseRate, Mathf.Max(0f, animSpeedLinearRise) * 2.6f);
        }

        float moveLinearRiseRate = Mathf.Max(0f, animMoveSpeedLinearRise) * linearBoost;
        float moveLinearFallRate = Mathf.Max(0f, animMoveSpeedLinearFall) * linearBoost;
        float verticalLinearRate = Mathf.Max(0f, animVerticalSpeedLinearRate) * linearBoost;
        UpdateAnimatorPlaybackSpeed(dashActive, deltaTime);
        runtimeAnimSpeedValue = MoveAnimatorFloatLinear(
            runtimeAnimSpeedValue,
            speed01,
            speedLinearRiseRate,
            speedLinearFallRate,
            deltaTime);
        if (forceRunSpeedForStandstillDash)
        {
            runtimeAnimSpeedValue = Mathf.Max(runtimeAnimSpeedValue, 1f);
        }

        runtimeAnimMoveSpeedValue = MoveAnimatorFloatLinear(
            runtimeAnimMoveSpeedValue,
            planarSpeed,
            moveLinearRiseRate,
            moveLinearFallRate,
            deltaTime);
        runtimeAnimVerticalSpeedValue = verticalLinearRate > 0f
            ? Mathf.MoveTowards(runtimeAnimVerticalSpeedValue, verticalVelocity, verticalLinearRate * deltaTime)
            : verticalVelocity;

        bool allowHardSpeedSnap = hardStop && !linearStopActive;
        if (!forceRunSpeedForStandstillDash
            && (allowHardSpeedSnap || (noAnimatorMovementInput && Mathf.Abs(runtimeAnimSpeedValue) <= zeroEpsilon)))
        {
            runtimeAnimSpeedValue = 0f;
        }

        float moveSnapThreshold = Mathf.Max(zeroEpsilon, planarStopSnapSpeed);
        if (hardStop || runtimeAnimMoveSpeedValue <= moveSnapThreshold)
        {
            runtimeAnimMoveSpeedValue = 0f;
        }

        if (isGrounded && Mathf.Abs(runtimeAnimVerticalSpeedValue) <= 0.001f)
        {
            runtimeAnimVerticalSpeedValue = 0f;
        }

        // Keep Speed parameter transitions purely linear.
        float previousAnimatorSpeed = animatorSpeedSmoothed;
        animatorSpeedSmoothed = runtimeAnimSpeedValue;
        if (forceRunSpeedForStandstillDash)
        {
            animatorSpeedSmoothed = 1f;
        }

        if (noAnimatorMovementInput && Mathf.Abs(animatorSpeedSmoothed) <= zeroEpsilon)
        {
            animatorSpeedSmoothed = 0f;
        }

        float animatorSpeedDelta = animatorSpeedSmoothed - previousAnimatorSpeed;
        bool animatorSpeedIsFalling = animatorSpeedDelta < -0.0001f;
        bool animatorSpeedIsRising = animatorSpeedDelta > 0.0001f;
        float stopArmThreshold = Mathf.Clamp01(stopArmAnimatorSpeedThreshold);
        float stopTriggerThreshold = Mathf.Clamp(stopTriggerAnimatorSpeedThreshold, 0f, stopArmThreshold);
        float sprintPrimeMinSpeed = Mathf.Max(0f, stopSprintPrimeMinPlanarSpeed);
        float sprintReleaseSpeed01Threshold = Mathf.Clamp01(stopSprintReleaseTriggerSpeed01);
        if (!isGrounded || currentState != MovementState.Grounded)
        {
            stopPrimedFromAnimatorSpeed = false;
        }
        else
        {
            if (!sprintHeld)
            {
                // Shift birakildiginda stop prime korunmasin; sonradan durunca stop tetiklenmesin.
                stopPrimedFromAnimatorSpeed = false;
            }

            bool hasRunLevelSpeed = animatorSpeedSmoothed > 0.5f
                || planarSpeed > Mathf.Max(sprintPrimeMinSpeed, moveSpeed * 1.02f);
            bool canPrimeFromSprint = sprintHeld && !noMovementInput && hasRunLevelSpeed;
            if (!stopPrimedFromAnimatorSpeed && canPrimeFromSprint)
            {
                stopPrimedFromAnimatorSpeed = true;
            }
            else if (!stopPrimedFromAnimatorSpeed && sprintHeld && animatorSpeedSmoothed > stopArmThreshold)
            {
                // Legacy fallback: eski animator-speed tabanli prime davranisini koru.
                stopPrimedFromAnimatorSpeed = true;
            }

            if (stopPrimedFromAnimatorSpeed && noMovementInput)
            {
                if (!sprintHeld)
                {
                    stopPrimedFromAnimatorSpeed = false;
                }
                else
                {
                    const float directSprintStopSpeed01Threshold = 0.9f;
                    bool reachedDirectSprintStopThreshold = animatorSpeedSmoothed <= directSprintStopSpeed01Threshold;
                    bool reachedSprintReleaseThreshold = reachedDirectSprintStopThreshold
                        || animatorSpeedSmoothed <= sprintReleaseSpeed01Threshold
                        || planarSpeed <= sprintPrimeMinSpeed;
                    bool reachedLegacyTriggerThreshold = animatorSpeedIsFalling && animatorSpeedSmoothed <= stopTriggerThreshold;
                    if (reachedSprintReleaseThreshold || reachedLegacyTriggerThreshold)
                    {
                        stopAnimationTimer = Mathf.Max(stopAnimationTimer, stopAnimationHoldTime);
                        stopPrimedFromAnimatorSpeed = false;
                    }
                }
            }
            else if (stopPrimedFromAnimatorSpeed && animatorSpeedIsRising)
            {
                stopPrimedFromAnimatorSpeed = false;
            }
        }

        float speedHardSnapEpsilon = noAnimatorMovementInput ? zeroEpsilon : -1f;
        SetAnimatorFloat(animSpeedParam, animatorSpeedSmoothed, speedHardSnapEpsilon);
        SetAnimatorFloat(animMoveSpeedParam, runtimeAnimMoveSpeedValue);
        SetAnimatorFloat(animVerticalSpeedParam, runtimeAnimVerticalSpeedValue);
        SetAnimatorBool(animGroundedParam, isGrounded);
        SetAnimatorBool(animHangParam, hangAnimatorActive);
        SetAnimatorBool(animHangLeftParam, hangLeftAnimatorActive);
        SetAnimatorBool(animHangRightParam, hangRightAnimatorActive);
        SetAnimatorBool(animClimbParam, climbAnimatorActive);
        SetAnimatorBool(animClimbJumpParam, climbJumpAnimatorFlag);
        bool landingPulseActive = landingAnimatorPulseTimer > 0f
            && groundedNow;
        SetAnimatorBool(resolvedLandParam, landingPulseActive);
        bool crouchAnimatorActive = isGrounded
            && currentState == MovementState.Crouching;
        SetAnimatorBool(animCrouchParam, crouchAnimatorActive);
        SetAnimatorBool(animSlideParam, currentState == MovementState.Sliding || currentState == MovementState.SlopeSliding);
        SetAnimatorBool(animDashParam, dashAnimatorActive);
        SetAnimatorBool(animGroundPoundParam, currentState == MovementState.GroundPound);
        SetAnimatorBool(animHardFallParam, groundPoundHardFallActive && !groundPoundHardLandActive);
        SetAnimatorBool(animHardLandParam, groundPoundHardLandActive);
        SetAnimatorBool(animDiveParam, currentState == MovementState.Dive);
        SetAnimatorBool(animRollParam, rollAnimatorActive);
        SetAnimatorBool(animTeeterParam, teeterAnimatorActive);
        SetAnimatorInt(animStateParam, (int)currentState);

        bool stopTimerWasActive = stopAnimationTimer > 0f;
        if (stopTimerWasActive)
        {
            stopAnimationTimer = Mathf.Max(0f, stopAnimationTimer - deltaTime);
        }

        if (stopTimerWasActive
            && stopAnimationTimer <= 0f
            && isGrounded
            && currentState == MovementState.Grounded
            && noMovementInput)
        {
            planarVelocity = Vector3.zero;
            runStopSlideTimer = 0f;
        }

        if (isGrounded && currentState != MovementState.Jumping && currentState != MovementState.Falling)
        {
            // Close jump/frontflip/backflip params on land even when controller uses Bool instead of Trigger.
            ResetAnimatorTrigger(animJumpTrigger);
            ResetAnimatorTrigger(animFrontflipTrigger);
            ResetAnimatorTrigger(animBackflipTrigger);
        }

        SetAnimatorBoolOrTriggerOnChange(animJumpTrigger, jumpAnimatorActive, ref jumpAnimatorWasActive);
        SetAnimatorBoolOrTriggerOnChange(animBackflipTrigger, backflipAnimatorHoldActive, ref backflipAnimatorWasActive);

        bool stopByTimer = stopAnimationTimer > 0f;
        bool stopByAnimationState = false;
        if (!stopByTimer
            && holdStopBoolUntilAnimationEnds
            && noMovementInput
            && isGrounded
            && currentState == MovementState.Grounded
            && planarSpeed <= stopReleasePlanarSpeed
            && HasAnimatorParameter(animStopParam, AnimatorControllerParameterType.Bool))
        {
            stopByAnimationState = IsStopAnimationStateStillPlaying();
        }

        bool stopAnimatorActive = stopByTimer || stopByAnimationState;
        SetAnimatorBoolOrTriggerOnChange(animStopParam, stopAnimatorActive, ref stopAnimatorWasActive);
        UpdateFallAnimatorStateByMovementState();
        ForceClearAnimatorActionBoolsOnStableGround();
        ForceGroundedAnimatorFalseStates();
        if (landingPulseActive)
        {
            TrySetAnimatorBoolDirect(resolvedLandParam, true);
        }
        landingAnimatorPulseTimer = Mathf.Max(0f, landingAnimatorPulseTimer - deltaTime);
        hadMovementInputLastFrame = hasMovementInput;
    }

    private bool IsHangAnimatorStateValid()
    {
        if (!wallHangActive || ledgeClimbActive || isGrounded)
        {
            return false;
        }

        if (controller == null)
        {
            return wallHangActive;
        }

        if (!TryGetWallHangNormal(out Vector3 activeWallNormal, true))
        {
            return false;
        }

        return IsFacingTowardWallForWallHangEntry(activeWallNormal);
    }

    private bool ShouldForceHangAnimatorDuringWallTraversal()
    {
        if (isGrounded)
        {
            return false;
        }

        // Keep the Hang bool enabled while wall traversal is active (hang, ledge, climb-jump).
        if (wallHangActive || ledgeHangActive || ledgeClimbActive || climbJumpAnimatorActive)
        {
            return true;
        }

        return wallJumpForwardBackLockActive;
    }

    private void ClearHangAnimatorBoolsImmediate()
    {
        TrySetAnimatorBoolDirect(animHangParam, false);
        TrySetAnimatorBoolDirect(animHangLeftParam, false);
        TrySetAnimatorBoolDirect(animHangRightParam, false);
    }

    private bool ShouldForceClearAnimatorActionBoolsOnStableGround()
    {
        bool groundedNow = isGrounded || (controller != null && controller.isGrounded);
        if (!groundedNow)
        {
            return false;
        }

        // Force cleanup only when the character is stable on the ground after action transition effects finish.
        if (currentState != MovementState.Grounded && currentState != MovementState.Crouching)
        {
            return false;
        }

        if (dashTimer > 0f
            || slideTimer > 0f
            || rollTimer > 0f
            || diveTimer > 0f
            || jumpHoldTimer > 0f
            || groundPoundDelayTimer > 0f
            || landingAnimatorPulseTimer > 0f)
        {
            return false;
        }

        if (wallHangActive
            || ledgeHangActive
            || ledgeClimbActive
            || climbJumpAnimatorActive
            || backflipAnimatorHoldActive
            || groundPoundHardFallActive
            || groundPoundHardLandActive
            || jumpConsumed)
        {
            return false;
        }

        return true;
    }

    private void ForceClearAnimatorActionBoolsOnStableGround()
    {
        if (!ShouldForceClearAnimatorActionBoolsOnStableGround())
        {
            return;
        }

        TrySetAnimatorBoolDirect(animHangParam, false);
        TrySetAnimatorBoolDirect(animHangLeftParam, false);
        TrySetAnimatorBoolDirect(animHangRightParam, false);
        TrySetAnimatorBoolDirect(animClimbParam, false);
        TrySetAnimatorBoolDirect(animClimbJumpParam, false);
        TrySetAnimatorBoolDirect(animFallParam, false);
        TrySetAnimatorBoolDirect(animLandParam, false);
        TrySetAnimatorBoolDirect(animSlideParam, false);
        TrySetAnimatorBoolDirect(animDashParam, false);
        TrySetAnimatorBoolDirect(animGroundPoundParam, false);
        TrySetAnimatorBoolDirect(animHardFallParam, false);
        TrySetAnimatorBoolDirect(animHardLandParam, false);
        TrySetAnimatorBoolDirect(animDiveParam, false);
        TrySetAnimatorBoolDirect(animRollParam, false);
        TrySetAnimatorBoolDirect(animTeeterParam, false);
        TrySetAnimatorBoolDirect(animJumpTrigger, false);
        TrySetAnimatorBoolDirect(animFrontflipTrigger, false);
        TrySetAnimatorBoolDirect(animBackflipTrigger, false);

        if (useAnimatorParameters)
        {
            ResetAnimatorTrigger(animJumpTrigger);
            ResetAnimatorTrigger(animFrontflipTrigger);
            ResetAnimatorTrigger(animBackflipTrigger);
            ResetAnimatorTrigger(animLandParam);
            ResetAnimatorTrigger(animDashParam);
            ResetAnimatorTrigger(animGroundPoundParam);
            ResetAnimatorTrigger(animSlideParam);
            ResetAnimatorTrigger(animDiveParam);
            ResetAnimatorTrigger(animRollParam);
            ResetAnimatorTrigger(animTeeterParam);
        }

        climbJumpAnimatorActive = false;
        forceFallAnimatorFromClimbJump = false;
        backflipAnimatorHoldActive = false;
        jumpAnimatorWasActive = false;
        backflipAnimatorWasActive = false;
    }

    private void ForceGroundedAnimatorFalseStates()
    {
        bool groundedNow = isGrounded || (controller != null && controller.isGrounded);
        if (!groundedNow)
        {
            return;
        }

        bool keepTeeterAnimatorActive = IsEdgeAnimatorActive();
        bool crouchAnimatorActive = currentState == MovementState.Crouching;

        // Hard rule: while grounded, these action bools/triggers must always stay off.
        TrySetAnimatorBoolDirect(animCrouchParam, crouchAnimatorActive);
        TrySetAnimatorBoolDirect(animFrontflipTrigger, false);
        TrySetAnimatorBoolDirect(animBackflipTrigger, false);
        TrySetAnimatorBoolDirect(animFallParam, false);
        if (!keepTeeterAnimatorActive)
        {
            TrySetAnimatorBoolDirect(animTeeterParam, false); // Edge
        }
        TrySetAnimatorBoolDirect(animHangParam, false);
        TrySetAnimatorBoolDirect(animHangLeftParam, false);
        TrySetAnimatorBoolDirect(animHangRightParam, false);
        TrySetAnimatorBoolDirect(animClimbParam, false);
        TrySetAnimatorBoolDirect(animClimbJumpParam, false);

        if (useAnimatorParameters)
        {
            ResetAnimatorTrigger(animFrontflipTrigger);
            ResetAnimatorTrigger(animBackflipTrigger);
        }

    }

    private void UpdateAnimatorPlaybackSpeed(bool dashActive, float deltaTime)
    {
        if (runtimeAnimator == null)
        {
            return;
        }

        float normalPlaybackSpeed = Mathf.Max(0.01f, animatorNormalPlaybackSpeed);
        float dashPlaybackSpeed = Mathf.Max(normalPlaybackSpeed, Mathf.Max(0.01f, animatorDashPlaybackSpeed));
        float targetPlaybackSpeed = dashActive ? dashPlaybackSpeed : normalPlaybackSpeed;
        runtimeAnimatorPlaybackSpeed = MoveAnimatorFloatLinear(
            runtimeAnimatorPlaybackSpeed,
            targetPlaybackSpeed,
            animatorPlaybackSpeedLinearRise,
            animatorPlaybackSpeedLinearFall,
            deltaTime);
        runtimeAnimator.speed = runtimeAnimatorPlaybackSpeed;
    }

    private float ComputeAnimatorSpeed01(float planarSpeed, bool noMovementInput)
    {
        float speed = planarSpeed;
        if (isGrounded && noMovementInput && speed <= Mathf.Max(animatorSpeedZeroEpsilon, planarStopSnapSpeed))
        {
            return 0f;
        }

        if (speed <= Mathf.Max(0f, animatorSpeedZeroEpsilon))
        {
            return 0f;
        }

        float walkSpeed = Mathf.Max(0.01f, moveSpeed);
        if (currentState == MovementState.Crouching)
        {
            float crouchTopSpeed = Mathf.Max(0.01f, walkSpeed * Mathf.Max(0.01f, crouchWalkSpeedMultiplier));
            float crouch01 = Mathf.Clamp01(speed / crouchTopSpeed);
            return crouch01 * 0.5f;
        }

        float runSpeed = Mathf.Max(walkSpeed, walkSpeed * Mathf.Max(1f, sprintSpeedMultiplier));
        if (speed <= walkSpeed || runSpeed <= walkSpeed + 0.0001f)
        {
            float walk01 = Mathf.Clamp01(speed / walkSpeed);
            return walk01 * 0.5f;
        }

        float run01 = Mathf.Clamp01((speed - walkSpeed) / Mathf.Max(0.0001f, runSpeed - walkSpeed));
        return Mathf.Lerp(0.5f, 1f, run01);
    }

    private void InitializeRuntimeAnimatorFloatState(float speedValue, float moveSpeedValue, float verticalSpeedValue)
    {
        if (runtimeAnimFloatInitialized || runtimeAnimator == null)
        {
            return;
        }

        runtimeAnimSpeedValue = HasAnimatorParameter(animSpeedParam, AnimatorControllerParameterType.Float)
            ? runtimeAnimator.GetFloat(animSpeedParam)
            : speedValue;
        runtimeAnimMoveSpeedValue = HasAnimatorParameter(animMoveSpeedParam, AnimatorControllerParameterType.Float)
            ? runtimeAnimator.GetFloat(animMoveSpeedParam)
            : moveSpeedValue;
        runtimeAnimVerticalSpeedValue = HasAnimatorParameter(animVerticalSpeedParam, AnimatorControllerParameterType.Float)
            ? runtimeAnimator.GetFloat(animVerticalSpeedParam)
            : verticalSpeedValue;
        runtimeAnimFloatInitialized = true;
    }

    private static float MoveAnimatorFloatLinear(float current, float target, float riseRate, float fallRate, float deltaTime)
    {
        float rate = target >= current ? Mathf.Max(0f, riseRate) : Mathf.Max(0f, fallRate);
        if (rate <= 0f)
        {
            return target;
        }

        return Mathf.MoveTowards(current, target, rate * deltaTime);
    }

    private static float EvaluateEaseInCirc01(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
    }

    private void SetAnimatorFloat(string parameter, float value, float hardSnapEpsilon = -1f)
    {
        if (HasAnimatorParameter(parameter, AnimatorControllerParameterType.Float))
        {
            if (hardSnapEpsilon >= 0f && Mathf.Abs(value) <= hardSnapEpsilon)
            {
                runtimeAnimator.SetFloat(parameter, 0f);
                return;
            }

            runtimeAnimator.SetFloat(parameter, value);
        }
    }

    private void SetAnimatorBool(string parameter, bool value)
    {
        if (TrySetAnimatorBool(parameter, value))
        {
            return;
        }
    }

    private void ApplyJumpMirrorAnimatorValue(bool value)
    {
        if (runtimeAnimator == null || string.IsNullOrWhiteSpace(animJumpMirrorParam))
        {
            return;
        }

        if (useAnimatorParameters)
        {
            TrySetAnimatorBool(animJumpMirrorParam, value);
            return;
        }

        TrySetAnimatorBoolDirect(animJumpMirrorParam, value);
    }

    private void ApplyDashMirrorAnimatorValue(bool value)
    {
        if (runtimeAnimator == null || string.IsNullOrWhiteSpace(animDashMirrorParam))
        {
            return;
        }

        if (useAnimatorParameters)
        {
            TrySetAnimatorBool(animDashMirrorParam, value);
            return;
        }

        TrySetAnimatorBoolDirect(animDashMirrorParam, value);
    }

    private void UpdateFallAnimatorStateByMovementState()
    {
        bool groundedNow = isGrounded || (controller != null && controller.isGrounded);
        if (groundedNow)
        {
            // Hard rule: Fall stays off whenever there is ground contact.
            SetFallAnimatorState(false);
            return;
        }

        SetFallAnimatorState(IsFallAnimatorStateActive());
    }

    private void SetFallAnimatorState(bool value)
    {
        bool groundedNow = isGrounded || (controller != null && controller.isGrounded);
        if (groundedNow)
        {
            value = false;
        }

        TrySetAnimatorBoolDirect(animFallParam, value);
    }

    private bool TrySetAnimatorBool(string parameter, bool value)
    {
        if (!useAnimatorParameters)
        {
            return false;
        }

        return TrySetAnimatorBoolDirect(parameter, value);
    }

    private bool IsStopAnimationStateStillPlaying()
    {
        if (runtimeAnimator == null
            || string.IsNullOrWhiteSpace(stopAnimationStateTag)
            || runtimeAnimator.layerCount <= 0)
        {
            return false;
        }

        string stateTag = stopAnimationStateTag.Trim();
        if (stateTag.Length == 0)
        {
            return false;
        }

        int layerIndex = Mathf.Clamp(stopAnimationLayerIndex, 0, runtimeAnimator.layerCount - 1);
        float endThreshold = Mathf.Clamp01(stopAnimationEndNormalizedTime);
        AnimatorStateInfo currentStateInfo = runtimeAnimator.GetCurrentAnimatorStateInfo(layerIndex);
        if (currentStateInfo.IsTag(stateTag) && currentStateInfo.normalizedTime < endThreshold)
        {
            return true;
        }

        if (runtimeAnimator.IsInTransition(layerIndex))
        {
            AnimatorStateInfo nextStateInfo = runtimeAnimator.GetNextAnimatorStateInfo(layerIndex);
            if (nextStateInfo.IsTag(stateTag))
            {
                return true;
            }
        }

        return false;
    }

    private bool TrySetAnimatorBoolDirect(string parameter, bool value)
    {
        if (runtimeAnimator == null || string.IsNullOrWhiteSpace(parameter))
        {
            return false;
        }

        string resolvedParameter = parameter.Trim();
        if (!HasAnimatorParameter(resolvedParameter, AnimatorControllerParameterType.Bool))
        {
            return false;
        }

        runtimeAnimator.SetBool(resolvedParameter, value);
        return true;
    }

    private void SetAnimatorBoolOrTrigger(string parameter, bool value)
    {
        if (!useAnimatorParameters || !CanAccessAnimatorParameters() || string.IsNullOrEmpty(parameter))
        {
            return;
        }

        if (!animatorParameterTypes.TryGetValue(parameter, out AnimatorControllerParameterType type))
        {
            return;
        }

        if (type == AnimatorControllerParameterType.Bool)
        {
            runtimeAnimator.SetBool(parameter, value);
            return;
        }

        if (type == AnimatorControllerParameterType.Trigger)
        {
            if (value)
            {
                runtimeAnimator.SetTrigger(parameter);
            }
            else
            {
                runtimeAnimator.ResetTrigger(parameter);
            }
        }
    }

    private void SetAnimatorBoolOrTriggerOnChange(string parameter, bool value, ref bool previousValue)
    {
        if (!useAnimatorParameters || !CanAccessAnimatorParameters() || string.IsNullOrEmpty(parameter))
        {
            previousValue = value;
            return;
        }

        if (!animatorParameterTypes.TryGetValue(parameter, out AnimatorControllerParameterType type))
        {
            previousValue = value;
            return;
        }

        if (type == AnimatorControllerParameterType.Bool)
        {
            runtimeAnimator.SetBool(parameter, value);
            previousValue = value;
            return;
        }

        if (type == AnimatorControllerParameterType.Trigger)
        {
            if (value && !previousValue)
            {
                runtimeAnimator.SetTrigger(parameter);
            }
            else if (!value && previousValue)
            {
                runtimeAnimator.ResetTrigger(parameter);
            }
        }

        previousValue = value;
    }

    private void SetAnimatorInt(string parameter, int value)
    {
        if (HasAnimatorParameter(parameter, AnimatorControllerParameterType.Int))
        {
            runtimeAnimator.SetInteger(parameter, value);
        }
    }

    private void TriggerAnimator(string triggerName)
    {
        TryTriggerAnimatorParameter(triggerName);
    }

    private bool TryTriggerAnimatorParameter(string triggerName)
    {
        if (!useAnimatorParameters || !CanAccessAnimatorParameters() || string.IsNullOrEmpty(triggerName))
        {
            return false;
        }

        if (!animatorParameterTypes.TryGetValue(triggerName, out AnimatorControllerParameterType type))
        {
            return false;
        }

        if (type == AnimatorControllerParameterType.Trigger)
        {
            runtimeAnimator.SetTrigger(triggerName);
            return true;
        }

        if (type == AnimatorControllerParameterType.Bool)
        {
            runtimeAnimator.SetBool(triggerName, true);
            return true;
        }

        return false;
    }

    private void ResetAnimatorTrigger(string triggerName)
    {
        if (!useAnimatorParameters || !CanAccessAnimatorParameters() || string.IsNullOrEmpty(triggerName))
        {
            return;
        }

        if (!animatorParameterTypes.TryGetValue(triggerName, out AnimatorControllerParameterType type))
        {
            return;
        }

        if (type == AnimatorControllerParameterType.Trigger)
        {
            runtimeAnimator.ResetTrigger(triggerName);
        }
        else if (type == AnimatorControllerParameterType.Bool)
        {
            runtimeAnimator.SetBool(triggerName, false);
        }
    }

    private void UpdateFeedback(float deltaTime)
    {
        cameraKick = Mathf.MoveTowards(cameraKick, 0f, cameraKickRecoverSpeed * deltaTime);

        if (!enableFeedback || !enableFootsteps || !isGrounded || runtimeAudioSource == null || footstepClip == null)
        {
            return;
        }

        if (currentState == MovementState.Sliding
            || currentState == MovementState.SlopeSliding
            || currentState == MovementState.Dashing
            || currentState == MovementState.GroundPound
            || currentState == MovementState.Rolling
            || currentState == MovementState.Dive
            || currentState == MovementState.Teetering)
        {
            footstepTimer = footstepBaseInterval;
            return;
        }

        float speed = planarVelocity.magnitude;
        if (speed < footstepMinSpeed)
        {
            footstepTimer = footstepBaseInterval;
            return;
        }

        footstepTimer -= deltaTime;
        if (footstepTimer > 0f)
        {
            return;
        }

        float intervalScale = Mathf.Lerp(1.1f, 0.6f, Mathf.Clamp01(speed / Mathf.Max(0.01f, moveSpeed)));
        footstepTimer = footstepBaseInterval * intervalScale;
        PlayClip(footstepClip, 0.7f);
    }

    private void UpdateParticleRuntime()
    {
        EnsureWalkingParticleForGroundedState();
    }

    private void EnsureWalkingParticleForGroundedState()
    {
        bool groundedNow = isGrounded || (controller != null && controller.isGrounded);
        if (walkingParticlePrefab == null || !groundedNow)
        {
            DestroyWalkingParticleInstance();
            return;
        }

        if (!ShouldPlayWalkingParticle())
        {
            if (walkingParticleInstance != null && walkingParticleInstance.isPlaying)
            {
                // Crouch/stand gecislerinde onceki parcaciklarin kuyrugu birikmesin.
                walkingParticleInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            return;
        }

        if (walkingParticleInstance == null)
        {
            walkingParticleInstance = SpawnParticleInstance(walkingParticlePrefab);
        }

        if (walkingParticleInstance == null)
        {
            return;
        }

        if (!walkingParticleInstance.gameObject.activeSelf)
        {
            walkingParticleInstance.gameObject.SetActive(true);
        }

        if (!walkingParticleInstance.isPlaying)
        {
            walkingParticleInstance.Play(true);
        }
    }

    private bool ShouldPlayWalkingParticle()
    {
        const float runParticleSpeedThreshold = 0.99f;
        const float runParticlePlanarSpeedMultiplier = 1.02f;
        bool hasMovementInput = moveInput.sqrMagnitude > movementInputDeadZone * movementInputDeadZone;
        if (!hasMovementInput)
        {
            return false;
        }

        if (currentState == MovementState.Crouching
            || currentState == MovementState.Sliding
            || currentState == MovementState.SlopeSliding
            || currentState == MovementState.Rolling)
        {
            return false;
        }

        float animatorSpeed = animatorSpeedSmoothed;
        if (runtimeAnimator != null && HasAnimatorParameter(animSpeedParam, AnimatorControllerParameterType.Float))
        {
            animatorSpeed = runtimeAnimator.GetFloat(animSpeedParam);
        }

        if (animatorSpeed >= runParticleSpeedThreshold)
        {
            return true;
        }

        // Animator Speed can briefly stay below 1 when exiting crouch.
        bool runIntentActive = enableSprint && sprintHeld;
        float runSpeedFloor = Mathf.Max(0.05f, moveSpeed * runParticlePlanarSpeedMultiplier);
        bool hasRunLevelPlanarSpeed = planarVelocity.sqrMagnitude >= runSpeedFloor * runSpeedFloor;
        return runIntentActive && hasRunLevelPlanarSpeed;
    }

    private void DestroyWalkingParticleInstance()
    {
        if (walkingParticleInstance == null)
        {
            return;
        }

        GameObject instanceObject = walkingParticleInstance.gameObject;
        walkingParticleInstance = null;
        if (instanceObject != null)
        {
            Destroy(instanceObject);
        }
    }

    private void SpawnLandingParticle()
    {
        if (landingParticlePrefab == null)
        {
            return;
        }

        ParticleSystem landingInstance = SpawnParticleInstance(landingParticlePrefab);
        if (landingInstance == null)
        {
            return;
        }

        landingInstance.Play(true);
    }

    private ParticleSystem SpawnParticleInstance(ParticleSystem prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        ParticleSystem instance = Instantiate(prefab, transform);
        if (instance == null)
        {
            return null;
        }

        Transform instanceTransform = instance.transform;
        instanceTransform.localPosition = particleLocalPosition;
        instanceTransform.localRotation = Quaternion.identity;
        if (!instance.gameObject.activeSelf)
        {
            instance.gameObject.SetActive(true);
        }

        return instance;
    }

    private void PlayClip(AudioClip clip, float volumeScale = 1f)
    {
        if (!enableFeedback || runtimeAudioSource == null || clip == null)
        {
            return;
        }

        runtimeAudioSource.PlayOneShot(clip, feedbackVolume * Mathf.Clamp01(volumeScale));
    }

    private void AddCameraKick(float amount)
    {
        if (!enableFeedback || amount <= 0f)
        {
            return;
        }

        cameraKick += amount;
        AddCameraShake(amount * 0.55f);
    }

    private void AddCameraShake(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        cameraShakeAmplitude = Mathf.Clamp(cameraShakeAmplitude + amount, 0f, cameraShakeMaxAmplitude);
    }

    private void ResetAirJumps()
    {
        remainingAirJumps = Mathf.Max(0, maxAirJumps);
    }

    private void ApplyCursorState()
    {
        if (!hideAndLockCursor)
        {
            cursorUnlockedByEscape = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (unlockCursorWithEscape && MinimoInputBridge.GetKeyDown(KeyCode.Escape))
        {
            cursorUnlockedByEscape = !cursorUnlockedByEscape;
        }

        if (cursorUnlockedByEscape)
        {
            if (MinimoInputBridge.GetMouseButtonDown(0))
            {
                cursorUnlockedByEscape = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private Vector3 GetCameraPivot()
    {
        return transform.position + Vector3.up * pivotHeight;
    }

    private float NormalizePitch(float pitchAngle)
    {
        if (pitchAngle > 180f)
        {
            pitchAngle -= 360f;
        }

        return Mathf.Clamp(pitchAngle, minPitch, maxPitch);
    }

    private bool IsBuffered(float pressedTime, float bufferWindow)
    {
        return Time.time - pressedTime <= bufferWindow;
    }

    private bool IsLegacyInputButtonDownSafe(string buttonName)
    {
        if (string.IsNullOrWhiteSpace(buttonName) || missingInputButtonNames.Contains(buttonName))
        {
            return false;
        }

        try
        {
            return MinimoInputBridge.GetButtonDown(buttonName);
        }
        catch (ArgumentException)
        {
            missingInputButtonNames.Add(buttonName);
            return false;
        }
    }

    private bool IsLegacyInputButtonHeldSafe(string buttonName)
    {
        if (string.IsNullOrWhiteSpace(buttonName) || missingInputButtonNames.Contains(buttonName))
        {
            return false;
        }

        try
        {
            return MinimoInputBridge.GetButton(buttonName);
        }
        catch (ArgumentException)
        {
            missingInputButtonNames.Add(buttonName);
            return false;
        }
    }

    private bool IsLegacyInputButtonUpSafe(string buttonName)
    {
        if (string.IsNullOrWhiteSpace(buttonName) || missingInputButtonNames.Contains(buttonName))
        {
            return false;
        }

        try
        {
            return MinimoInputBridge.GetButtonUp(buttonName);
        }
        catch (ArgumentException)
        {
            missingInputButtonNames.Add(buttonName);
            return false;
        }
    }

    private bool HasBufferedRollInput()
    {
        if (rollInputVersion <= consumedRollInputVersion)
        {
            return false;
        }

        return IsBuffered(lastRollPressedTime, rollBufferTime);
    }

    private bool IsRollInputHeld()
    {
        if (!enableDiveRoll)
        {
            return false;
        }

        bool keyboardHeld = rollKey != KeyCode.None && MinimoInputBridge.GetKey(rollKey);
        bool gamepadHeld = IsGamepadButtonHeld(gamepadRollButton);
        return keyboardHeld || gamepadHeld;
    }

    private static bool IsKeyboardJumpDown()
    {
        return MinimoInputBridge.GetKeyDown(KeyCode.Space);
    }

    private static bool IsKeyboardJumpHeld()
    {
        return MinimoInputBridge.GetKey(KeyCode.Space);
    }

    private static bool IsKeyboardJumpUp()
    {
        return MinimoInputBridge.GetKeyUp(KeyCode.Space);
    }

    private bool IsGamepadButtonDown(KeyCode gamepadButton)
    {
        if (!enableGamepadInput || gamepadButton == KeyCode.None)
        {
            return false;
        }

        EnsureGamepadButtonCacheCurrent();
        return gamepadButtonDownCache.TryGetValue(gamepadButton, out bool down) && down;
    }

    private bool IsGamepadButtonHeld(KeyCode gamepadButton)
    {
        if (!enableGamepadInput || gamepadButton == KeyCode.None)
        {
            return false;
        }

        EnsureGamepadButtonCacheCurrent();
        return gamepadButtonHeldCache.TryGetValue(gamepadButton, out bool held) && held;
    }

    private bool IsGamepadButtonUp(KeyCode gamepadButton)
    {
        if (!enableGamepadInput || gamepadButton == KeyCode.None)
        {
            return false;
        }

        EnsureGamepadButtonCacheCurrent();
        return gamepadButtonUpCache.TryGetValue(gamepadButton, out bool up) && up;
    }

    private void EnsureGamepadButtonCacheCurrent()
    {
        if (!enableGamepadInput)
        {
            ResetGamepadButtonCaches();
            return;
        }

        int frame = Time.frameCount;
        if (gamepadButtonCacheFrame == frame)
        {
            return;
        }

        gamepadButtonHeldCache.Clear();
        gamepadButtonDownCache.Clear();
        gamepadButtonUpCache.Clear();

        TrackGamepadButtonState(gamepadJumpButton);
        TrackGamepadButtonState(gamepadSprintButton);
        TrackGamepadButtonState(gamepadDashButton);
        TrackGamepadButtonState(gamepadGroundPoundButton);
        TrackGamepadButtonState(gamepadDiveButton);
        TrackGamepadButtonState(gamepadRollButton);
        TrackGamepadButtonState(gamepadCrouchButton);
        TrackGamepadButtonState(gamepadKillButton);

        gamepadButtonPreviousHeld.Clear();
        foreach (KeyValuePair<KeyCode, bool> entry in gamepadButtonHeldCache)
        {
            gamepadButtonPreviousHeld[entry.Key] = entry.Value;
        }

        gamepadButtonCacheFrame = frame;
    }

    private void TrackGamepadButtonState(KeyCode gamepadButton)
    {
        if (gamepadButton == KeyCode.None || gamepadButtonHeldCache.ContainsKey(gamepadButton))
        {
            return;
        }

        bool held = ReadRawGamepadButtonHeld(gamepadButton);
        bool wasHeld = gamepadButtonPreviousHeld.TryGetValue(gamepadButton, out bool previous) && previous;
        gamepadButtonHeldCache[gamepadButton] = held;
        gamepadButtonDownCache[gamepadButton] = held && !wasHeld;
        gamepadButtonUpCache[gamepadButton] = !held && wasHeld;
    }

    private bool ReadRawGamepadButtonHeld(KeyCode gamepadButton)
    {
        if (!enableGamepadInput || gamepadButton == KeyCode.None)
        {
            return false;
        }

        bool held = false;
        bool hasInputSystemGamepad = false;

#if ENABLE_INPUT_SYSTEM
        hasInputSystemGamepad = GetActiveGamepad() != null;
        if (hasInputSystemGamepad)
        {
            held = ReadInputSystemButton(gamepadButton, pressedThisFrame: false);
        }
#endif

        if (!hasInputSystemGamepad)
        {
            held = MinimoInputBridge.GetKey(gamepadButton);
        }

        return held;
    }

    private float ReadGamepadSprintTriggerAmount()
    {
        if (!enableGamepadInput || !enableSprint || !useGamepadLeftTriggerForSprint)
        {
            return 0f;
        }

        float raw = 0f;
        bool hasInputSystemGamepad = false;

#if ENABLE_INPUT_SYSTEM
        Gamepad pad = GetActiveGamepad();
        if (pad != null)
        {
            hasInputSystemGamepad = true;
            raw = Mathf.Max(raw, pad.rightTrigger.ReadValue());
        }
#endif

        if (!hasInputSystemGamepad && raw <= 0.0001f)
        {
            raw = Mathf.Max(raw, ReadAxisSafe(gamepadSprintAxis));
        }

        raw = Mathf.Clamp01(raw);
        float deadZone = Mathf.Clamp01(gamepadSprintDeadZone);
        if (raw <= deadZone)
        {
            return 0f;
        }

        return Mathf.InverseLerp(deadZone, 1f, raw);
    }

    private Vector2 ReadGamepadLookInput()
    {
        if (!enableGamepadInput)
        {
            return Vector2.zero;
        }

        Vector2 raw = Vector2.zero;
        bool hasInputSystemGamepad = false;

#if ENABLE_INPUT_SYSTEM
        Gamepad pad = GetActiveGamepad();
        if (pad != null)
        {
            hasInputSystemGamepad = true;
            raw = pad.rightStick.ReadValue();
        }
#endif

        if (!hasInputSystemGamepad && raw.sqrMagnitude <= 0.0001f)
        {
            float lookX = ReadAxisSafe(gamepadLookXAxis);
            float lookY = ReadAxisSafe(gamepadLookYAxis);
            raw = new Vector2(lookX, lookY);
        }

        float magnitude = raw.magnitude;
        float deadZone = Mathf.Clamp01(gamepadLookDeadZone);
        if (magnitude <= deadZone)
        {
            return Vector2.zero;
        }

        float scaledMagnitude = Mathf.InverseLerp(deadZone, 1f, Mathf.Clamp01(magnitude));
        return raw.normalized * scaledMagnitude;
    }

    private Vector2 ReadGamepadMoveInput()
    {
        if (!enableGamepadInput)
        {
            return Vector2.zero;
        }

        Vector2 raw = Vector2.zero;
        bool hasInputSystemGamepad = false;

#if ENABLE_INPUT_SYSTEM
        Gamepad pad = GetActiveGamepad();
        if (pad != null)
        {
            hasInputSystemGamepad = true;
            raw = pad.leftStick.ReadValue();
        }
#endif

        if (!hasInputSystemGamepad && raw.sqrMagnitude <= 0.0001f)
        {
            raw = new Vector2(ReadAxisSafe(gamepadMoveXAxis), ReadAxisSafe(gamepadMoveYAxis));
        }

        float magnitude = raw.magnitude;
        float deadZone = Mathf.Clamp01(gamepadMoveDeadZone);
        if (magnitude <= deadZone)
        {
            return Vector2.zero;
        }

        float scaledMagnitude = Mathf.InverseLerp(deadZone, 1f, Mathf.Clamp01(magnitude));
        return raw.normalized * scaledMagnitude;
    }

    private float ReadGamepadFreeCameraVerticalInput()
    {
        if (!enableGamepadInput)
        {
            return 0f;
        }

        float upAmount = 0f;
        float downAmount = 0f;
        bool hasInputSystemGamepad = false;

#if ENABLE_INPUT_SYSTEM
        Gamepad pad = GetActiveGamepad();
        if (pad != null)
        {
            hasInputSystemGamepad = true;
            upAmount = Mathf.Max(upAmount, pad.rightTrigger.ReadValue());
            downAmount = Mathf.Max(downAmount, pad.leftTrigger.ReadValue());
        }
#endif

        if (!hasInputSystemGamepad)
        {
            upAmount = Mathf.Max(upAmount, ReadAxisSafe("RightTrigger"));
            downAmount = Mathf.Max(downAmount, ReadAxisSafe("LeftTrigger"));
        }

        upAmount = ApplyGamepadFreeCameraTriggerDeadZone(upAmount);
        downAmount = ApplyGamepadFreeCameraTriggerDeadZone(downAmount);
        return Mathf.Clamp(upAmount - downAmount, -1f, 1f);
    }

    private static float ApplyGamepadFreeCameraTriggerDeadZone(float value)
    {
        float clampedValue = Mathf.Clamp01(value);
        if (clampedValue <= GamepadFreeCameraTriggerDeadZone)
        {
            return 0f;
        }

        return Mathf.InverseLerp(GamepadFreeCameraTriggerDeadZone, 1f, clampedValue);
    }

    private float ReadAxisSafe(string axisName)
    {
        if (string.IsNullOrWhiteSpace(axisName) || missingInputAxisNames.Contains(axisName))
        {
            return 0f;
        }

        try
        {
            return MinimoInputBridge.GetAxis(axisName);
        }
        catch (ArgumentException)
        {
            missingInputAxisNames.Add(axisName);
            return 0f;
        }
    }

    private bool HasConnectedGamepad()
    {
        if (!enableGamepadInput)
        {
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        if (GetActiveGamepad() != null)
        {
            return true;
        }
#endif

        string[] names = MinimoInputBridge.GetJoystickNames();
        for (int i = 0; i < names.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(names[i]))
            {
                return true;
            }
        }

        return false;
    }

#if ENABLE_INPUT_SYSTEM
    private static Gamepad GetActiveGamepad()
    {
        if (Gamepad.current != null)
        {
            return Gamepad.current;
        }

        return Gamepad.all.Count > 0 ? Gamepad.all[0] : null;
    }

    private static bool ReadInputSystemButton(KeyCode gamepadButton, bool pressedThisFrame)
    {
        Gamepad pad = GetActiveGamepad();
        if (pad == null)
        {
            return false;
        }

        ButtonControl button = gamepadButton switch
        {
            KeyCode.JoystickButton0 => pad.buttonSouth,
            KeyCode.JoystickButton1 => pad.buttonEast,
            KeyCode.JoystickButton2 => pad.buttonWest,
            KeyCode.JoystickButton3 => pad.buttonNorth,
            KeyCode.JoystickButton4 => pad.leftShoulder,
            KeyCode.JoystickButton5 => pad.rightShoulder,
            KeyCode.JoystickButton6 => pad.selectButton,
            KeyCode.JoystickButton7 => pad.startButton,
            KeyCode.JoystickButton8 => pad.leftStickButton,
            KeyCode.JoystickButton9 => pad.rightStickButton,
            _ => null
        };

        if (button == null)
        {
            return false;
        }

        return pressedThisFrame ? button.wasPressedThisFrame : button.isPressed;
    }

#endif

    private static string FormatGamepadButton(KeyCode key)
    {
        return key switch
        {
            KeyCode.JoystickButton0 => "A",
            KeyCode.JoystickButton1 => "B",
            KeyCode.JoystickButton2 => "X",
            KeyCode.JoystickButton3 => "Y",
            KeyCode.JoystickButton4 => "LB",
            KeyCode.JoystickButton5 => "RB",
            KeyCode.JoystickButton6 => "Back",
            KeyCode.JoystickButton7 => "Start",
            KeyCode.JoystickButton8 => "L3",
            KeyCode.JoystickButton9 => "R3",
            KeyCode.None => "-",
            _ => key.ToString().Replace("JoystickButton", "Pad")
        };
    }

    private string FormatGamepadSprintBinding()
    {
        string buttonLabel = FormatGamepadButton(gamepadSprintButton);
        if (useGamepadLeftTriggerForSprint)
        {
            return gamepadSprintButton == KeyCode.None ? "RT" : $"RT / {buttonLabel}";
        }

        return buttonLabel;
    }

    private string FormatGamepadGroundPoundingBinding()
    {
        string groundPoundButton = FormatGamepadButton(gamepadGroundPoundButton);
        if (gamepadDiveButton == KeyCode.None)
        {
            return groundPoundButton;
        }

        return $"{FormatGamepadButton(gamepadDiveButton)} / {groundPoundButton}";
    }

    private void DrawControlsOverlay()
    {
        if (!showControlsOverlay)
        {
            return;
        }

        EnsureHudStyles();
        Rect panel = new Rect(Screen.width - ControlsHudWidth - 18f, 18f, ControlsHudWidth, ControlsHudHeight);
        DrawHudPanel(panel, "CHARACTER CONTROLS", hudControlsPanelTexture, hudControlsShadowTexture);
        float y = panel.y + 44f;
        float x = panel.x + 17f;
        float width = panel.width - 34f;
        if (hudInputMode == HudInputMode.Gamepad)
        {
            DrawHudRow(x, ref y, width, "Move", "Left Stick");
            DrawHudRow(x, ref y, width, "Sprint", FormatGamepadSprintBinding());
            DrawHudRow(x, ref y, width, "Crouch/Slide", FormatGamepadButton(gamepadCrouchButton));
            DrawHudRow(x, ref y, width, "Camera", "Right Stick");
            DrawHudRow(x, ref y, width, "Jump", FormatGamepadButton(gamepadJumpButton));
            DrawHudRow(x, ref y, width, "Dash", FormatGamepadButton(gamepadDashButton));
            DrawHudRow(x, ref y, width, "Roll", FormatGamepadButton(gamepadRollButton));
            DrawHudRow(x, ref y, width, "Kill", FormatGamepadButton(gamepadKillButton));
            DrawHudRow(x, ref y, width, "Ground Pounding", FormatGamepadGroundPoundingBinding());
            DrawHudRow(x, ref y, width, "Customize", "Back / Start");
            DrawHudFooterHint(panel, "D-Pad Right  Toggle Controls");
            return;
        }

        DrawHudRow(x, ref y, width, "Move", "WASD");
        DrawHudRow(x, ref y, width, "Sprint", "L Shift");
        DrawHudRow(x, ref y, width, "Crouch/Slide", "C");
        DrawHudRow(x, ref y, width, "Camera", "Mouse");
        DrawHudRow(x, ref y, width, "Jump", "Space");
        DrawHudRow(x, ref y, width, "Dash", "Q");
        DrawHudRow(x, ref y, width, "Roll", "LAlt");
        DrawHudRow(x, ref y, width, "Kill", "T");
        DrawHudRow(x, ref y, width, "Ground Pounding", "E / L Ctrl");
        DrawHudRow(x, ref y, width, "Customize", "Esc");
        DrawHudFooterHint(panel, "F2 Toggle Controls");
    }

    private void DrawStateHud()
    {
        if (!showStateHud)
        {
            return;
        }

        EnsureHudStyles();
        Rect panel = new Rect(18f, 18f, PerformanceHudWidth, PerformanceHudHeight);
        DrawHudPanel(panel, "PERFORMANCE", hudPerformancePanelTexture, hudPerformanceShadowTexture);
        float y = panel.y + 44f;
        float x = panel.x + 17f;
        float width = panel.width - 34f;
        DrawHudRow(x, ref y, width, "FPS", $"{displayedFps:F0}");

        if (hasFrameTimingSample)
        {
            DrawHudRow(x, ref y, width, "CPU", $"{displayedCpuUsagePercent:F0}% ({displayedCpuFrameTimeMs:F2} ms)");
            DrawHudRow(x, ref y, width, "GPU", $"{displayedGpuUsagePercent:F0}% ({displayedGpuFrameTimeMs:F2} ms)");
        }
        else
        {
            DrawHudRow(x, ref y, width, "CPU", "N/A");
            DrawHudRow(x, ref y, width, "GPU", "N/A");
        }

        DrawHudFooterHint(
            panel,
            hudInputMode == HudInputMode.Gamepad
                ? "D-Pad Left  Toggle Performance"
                : "F1 Toggle Performance");
    }

    private void DrawHudPanel(Rect panel, string title, Texture2D panelTexture, Texture2D shadowTexture)
    {
        GUI.DrawTexture(new Rect(panel.x + 1f, panel.y + 3f, panel.width, panel.height), shadowTexture);
        GUI.DrawTexture(panel, panelTexture);
        GUI.Label(new Rect(panel.x + 17f, panel.y + 11f, panel.width - 34f, 22f), title, hudTitleStyle);
    }

    private void DrawHudRow(float x, ref float y, float width, string label, string value)
    {
        const float rowHeight = 21f;
        float labelWidth = width * 0.52f;
        GUI.Label(new Rect(x, y, labelWidth, rowHeight), label, hudLabelStyle);
        GUI.Label(new Rect(x + labelWidth, y, width - labelWidth, rowHeight), value, hudValueStyle);
        y += rowHeight;
    }

    private void DrawHudFooterHint(Rect panel, string text)
    {
        GUI.Label(new Rect(panel.x + 17f, panel.yMax - 20f, panel.width - 34f, 16f), text, hudHintStyle);
    }

    private void EnsureHudStyles()
    {
        if (hudStylesInitialized)
        {
            return;
        }

        hudFont = TryGetBuiltinFont("LegacyRuntime.ttf");
        hudFont ??= TryGetBuiltinFont("Arial.ttf");
        Color panelColor = new Color(0.08f, 0.1f, 0.13f, 0.54f);
        Color shadowColor = new Color(0f, 0f, 0f, 0.16f);
        hudControlsPanelTexture = CreateRoundedRectTexture((int)ControlsHudWidth, (int)ControlsHudHeight, HudCornerRadiusPixels, panelColor);
        hudControlsShadowTexture = CreateRoundedRectTexture((int)ControlsHudWidth, (int)ControlsHudHeight, HudCornerRadiusPixels, shadowColor);
        hudPerformancePanelTexture = CreateRoundedRectTexture((int)PerformanceHudWidth, (int)PerformanceHudHeight, HudCornerRadiusPixels, panelColor);
        hudPerformanceShadowTexture = CreateRoundedRectTexture((int)PerformanceHudWidth, (int)PerformanceHudHeight, HudCornerRadiusPixels, shadowColor);

        hudTitleStyle = new GUIStyle(GUI.skin.label)
        {
            font = hudFont,
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        hudTitleStyle.normal.textColor = new Color(0.97f, 0.98f, 1f, 1f);

        hudLabelStyle = new GUIStyle(GUI.skin.label)
        {
            font = hudFont,
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft
        };
        hudLabelStyle.normal.textColor = new Color(0.75f, 0.8f, 0.86f, 0.98f);

        hudValueStyle = new GUIStyle(GUI.skin.label)
        {
            font = hudFont,
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight
        };
        hudValueStyle.normal.textColor = new Color(0.96f, 0.97f, 0.99f, 1f);

        hudHintStyle = new GUIStyle(GUI.skin.label)
        {
            font = hudFont,
            fontSize = 10,
            alignment = TextAnchor.MiddleRight
        };
        hudHintStyle.normal.textColor = new Color(0.64f, 0.7f, 0.78f, 0.92f);
        hudStylesInitialized = true;
    }

    private static Texture2D CreateRoundedRectTexture(int width, int height, float cornerRadiusPixels, Color fillColor)
    {
        int resolvedWidth = Mathf.Max(8, width);
        int resolvedHeight = Mathf.Max(8, height);
        float radius = Mathf.Clamp(cornerRadiusPixels, 0f, Mathf.Min(resolvedWidth, resolvedHeight) * 0.5f - 1f);
        Texture2D texture = new Texture2D(resolvedWidth, resolvedHeight, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color[] pixels = new Color[resolvedWidth * resolvedHeight];
        for (int y = 0; y < resolvedHeight; y++)
        {
            for (int x = 0; x < resolvedWidth; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                int index = y * resolvedWidth + x;
                pixels[index] = IsInsideRoundedRect(px, py, resolvedWidth, resolvedHeight, radius) ? fillColor : clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static bool IsInsideRoundedRect(float px, float py, float width, float height, float radius)
    {
        if (width <= 0f || height <= 0f)
        {
            return false;
        }

        if (px < 0f || px > width || py < 0f || py > height)
        {
            return false;
        }

        if (radius <= 0.001f)
        {
            return true;
        }

        float left = radius;
        float right = width - radius;
        float bottom = radius;
        float top = height - radius;
        if ((px >= left && px <= right) || (py >= bottom && py <= top))
        {
            return true;
        }

        float cornerX = px < left ? left : right;
        float cornerY = py < bottom ? bottom : top;
        float dx = px - cornerX;
        float dy = py - cornerY;
        return dx * dx + dy * dy <= radius * radius;
    }

    private static Font TryGetBuiltinFont(string resourceName)
    {
        try
        {
            return Resources.GetBuiltinResource<Font>(resourceName);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private void ReleaseHudResources()
    {
        if (hudControlsPanelTexture != null)
        {
            Destroy(hudControlsPanelTexture);
            hudControlsPanelTexture = null;
        }

        if (hudControlsShadowTexture != null)
        {
            Destroy(hudControlsShadowTexture);
            hudControlsShadowTexture = null;
        }

        if (hudPerformancePanelTexture != null)
        {
            Destroy(hudPerformancePanelTexture);
            hudPerformancePanelTexture = null;
        }

        if (hudPerformanceShadowTexture != null)
        {
            Destroy(hudPerformanceShadowTexture);
            hudPerformanceShadowTexture = null;
        }

        hudStylesInitialized = false;
    }

    private void UpdateHudInputMode()
    {
        if (!HasConnectedGamepad())
        {
            hudInputMode = HudInputMode.KeyboardMouse;
            return;
        }

        bool gamepadActive = IsGamepadHudInputActiveThisFrame();
        bool keyboardMouseActive = IsKeyboardMouseHudInputActiveThisFrame();
        if (gamepadActive)
        {
            hudInputMode = HudInputMode.Gamepad;
            return;
        }

        if (keyboardMouseActive)
        {
            hudInputMode = HudInputMode.KeyboardMouse;
        }
    }

    private bool IsGamepadHudInputActiveThisFrame()
    {
        if (!enableGamepadInput || !HasConnectedGamepad())
        {
            return false;
        }

        if (ReadGamepadMoveInput().sqrMagnitude > 0.0004f)
        {
            return true;
        }

        if (ReadGamepadLookInput().sqrMagnitude > 0.0004f)
        {
            return true;
        }

        if (ReadGamepadSprintTriggerAmount() > 0.05f)
        {
            return true;
        }

        return IsGamepadButtonHeld(gamepadJumpButton)
            || IsGamepadButtonHeld(gamepadSprintButton)
            || IsGamepadButtonHeld(gamepadDashButton)
            || IsGamepadButtonHeld(gamepadGroundPoundButton)
            || IsGamepadButtonHeld(gamepadDiveButton)
            || IsGamepadButtonHeld(gamepadRollButton)
            || IsGamepadButtonHeld(gamepadCrouchButton)
            || IsGamepadButtonHeld(gamepadKillButton)
            || IsGamepadDpadHeld();
    }

    private static bool IsGamepadDpadHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Gamepad gamepad = Gamepad.current;
        return gamepad != null
               && (gamepad.dpad.left.isPressed
                   || gamepad.dpad.right.isPressed
                   || gamepad.dpad.up.isPressed
                   || gamepad.dpad.down.isPressed);
#else
        return false;
#endif
    }

    private bool IsKeyboardMouseHudInputActiveThisFrame()
    {
        if (MinimoInputBridge.GetMouseButton(0) || MinimoInputBridge.GetMouseButton(1) || MinimoInputBridge.GetMouseButton(2))
        {
            return true;
        }

        if (Mathf.Abs(ReadAxisSafe("Mouse X")) > 0.0001f || Mathf.Abs(ReadAxisSafe("Mouse Y")) > 0.0001f)
        {
            return true;
        }

        return MinimoInputBridge.GetKey(KeyCode.W)
            || MinimoInputBridge.GetKey(KeyCode.A)
            || MinimoInputBridge.GetKey(KeyCode.S)
            || MinimoInputBridge.GetKey(KeyCode.D)
            || MinimoInputBridge.GetKey(KeyCode.Space)
            || MinimoInputBridge.GetKey(KeyCode.LeftShift)
            || MinimoInputBridge.GetKey(KeyCode.RightShift)
            || MinimoInputBridge.GetKey(KeyCode.Q)
            || MinimoInputBridge.GetKey(KeyCode.E)
            || MinimoInputBridge.GetKey(KeyCode.LeftControl)
            || MinimoInputBridge.GetKey(KeyCode.RightControl)
            || MinimoInputBridge.GetKey(KeyCode.LeftAlt)
            || MinimoInputBridge.GetKey(KeyCode.RightAlt)
            || MinimoInputBridge.GetKey(KeyCode.C)
            || MinimoInputBridge.GetKey(KeyCode.T)
            || MinimoInputBridge.GetKey(KeyCode.F1)
            || MinimoInputBridge.GetKey(KeyCode.F2);
    }

    private void UpdatePerformanceHudMetrics()
    {
        float delta = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        float instantFps = 1f / delta;
        float smoothing = 1f - Mathf.Exp(-6f * delta);
        displayedFps = Mathf.Lerp(displayedFps, instantFps, smoothing);

        performanceSampleTimer -= Time.unscaledDeltaTime;
        if (performanceSampleTimer > 0f)
        {
            return;
        }

        performanceSampleTimer = Mathf.Max(0.05f, performanceSampleInterval);
        CaptureFrameTimingSample();
    }

    private void CaptureFrameTimingSample()
    {
        FrameTimingManager.CaptureFrameTimings();
        uint count = FrameTimingManager.GetLatestTimings(1, performanceFrameTimings);
        if (count == 0)
        {
            hasFrameTimingSample = false;
            if (!frameTimingUnsupportedLogged && Application.isPlaying)
            {
                frameTimingUnsupportedLogged = true;
            }

            return;
        }

        FrameTiming timing = performanceFrameTimings[0];
        displayedCpuFrameTimeMs = Mathf.Max(0f, (float)timing.cpuMainThreadFrameTime);
        displayedGpuFrameTimeMs = Mathf.Max(0f, (float)timing.gpuFrameTime);
        float frameBudgetMs = 1000f / Mathf.Max(1f, displayedFps);
        displayedCpuUsagePercent = CalculateFrameLoadPercent(displayedCpuFrameTimeMs, frameBudgetMs);
        displayedGpuUsagePercent = CalculateFrameLoadPercent(displayedGpuFrameTimeMs, frameBudgetMs);
        hasFrameTimingSample = true;
    }

    private static float CalculateFrameLoadPercent(float frameTimeMs, float frameBudgetMs)
    {
        if (frameBudgetMs <= 0.0001f)
        {
            return 0f;
        }

        return Mathf.Clamp(frameTimeMs / frameBudgetMs * 100f, 0f, 999f);
    }

    private void DrawWindOverlay()
    {
        if (!enableSpeedFov || !enableWindOverlay || windOverlayIntensity <= 0.001f)
        {
            return;
        }

        if (Event.current == null || Event.current.type != EventType.Repaint)
        {
            return;
        }

        float width = Screen.width;
        float height = Screen.height;
        if (width <= 1f || height <= 1f)
        {
            return;
        }

        float edgeWidth = width * windOverlayWidth;
        int lineCount = Mathf.Max(1, windOverlayLineCount);
        Texture texture = Texture2D.whiteTexture;
        for (int i = 0; i < lineCount; i++)
        {
            float t = (i + 1f) / (lineCount + 1f);
            float alpha = windOverlayIntensity * windOverlayMaxAlpha * Mathf.Lerp(1f, 0.28f, t);
            float lineWidth = Mathf.Lerp(2.5f, 9f, t);
            float yPadding = height * Mathf.Lerp(0.04f, 0.22f, t);
            float xL = Mathf.Lerp(0f, edgeWidth - lineWidth, t * t);
            float xR = width - xL - lineWidth;
            Rect leftLine = new Rect(xL, yPadding, lineWidth, height - yPadding * 2f);
            Rect rightLine = new Rect(xR, yPadding, lineWidth, height - yPadding * 2f);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(leftLine, texture);
            GUI.DrawTexture(rightLine, texture);
        }

        float bandAlpha = windOverlayIntensity * windOverlayMaxAlpha * 0.45f;
        GUI.color = new Color(1f, 1f, 1f, bandAlpha);
        GUI.DrawTexture(new Rect(0f, 0f, width, height * 0.012f), texture);
        GUI.DrawTexture(new Rect(0f, height * 0.988f, width, height * 0.012f), texture);
        GUI.color = Color.white;
    }

    private void OnGUI()
    {
        DrawWindOverlay();
        DrawControlsOverlay();
        DrawStateHud();
        DrawGizmoHoverLabel();
    }

    private void DrawGizmoHoverLabel()
    {
        if (!showDebugGizmos || !showGizmoHoverLabel)
        {
            return;
        }

        // Placeholder hook for hover labels; keeps inspector settings live even when overlay is disabled.
        _ = gizmoHoverPixelRadius;
        _ = gizmoHoverLabelTextColor;
        _ = gizmoHoverLabelBackgroundColor;
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos)
        {
            return;
        }

        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
        }

        if (controller != null && drawGroundProbeGizmo)
        {
            Bounds controllerBounds = controller.bounds;
            Vector3 probeOrigin = controllerBounds.center + Vector3.up * groundProbeOffset;
            float probeDistance = controllerBounds.extents.y + groundProbeDistance;
            float probeRadius = Mathf.Clamp(groundProbeRadius, 0.05f, controllerBounds.extents.x * 0.95f);

            Gizmos.color = groundProbeGizmoColor;
            Gizmos.DrawWireSphere(probeOrigin, probeRadius);
            Gizmos.DrawLine(probeOrigin, probeOrigin + Vector3.down * probeDistance);
            Gizmos.DrawWireSphere(probeOrigin + Vector3.down * probeDistance, probeRadius);
        }

        if (drawSlopeGizmo && hasGroundNormal)
        {
            Vector3 origin = groundHitPoint;
            Gizmos.color = slopeNormalGizmoColor;
            Gizmos.DrawLine(origin, origin + groundNormal * 0.8f);

            if (groundDownhillDirection.sqrMagnitude > 0.0001f)
            {
                Gizmos.color = slopeDownhillGizmoColor;
                Gizmos.DrawLine(origin, origin + groundDownhillDirection * 0.8f);
            }
        }

        if (drawStepGizmo)
        {
            float stepRadius = Mathf.Max(0.02f, debugStepProbeRadius);
            Color lowColor = debugStepLowerBlocked ? stepBlockedGizmoColor : stepClearGizmoColor;
            Gizmos.color = lowColor;
            Gizmos.DrawWireSphere(debugStepLowerOrigin, stepRadius);
            Gizmos.DrawLine(debugStepLowerOrigin, debugStepLowerOrigin + debugStepDirection * debugStepProbeDistance);

            Color upperColor = debugStepUpperBlocked ? stepBlockedGizmoColor : stepClearGizmoColor;
            Gizmos.color = upperColor;
            Gizmos.DrawWireSphere(debugStepUpperOrigin, stepRadius);
            Gizmos.DrawLine(debugStepUpperOrigin, debugStepUpperOrigin + debugStepDirection * debugStepProbeDistance);

            Gizmos.color = stepSurfaceGizmoColor;
            Gizmos.DrawLine(debugStepProbeOrigin, debugStepProbeOrigin + Vector3.down * (maxStepHeight + stepSurfaceProbeHeight + 0.25f));
            if (debugStepCandidateFound)
            {
                Gizmos.color = stepCandidateGizmoColor;
                Gizmos.DrawWireSphere(debugStepHitPoint, 0.06f);
            }
        }

        if (drawTeeterGizmo)
        {
            float probeRadius = Mathf.Max(0.02f, debugTeeterProbeRadius);
            Gizmos.color = debugTeeterHasGroundAhead ? teeterGroundedGizmoColor : teeterOpenEdgeGizmoColor;
            Gizmos.DrawWireSphere(debugTeeterProbeOrigin, probeRadius);
            Gizmos.DrawLine(debugTeeterProbeOrigin, debugTeeterProbeOrigin + Vector3.down * debugTeeterProbeDistance);
            Gizmos.DrawWireSphere(debugTeeterHitPoint, 0.06f);

            if (currentState == MovementState.Teetering)
            {
                Vector3 teeterDirectionForGizmo = teeterEdgeDirection.sqrMagnitude > 0.0001f
                    ? -teeterEdgeDirection
                    : debugTeeterEdgeDirection.sqrMagnitude > 0.0001f
                        ? -debugTeeterEdgeDirection
                        : Vector3.zero;
                if (teeterDirectionForGizmo.sqrMagnitude > 0.0001f)
                {
                    Gizmos.color = teeterDirectionGizmoColor;
                    Vector3 origin = transform.position + Vector3.up * 0.1f;
                    DrawDirectionArrow(origin, teeterDirectionForGizmo, 1.2f, 0.16f);
                }

                DrawTeeterGroundGuides();
            }
        }

        if (drawTeeterEventGizmo)
        {
            DrawTeeterEventGizmo();
        }

        if (drawDashGizmo)
        {
            DrawDashActionGizmo();
        }

        if (drawDiveGizmo)
        {
            DrawDiveActionGizmo();
        }

        if (drawSlideGizmo)
        {
            DrawSlideActionGizmo();
        }

        if (drawActionDirectionGizmo)
        {
            DrawCurrentActionDirectionGizmo();
        }

        if (drawJumpHeadroomGizmo
            && TryGetJumpHeadroomInfo(out float clearance, out Vector3 headPoint, out Vector3 ceilingPoint, out float requiredHeadroom)
            && clearance < requiredHeadroom)
        {
            bool blockedJump = clearance + 0.001f < requiredHeadroom;
            Gizmos.color = blockedJump ? jumpHeadroomBlockedGizmoColor : jumpHeadroomClearGizmoColor;
            Gizmos.DrawWireSphere(headPoint, 0.05f);
            Gizmos.DrawWireSphere(ceilingPoint, 0.05f);
            Gizmos.DrawLine(headPoint, ceilingPoint);
        }

        if (drawLandingGizmo)
        {
            DrawLandingImpactGizmo();
        }

        if (drawCameraPivotGizmo)
        {
            Gizmos.color = cameraPivotGizmoColor;
            Vector3 pivot = transform.position + Vector3.up * pivotHeight;
            Gizmos.DrawWireSphere(pivot, 0.1f);
        }
    }

    private void DrawTeeterEventGizmo()
    {
        float alpha = GetHistoryAlpha(debugTeeterEnterTime);
        if (alpha <= 0f)
        {
            return;
        }

        Color baseColor = new Color(teeterEventGizmoColor.r, teeterEventGizmoColor.g, teeterEventGizmoColor.b, alpha);
        Vector3 direction = debugTeeterEnterDirection.sqrMagnitude > 0.0001f ? debugTeeterEnterDirection.normalized : transform.forward;
        Gizmos.color = baseColor;
        Gizmos.DrawWireSphere(debugTeeterEnterPoint, 0.08f);
        DrawDirectionArrow(debugTeeterEnterPoint, direction, 0.95f, 0.18f);

        if (debugTeeterHadSnapBack)
        {
            Gizmos.color = new Color(teeterSnapBackGizmoColor.r, teeterSnapBackGizmoColor.g, teeterSnapBackGizmoColor.b, alpha);
            Gizmos.DrawLine(debugTeeterEnterPoint, debugTeeterSnapTargetPoint);
            Gizmos.DrawWireCube(debugTeeterSnapTargetPoint, Vector3.one * 0.09f);
        }
    }

    private void DrawDashActionGizmo()
    {
        float alpha = GetHistoryAlpha(debugDashStartTime);
        if (alpha <= 0f)
        {
            return;
        }

        Color dashColor = new Color(dashGizmoColor.r, dashGizmoColor.g, dashGizmoColor.b, alpha);
        Gizmos.color = dashColor;
        Gizmos.DrawWireSphere(debugDashStartPoint, 0.07f);
        DrawDirectionArrow(debugDashStartPoint, debugDashDirection, Vector3.Distance(debugDashStartPoint, debugDashEndPoint), 0.2f);
        Gizmos.DrawWireSphere(debugDashEndPoint, 0.09f);
    }

    private void DrawDiveActionGizmo()
    {
        float alpha = GetHistoryAlpha(debugDiveStartTime);
        if (alpha <= 0f)
        {
            return;
        }

        Gizmos.color = new Color(diveGizmoColor.r, diveGizmoColor.g, diveGizmoColor.b, alpha);
        Vector3 previous = debugDiveStartPoint;
        int steps = 16;
        float duration = Mathf.Max(0.01f, diveDuration);
        float gravityAccel = gravity * diveGravityMultiplier;
        for (int i = 1; i <= steps; i++)
        {
            float t = duration * i / steps;
            Vector3 point = debugDiveStartPoint
                + debugDiveDirection * (debugDiveStartSpeed * t)
                + Vector3.up * (debugDiveStartVerticalSpeed * t + 0.5f * gravityAccel * t * t);
            Gizmos.DrawLine(previous, point);
            previous = point;
        }

        Gizmos.DrawWireSphere(debugDiveStartPoint, 0.07f);
        Gizmos.DrawWireSphere(debugDivePredictedEndPoint, 0.09f);
    }

    private void DrawSlideActionGizmo()
    {
        float alpha = GetHistoryAlpha(debugSlideStartTime);
        if (alpha > 0f)
        {
            Color historyColor = debugSlideWasSlope
                ? new Color(slopeSlideGizmoColor.r, slopeSlideGizmoColor.g, slopeSlideGizmoColor.b, alpha)
                : new Color(slideGizmoColor.r, slideGizmoColor.g, slideGizmoColor.b, alpha);
            Gizmos.color = historyColor;
            Gizmos.DrawWireSphere(debugSlideStartPoint, 0.07f);
            DrawDirectionArrow(debugSlideStartPoint, debugSlideDirection, Vector3.Distance(debugSlideStartPoint, debugSlidePredictedEndPoint), 0.18f);
            Gizmos.DrawWireSphere(debugSlidePredictedEndPoint, 0.08f);
        }

        if (currentState == MovementState.Sliding || currentState == MovementState.SlopeSliding)
        {
            float speed = planarVelocity.magnitude;
            Vector3 direction = slideDirection;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = planarVelocity;
            }
            direction = ResolveDebugDirection(direction);
            float deceleration = currentState == MovementState.SlopeSliding
                ? Mathf.Max(0.01f, slopeSlideFriction)
                : Mathf.Max(0.01f, slideDeceleration);
            float maxDuration = currentState == MovementState.SlopeSliding
                ? Mathf.Max(0.2f, slideTimer)
                : Mathf.Max(0.1f, slideTimer);
            float distance = EstimateStopDistance(speed, deceleration, maxDuration);
            Vector3 origin = transform.position + Vector3.up * 0.06f;
            Vector3 end = origin + direction * distance;

            Gizmos.color = activeSlidePredictionGizmoColor;
            Gizmos.DrawLine(origin, end);
            Gizmos.DrawWireSphere(end, 0.06f);
        }
    }

    private void DrawCurrentActionDirectionGizmo()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        Vector3 direction = Vector3.zero;
        Color color = activeSlidePredictionGizmoColor;
        float predictionDistance = 1.25f;
        bool drawStopPoint = false;

        switch (currentState)
        {
            case MovementState.Dashing:
                direction = dashDirection;
                color = dashGizmoColor;
                predictionDistance = Mathf.Max(1f, dashSpeed * Mathf.Max(0.08f, dashTimer));
                break;
            case MovementState.Dive:
                direction = diveDirection;
                color = diveGizmoColor;
                predictionDistance = Mathf.Max(1f, planarVelocity.magnitude * Mathf.Max(0.12f, diveTimer));
                break;
            case MovementState.Rolling:
                direction = rollDirection.sqrMagnitude > 0.0001f ? rollDirection : planarVelocity;
                color = rollGizmoColor;
                predictionDistance = EstimateStopDistance(
                    planarVelocity.magnitude,
                    Mathf.Max(0.01f, rollDeceleration),
                    Mathf.Max(0.05f, rollTimer));
                predictionDistance = Mathf.Max(0.5f, predictionDistance);
                drawStopPoint = true;
                break;
            case MovementState.SlopeSliding:
            case MovementState.Sliding:
                direction = slideDirection;
                color = slideGizmoColor;
                predictionDistance = Mathf.Max(1f, planarVelocity.magnitude * 0.45f);
                break;
            case MovementState.Teetering:
                direction = planarVelocity.sqrMagnitude > 0.0001f ? planarVelocity : -teeterEdgeDirection;
                color = teeterEventGizmoColor;
                predictionDistance = 1f;
                break;
            default:
                direction = planarVelocity.sqrMagnitude > 0.0001f ? planarVelocity : desiredMoveDirection;
                color = activeSlidePredictionGizmoColor;
                predictionDistance = Mathf.Clamp(planarVelocity.magnitude * 0.4f + 0.9f, 0.9f, 3f);
                break;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        direction = ResolveDebugDirection(direction);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Gizmos.color = color;
        DrawDirectionArrow(origin, direction, predictionDistance, 0.2f);

        if (drawStopPoint)
        {
            Vector3 end = origin + direction * predictionDistance;
            Gizmos.DrawWireSphere(end, 0.08f);
        }
    }

    private void DrawLandingImpactGizmo()
    {
        float alpha = GetHistoryAlpha(debugLastLandingTime);
        if (alpha <= 0f)
        {
            return;
        }

        float impact01 = Mathf.InverseLerp(landingImpactMinSpeed, landingImpactMaxSpeed, debugLastLandingImpactSpeed);
        float radius = Mathf.Lerp(0.06f, 0.24f, impact01);
        Vector3 point = debugLastLandingPoint;

        Gizmos.color = new Color(landingGizmoColor.r, landingGizmoColor.g, landingGizmoColor.b, alpha);
        Gizmos.DrawWireSphere(point, radius);
        Gizmos.DrawLine(point + Vector3.left * radius, point + Vector3.right * radius);
        Gizmos.DrawLine(point + Vector3.forward * radius, point + Vector3.back * radius);
        Gizmos.DrawLine(point, point + Vector3.up * (0.25f + radius));
    }

    private float GetHistoryAlpha(float eventTime)
    {
        if (float.IsNegativeInfinity(eventTime))
        {
            return 0f;
        }

        if (!Application.isPlaying)
        {
            return 1f;
        }

        float age = Time.time - eventTime;
        if (age < 0f)
        {
            return 1f;
        }

        float duration = Mathf.Max(0.1f, actionGizmoHistoryDuration);
        return Mathf.Clamp01(1f - age / duration);
    }

    private void DrawDirectionArrow(Vector3 origin, Vector3 direction, float length, float headSize)
    {
        Vector3 dir = direction.normalized;
        if (dir.sqrMagnitude <= 0.0001f || length <= 0f)
        {
            return;
        }

        Vector3 end = origin + dir * length;
        Gizmos.DrawLine(origin, end);

        Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
        Vector3 rightWing = look * Quaternion.Euler(0f, 160f, 0f) * Vector3.forward;
        Vector3 leftWing = look * Quaternion.Euler(0f, 200f, 0f) * Vector3.forward;
        Gizmos.DrawLine(end, end + rightWing * headSize);
        Gizmos.DrawLine(end, end + leftWing * headSize);
    }

    private void DrawTeeterGroundGuides()
    {
        Vector3 characterGroundPoint;
        if (isGrounded && hasGroundNormal)
        {
            characterGroundPoint = groundHitPoint;
        }
        else if (controller != null)
        {
            Bounds bounds = controller.bounds;
            characterGroundPoint = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }
        else
        {
            characterGroundPoint = transform.position;
        }

        Vector3 gapGroundPoint = new Vector3(debugTeeterProbeOrigin.x, characterGroundPoint.y, debugTeeterProbeOrigin.z);
        Vector3 gapDirection = gapGroundPoint - characterGroundPoint;
        gapDirection.y = 0f;
        float gapDistance = gapDirection.magnitude;

        Vector3 characterMarker = characterGroundPoint + Vector3.up * 0.03f;
        Vector3 gapMarker = gapGroundPoint + Vector3.up * 0.03f;

        Gizmos.color = teeterGroundedGizmoColor;
        Gizmos.DrawWireSphere(characterMarker, 0.08f);
        Gizmos.color = teeterOpenEdgeGizmoColor;
        Gizmos.DrawWireCube(gapMarker, new Vector3(0.14f, 0.06f, 0.14f));

        if (gapDistance > 0.001f)
        {
            Gizmos.color = teeterDirectionGizmoColor;
            Gizmos.DrawLine(characterMarker, gapMarker);
            DrawDirectionArrow(characterMarker, gapDirection.normalized, gapDistance, 0.14f);

#if UNITY_EDITOR
            Vector3 labelPos = Vector3.Lerp(characterMarker, gapMarker, 0.5f) + Vector3.up * 0.08f;
            Handles.Label(labelPos, $"Gap: {gapDistance:F2}m");
#endif
        }
    }
}





