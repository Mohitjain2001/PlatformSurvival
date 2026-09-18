using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Invisible Touch Controller.
/// Removes visible joystick icons while providing smooth, natural 360-degree touch drag controls.
/// Anywhere the player touches and drags drives the character.
/// </summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Joystick UI Setup")]
    [SerializeField] private RectTransform containerBackground;
    [SerializeField] private RectTransform handleGraphic;
    [SerializeField] private float touchRadius = 90f; // Screen pixels to reach full run speed

    private Vector2 inputVector = Vector2.zero;
    private Vector2 pointerDownPosition;
    private bool isTouching = false;

    public float Horizontal => inputVector.x != 0 ? inputVector.x : Input.GetAxisRaw("Horizontal");
    public float Vertical => inputVector.y != 0 ? inputVector.y : Input.GetAxisRaw("Vertical");
    public Vector2 Direction => new Vector2(Horizontal, Vertical).normalized;

    private void Awake()
    {
        // Visually remove the joystick background and handle icons
        Image bgImage = GetComponent<Image>();
        if (bgImage != null)
        {
            bgImage.color = Color.clear; // Completely transparent, but captures touch raycasts
        }

        if (handleGraphic != null)
        {
            Image handleImage = handleGraphic.GetComponent<Image>();
            if (handleImage != null)
            {
                handleImage.color = Color.clear;
            }
            handleGraphic.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        if (containerBackground == null)
        {
            containerBackground = GetComponent<RectTransform>();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isTouching = true;
        pointerDownPosition = eventData.position;
        inputVector = Vector2.zero;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isTouching)
        {
            isTouching = true;
            pointerDownPosition = eventData.position;
        }

        Vector2 diff = eventData.position - pointerDownPosition;
        float dist = diff.magnitude;

        if (dist > 4f)
        {
            Vector2 dir = diff / dist;
            float factor = Mathf.Clamp01(dist / touchRadius);
            inputVector = dir * factor;
        }
        else
        {
            inputVector = Vector2.zero;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isTouching = false;
        inputVector = Vector2.zero;
    }

    public void ResetJoystick()
    {
        isTouching = false;
        inputVector = Vector2.zero;
    }
}
