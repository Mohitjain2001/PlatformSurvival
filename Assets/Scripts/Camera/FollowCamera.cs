using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    [Header("Target & Offset")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0, 15.0f, -11.0f);
    [SerializeField] private float smoothTimeXZ = 0.15f;
    [SerializeField] private float smoothTimeY = 0.45f;
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
            transform.LookAt(target.position + Vector3.up * 0.8f);
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
        
        // Smooth Y gently between floors, but freeze if player fell off the arena
        if (!isPlayerFallingToVoid)
        {
            targetY = Mathf.MoveTowards(targetY, targetPos.y, Time.deltaTime * 8.0f);
        }

        float desiredY = Mathf.Max(targetY + offset.y, minCameraY);
        Vector3 desiredCameraPos = new Vector3(desiredX, desiredY, desiredZ);

        // Smooth position
        transform.position = Vector3.SmoothDamp(
            transform.position, 
            desiredCameraPos, 
            ref currentVelocity, 
            Mathf.Max(smoothTimeXZ, smoothTimeY)
        );

        // Look at player or last known focus point
        Vector3 lookTarget = new Vector3(targetPos.x, targetY + 0.8f, targetPos.z);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, 
            Quaternion.LookRotation(lookTarget - transform.position), 
            Time.deltaTime * 10f
        );
    }
}
