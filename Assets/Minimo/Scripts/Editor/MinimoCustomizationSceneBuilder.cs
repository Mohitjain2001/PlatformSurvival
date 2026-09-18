using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MinimoCustomizationSceneBuilder
{
    private const string ScenePath = "Assets/Minimo/Scene/Customization/Customization.unity";

    public static void CreateCustomizationScene()
    {
        OpenExistingCustomizationScene();
    }

    public static void CreateCustomizationSceneBatch()
    {
        OpenExistingCustomizationScene();
    }

    private static void OpenExistingCustomizationScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        SceneAsset existingScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if (existingScene == null)
        {
            return;
        }

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Selection.activeObject = existingScene;
    }
}
