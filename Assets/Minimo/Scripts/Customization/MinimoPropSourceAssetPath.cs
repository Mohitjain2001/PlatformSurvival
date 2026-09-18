using UnityEngine;

[DisallowMultipleComponent]
public sealed class MinimoPropSourceAssetPath : MonoBehaviour
{
    [SerializeField] private string assetPath;

    public string AssetPath
    {
        get => assetPath;
        set => assetPath = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace('\\', '/');
    }
}
