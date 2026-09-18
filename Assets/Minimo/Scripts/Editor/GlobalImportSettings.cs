using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine.SceneManagement;
#endif

public class GlobalImportSettings
{
#if UNITY_EDITOR
    private const string TagManagerPath = "ProjectSettings/TagManager.asset";
    private const string DynamicsManagerPath = "ProjectSettings/DynamicsManager.asset";
    private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    private const string TmpExamplesAndExtrasPath = "Assets/TextMesh Pro/Examples & Extras";
    private const string TmpEssentialResourcesPackageName = "TMP Essential Resources.unitypackage";
    private const string TmpExamplesAndExtrasPackageName = "TMP Examples & Extras.unitypackage";
    private const string ShaderGraphPackageName = "com.unity.shadergraph";
    private const string ShaderGraphPackagePath = "Packages/com.unity.shadergraph";
    private const string ShaderGraphAddInProgressKey = "Minimo.GlobalImportSettings.ShaderGraphAddInProgress";
    private const string ShaderGraphAddFailedKey = "Minimo.GlobalImportSettings.ShaderGraphAddFailed";
    private const string ShaderGraphAssetsReimportedKey = "Minimo.GlobalImportSettings.ShaderGraphAssetsReimported.v2";
    private const string PostProcessingPackageName = "com.unity.postprocessing";
    private const string PostProcessingPackagePath = "Packages/com.unity.postprocessing";
    private const string PostProcessingAddInProgressKey = "Minimo.GlobalImportSettings.PostProcessingAddInProgress";
    private const string PostProcessingAddFailedKey = "Minimo.GlobalImportSettings.PostProcessingAddFailed";
    private const string PostProcessingAssetsImportedKey = "Minimo.GlobalImportSettings.PostProcessingAssetsImported.v1";
    private const double PackageManagerStartDelaySeconds = 0d;
    private const string MinimoRootPath = "Assets/Minimo";
    private const string StarterPackFolderPath = "Assets/Minimo/Character/Categories/Folders/Default";
    private const string StarterPackUrl = "https://assetstore.unity.com/publishers/116468";
    private const string StarterPackPromptHandledKey = "Minimo.StarterPackPrompt.Handled.v2";

    private static readonly RequiredLayer[] RequiredLayers =
    {
        new RequiredLayer("Ground", 6),
        new RequiredLayer("Character", 7),
        new RequiredLayer("Wall", 8)
    };

    private static readonly string[] PhysicsMatrixLayerNames =
    {
        "Default",
        "TransparentFX",
        "Ignore Raycast",
        "Water",
        "UI",
        "Ground",
        "Character",
        "Wall"
    };

    private static AddRequest shaderGraphAddRequest;
    private static bool shaderGraphAddQueued;
    private static double shaderGraphAddEarliestTime;
    private static AddRequest postProcessingAddRequest;
    private static bool postProcessingAddQueued;
    private static double postProcessingAddEarliestTime;

    internal static bool EnsureTextMeshProAndShaderGraphResourcesIfMissing()
    {
        ShowStarterPackPromptIfNeeded();

        try
        {
            ImportTextMeshProResourcesIfMissing();
        }
        catch (Exception)
        {
        }

        bool shaderGraphReady = EnsureShaderGraphPackageInstalled();
        return AreTextMeshProResourcesImported() && shaderGraphReady;
    }

    internal static bool EnsureBuiltInPostProcessingResourcesIfMissing()
    {
        ShowStarterPackPromptIfNeeded();

        if (IsPostProcessingPackageAvailable())
        {
            SessionState.SetBool(PostProcessingAddInProgressKey, false);
            SessionState.SetBool(PostProcessingAddFailedKey, false);
            if (!IsPostProcessingRuntimeReady())
            {
                return false;
            }

            ImportPostProcessingPackageAssetsOnce();
            return true;
        }

        if (postProcessingAddRequest != null)
        {
            if (!postProcessingAddRequest.IsCompleted)
            {
                return false;
            }

            bool succeeded = postProcessingAddRequest.Status == StatusCode.Success;
            postProcessingAddRequest = null;
            SessionState.SetBool(PostProcessingAddInProgressKey, false);
            if (succeeded)
            {
                AssetDatabase.Refresh();
                return false;
            }

            SessionState.SetBool(PostProcessingAddFailedKey, true);
            QueuePostProcessingPackageAdd();
            return false;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return false;
        }

        QueuePostProcessingPackageAdd();
        return false;
    }

