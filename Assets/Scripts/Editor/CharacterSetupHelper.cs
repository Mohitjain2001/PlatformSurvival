#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CharacterSetupHelper
{
    static CharacterSetupHelper()
    {
        EditorApplication.delayCall += EnsurePolicePrefab;
    }

    [InitializeOnLoadMethod]
    private static void OnInit()
    {
        EditorApplication.delayCall += EnsurePolicePrefab;
    }

    [MenuItem("Tools/Setup Police Prefab")]
    public static void EnsurePolicePrefab()
    {
        string fbxPath = "Assets/Models/character_police.fbx";
        string targetPrefabPath = "Assets/Resources/Character_Police.prefab";

        // 1. Ensure ModelImporter has Humanoid rig
        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer != null)
        {
            bool needsReimport = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                needsReimport = true;
            }

            if (needsReimport)
            {
                importer.SaveAndReimport();
            }
        }

        // 2. Check if Resources prefab already exists
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(targetPrefabPath);
        if (existingPrefab != null)
        {
            return;
        }

        // 3. Load FBX asset
        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbxAsset == null)
        {
            Debug.LogWarning("[CharacterSetupHelper] Could not find " + fbxPath);
            return;
        }

        // 4. Instantiate temporary instance in scene
        GameObject instance = Object.Instantiate(fbxAsset);
        instance.name = "Character_Police";

        // Assign Animator & Controller
        Animator anim = instance.GetComponent<Animator>();
        if (anim == null) anim = instance.AddComponent<Animator>();

        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Minimo/Character/Animations/Character.controller");
        if (controller != null)
        {
            anim.runtimeAnimatorController = controller;
        }

        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // Clean any colliders on the visual model
        Collider[] cols = instance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            Object.DestroyImmediate(cols[i]);
        }

        // Save as prefab in Resources
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        PrefabUtility.SaveAsPrefabAsset(instance, targetPrefabPath);
        PrefabUtility.SaveAsPrefabAsset(instance, "Assets/Prefabs/Character_Police.prefab");

        Object.DestroyImmediate(instance);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[CharacterSetupHelper] Successfully created Character_Police.prefab in Assets/Resources and Assets/Prefabs!");
    }
}
#endif
