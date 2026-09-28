#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ensures CharacterButton (Shop Button) exists as a permanent GameObject in SplashScene.unity hierarchy.
/// </summary>
[InitializeOnLoad]
public static class BakeCharacterButton
{
    static BakeCharacterButton()
    {
        EditorApplication.delayCall += EnsureCharacterButtonInScene;
    }

    public static void EnsureCharacterButtonInScene()
    {
        UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (activeScene.name != null && activeScene.name.Contains("Splash"))
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            Transform existingBtn = canvas.transform.Find("CharacterButton");
            if (existingBtn == null) existingBtn = canvas.transform.Find("ShopButton");
            if (existingBtn == null) existingBtn = canvas.transform.Find("Shop Button");

            if (existingBtn == null)
            {
                GameObject btnObj = new GameObject("CharacterButton");
                btnObj.transform.SetParent(canvas.transform, false);

                RectTransform rt = btnObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(90f, -88f);
                rt.sizeDelta = new Vector2(105f, 105f);

                Image img = btnObj.AddComponent<Image>();
                img.color = Color.white;

                Sprite shopSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Background/Shop.png");
                if (shopSprite == null)
                {
                    Object[] sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/UI/Background/Shop.png");
                    foreach (Object s in sprites)
                    {
                        if (s is Sprite sp)
                        {
                            shopSprite = sp;
                            break;
                        }
                    }
                }
                if (shopSprite != null) img.sprite = shopSprite;

                Button btn = btnObj.AddComponent<Button>();

                SplashManager sm = canvas.GetComponent<SplashManager>();
                if (sm == null) sm = Object.FindFirstObjectByType<SplashManager>();
                if (sm != null)
                {
                    SerializedObject so = new SerializedObject(sm);
                    SerializedProperty prop = so.FindProperty("characterButton");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = btn;
                        so.ApplyModifiedProperties();
                    }
                }

                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log("[BakeCharacterButton] Permanent CharacterButton added to UI Canvas in SplashScene.unity!");
            }
        }
    }
}
#endif
