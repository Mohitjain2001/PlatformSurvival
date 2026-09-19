using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    [Header("Target & Offset")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0, 6.5f, -6.2f);
    [SerializeField] private Vector3 lookOffset = new Vector3(0, 0.8f, 0.8f);
    [SerializeField] private float smoothTimeXZ = 0.18f;
    [SerializeField] private float smoothTimeY = 0.25f;
    [SerializeField] private float minCameraY = 2.0f; // Prevent camera from plunging below arena

    private Vector3 currentVelocity;
    private Camera cam;
    private float targetY;
    private bool isPlayerFallingToVoid = false;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam != null)
        {
            // Adjust FOV for mobile portrait screen
            cam.fieldOfView = 65f;
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            targetY = target.position.y;
            Vector3 initPos = target.position + offset;
            initPos.y = Mathf.Max(initPos.y, minCameraY);
            transform.position = initPos;
            transform.LookAt(target.position + lookOffset);
        }
    }

    public void SetMinCameraY(float minY)
    {
        minCameraY = minY;
    }

    private void LateUpdate()
    {
        if (target == null || !target.gameObject.activeInHierarchy) return;

        Vector3 targetPos = target.position;

        // If player has fallen off the lowest floor into the void, stop tracking Y down!
        if (targetPos.y < minCameraY - offset.y)
        {
            isPlayerFallingToVoid = true;
        }

        // Target camera position
        float desiredX = targetPos.x + offset.x;
        float desiredZ = targetPos.z + offset.z;
        
        // Fast and smooth vertical follow so camera drops cleanly with player between floors
        if (!isPlayerFallingToVoid)
        {
            targetY = Mathf.Lerp(targetY, targetPos.y, Time.deltaTime * 18.0f);
        }

        float desiredY = Mathf.Max(targetY + offset.y, minCameraY);
        Vector3 desiredCameraPos = new Vector3(desiredX, desiredY, desiredZ);

        // Smooth position
        transform.position = Vector3.SmoothDamp(
            transform.position, 
            desiredCameraPos, 
            ref currentVelocity, 
            smoothTimeXZ
        );

        // Look at player or last known focus point with lookOffset
        Vector3 lookTarget = new Vector3(targetPos.x + lookOffset.x, targetY + lookOffset.y, targetPos.z + lookOffset.z);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, 
            Quaternion.LookRotation(lookTarget - transform.position), 
            Time.deltaTime * 10f
        );
    }
}