    private static void QueuePostProcessingPackageAdd()
    {
        if (postProcessingAddQueued)
        {
            return;
        }

        postProcessingAddQueued = true;
        SessionState.SetBool(PostProcessingAddFailedKey, false);
        postProcessingAddEarliestTime = EditorApplication.timeSinceStartup + PackageManagerStartDelaySeconds;
        EditorApplication.update -= StartQueuedPostProcessingPackageAdd;
        EditorApplication.update += StartQueuedPostProcessingPackageAdd;
    }

    private static void StartQueuedPostProcessingPackageAdd()
    {
        if (!postProcessingAddQueued)
        {
            EditorApplication.update -= StartQueuedPostProcessingPackageAdd;
            return;
        }

        if (EditorApplication.isCompiling
            || EditorApplication.isUpdating
            || EditorApplication.timeSinceStartup < postProcessingAddEarliestTime)
        {
            return;
        }

        postProcessingAddQueued = false;
        EditorApplication.update -= StartQueuedPostProcessingPackageAdd;
        if (IsPostProcessingPackageAvailable() || postProcessingAddRequest != null)
        {
            return;
        }

        try
        {
            postProcessingAddRequest = Client.Add(PostProcessingPackageName);
            SessionState.SetBool(PostProcessingAddInProgressKey, true);
        }
        catch (Exception exception)
        {
            postProcessingAddRequest = null;
            SessionState.SetBool(PostProcessingAddInProgressKey, false);
            Debug.LogWarning($"Minimo could not start the Post Processing package installation yet: {exception.Message}");
            QueuePostProcessingPackageAdd();
        }
    }

    private static bool IsPostProcessingPackageAvailable()
    {
        return IsPackageListedInManifest(PostProcessingPackageName)
               || AssetDatabase.IsValidFolder(PostProcessingPackagePath)
               || Directory.Exists(PostProcessingPackagePath);
    }

    private static bool IsPostProcessingRuntimeReady()
    {
        const string fullTypeName = "UnityEngine.Rendering.PostProcessing.PostProcessVolume";
        Type volumeType = Type.GetType(
            $"{fullTypeName}, Unity.Postprocessing.Runtime",
            false);
        if (volumeType != null)
        {
            return true;
        }

        System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            if (assemblies[i].GetType(fullTypeName, false) != null)
            {
                return true;
            }
        }

