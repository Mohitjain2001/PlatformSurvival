#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Exports PNG assets for the Character Shop UI concept (matching media_1790511363312.png)
/// into Assets/UI/Shop/
/// </summary>
public static class ShopAssetExporter
{
    [MenuItem("Tools/Export Shop UI PNG Assets")]
    public static void ExportShopAssets()
    {
        string dir = "Assets/UI/Shop";
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        SaveSpriteAsPNG(ShopUIAssets.GetCharacterShopBanner(), Path.Combine(dir, "Shop_Banner.png"));
        SaveSpriteAsPNG(ShopUIAssets.GetNavArrowLeft(), Path.Combine(dir, "Shop_ArrowLeft.png"));
        SaveSpriteAsPNG(ShopUIAssets.GetNavArrowRight(), Path.Combine(dir, "Shop_ArrowRight.png"));
        SaveSpriteAsPNG(ShopUIAssets.GetRedCloseBtn(), Path.Combine(dir, "Shop_RedCloseBtn.png"));
        SaveSpriteAsPNG(ShopUIAssets.GetNamePill(), Path.Combine(dir, "Shop_NamePill.png"));
        SaveSpriteAsPNG(ShopUIAssets.GetEquippedBadge(), Path.Combine(dir, "Shop_EquippedBadge.png"));
        SaveSpriteAsPNG(ShopUIAssets.GetMainPanelBg(), Path.Combine(dir, "Shop_PanelBg.png"));
        SaveSpriteAsPNG(ShopUIAssets.GetSelectedCardBg(), Path.Combine(dir, "Shop_SelectedCard.png"));
        SaveSpriteAsPNG(ShopUIAssets.GetLockedCardBg(), Path.Combine(dir, "Shop_LockedCard.png"));
        SaveSpriteAsPNG(ShopUIAssets.GetLockIcon(), Path.Combine(dir, "Shop_LockIcon.png"));
        SaveSpriteAsPNG(ShopUIAssets.GetCheckmarkBadge(), Path.Combine(dir, "Shop_CheckmarkBadge.png"));
        SaveSpriteAsPNG(ShopUIAssets.GetWatchAdBtn(), Path.Combine(dir, "Shop_WatchAdBtn.png"));
        SaveSpriteAsPNG(ShopUIAssets.GetLevelUnlockBtn(), Path.Combine(dir, "Shop_LevelUnlockBtn.png"));
        SaveSpriteAsPNG(ShopUIAssets.GetGoldCoin(), Path.Combine(dir, "Shop_GoldCoin.png"));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string[] files = Directory.GetFiles(dir, "*.png");
        foreach (string file in files)
        {
            TextureImporter importer = AssetImporter.GetAtPath(file) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
        }

        Debug.Log("[ShopAssetExporter] Exported all Character Shop UI PNG assets to Assets/UI/Shop/");
    }

    private static void SaveSpriteAsPNG(Sprite sprite, string path)
    {
        if (sprite == null || sprite.texture == null) return;
        byte[] bytes = sprite.texture.EncodeToPNG();
        if (bytes != null)
        {
            File.WriteAllBytes(path, bytes);
        }
    }
}
#endif
