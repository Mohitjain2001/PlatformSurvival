using UnityEngine;
[CreateAssetMenu(fileName = "CategoryInfo", menuName = "Minimo/Category Info")]
public class MinimoCategoryInfo : ScriptableObject
{
    [Header("Category")]
    public string theme;

    [Header("Content")]
    public string headerText;

    public string subHeaderText;

    public Sprite previewSprite;

    [TextArea(3, 10)]
    public string contextText;

    [Header("Link")]
    [Tooltip("Configures link.")]
    public string link;

    public string titleText => headerText;
    public string contentText => contextText;
    public Sprite spriteTexture => previewSprite;
}
