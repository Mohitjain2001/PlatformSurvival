using System;
using UnityEngine;

[Serializable]
public class CharacterData
{
    public string id;
    public string displayName;
    public string prefabPath;
    public float scale = 1.0f;
    public Vector3 localOffset = Vector3.zero;
    public int price = 500;
    public Color themeColor = Color.white;

    public CharacterData(string id, string displayName, string prefabPath, float scale = 1.0f, Vector3 localOffset = default, int price = 500, Color themeColor = default)
    {
        this.id = id;
        this.displayName = displayName;
        this.prefabPath = prefabPath;
        this.scale = scale;
        this.localOffset = localOffset;
        this.price = price;
        this.themeColor = (themeColor == default) ? Color.white : themeColor;
    }
}
