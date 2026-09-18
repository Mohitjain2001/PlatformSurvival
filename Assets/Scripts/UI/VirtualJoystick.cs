using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Joystick UI Setup")]
    [SerializeField] private RectTransform containerBackground;
    [SerializeField] private RectTransform handleGraphic;

    private Vector2 inputVector = Vector2.zero;
    private Camera uiCamera;

    public float Horizontal => inputVector.x != 0 ? inputVector.x : Input.GetAxisRaw("Horizontal");
    public float Vertical => inputVector.y != 0 ? inputVector.y : Input.GetAxisRaw("Vertical");
    public Vector2 Direction => new Vector2(Horizontal, Vertical).normalized;

    private void Start()
    {
        if (containerBackground == null)
        {
            containerBackground = GetComponent<RectTransform>();
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            uiCamera = canvas.worldCamera;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (containerBackground == null) return;

        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            containerBackground, 
            eventData.position, 
            uiCamera, 
            out localPoint))
        {
            // Calculate center-relative offset for clean 360-degree control
            Vector2 pivotOffset = new Vector2(
                (containerBackground.pivot.x - 0.5f) * containerBackground.sizeDelta.x,
                (containerBackground.pivot.y - 0.5f) * containerBackground.sizeDelta.y
            );
            Vector2 centerPoint = localPoint + pivotOffset;

            float radius = Mathf.Min(containerBackground.sizeDelta.x, containerBackground.sizeDelta.y) * 0.5f;
            Vector2 normalizedPos = centerPoint / radius;

            inputVector = (normalizedPos.magnitude > 1.0f) ? normalizedPos.normalized : normalizedPos;

            if (handleGraphic != null)
            {
                handleGraphic.anchoredPosition = inputVector * (radius * 0.8f);
            }
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        inputVector = Vector2.zero;
        if (handleGraphic != null)
        {
            handleGraphic.anchoredPosition = Vector2.zero;
        }
    }

    public void ResetJoystick()
    {
        inputVector = Vector2.zero;
        if (handleGraphic != null)
        {
            handleGraphic.anchoredPosition = Vector2.zero;
        }
    }
}
