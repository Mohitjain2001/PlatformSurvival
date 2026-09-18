#pragma warning disable CS0619 // GetInstanceID() is obsolete in Unity 6 – third-party Minimo asset
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
[AddComponentMenu("Minimo/Jumping Platform")]
public class MinimoJumpingPlatform : MonoBehaviour
{
    [Tooltip("Configures bounce multiplier.")]
    [SerializeField] [Min(0.05f)] private float bounceMultiplier = 1.35f;
    [Tooltip("Configures minimum force.")]
    [FormerlySerializedAs("minimumLaunchSpeed")]
    [SerializeField] [Min(0.05f)] private float minimumForce = 7.5f;
    [Tooltip("Configures lateral carry ratio.")]
    [SerializeField] [Range(0f, 1f)] private float lateralCarryRatio = 0.3f;
    [Tooltip("Configures maximum force.")]
    [SerializeField] [Min(0.05f)] private float maximumForce = 20f;

    private const float RehitCooldown = 0.16f;

    private readonly Dictionary<int, float> nextBounceTimeByCharacterId = new Dictionary<int, float>();

    public bool TryComputeBounceVelocity(
        MinimoTpsController controller,
        Collider hitCollider,
        Vector3 hitNormal,
        out Vector3 bounceVelocity)
    {
        bounceVelocity = Vector3.zero;
        if (controller == null || hitCollider == null)
        {
            return false;
        }

        _ = hitNormal;

        Transform hitTransform = hitCollider.transform;
        if (hitTransform != transform && !hitTransform.IsChildOf(transform))
        {
            return false;
        }

        int characterId = controller.GetInstanceID();
        if (nextBounceTimeByCharacterId.TryGetValue(characterId, out float nextBounceTime) && Time.time < nextBounceTime)
        {
            return false;
        }

        Vector3 platformUp = transform.up.sqrMagnitude > 0.0001f ? transform.up.normalized : Vector3.up;
        Vector3 incomingVelocity = controller.GetCurrentVelocityWorld();
        float clampedMaxForce = Mathf.Max(0.05f, maximumForce);
        float clampedMinForce = Mathf.Clamp(Mathf.Max(0.05f, minimumForce), 0.05f, clampedMaxForce);
        float approachSpeed = Mathf.Max(0f, Vector3.Dot(-incomingVelocity, platformUp));
        float launchSpeed = Mathf.Max(
            clampedMinForce,
            approachSpeed * Mathf.Max(0.05f, bounceMultiplier));

        if (launchSpeed > clampedMaxForce)
        {
            launchSpeed = clampedMaxForce;
        }

        Vector3 lateralVelocity = Vector3.ProjectOnPlane(incomingVelocity, platformUp);
        float clampedCarryRatio = Mathf.Clamp01(lateralCarryRatio);
        float lateralLimit = launchSpeed * clampedCarryRatio;
        float lateralSpeed = lateralVelocity.magnitude;
        if (lateralSpeed > lateralLimit && lateralSpeed > 0.0001f)
        {
            lateralVelocity = lateralVelocity / lateralSpeed * lateralLimit;
        }

        Vector3 launchVelocity = lateralVelocity + platformUp * launchSpeed;
        float launchVelocityMagnitude = launchVelocity.magnitude;
        if (launchVelocityMagnitude > clampedMaxForce && launchVelocityMagnitude > 0.0001f)
        {
            launchVelocity = launchVelocity / launchVelocityMagnitude * clampedMaxForce;
        }
        else if (launchVelocityMagnitude < clampedMinForce)
        {
            launchVelocity = launchVelocityMagnitude > 0.0001f
                ? launchVelocity / launchVelocityMagnitude * clampedMinForce
                : platformUp * clampedMinForce;
        }

        bounceVelocity = launchVelocity;
        nextBounceTimeByCharacterId[characterId] = Time.time + RehitCooldown;
        return true;
    }
}