        return false;
    }

    private static void ImportPostProcessingPackageAssetsOnce()
    {
        if (SessionState.GetBool(PostProcessingAssetsImportedKey, false))
        {
            return;
        }

        if (AssetDatabase.IsValidFolder(PostProcessingPackagePath))
        {
            AssetDatabase.ImportAsset(
                PostProcessingPackagePath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh();
        }

        SessionState.SetBool(PostProcessingAssetsImportedKey, true);
    }

    internal static bool ArePipelineIndependentImportSettingsApplied()
    {
        return AreTextMeshProAndShaderGraphResourcesImported()
               && AreLayerSettingsApplied()
               && ArePhysicsLayerCollisionSettingsApplied();
    }

    internal static bool AreTextMeshProAndShaderGraphResourcesImported()
    {
        return AreTextMeshProResourcesImported()
               && IsShaderGraphPackageAvailable()
               && IsShaderGraphImporterReady();
    }

    internal static bool EnsureLayerAndPhysicsSettings()
    {
        ShowStarterPackPromptIfNeeded();

        bool changed = EnsureLayerSettings();
        changed |= EnsurePhysicsLayerCollisionSettings();
        return changed;
    }

    internal static bool EnsureMinimoEnvironmentLightingSettings()
    {
        ShowStarterPackPromptIfNeeded();

        if (Application.isPlaying
            || EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling
            || EditorApplication.isUpdating
            || !AssetDatabase.IsValidFolder(MinimoRootPath))
        {
            return false;
        }

        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { MinimoRootPath });
        bool changed = false;
        Color ambientColor = new Color32(128, 128, 128, 255);

        for (int i = 0; i < sceneGuids.Length; i++)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
            bool openedForUpdate = false;
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            Scene previousActiveScene = SceneManager.GetActiveScene();

            try
            {
                if (!scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    openedForUpdate = true;
                }

                if (!scene.IsValid() || !scene.isLoaded || !SceneManager.SetActiveScene(scene))
                {
                    continue;
                }

                Color32 currentAmbientColor = RenderSettings.ambientLight;
                Color32 requiredAmbientColor = ambientColor;
                bool sceneChanged = RenderSettings.ambientMode != AmbientMode.Flat
                                    || currentAmbientColor.r != requiredAmbientColor.r
                                    || currentAmbientColor.g != requiredAmbientColor.g
                                    || currentAmbientColor.b != requiredAmbientColor.b
                                    || currentAmbientColor.a != requiredAmbientColor.a;
                if (!sceneChanged)
                {
                    continue;
                }

                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = ambientColor;
                EditorSceneManager.MarkSceneDirty(scene);
                if (EditorSceneManager.SaveScene(scene))
                {
                    changed = true;
                }
            }
            finally
            {
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }

                if (openedForUpdate && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        return changed;
    }

    internal static void ShowStarterPackPromptIfNeeded(bool forcePrompt = false)
    {
        if (Application.isBatchMode
            || EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling
            || EditorApplication.isUpdating
            || AssetDatabase.IsValidFolder(StarterPackFolderPath)
            || Directory.Exists(StarterPackFolderPath)
            || !forcePrompt && SessionState.GetBool(StarterPackPromptHandledKey, false))
        {
            return;
        }

        bool shouldOpen = EditorUtility.DisplayDialog(
            "Minimo Starter Pack",
            "MINIMO STARTER PACK - Free Character & Essential Controls is not installed.\n\nWould you like to install it? The Starter Pack is required to access the Character Customization and Playground scenes.",
            "Get Starter Pack",
            "Not Now");
        SessionState.SetBool(StarterPackPromptHandledKey, true);

        if (shouldOpen)
        {
            Application.OpenURL(StarterPackUrl);
        }
    }

    internal static void ScheduleStarterPackPromptIfNeeded()
    {
        EditorApplication.update -= ShowScheduledStarterPackPrompt;
        EditorApplication.update += ShowScheduledStarterPackPrompt;
    }

    private static void ShowScheduledStarterPackPrompt()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.update -= ShowScheduledStarterPackPrompt;
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        EditorApplication.update -= ShowScheduledStarterPackPrompt;
        ShowStarterPackPromptIfNeeded();
    }

    private static void ImportTextMeshProResourcesIfMissing()
    {
        bool missingEssentials = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TmpSettingsPath) == null;
        bool missingExamplesAndExtras = !AssetDatabase.IsValidFolder(TmpExamplesAndExtrasPath);
        if (!missingEssentials && !missingExamplesAndExtras)
        {
            return;
        }

        bool imported = false;
        if (missingEssentials)
        {
            imported |= ImportTextMeshProPackageIfFound(TmpEssentialResourcesPackageName);
        }

        if (missingExamplesAndExtras)
        {
            imported |= ImportTextMeshProPackageIfFound(TmpExamplesAndExtrasPackageName);
        }

        if (imported)
        {
            AssetDatabase.Refresh();
        }
    }

    private static bool ImportTextMeshProPackageIfFound(string packageName)
    {
        if (!TryFindTextMeshProPackage(packageName, out string packagePath))
        {
            return false;
        }

        AssetDatabase.ImportPackage(packagePath, false);
        return true;
    }

    private static bool TryFindTextMeshProPackage(string packageName, out string packagePath)
    {
        string[] projectRelativePackageResourceFolders =
        {
            "Packages/com.unity.textmeshpro/Package Resources",
            "Packages/com.unity.ugui/Package Resources"
        };

        for (int i = 0; i < projectRelativePackageResourceFolders.Length; i++)
        {
            string candidate = $"{projectRelativePackageResourceFolders[i]}/{packageName}";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(candidate) != null || File.Exists(candidate))
            {
                packagePath = candidate;
                return true;
            }
        }

        string packageCachePath = Path.Combine("Library", "PackageCache");
        if (Directory.Exists(packageCachePath))
        {
            string[] packageDirectoryPatterns = { "com.unity.textmeshpro*", "com.unity.ugui*" };
            for (int i = 0; i < packageDirectoryPatterns.Length; i++)
            {
                string[] packageDirectories = Directory.GetDirectories(
                    packageCachePath,
                    packageDirectoryPatterns[i],
                    SearchOption.TopDirectoryOnly);
                for (int j = 0; j < packageDirectories.Length; j++)
                {
                    string candidate = Path.Combine(packageDirectories[j], "Package Resources", packageName);
                    if (File.Exists(candidate))
                    {
                        packagePath = Path.GetFullPath(candidate).Replace('\\', '/');
                        return true;
                    }
                }
            }
        }

        packagePath = null;
        return false;
    }

    private static bool AreTextMeshProResourcesImported()
    {
        return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TmpSettingsPath) != null
               && AssetDatabase.IsValidFolder(TmpExamplesAndExtrasPath);
    }

    private static bool EnsureShaderGraphPackageInstalled()
    {
        if (IsShaderGraphPackageAvailable())
        {
            SessionState.SetBool(ShaderGraphAddInProgressKey, false);
            SessionState.SetBool(ShaderGraphAddFailedKey, false);
            if (!IsShaderGraphImporterReady())
            {
                return false;
            }

            ReimportMinimoShaderGraphAssetsOnce();
            return true;
        }

        if (Application.isBatchMode || SessionState.GetBool(ShaderGraphAddFailedKey, false))
        {
            return false;
        }

        if (shaderGraphAddRequest != null)
        {
            if (!shaderGraphAddRequest.IsCompleted)
            {
                return false;
            }

            bool succeeded = shaderGraphAddRequest.Status == StatusCode.Success;
            shaderGraphAddRequest = null;
            SessionState.SetBool(ShaderGraphAddInProgressKey, false);
            if (succeeded)
            {
                AssetDatabase.Refresh();
                return false;
            }

            SessionState.SetBool(ShaderGraphAddFailedKey, true);
            return false;
        }

        if (EditorApplication.isCompiling
            || EditorApplication.isUpdating
            || SessionState.GetBool(ShaderGraphAddInProgressKey, false))
        {
            return false;
        }

        QueueShaderGraphPackageAdd();
        return false;
    }

    private static void QueueShaderGraphPackageAdd()
    {
        if (shaderGraphAddQueued)
        {
            return;
        }

        shaderGraphAddQueued = true;
        shaderGraphAddEarliestTime = EditorApplication.timeSinceStartup + PackageManagerStartDelaySeconds;
        EditorApplication.update -= StartQueuedShaderGraphPackageAdd;
        EditorApplication.update += StartQueuedShaderGraphPackageAdd;
    }

    private static void StartQueuedShaderGraphPackageAdd()
    {
        if (!shaderGraphAddQueued)
        {
            EditorApplication.update -= StartQueuedShaderGraphPackageAdd;
            return;
        }

        if (EditorApplication.isCompiling
            || EditorApplication.isUpdating
            || EditorApplication.timeSinceStartup < shaderGraphAddEarliestTime)
        {
            return;
        }

        shaderGraphAddQueued = false;
        EditorApplication.update -= StartQueuedShaderGraphPackageAdd;
        if (IsShaderGraphPackageAvailable() || shaderGraphAddRequest != null)
        {
            return;
        }

        shaderGraphAddRequest = Client.Add(ShaderGraphPackageName);
        SessionState.SetBool(ShaderGraphAddInProgressKey, true);
    }

    private static void ImportShaderGraphPackageAssetsIfPresent()
    {
        if (!AssetDatabase.IsValidFolder(ShaderGraphPackagePath))
        {
            return;
        }

        AssetDatabase.ImportAsset(
            ShaderGraphPackagePath,
            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.Refresh();
    }

    private static bool IsShaderGraphPackageAvailable()
    {
        return IsPackageListedInManifest(ShaderGraphPackageName);
    }

    private static bool IsShaderGraphImporterReady()
    {
        return Type.GetType(
                   "UnityEditor.ShaderGraph.ShaderGraphImporter, Unity.ShaderGraph.Editor",
                   false) != null;
    }

    private static void ReimportMinimoShaderGraphAssetsOnce()
    {
        if (SessionState.GetBool(ShaderGraphAssetsReimportedKey, false)
            || !Directory.Exists(MinimoRootPath))
        {
            return;
        }

        ImportShaderGraphPackageAssetsIfPresent();

        string[] shaderGraphPaths = Directory.GetFiles(
            MinimoRootPath,
            "*.shadergraph",
            SearchOption.AllDirectories);
        for (int i = 0; i < shaderGraphPaths.Length; i++)
        {
            AssetDatabase.ImportAsset(
                shaderGraphPaths[i].Replace('\\', '/'),
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        SessionState.SetBool(ShaderGraphAssetsReimportedKey, true);
    }

    private static bool IsPackageListedInManifest(string packageName)
    {
        const string manifestPath = "Packages/manifest.json";
        return File.Exists(manifestPath)
               && File.ReadAllText(manifestPath).IndexOf(
                   $"\"{packageName}\"",
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool EnsureLayerSettings()
    {
        UnityEngine.Object tagManager = LoadProjectSettingsAsset(TagManagerPath);
        if (tagManager == null)
        {
            return false;
        }

        SerializedObject serializedTagManager = new SerializedObject(tagManager);
        SerializedProperty layers = serializedTagManager.FindProperty("layers");
        if (layers == null || !layers.isArray || layers.arraySize < 32)
        {
            return false;
        }

        bool changed = false;
        for (int i = 0; i < RequiredLayers.Length; i++)
        {
            RequiredLayer requiredLayer = RequiredLayers[i];
            int existingIndex = FindLayerIndex(layers, requiredLayer.Name);
            if (existingIndex >= 0)
            {
                continue;
            }

            int targetIndex = IsLayerIndexWritable(requiredLayer.PreferredIndex)
                              && IsLayerEmpty(layers, requiredLayer.PreferredIndex)
                ? requiredLayer.PreferredIndex
                : FindFirstEmptyLayerIndex(layers);

            if (targetIndex < 0)
            {
                continue;
            }

            layers.GetArrayElementAtIndex(targetIndex).stringValue = requiredLayer.Name;
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        serializedTagManager.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tagManager);
        return true;
    }

    private static bool AreLayerSettingsApplied()
    {
        UnityEngine.Object tagManager = LoadProjectSettingsAsset(TagManagerPath);
        if (tagManager == null)
        {
            return false;
        }

        SerializedObject serializedTagManager = new SerializedObject(tagManager);
        SerializedProperty layers = serializedTagManager.FindProperty("layers");
        if (layers == null || !layers.isArray)
        {
            return false;
        }

        for (int i = 0; i < RequiredLayers.Length; i++)
        {
            if (FindLayerIndex(layers, RequiredLayers[i].Name) < 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool EnsurePhysicsLayerCollisionSettings()
    {
        bool changed = false;
        for (int i = 0; i < PhysicsMatrixLayerNames.Length; i++)
        {
            int layerA = GetConfiguredLayerIndex(PhysicsMatrixLayerNames[i]);
            if (layerA < 0)
            {
                continue;
            }

            for (int j = i; j < PhysicsMatrixLayerNames.Length; j++)
            {
                int layerB = GetConfiguredLayerIndex(PhysicsMatrixLayerNames[j]);
                if (layerB < 0)
                {
                    continue;
                }

                bool shouldIgnore = !ShouldPhysicsLayersCollide(PhysicsMatrixLayerNames[i], PhysicsMatrixLayerNames[j]);
                if (Physics.GetIgnoreLayerCollision(layerA, layerB) == shouldIgnore)
                {
                    continue;
                }

                Physics.IgnoreLayerCollision(layerA, layerB, shouldIgnore);
                changed = true;
            }
        }

        if (changed)
        {
            MarkProjectSettingsDirty(DynamicsManagerPath);
        }

        return changed;
    }

    private static bool ArePhysicsLayerCollisionSettingsApplied()
    {
        for (int i = 0; i < PhysicsMatrixLayerNames.Length; i++)
        {
            int layerA = GetConfiguredLayerIndex(PhysicsMatrixLayerNames[i]);
            if (layerA < 0)
            {
                return false;
            }

            for (int j = i; j < PhysicsMatrixLayerNames.Length; j++)
            {
                int layerB = GetConfiguredLayerIndex(PhysicsMatrixLayerNames[j]);
                if (layerB < 0)
                {
                    return false;
                }

                bool shouldIgnore = !ShouldPhysicsLayersCollide(PhysicsMatrixLayerNames[i], PhysicsMatrixLayerNames[j]);
                if (Physics.GetIgnoreLayerCollision(layerA, layerB) != shouldIgnore)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool ShouldPhysicsLayersCollide(string layerA, string layerB)
    {
        return !IsLayerPair(layerA, layerB, "Default", "Character");
    }

    private static bool IsLayerPair(string layerA, string layerB, string expectedA, string expectedB)
    {
        return string.Equals(layerA, expectedA, StringComparison.Ordinal)
               && string.Equals(layerB, expectedB, StringComparison.Ordinal)
               || string.Equals(layerA, expectedB, StringComparison.Ordinal)
               && string.Equals(layerB, expectedA, StringComparison.Ordinal);
    }

    private static int GetConfiguredLayerIndex(string layerName)
    {
        UnityEngine.Object tagManager = LoadProjectSettingsAsset(TagManagerPath);
        if (tagManager == null)
        {
            return -1;
        }

        SerializedObject serializedTagManager = new SerializedObject(tagManager);
        SerializedProperty layers = serializedTagManager.FindProperty("layers");
        return layers != null && layers.isArray ? FindLayerIndex(layers, layerName) : -1;
    }

    private static int FindLayerIndex(SerializedProperty layers, string layerName)
    {
        for (int i = 0; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (string.Equals(layer.stringValue, layerName, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindFirstEmptyLayerIndex(SerializedProperty layers)
    {
        for (int i = 6; i < layers.arraySize; i++)
        {
            if (IsLayerEmpty(layers, i))
            {
                return i;
            }
        }

        return IsLayerEmpty(layers, 3) ? 3 : -1;
    }

    private static bool IsLayerEmpty(SerializedProperty layers, int index)
    {
        return index >= 0
               && index < layers.arraySize
               && string.IsNullOrEmpty(layers.GetArrayElementAtIndex(index).stringValue);
    }

    private static bool IsLayerIndexWritable(int index)
    {
        return index == 3 || index >= 6 && index <= 31;
    }

    private static UnityEngine.Object LoadProjectSettingsAsset(string path)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        return assets != null && assets.Length > 0 ? assets[0] : null;
    }

    private static void MarkProjectSettingsDirty(string path)
    {
        UnityEngine.Object settingsAsset = LoadProjectSettingsAsset(path);
        if (settingsAsset != null)
        {
            EditorUtility.SetDirty(settingsAsset);
        }
    }

    private readonly struct RequiredLayer
    {
        public RequiredLayer(string name, int preferredIndex)
        {
            Name = name;
            PreferredIndex = preferredIndex;
        }

        public string Name { get; }
        public int PreferredIndex { get; }
    }
#endif
}
