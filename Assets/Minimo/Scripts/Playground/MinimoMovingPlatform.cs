using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Minimo/Moving Platform")]
public class MinimoMovingPlatform : MonoBehaviour
{
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    [SerializeField] [Min(0f)] private float rotationSpeed = 90f;
    [SerializeField] private Vector3 verticalTravelDistance = new Vector3(0f, 2f, 0f);
    [SerializeField] private Vector3 verticalCycleDuration = new Vector3(3.2f, 3.2f, 3.2f);
    [SerializeField] private float phaseOffset;
    [Header("Crush Prevention")]
    [SerializeField] private bool preventCrushingCharacter = true;
    [SerializeField] [Min(0f)] private float crushProbePadding = 0.05f;
    [SerializeField] private LayerMask crushDetectionMask = ~0;

    private Vector3 baseLocalPosition;
    private float runtimeVerticalPhaseOffset;
    private Collider[] platformColliders = new Collider[0];

    private void Awake()
    {
        EnsureRuntimeDynamicFlags();
        baseLocalPosition = transform.localPosition;
        runtimeVerticalPhaseOffset = 0f;
        RefreshColliderCache();
    }

    private void OnEnable()
    {
        EnsureRuntimeDynamicFlags();
        baseLocalPosition = transform.localPosition;
        runtimeVerticalPhaseOffset = 0f;
        RefreshColliderCache();
    }

    private void FixedUpdate()
    {
        RotatePlatform();
        MovePlatformOnAxes();
    }

    public void ApplySettings(float newRotationSpeed, float newVerticalDistance, float newCycleDuration, float newPhaseOffset)
    {
        ApplySettings(
            newRotationSpeed,
            new Vector3(0f, newVerticalDistance, 0f),
            new Vector3(newCycleDuration, newCycleDuration, newCycleDuration),
            newPhaseOffset);
    }

    public void ApplySettings(float newRotationSpeed, Vector3 newTravelDistance, Vector3 newCycleDuration, float newPhaseOffset)
    {
        rotationSpeed = newRotationSpeed;
        verticalTravelDistance = newTravelDistance;
        verticalCycleDuration = ClampMin(newCycleDuration, 0.25f);
        phaseOffset = newPhaseOffset;
        runtimeVerticalPhaseOffset = 0f;
    }

    private void RotatePlatform()
    {
        if (rotationSpeed == 0f)
        {
            return;
        }

        Vector3 axis = rotationAxis.sqrMagnitude < 0.0001f ? Vector3.up : rotationAxis.normalized;
        transform.Rotate(axis, rotationSpeed * Time.fixedDeltaTime, Space.Self);
    }

    private void MovePlatformOnAxes()
    {
        Vector3 travelDistance = verticalTravelDistance;
        if (travelDistance.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 cycleDuration = ClampMin(verticalCycleDuration, 0.25f);
        Vector3 targetLocalPosition = GetTargetLocalPosition(travelDistance, cycleDuration);
        Vector3 localDelta = targetLocalPosition - transform.localPosition;
        Vector3 worldDelta = transform.parent != null ? transform.parent.TransformVector(localDelta) : localDelta;
        bool descendingOnWorldY = worldDelta.y < -0.0001f;
        if (descendingOnWorldY && ShouldReverseForCharacterCrush(worldDelta))
        {
            ReverseVerticalMotionPhase(cycleDuration.y);
            targetLocalPosition = GetTargetLocalPosition(travelDistance, cycleDuration);
        }

        transform.localPosition = targetLocalPosition;
    }

    private Vector3 GetTargetLocalPosition(Vector3 travelDistance, Vector3 cycleDuration)
    {
        float baseTimeWithOffset = Time.time + phaseOffset;
        float verticalTimeWithOffset = Time.time + phaseOffset + runtimeVerticalPhaseOffset;
        Vector3 cycleTime = new Vector3(
            baseTimeWithOffset / cycleDuration.x,
            verticalTimeWithOffset / cycleDuration.y,
            baseTimeWithOffset / cycleDuration.z);

        Vector3 oscillation = new Vector3(
            Mathf.Sin(cycleTime.x * Mathf.PI * 2f) * 0.5f + 0.5f,
            Mathf.Sin(cycleTime.y * Mathf.PI * 2f) * 0.5f + 0.5f,
            Mathf.Sin(cycleTime.z * Mathf.PI * 2f) * 0.5f + 0.5f);

        return baseLocalPosition + Vector3.Scale(oscillation, travelDistance);
    }

    private bool ShouldReverseForCharacterCrush(Vector3 worldDelta)
    {
        if (!preventCrushingCharacter)
        {
            return false;
        }

        float castDistance = Mathf.Abs(worldDelta.y) + Mathf.Max(0f, crushProbePadding);
        if (castDistance <= 0.0001f)
        {
            return false;
        }

        if (platformColliders == null || platformColliders.Length == 0)
        {
            RefreshColliderCache();
        }

        for (int i = 0; i < platformColliders.Length; i++)
        {
            Collider platformCollider = platformColliders[i];
            if (platformCollider == null || !platformCollider.enabled || platformCollider.isTrigger)
            {
                continue;
            }

            Bounds bounds = platformCollider.bounds;
            if (bounds.size.sqrMagnitude <= 0.0000001f)
            {
                continue;
            }

            Vector3 halfExtents = bounds.extents * 0.96f;
            if (halfExtents.x <= 0.0001f || halfExtents.y <= 0.0001f || halfExtents.z <= 0.0001f)
            {
                continue;
            }

            RaycastHit[] hits = Physics.BoxCastAll(
                bounds.center,
                halfExtents,
                Vector3.down,
                Quaternion.identity,
                castDistance,
                crushDetectionMask,
                QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
            {
                continue;
            }

            for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
            {
                RaycastHit hit = hits[hitIndex];
                Transform hitTransform = hit.collider != null ? hit.collider.transform : null;
                if (hitTransform == null)
                {
                    continue;
                }

                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                CharacterController characterController = hit.collider.GetComponentInParent<CharacterController>();
                if (characterController != null)
                {
                    return true;
                }

                MinimoTpsController controller = hit.collider.GetComponentInParent<MinimoTpsController>();
                if (controller != null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void ReverseVerticalMotionPhase(float cycleDurationY)
    {
        float safeDuration = Mathf.Max(0.0001f, cycleDurationY);
        float normalized = Mathf.Repeat((Time.time + phaseOffset + runtimeVerticalPhaseOffset) / safeDuration, 1f);
        float mirrored = Mathf.Repeat(0.5f - normalized, 1f);
        runtimeVerticalPhaseOffset = mirrored * safeDuration - Time.time - phaseOffset;
    }

    private void RefreshColliderCache()
    {
        platformColliders = GetComponentsInChildren<Collider>(true);
    }

    private static Vector3 ClampMin(Vector3 value, float min)
    {
        return new Vector3(
            Mathf.Max(min, value.x),
            Mathf.Max(min, value.y),
            Mathf.Max(min, value.z));
    }

    private void EnsureRuntimeDynamicFlags()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ClearStaticFlagsRecursive(transform);
    }

    private static void ClearStaticFlagsRecursive(Transform root)
    {
        if (root == null)
        {
            return;
        }

        GameObject rootObject = root.gameObject;
        if (rootObject != null && rootObject.isStatic)
        {
            rootObject.isStatic = false;
        }

        int childCount = root.childCount;
        for (int i = 0; i < childCount; i++)
        {
            ClearStaticFlagsRecursive(root.GetChild(i));
        }
    }
}
