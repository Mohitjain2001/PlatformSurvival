using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    [Header("Target & Offset")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0, 8.5f, -7.0f);
    [SerializeField] private float smoothTime = 0.2f;

    private Vector3 currentVelocity;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void LateUpdate()
    {
        if (target == null || !target.gameObject.activeInHierarchy) return;

        Vector3 targetPosition = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);
        transform.LookAt(target.position + Vector3.up * 0.5f);
    }
}
