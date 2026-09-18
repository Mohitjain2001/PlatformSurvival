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
        Vector2 position = Vector2.zero;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            containerBackground, 
            eventData.position, 
            uiCamera, 
            out position))
        {
            position.x = (position.x / containerBackground.sizeDelta.x);
            position.y = (position.y / containerBackground.sizeDelta.y);

            // Pivot compensation
            position = (containerBackground.pivot.x == 1) ? position * 2 + new Vector2(1, 0) : position * 2 - new Vector2(1, 0);

            inputVector = (position.magnitude > 1.0f) ? position.normalized : position;

            if (handleGraphic != null)
            {
                handleGraphic.anchoredPosition = new Vector2(
                    inputVector.x * (containerBackground.sizeDelta.x / 2.5f),
                    inputVector.y * (containerBackground.sizeDelta.y / 2.5f)
                );
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
