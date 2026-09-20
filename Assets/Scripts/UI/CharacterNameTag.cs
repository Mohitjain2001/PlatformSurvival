using UnityEngine;
using TMPro;

/// <summary>
/// Floating 3D billboard name tag above character head.
/// Displays "Player" in vibrant lime green with a dark outline,
/// facing the active camera at all times.
/// </summary>
public class CharacterNameTag : MonoBehaviour
{
    [SerializeField] private string displayName = "Player";
    [SerializeField] private Color textColor = new Color(0.33f, 0.92f, 0.22f); // Vibrant lime green
    [SerializeField] private float heightOffset = 1.80f;

    private Transform cameraTransform;
    private TextMeshProUGUI textComponent;
    private Canvas canvas;

    public void Setup(string name, Color color, float height = 1.80f)
    {
        displayName = name;
        textColor = color;
        heightOffset = height;
        if (textComponent != null)
        {
            textComponent.text = displayName;
            textComponent.color = textColor;
        }
    }

    private void Awake()
    {
        CreateNameTagCanvas();
    }

    private void Start()
    {
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void CreateNameTagCanvas()
    {
        // 1. Root World-Space Canvas
        GameObject canvasObj = new GameObject("NameTagCanvas");
        canvasObj.transform.SetParent(transform, false);
        canvasObj.transform.localPosition = new Vector3(0, heightOffset, 0);

        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        RectTransform rt = canvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300f, 90f);
        rt.localScale = new Vector3(0.007f, 0.007f, 0.007f);

        // 2. TextMeshProUGUI Element
        GameObject textObj = new GameObject("NameText");
        textObj.transform.SetParent(canvasObj.transform, false);

        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;
        textRt.anchoredPosition = Vector2.zero;

        textComponent = textObj.AddComponent<TextMeshProUGUI>();
        if (textComponent.font == null)
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null) textComponent.font = font;
        }
        textComponent.text = displayName;
        textComponent.fontSize = 44;
        textComponent.fontStyle = FontStyles.Bold;
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.color = textColor;

        // Outline for clear readability against all backgrounds
        textComponent.outlineWidth = 0.22f;
        textComponent.outlineColor = new Color32(10, 45, 10, 255);
    }

    private void LateUpdate()
    {
        if (cameraTransform == null)
        {
            if (Camera.main != null) cameraTransform = Camera.main.transform;
            if (cameraTransform == null) return;
        }

        if (canvas != null)
        {
            // Billboard: face camera directly without flipping
            canvas.transform.rotation = cameraTransform.rotation;
        }
    }
}
