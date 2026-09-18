using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
[AddComponentMenu("Minimo/Moving Platform Rider")]
public class MinimoMovingPlatformRider : MonoBehaviour
{
    [SerializeField] private LayerMask platformDetectionMask = ~0;
    [SerializeField] [Min(0.05f)] private float platformProbeDistance = 0.65f;
    [SerializeField] [Range(0.35f, 1f)] private float platformProbeRadiusScale = 0.8f;
    [SerializeField] private bool requireGrounded = true;
    [SerializeField] [Min(0f)] private float platformContactLostGraceTime = 0.1f;
    [SerializeField] [Min(0f)] private float airborneProbeExtraDistance = 0.2f;
    [SerializeField] private bool disableRigidbodyGravityWhileAttached = true;

    private CharacterController controller;
    private MinimoTpsController tpsController;
    private Rigidbody attachedRigidbody;
    private bool cachedOriginalRigidbodyGravity;
    private bool hasCachedOriginalRigidbodyGravity;
    private Transform originalParent;
    private MinimoMovingPlatform currentPlatform;
    private Transform notifiedPlatformTransform;
    private bool platformParentingNotified;
    private bool blockAttachUntilGrounded;
    private float lastPlatformContactTime = float.NegativeInfinity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        tpsController = GetComponent<MinimoTpsController>();
        attachedRigidbody = GetComponent<Rigidbody>();
        if (attachedRigidbody != null)
        {
            cachedOriginalRigidbodyGravity = attachedRigidbody.useGravity;
            hasCachedOriginalRigidbodyGravity = true;
        }

        originalParent = transform.parent;
    }

    private void LateUpdate()
    {
        UpdatePlatformAttachment();
        RefreshAttachedCharacterControllerState();
    }

    private void OnDisable()
    {
        bool lifecycleSafe = !gameObject.activeInHierarchy
            || (transform.parent != null && !transform.parent.gameObject.activeInHierarchy);
        DetachFromCurrentPlatform(lifecycleSafe);
    }

    private void OnDestroy()
    {
        DetachFromCurrentPlatform(lifecycleSafe: true);
    }

    private void UpdatePlatformAttachment()
    {
        if (controller == null)
        {
            return;
        }

        bool groundedNow = controller.isGrounded;
        if (groundedNow)
        {
            blockAttachUntilGrounded = false;
        }
        else if (blockAttachUntilGrounded)
        {
            DetachFromCurrentPlatform();
            return;
        }

        if (currentPlatform != null && !currentPlatform.isActiveAndEnabled)
        {
            DetachFromCurrentPlatform();
            return;
        }

        float extraProbeDistance = groundedNow ? 0f : airborneProbeExtraDistance;
        MinimoMovingPlatform platform = DetectPlatformUnderCharacter(extraProbeDistance, out bool hasGroundHit);
        if (platform != null)
        {
            lastPlatformContactTime = Time.time;
        }

        if (platform == currentPlatform)
        {
            if (platform != null)
            {
                NotifyPlatformParentingState(true, platform.transform);
                ApplyRigidbodyGravityState(attached: true);
            }

            return;
        }

        if (platform == null)
        {
            bool keepByGroundedMiss = currentPlatform != null && groundedNow && !hasGroundHit;
            bool keepByContactGrace = currentPlatform != null
                && groundedNow
                && Time.time - lastPlatformContactTime <= Mathf.Max(0f, platformContactLostGraceTime);
            if (keepByGroundedMiss || keepByContactGrace)
            {
                NotifyPlatformParentingState(true, currentPlatform.transform);
                ApplyRigidbodyGravityState(attached: true);
                return;
            }

            DetachFromCurrentPlatform();
            return;
        }

        bool allowUngroundedAttachForCurrentPlatform = currentPlatform != null && platform == currentPlatform;
        if (requireGrounded && !groundedNow && !allowUngroundedAttachForCurrentPlatform)
        {
            DetachFromCurrentPlatform();
            return;
        }

        AttachToPlatform(platform);
    }

    private void RefreshAttachedCharacterControllerState()
    {
        if (controller == null
            || currentPlatform == null
            || transform.parent != currentPlatform.transform
            || blockAttachUntilGrounded)
        {
            return;
        }

        // Parent movement does not automatically update CharacterController collision state.
        // Refresh grounded and collision state with a zero Move call.
        controller.Move(Vector3.zero);
    }

    private MinimoMovingPlatform DetectPlatformUnderCharacter(float extraProbeDistance, out bool hasGroundHit)
    {
        hasGroundHit = false;
        Bounds bounds = controller.bounds;
        float radius = Mathf.Max(0.06f, controller.radius * platformProbeRadiusScale);
        Vector3 origin = bounds.center + Vector3.up * 0.05f;
        float distance = bounds.extents.y + Mathf.Max(0.05f, platformProbeDistance) + Mathf.Max(0f, extraProbeDistance);

        if (!Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, distance, platformDetectionMask, QueryTriggerInteraction.Ignore))
        {
            return null;
        }

        hasGroundHit = true;
        Transform hitTransform = hit.collider.transform;
        if (hitTransform == transform || hitTransform.IsChildOf(transform))
        {
            return null;
        }

        return hit.collider.GetComponentInParent<MinimoMovingPlatform>();
    }

    private void AttachToPlatform(MinimoMovingPlatform platform)
    {
        if (platform == null)
        {
            DetachFromCurrentPlatform();
            return;
        }

        currentPlatform = platform;
        if (transform.parent != platform.transform)
        {
            if (!TrySetParent(platform.transform))
            {
                currentPlatform = null;
                NotifyPlatformParentingState(false, null);
                ApplyRigidbodyGravityState(attached: false);
                return;
            }
        }

        NotifyPlatformParentingState(true, platform.transform);
        ApplyRigidbodyGravityState(attached: true);
    }

    public void DetachForAirborneAction()
    {
        blockAttachUntilGrounded = true;
        DetachFromCurrentPlatform();
    }

    private void DetachFromCurrentPlatform(bool lifecycleSafe = false)
    {
        bool shouldReparent = !lifecycleSafe && transform.parent != originalParent;

        currentPlatform = null;
        if (shouldReparent)
        {
            _ = TrySetParent(originalParent);
        }

        NotifyPlatformParentingState(false, null);
        ApplyRigidbodyGravityState(attached: false);
    }

    private bool TrySetParent(Transform targetParent)
    {
        if (transform == null)
        {
            return false;
        }

        try
        {
            transform.SetParent(targetParent, true);
            return true;
        }
        catch (UnityException)
        {
            return false;
        }
    }

    private void NotifyPlatformParentingState(bool attached, Transform platformTransform)
    {
        Transform nextPlatform = attached ? platformTransform : null;
        if (platformParentingNotified == attached && notifiedPlatformTransform == nextPlatform)
        {
            return;
        }

        platformParentingNotified = attached;
        notifiedPlatformTransform = nextPlatform;
        if (tpsController != null)
        {
            tpsController.SetMovingPlatformParentingState(attached, nextPlatform);
        }
    }

    private void ApplyRigidbodyGravityState(bool attached)
    {
        if (!disableRigidbodyGravityWhileAttached || attachedRigidbody == null)
        {
            return;
        }

        if (!hasCachedOriginalRigidbodyGravity)
        {
            cachedOriginalRigidbodyGravity = attachedRigidbody.useGravity;
            hasCachedOriginalRigidbodyGravity = true;
        }

        attachedRigidbody.useGravity = attached ? false : cachedOriginalRigidbodyGravity;
    }
}

