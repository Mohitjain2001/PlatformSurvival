using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
#endif

public class CharacterImportSettings
{
}

#if UNITY_EDITOR
[InitializeOnLoad]
internal static class CharacterImportSettingsProcessor
{
    private const string ImportSettingsVersion = "v23";
    private const string PipelineImportPromptVersion = "v3";
    private const string PipelinePackageImportVersion = "v17";
    private const double PostImportDelaySeconds = 0.1d;
    internal const string CharacterImportSettingsPath = "Assets/Minimo/Scripts/Editor/CharacterImportSettings.cs";
    private const string EditorImportSettingsPath = "Assets/Minimo/Scripts/Editor/EditorImportSettings.cs";
    private const string TagManagerPath = "ProjectSettings/TagManager.asset";
    private const string DynamicsManagerPath = "ProjectSettings/DynamicsManager.asset";
    private const string MinimoRootPath = "Assets/Minimo";
    private const string UrpFolderPath = "Assets/Minimo/Scene/Visuals/Pipelines/URP";
    private const string HdrpFolderPath = "Assets/Minimo/Scene/Visuals/Pipelines/HDRP";
    private const string BuiltInFolderPath = "Assets/Minimo/Scene/Visuals/Pipelines/Built-In";
    private const string PipelinePackagesRootPath = "Assets/Minimo/Scene/Visuals/Pipelines/Package Assets";
    private const string UrpRenderPipelinePath = "Assets/Minimo/Scene/Visuals/Pipelines/URP/Render Pipeline.asset";
    private const string UrpRendererPath = "Assets/Minimo/Scene/Visuals/Pipelines/URP/Renderer.asset";
    private const string UrpGlobalVolumeProfilePath = "Assets/Minimo/Scene/Visuals/Pipelines/URP/Global Volume Profile.asset";
    private const string HdrpRenderPipelinePath = "Assets/Minimo/Scene/Visuals/Pipelines/HDRP/Render Pipeline.asset";
    private const string HdrpVolumeProfilePath = "Assets/Minimo/Scene/Visuals/Pipelines/HDRP/Volume Profile.asset";
    private const string UniversalRenderPipelineAssetTypeName = "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset";
    private const string HighDefinitionRenderPipelineAssetTypeName = "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset";
    private const string SavedPrefabsRootPath = "Assets/Minimo/Character/SavedPrefabs";
    private const string SavedPrefabsCharactersRootPath = "Assets/Minimo/Character/SavedPrefabs/Characters";
    private const string CustomizationCharacterPrefabPath = "Assets/Minimo/Scene/Customization/Assets/Character.prefab";
    private const string CustomizationDisplayPrefabPath = "Assets/Minimo/Scene/Customization/Assets/Display.prefab";
    private const ImportAssetOptions SavedPrefabImportOptions =
        ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport;
    internal const string ImportPostprocessSuppressKey = "Minimo.ImportSettings.SuppressMinimoImportPostprocess";

    private static readonly string[] PipelineFolderPaths =
    {
        "Assets/Minimo/Scene/Built-In",
        "Assets/Minimo/Scene/BuiltIn",
        "Assets/Minimo/Scene/HDRP",
        "Assets/Minimo/Scene/URP",
        UrpFolderPath,
        HdrpFolderPath,
        BuiltInFolderPath,
        "Assets/Minimo/Scene/Visuals/Pipelines/Built-In",
        "Assets/Minimo/Scene/Visuals/Pipelines/BuiltIn"
    };

    private static readonly string[] CharacterPrefabSearchRoots =
    {
        "Assets/Minimo/Customize",
        SavedPrefabsCharactersRootPath
    };

    private static readonly string[] CharacterMaterialSearchRoots =
    {
        "Assets/Minimo/Character/Categories",
        "Assets/Minimo/Character/SavedPrefabs/Materials"
    };

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

    private static bool isPostImportWorkQueued;
    private static bool isApplyingCharacterImportSettings;
    private static bool isReimportingCharacterAssets;
    private static double nextPostImportWorkTime;

    static CharacterImportSettingsProcessor()
    {
        InitializePackageImportHook();
        ScheduleIfRequired();
    }

    [InitializeOnLoadMethod]
    private static void InitializeAfterEditorLoad()
    {
        InitializePackageImportHook();
        ScheduleIfRequired();
    }

    [DidReloadScripts]
    private static void HandleScriptsReloaded()
    {
        InitializePackageImportHook();
        ScheduleIfRequired();
    }

    internal static bool IsReimportingCharacterAssets => isReimportingCharacterAssets;

    private static bool IsEditorUnavailableForImportWork()
    {
        return Application.isPlaying
               || EditorApplication.isPlayingOrWillChangePlaymode
               || EditorApplication.isCompiling
               || EditorApplication.isUpdating;
    }

    internal static void ScheduleFromScriptImport()
    {
        SessionState.SetBool(BuildForceRunKey(), true);
        QueuePostImportWork();
    }

    private static void InitializePackageImportHook()
    {
        AssetDatabase.importPackageCompleted -= HandlePackageImportCompleted;
        AssetDatabase.importPackageCompleted += HandlePackageImportCompleted;
        AssetDatabase.importPackageCancelled -= HandlePackageImportCancelled;
        AssetDatabase.importPackageCancelled += HandlePackageImportCancelled;
        AssetDatabase.importPackageFailed -= HandlePackageImportFailed;
        AssetDatabase.importPackageFailed += HandlePackageImportFailed;
    }

    private static void HandlePackageImportCancelled(string packageName)
    {
        if (HasEditorImportSettingsScript())
        {
            return;
        }

        MinimoImportCompatibility.RestorePipelineImportProtectedState();
        RetryActivePipelinePackageImport();
    }

    private static void HandlePackageImportFailed(string packageName, string errorMessage)
    {
        if (HasEditorImportSettingsScript())
        {
            return;
        }

        MinimoImportCompatibility.RestorePipelineImportProtectedState();
        Debug.LogWarning($"Minimo package import failed for '{packageName}': {errorMessage}");
        RetryActivePipelinePackageImport();
    }

    private static void RetryActivePipelinePackageImport()
    {
        RenderPipelineFlavor flavor = DetectProjectRenderPipelineFlavor();
        ClearInactivePipelinePackageImportQueues(flavor);
        if (IsImportablePipelineFlavor(flavor)
            && IsPipelinePackageImportQueued(flavor)
            && IsPipelinePackageImportActive(flavor))
        {
            int index = SessionState.GetInt(BuildPipelinePackageQueueIndexKey(flavor), 0);
            SessionState.SetInt(BuildPipelinePackageQueueIndexKey(flavor), Mathf.Max(0, index - 1));
            SessionState.SetBool(BuildPipelinePackageQueueActiveKey(flavor), false);
            EditorApplication.delayCall += ScheduleFromScriptImport;
        }
    }

    private static void HandlePackageImportCompleted(string packageName)
    {
        if (HasEditorImportSettingsScript())
        {
            return;
        }

        MinimoImportCompatibility.RestorePipelineImportProtectedState();

        if (ContinueAnyPipelinePackageImportQueue())
        {
            return;
        }

        if (IsLikelyMinimoPackageName(packageName))
        {
            RenderPipelineFlavor activeFlavor = DetectProjectRenderPipelineFlavor();
            if (IsImportablePipelineFlavor(activeFlavor)
                && !HasPipelineImportApproval(activeFlavor)
                && !HasPipelineImportCompleted(activeFlavor))
            {
                SessionState.SetBool(BuildPipelineImportPromptRequiredKey(activeFlavor), true);
            }
        }

        ScheduleFromScriptImport();
    }

    private static void ScheduleIfRequired()
    {
        GlobalImportSettings.ScheduleStarterPackPromptIfNeeded();

        if (!ShouldRunCharacterImportSettings())
        {
            return;
        }

        QueuePostImportWork();
    }

    private static bool ShouldRunCharacterImportSettings()
    {
        if (SessionState.GetBool(BuildForceRunKey(), false))
        {
            return true;
        }

        RenderPipelineFlavor activeFlavor = DetectProjectRenderPipelineFlavor();
        if (IsImportablePipelineFlavor(activeFlavor)
            && !EditorPrefs.GetBool(BuildInitialSetupPromptAcceptedKey(activeFlavor), false))
        {
            return true;
        }

        if (activeFlavor == RenderPipelineFlavor.BuiltIn
            && (!EditorPrefs.GetBool(BuildPersistentPipelineImportConsentKey(activeFlavor), false)
                || !HasPipelineImportCompleted(activeFlavor)
                || !ArePipelinePackageImportsCurrent(activeFlavor)
                || !MinimoImportCompatibility.AreBuiltInPostProcessingResourcesReady()))
        {
            return true;
        }

        string currentImportHash = BuildCurrentImportHash();
        string completedImportHash = EditorPrefs.GetString(BuildCompletedImportHashKey(), string.Empty);
        return !string.Equals(currentImportHash, completedImportHash, StringComparison.Ordinal);
    }

    private static void QueuePostImportWork()
    {
        isPostImportWorkQueued = true;
        nextPostImportWorkTime = EditorApplication.timeSinceStartup + PostImportDelaySeconds;
        EditorApplication.update -= RunPostImportWorkWhenEditorIsIdle;
        EditorApplication.update += RunPostImportWorkWhenEditorIsIdle;
    }

    private static void RunPostImportWorkWhenEditorIsIdle()
    {
        if (!isPostImportWorkQueued)
        {
            EditorApplication.update -= RunPostImportWorkWhenEditorIsIdle;
            return;
        }

        if (isApplyingCharacterImportSettings
            || IsEditorUnavailableForImportWork()
            || EditorApplication.timeSinceStartup < nextPostImportWorkTime)
        {
            return;
        }

        isPostImportWorkQueued = false;
        EditorApplication.update -= RunPostImportWorkWhenEditorIsIdle;
        ApplyCharacterImportSettingsOnce();
    }

    #region Global Settings

    private static void ApplyCharacterImportSettingsOnce()
    {
        if (IsEditorUnavailableForImportWork())
        {
            QueuePostImportWork();
            return;
        }

        if (isApplyingCharacterImportSettings)
        {
            return;
        }

        GlobalImportSettings.ShowStarterPackPromptIfNeeded();

        bool hasEditorImportSettings = HasEditorImportSettingsScript();
        if (hasEditorImportSettings)
        {
            ScheduleEditorImportSettings();
            SessionState.SetBool(BuildForceRunKey(), false);
            return;
        }

        RenderPipelineFlavor activeFlavor = DetectProjectRenderPipelineFlavor();
        ClearInactivePipelinePackageImportQueues(activeFlavor);
        if (IsImportablePipelineFlavor(activeFlavor)
            && !EnsurePipelineImportApproval(activeFlavor))
        {
            SessionState.SetBool(BuildForceRunKey(), false);
            return;
        }

        isApplyingCharacterImportSettings = true;
        bool completed = false;
        int removedMissingScripts = 0;
        int removedAnimators = 0;
        bool changedProjectSettings = false;

        try
        {
            if (!hasEditorImportSettings)
            {
                changedProjectSettings |= ApplyRenderPipelineImportSettingsForDetectedPipeline();
            }

            if (IsAnyPipelinePackageImportQueued())
            {
                return;
            }

            changedProjectSettings |= GlobalImportSettings.EnsureMinimoEnvironmentLightingSettings();
            bool coreResourcesReady = GlobalImportSettings.EnsureTextMeshProAndShaderGraphResourcesIfMissing();
            changedProjectSettings |= GlobalImportSettings.EnsureLayerAndPhysicsSettings();

            if (changedProjectSettings)
            {
                AssetDatabase.SaveAssets();
            }

            bool builtInResourcesReady = activeFlavor != RenderPipelineFlavor.BuiltIn
                                         || MinimoImportCompatibility.AreBuiltInPostProcessingResourcesReady();
            if (!coreResourcesReady || !builtInResourcesReady)
            {
                return;
            }

            ReimportSavedPrefabsNow();
            removedMissingScripts = RemoveMissingScriptsFromMinimoPrefabs();
            if (!hasEditorImportSettings)
            {
                removedAnimators = RemoveAnimatorsFromSavedPrefabs();
            }

            if (removedMissingScripts > 0 || removedAnimators > 0)
            {
                AssetDatabase.SaveAssets();
            }

            ReimportCharacterPrefabsAndMaterials();

            removedMissingScripts += RemoveMissingScriptsFromMinimoScenes();
            if (removedMissingScripts > 0)
            {
                AssetDatabase.SaveAssets();
            }

            ReimportSavedPrefabsNow();
            completed = true;
        }
        catch (Exception)
        {
        }
        finally
        {
            isApplyingCharacterImportSettings = false;
            bool shouldRetry = !completed && !hasEditorImportSettings;
            SessionState.SetBool(BuildForceRunKey(), shouldRetry);
            if (shouldRetry)
            {
                QueuePostImportWork();
            }
        }

        if (!completed)
        {
            return;
        }

        EditorPrefs.SetString(BuildCompletedImportHashKey(), BuildCurrentImportHash());

    }

    [MenuItem("Tools/Occlusion/Minimo/Apply Project Settings", false, 2100)]
    private static void ApplyProjectSettingsMenu()
    {
        GlobalImportSettings.ShowStarterPackPromptIfNeeded(true);

        if (InvokeEditorImportSettingsMenu(nameof(ApplyProjectSettingsMenu)))
        {
            return;
        }

        ScheduleFromScriptImport();
    }

    [MenuItem("Tools/Occlusion/Minimo/Refresh Characters", false, 2101)]
    private static void RefreshCharactersMenu()
    {
        if (InvokeEditorImportSettingsMenu(nameof(RefreshCharactersMenu)))
        {
            return;
        }

        ReimportCharacterPrefabsAndMaterials();
        ReimportSavedPrefabsNow();
    }

    private static void ApplyProjectImportSettings()
    {
        GlobalImportSettings.ShowStarterPackPromptIfNeeded();

        RenderPipelineFlavor activeFlavor = DetectProjectRenderPipelineFlavor();
        ClearInactivePipelinePackageImportQueues(activeFlavor);
        if (IsImportablePipelineFlavor(activeFlavor)
            && !EnsurePipelineImportApproval(activeFlavor))
        {
            return;
        }

        bool changedProjectSettings = false;
        if (!HasEditorImportSettingsScript())
        {
            changedProjectSettings |= ApplyRenderPipelineImportSettingsForDetectedPipeline();
        }

        if (IsAnyPipelinePackageImportQueued())
        {
            return;
        }

        changedProjectSettings |= GlobalImportSettings.EnsureMinimoEnvironmentLightingSettings();
        GlobalImportSettings.EnsureTextMeshProAndShaderGraphResourcesIfMissing();
        changedProjectSettings |= GlobalImportSettings.EnsureLayerAndPhysicsSettings();

        if (changedProjectSettings)
        {
            AssetDatabase.SaveAssets();
        }

        ReimportSavedPrefabsNow();
        int removedMissingScripts = RemoveMissingScriptsFromMinimoPrefabs();
        removedMissingScripts += RemoveMissingScriptsFromMinimoScenes();
        if (removedMissingScripts > 0)
        {
            AssetDatabase.SaveAssets();
        }

        ReimportSavedPrefabsNow();
    }

    private static bool InvokeEditorImportSettingsMenu(string methodName)
    {
        Type editorImportSettingsType =
            typeof(CharacterImportSettingsProcessor).Assembly.GetType("EditorImportSettings")
            ?? Type.GetType("EditorImportSettings");
        MethodInfo method = editorImportSettingsType?.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null)
        {
            return false;
        }

        method.Invoke(null, null);
        return true;
    }

    private static bool ScheduleEditorImportSettings()
    {
        Type editorImportSettingsType =
            typeof(CharacterImportSettingsProcessor).Assembly.GetType("EditorImportSettings")
            ?? Type.GetType("EditorImportSettings");
        MethodInfo method = editorImportSettingsType?.GetMethod(
            "ScheduleFirstImportSettings",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
        {
            return false;
        }

        method.Invoke(null, new object[] { true });
        return true;
    }

    private static int RemoveMissingScriptsFromMinimoPrefabs()
    {
        SortedSet<string> prefabPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        AddAssetPaths(prefabPaths, "t:Prefab", new[] { MinimoRootPath }, ".prefab");

        int removedCount = 0;

        foreach (string prefabPath in prefabPaths)
        {
            GameObject prefabRoot = null;
            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                int removedFromPrefab = RemoveMissingScriptsRecursive(prefabRoot);
                int updatedVolumes = EnsureUrpGlobalVolumeProfilesInPrefab(prefabRoot);
                if (removedFromPrefab <= 0 && updatedVolumes <= 0)
                {
                    continue;
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                removedCount += removedFromPrefab;
            }
            catch (Exception)
            {
            }
            finally
            {
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        return removedCount;
    }

    private static int RemoveAnimatorsFromSavedPrefabs()
    {
        string[] prefabPaths = FindSavedPrefabCharacterPaths();
        int removedCount = 0;

        for (int i = 0; i < prefabPaths.Length; i++)
        {
            string prefabPath = prefabPaths[i];
            GameObject prefabRoot = null;
            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                Animator[] animators = prefabRoot.GetComponentsInChildren<Animator>(true);
                if (animators == null || animators.Length == 0)
                {
                    continue;
                }

                for (int j = 0; j < animators.Length; j++)
                {
                    if (animators[j] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(animators[j], true);
                        removedCount++;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            catch (Exception)
            {
            }
            finally
            {
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        return removedCount;
    }

    private static int RemoveMissingScriptsRecursive(GameObject prefabRoot)
    {
        if (prefabRoot == null)
        {
            return 0;
        }

        int removedCount = 0;
        Transform[] transforms = prefabRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];
            if (current != null)
            {
                removedCount += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(current.gameObject);
            }
        }

        return removedCount;
    }

    private static int RemoveMissingScriptsFromOpenMinimoSceneObjects()
    {
        int removedCount = 0;
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded)
            {
                continue;
            }

            bool cleanWholeScene = IsPathUnderMinimo(scene.path);
            bool changedScene = false;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject root = roots[rootIndex];
                if (root == null)
                {
                    continue;
                }

                int before = removedCount;
                removedCount += RemoveMissingScriptsFromSceneObject(root, cleanWholeScene);
                changedScene |= removedCount != before;
            }

            if (changedScene)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        return removedCount;
    }

    private static int RemoveMissingScriptsFromMinimoScenes()
    {
        if (IsEditorUnavailableForImportWork() || !AssetDatabase.IsValidFolder(MinimoRootPath))
        {
            return 0;
        }

        SortedSet<string> scenePaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        AddAssetPaths(scenePaths, "t:Scene", new[] { MinimoRootPath }, ".unity");

        int removedCount = 0;
        foreach (string scenePath in scenePaths)
        {
            bool openedForCleanup = false;
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            try
            {
                if (!scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    openedForCleanup = true;
                }

                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                bool changedScene = false;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    GameObject root = roots[rootIndex];
                    if (root == null)
                    {
                        continue;
                    }

                    int before = removedCount;
                    removedCount += RemoveMissingScriptsFromSceneObject(root, true);
                    changedScene |= removedCount != before;
                }

                changedScene |= EnsureUrpGlobalVolumeProfilesInScene(scene) > 0;
                if (changedScene)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                if (openedForCleanup && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        return removedCount;
    }

    private static int RemoveMissingScriptsFromSceneObject(GameObject root, bool cleanWholeScene)
    {
        if (root == null)
        {
            return 0;
        }

        int removedCount = 0;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];
            if (current == null)
            {
                continue;
            }

            GameObject currentObject = current.gameObject;
            if (!cleanWholeScene && !IsGameObjectFromMinimoAsset(currentObject))
            {
                continue;
            }

            removedCount += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(currentObject);
        }

        return removedCount;
    }

    private static bool IsGameObjectFromMinimoAsset(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return false;
        }

        UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
        string assetPath = source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
        return IsPathUnderMinimo(assetPath);
    }

    private static bool IsPathUnderMinimo(string path)
    {
        return !string.IsNullOrWhiteSpace(path)
               && path.Replace('\\', '/').StartsWith(MinimoRootPath + "/", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Render Pipeline Import Settings

    private static bool ApplyRenderPipelineImportSettingsForDetectedPipeline()
    {
        RenderPipelineFlavor activeFlavor = DetectProjectRenderPipelineFlavor();
        if (!IsImportablePipelineFlavor(activeFlavor))
        {
            return false;
        }

        if (!ShouldApplyPipelineImportSettings(activeFlavor))
        {
            return false;
        }

        bool builtInPostProcessingReady = true;
        if (activeFlavor == RenderPipelineFlavor.BuiltIn)
        {
            builtInPostProcessingReady =
                GlobalImportSettings.EnsureBuiltInPostProcessingResourcesIfMissing();
        }
        else if (!GlobalImportSettings.EnsureTextMeshProAndShaderGraphResourcesIfMissing())
        {
            return true;
        }

        bool changed = ImportPipelinePackagesForDetectedPipeline(activeFlavor);
        if (IsPipelinePackageImportQueued(activeFlavor))
        {
            return true;
        }

        if (!builtInPostProcessingReady)
        {
            return true;
        }

        if (activeFlavor == RenderPipelineFlavor.BuiltIn)
        {
            if (!MinimoImportCompatibility.EnsureBuiltInPostProcessingVolumesInMinimoScenes(
                    out bool changedBuiltInVolumes))
            {
                return true;
            }

            changed |= changedBuiltInVolumes;
        }

        changed |= EnsureRenderPipelineSettings();
        changed |= CleanupUnusedRenderPipelineAssets(activeFlavor);

        if (ArePipelinePackageImportsCurrent(activeFlavor))
        {
            MarkPipelineImportCompleted(activeFlavor);
        }

        return changed;
    }

    private static bool ImportPipelinePackagesForDetectedPipeline(RenderPipelineFlavor activeFlavor)
    {
        if (!IsImportablePipelineFlavor(activeFlavor))
        {
            return false;
        }

        if (IsPipelinePackageImportQueued(activeFlavor))
        {
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                SessionState.SetBool(BuildPipelinePackageQueueActiveKey(activeFlavor), false);
                ContinuePipelinePackageImportQueue(activeFlavor);
            }

            return true;
        }

        string[] packagePaths = FindPipelinePackagePaths(activeFlavor);
        if (packagePaths.Length == 0)
        {
            return false;
        }

        string packageHash = BuildPipelinePackageImportHash(activeFlavor, packagePaths);
        string packageHashKey = BuildPipelinePackageImportHashKey(activeFlavor);
        if (string.Equals(
                EditorPrefs.GetString(packageHashKey, string.Empty),
                packageHash,
                StringComparison.Ordinal))
        {
            return false;
        }

        StartPipelinePackageImportQueue(activeFlavor, packagePaths, packageHash);
        return true;
    }

    private static bool ArePipelinePackageImportsCurrent(RenderPipelineFlavor flavor)
    {
        string[] packagePaths = FindPipelinePackagePaths(flavor);
        if (packagePaths.Length == 0)
        {
            return false;
        }

        string expectedHash = BuildPipelinePackageImportHash(flavor, packagePaths);
        return string.Equals(
            EditorPrefs.GetString(BuildPipelinePackageImportHashKey(flavor), string.Empty),
            expectedHash,
            StringComparison.Ordinal);
    }

    private static string[] FindPipelinePackagePaths(RenderPipelineFlavor flavor)
    {
        if (!IsImportablePipelineFlavor(flavor) || !Directory.Exists(PipelinePackagesRootPath))
        {
            return Array.Empty<string>();
        }

        SortedSet<string> packagePaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] candidatePaths = Directory.GetFiles(
            PipelinePackagesRootPath,
            "*",
            SearchOption.AllDirectories);
        for (int i = 0; i < candidatePaths.Length; i++)
        {
            string packagePath = candidatePaths[i].Replace('\\', '/');
            if (IsSupportedPackageFile(packagePath) && IsPipelinePackageForFlavor(packagePath, flavor))
            {
                packagePaths.Add(packagePath);
            }
        }

        string[] result = new string[packagePaths.Count];
        packagePaths.CopyTo(result);
        return result;
    }

    private static bool IsSupportedPackageFile(string packagePath)
    {
        string extension = Path.GetExtension(packagePath);
        return string.Equals(extension, ".unitypackage", StringComparison.OrdinalIgnoreCase)
               || string.Equals(extension, ".package", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPipelinePackageForFlavor(string packagePath, RenderPipelineFlavor flavor)
    {
        if (!IsImportablePipelineFlavor(flavor))
        {
            return false;
        }

        string packageName = Path.GetFileNameWithoutExtension(packagePath);
        return flavor switch
        {
            RenderPipelineFlavor.Universal =>
                ContainsIgnoreCase(packageName, "URP")
                || ContainsIgnoreCase(packageName, "Universal"),
            RenderPipelineFlavor.HighDefinition =>
                ContainsIgnoreCase(packageName, "HDRP")
                || ContainsIgnoreCase(packageName, "High Definition"),
            RenderPipelineFlavor.BuiltIn =>
                ContainsIgnoreCase(packageName, "Built-In")
                || ContainsIgnoreCase(packageName, "Built In")
                || ContainsIgnoreCase(packageName, "BuiltIn")
                || ContainsIgnoreCase(packageName, "Builtin"),
            RenderPipelineFlavor.Other =>
                ContainsIgnoreCase(packageName, "Built-In")
                || ContainsIgnoreCase(packageName, "Built In")
                || ContainsIgnoreCase(packageName, "BuiltIn")
                || ContainsIgnoreCase(packageName, "Builtin"),
            _ => false
        };
    }

    private static bool ContainsIgnoreCase(string value, string expected)
    {
        return !string.IsNullOrEmpty(value)
               && value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsLikelyMinimoPackageName(string packageName)
    {
        return string.IsNullOrWhiteSpace(packageName)
               || ContainsIgnoreCase(packageName, "Minimo")
               || ContainsIgnoreCase(packageName, "Occlusionn");
    }

    private static string BuildPipelinePackageImportHash(
        RenderPipelineFlavor flavor,
        string[] packagePaths)
    {
        List<string> packageStamps = new List<string>();
        for (int i = 0; i < packagePaths.Length; i++)
        {
            string packagePath = packagePaths[i].Replace('\\', '/');
            string stamp = packagePath;
            if (File.Exists(packagePath))
            {
                FileInfo packageInfo = new FileInfo(packagePath);
                stamp = $"{packagePath}:{packageInfo.Length}:{packageInfo.LastWriteTimeUtc.Ticks}";
            }

            packageStamps.Add(stamp);
        }

        return $"{PipelinePackageImportVersion}:{flavor}:{ComputeStableHash(string.Join("|", packageStamps))}";
    }

    private static void StartPipelinePackageImportQueue(
        RenderPipelineFlavor flavor,
        string[] packagePaths,
        string packageHash)
    {
        if (packagePaths == null || packagePaths.Length == 0)
        {
            return;
        }

        SessionState.SetString(BuildPipelinePackageQueuePathsKey(flavor), string.Join("\n", packagePaths));
        SessionState.SetInt(BuildPipelinePackageQueueIndexKey(flavor), 0);
        SessionState.SetString(BuildPipelinePackageQueueHashKey(flavor), packageHash ?? string.Empty);
        SessionState.SetBool(BuildPipelinePackageQueueActiveKey(flavor), false);
        ImportNextPipelinePackageInQueue(flavor);
    }

    private static bool ContinuePipelinePackageImportQueue(RenderPipelineFlavor flavor)
    {
        if (!IsPipelinePackageImportQueued(flavor))
        {
            return false;
        }

        if (SessionState.GetBool(BuildPipelinePackageQueueActiveKey(flavor), false))
        {
            SessionState.SetBool(BuildPipelinePackageQueueActiveKey(flavor), false);
        }

        if (ImportNextPipelinePackageInQueue(flavor))
        {
            return true;
        }

        string packageHash = SessionState.GetString(BuildPipelinePackageQueueHashKey(flavor), string.Empty);
        if (!string.IsNullOrEmpty(packageHash))
        {
            EditorPrefs.SetString(BuildPipelinePackageImportHashKey(flavor), packageHash);
        }

        ClearPipelinePackageImportQueue(flavor);
        AssetDatabase.Refresh();
        ScheduleFromScriptImport();
        return true;
    }

    private static bool ContinueAnyPipelinePackageImportQueue()
    {
        RenderPipelineFlavor activeFlavor = DetectProjectRenderPipelineFlavor();
        ClearInactivePipelinePackageImportQueues(activeFlavor);
        return IsImportablePipelineFlavor(activeFlavor)
               && ContinuePipelinePackageImportQueue(activeFlavor);
    }

    private static bool IsAnyPipelinePackageImportQueued()
    {
        RenderPipelineFlavor[] flavors = GetImportablePipelineFlavors();
        for (int i = 0; i < flavors.Length; i++)
        {
            if (IsPipelinePackageImportQueued(flavors[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPipelinePackageImportQueued(RenderPipelineFlavor flavor)
    {
        return !string.IsNullOrEmpty(SessionState.GetString(BuildPipelinePackageQueuePathsKey(flavor), string.Empty));
    }

    private static bool IsPipelinePackageImportActive(RenderPipelineFlavor flavor)
    {
        return SessionState.GetBool(BuildPipelinePackageQueueActiveKey(flavor), false);
    }

    private static bool ImportNextPipelinePackageInQueue(RenderPipelineFlavor flavor)
    {
        string[] packagePaths = GetQueuedPipelinePackagePaths(flavor);
        int index = SessionState.GetInt(BuildPipelinePackageQueueIndexKey(flavor), 0);
        while (index < packagePaths.Length)
        {
            string packagePath = packagePaths[index];
            SessionState.SetInt(BuildPipelinePackageQueueIndexKey(flavor), index + 1);
            index++;

            if (string.IsNullOrWhiteSpace(packagePath)
                || !File.Exists(packagePath)
                || !IsSupportedPackageFile(packagePath)
                || !IsPipelinePackageForFlavor(packagePath, flavor))
            {
                continue;
            }

            SessionState.SetBool(BuildPipelinePackageQueueActiveKey(flavor), true);
            string absolutePackagePath = Path.GetFullPath(packagePath).Replace('\\', '/');
            MinimoImportCompatibility.CapturePipelineImportProtectedState(
                flavor == RenderPipelineFlavor.Universal);

            Debug.Log($"Minimo importing {GetPipelineDisplayName(flavor)} package: {absolutePackagePath}");
            try
            {
                AssetDatabase.ImportPackage(absolutePackagePath, false);
            }
            catch (Exception exception)
            {
                SessionState.SetInt(BuildPipelinePackageQueueIndexKey(flavor), Mathf.Max(0, index - 1));
                SessionState.SetBool(BuildPipelinePackageQueueActiveKey(flavor), false);
                MinimoImportCompatibility.RestorePipelineImportProtectedState();
                Debug.LogException(exception);
                EditorApplication.delayCall += ScheduleFromScriptImport;
            }

            return true;
        }

        return false;
    }

    private static string[] GetQueuedPipelinePackagePaths(RenderPipelineFlavor flavor)
    {
        string queuedPaths = SessionState.GetString(BuildPipelinePackageQueuePathsKey(flavor), string.Empty);
        return string.IsNullOrEmpty(queuedPaths)
            ? Array.Empty<string>()
            : queuedPaths.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static void ClearPipelinePackageImportQueue(RenderPipelineFlavor flavor)
    {
        SessionState.EraseString(BuildPipelinePackageQueuePathsKey(flavor));
        SessionState.EraseInt(BuildPipelinePackageQueueIndexKey(flavor));
        SessionState.EraseString(BuildPipelinePackageQueueHashKey(flavor));
        SessionState.SetBool(BuildPipelinePackageQueueActiveKey(flavor), false);
    }

    private static void ClearInactivePipelinePackageImportQueues(RenderPipelineFlavor activeFlavor)
    {
        RenderPipelineFlavor[] flavors = GetImportablePipelineFlavors();
        for (int i = 0; i < flavors.Length; i++)
        {
            if (flavors[i] != activeFlavor && IsPipelinePackageImportQueued(flavors[i]))
            {
                ClearPipelinePackageImportQueue(flavors[i]);
            }
        }
    }

    #endregion

    #region Global Settings

    private static int ReimportSavedPrefabsNow()
    {
        if (IsEditorUnavailableForImportWork())
        {
            return 0;
        }

        bool hasSavedPrefabsRoot = AssetDatabase.IsValidFolder(SavedPrefabsRootPath);

        string[] prefabGuids = hasSavedPrefabsRoot
            ? AssetDatabase.FindAssets("t:Prefab", new[] { SavedPrefabsRootPath })
            : Array.Empty<string>();
        string[] prefabPaths = Array.ConvertAll(prefabGuids, AssetDatabase.GUIDToAssetPath);
        Array.Sort(prefabPaths, StringComparer.OrdinalIgnoreCase);

        if (prefabPaths.Length == 0
            && !AssetExistsAtPath(CustomizationCharacterPrefabPath)
            && !AssetExistsAtPath(CustomizationDisplayPrefabPath))
        {
            return 0;
        }

        int importedCount = 0;
        bool wasReimportingCharacterAssets = isReimportingCharacterAssets;
        isReimportingCharacterAssets = true;
        SessionState.SetBool(ImportPostprocessSuppressKey, true);
        try
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < prefabPaths.Length; i++)
                {
                    string prefabPath = prefabPaths[i];
                    if (!IsPrefabUnderSavedPrefabs(prefabPath))
                    {
                        continue;
                    }

                    AssetDatabase.ImportAsset(prefabPath, SavedPrefabImportOptions);
                    importedCount++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            importedCount += ReimportCustomizationPrefab(CustomizationCharacterPrefabPath);
            importedCount += ReimportCustomizationPrefab(CustomizationDisplayPrefabPath);
        }
        finally
        {
            SessionState.SetBool(ImportPostprocessSuppressKey, false);
            isReimportingCharacterAssets = wasReimportingCharacterAssets;
        }

        return importedCount;
    }

    private static bool IsPrefabUnderSavedPrefabs(string assetPath)
    {
        return !string.IsNullOrWhiteSpace(assetPath)
               && assetPath.StartsWith(SavedPrefabsRootPath + "/", StringComparison.OrdinalIgnoreCase)
               && assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
    }

    private static int ReimportCustomizationPrefab(string prefabPath)
    {
        if (!AssetExistsAtPath(prefabPath))
        {
            return 0;
        }

        AssetDatabase.ImportAsset(prefabPath, SavedPrefabImportOptions);
        return 1;
    }

    private static bool AssetExistsAtPath(string assetPath)
    {
        return !string.IsNullOrWhiteSpace(assetPath)
               && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath));
    }

    private static int ReimportCharacterPrefabsAndMaterials()
    {
        string[] materialPaths = FindCharacterMaterialPaths();
        string[] prefabPaths = FindCharacterPrefabPaths();
        int assetCount = materialPaths.Length + prefabPaths.Length;
        if (assetCount == 0)
        {
            return 0;
        }

        isReimportingCharacterAssets = true;
        SessionState.SetBool(ImportPostprocessSuppressKey, true);
        try
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < materialPaths.Length; i++)
                {
                    AssetDatabase.ImportAsset(materialPaths[i], ImportAssetOptions.ForceUpdate);
                }

                for (int i = 0; i < prefabPaths.Length; i++)
                {
                    AssetDatabase.ImportAsset(prefabPaths[i], ImportAssetOptions.ForceUpdate);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }
        finally
        {
            SessionState.SetBool(ImportPostprocessSuppressKey, false);
            isReimportingCharacterAssets = false;
        }

        return assetCount;
    }

    private static string[] FindCharacterMaterialPaths()
    {
        SortedSet<string> paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        AddAssetPaths(paths, "t:Material", CharacterMaterialSearchRoots, ".mat");

        string[] result = new string[paths.Count];
        paths.CopyTo(result);
        return result;
    }

    private static string[] FindCharacterPrefabPaths()
    {
        SortedSet<string> paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        AddAssetPaths(paths, "t:Prefab", CharacterPrefabSearchRoots, ".prefab");
        paths.RemoveWhere(IsPrefabUnderSavedPrefabs);
        paths.Remove(CustomizationCharacterPrefabPath);
        paths.Remove(CustomizationDisplayPrefabPath);

        string[] result = new string[paths.Count];
        paths.CopyTo(result);
        return result;
    }

    private static string[] FindSavedPrefabCharacterPaths()
    {
        SortedSet<string> paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        AddAssetPaths(paths, "t:Prefab", new[] { SavedPrefabsCharactersRootPath }, ".prefab");

        string[] result = new string[paths.Count];
        paths.CopyTo(result);
        return result;
    }

    private static void AddAssetPaths(SortedSet<string> paths, string filter, string[] roots, string extension)
    {
        if (paths == null || roots == null)
        {
            return;
        }

        List<string> validRoots = new List<string>();
        for (int i = 0; i < roots.Length; i++)
        {
            string root = roots[i];
            if (AssetDatabase.IsValidFolder(root))
            {
                validRoots.Add(root);
            }
        }

        if (validRoots.Count == 0)
        {
            return;
        }

        string[] guids = AssetDatabase.FindAssets(filter, validRoots.ToArray());
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!string.IsNullOrWhiteSpace(assetPath)
                && assetPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(assetPath);
            }
        }
    }

    #endregion

    #region Render Pipeline Import Settings

    private static bool CleanupUnusedRenderPipelineAssets(RenderPipelineFlavor activeFlavor)
    {
        if (!IsImportablePipelineFlavor(activeFlavor))
        {
            return false;
        }

        if (activeFlavor == RenderPipelineFlavor.Other)
        {
            return false;
        }

        bool deletedAny = false;
        for (int i = 0; i < PipelineFolderPaths.Length; i++)
        {
            string folderPath = PipelineFolderPaths[i];
            if (GetPipelineFlavorForFolderPath(folderPath) != activeFlavor)
            {
                deletedAny |= DeletePipelineFolderIfExists(folderPath);
            }
        }

        if (deletedAny)
        {
            AssetDatabase.Refresh();
        }

        return deletedAny;
    }

    private static bool DeletePipelineFolderIfExists(string folderPath)
    {
        return AssetDatabase.IsValidFolder(folderPath) && AssetDatabase.DeleteAsset(folderPath);
    }

    private static RenderPipelineFlavor GetPipelineFlavorForFolderPath(string folderPath)
    {
        string normalizedPath = string.IsNullOrWhiteSpace(folderPath)
            ? string.Empty
            : folderPath.Replace('\\', '/');
        string folderName = Path.GetFileName(normalizedPath);
        if (ContainsIgnoreCase(folderName, "URP"))
        {
            return RenderPipelineFlavor.Universal;
        }

        if (ContainsIgnoreCase(folderName, "HDRP"))
        {
            return RenderPipelineFlavor.HighDefinition;
        }

        if (ContainsIgnoreCase(folderName, "Built-In")
            || ContainsIgnoreCase(folderName, "BuiltIn")
            || ContainsIgnoreCase(folderName, "Builtin"))
        {
            return RenderPipelineFlavor.BuiltIn;
        }

        return RenderPipelineFlavor.Unknown;
    }

    private static bool EnsureRenderPipelineSettings()
    {
        if (!TryGetRenderPipelineImportSettings(out RenderPipelineImportSettings settings))
        {
            return false;
        }

        bool changed = false;
        if (settings.Renderer != null)
        {
            changed |= EnsureRendererAssetSafe(settings.Renderer);
            changed |= EnsureRendererOnPipelineAsset(settings.DefaultRenderPipeline, settings.Renderer);
        }

        if (settings.VolumeProfile != null)
        {
            changed |= EnsureGlobalVolumeProfileOnPipelineAsset(settings.DefaultRenderPipeline, settings.VolumeProfile);
        }

        changed |= SetGraphicsSettingsDefaultRenderPipeline(settings.DefaultRenderPipeline);

        if (QualitySettings.renderPipeline != settings.QualityRenderPipeline)
        {
            QualitySettings.renderPipeline = settings.QualityRenderPipeline;
            changed = true;
        }

        return changed;
    }

    private static bool TryGetRenderPipelineImportSettings(out RenderPipelineImportSettings settings)
    {
        RenderPipelineFlavor detectedFlavor = DetectProjectRenderPipelineFlavor();
        if (detectedFlavor == RenderPipelineFlavor.Universal)
        {
            RenderPipelineAsset urpRenderPipeline =
                AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(UrpRenderPipelinePath);
            UnityEngine.Object urpRenderer = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(UrpRendererPath);
            if (urpRenderPipeline == null || urpRenderer == null)
            {
                settings = default;
                return false;
            }

            settings = new RenderPipelineImportSettings(
                RenderPipelineFlavor.Universal,
                urpRenderPipeline,
                urpRenderPipeline,
                urpRenderer,
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(UrpGlobalVolumeProfilePath));
            return true;
        }

        if (detectedFlavor == RenderPipelineFlavor.HighDefinition)
        {
            RenderPipelineAsset hdrpRenderPipeline =
                AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(HdrpRenderPipelinePath);
            if (hdrpRenderPipeline == null)
            {
                settings = default;
                return false;
            }

            settings = new RenderPipelineImportSettings(
                RenderPipelineFlavor.HighDefinition,
                hdrpRenderPipeline,
                hdrpRenderPipeline,
                null,
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(HdrpVolumeProfilePath));
            return true;
        }

        if (detectedFlavor == RenderPipelineFlavor.BuiltIn)
        {
            settings = new RenderPipelineImportSettings(
                RenderPipelineFlavor.BuiltIn,
                null,
                null,
                null,
                null);
            return true;
        }

        if (detectedFlavor == RenderPipelineFlavor.Other)
        {
            settings = new RenderPipelineImportSettings(
                RenderPipelineFlavor.Other,
                GetGraphicsSettingsDefaultRenderPipeline(),
                QualitySettings.renderPipeline,
                null,
                null);
            return true;
        }

        settings = default;
        return false;
    }

    private static RenderPipelineFlavor DetectProjectRenderPipelineFlavor()
    {
        RenderPipelineAsset graphicsRenderPipeline = GetGraphicsSettingsDefaultRenderPipeline();
        RenderPipelineFlavor graphicsPipelineFlavor = GetRenderPipelineFlavor(graphicsRenderPipeline);
        if (graphicsPipelineFlavor != RenderPipelineFlavor.Unknown)
        {
            return graphicsPipelineFlavor;
        }

        RenderPipelineAsset qualityRenderPipeline = QualitySettings.renderPipeline;
        RenderPipelineFlavor qualityPipelineFlavor = GetRenderPipelineFlavor(qualityRenderPipeline);
        if (qualityPipelineFlavor != RenderPipelineFlavor.Unknown)
        {
            return qualityPipelineFlavor;
        }

        if (graphicsRenderPipeline == null && qualityRenderPipeline == null)
        {
            return RenderPipelineFlavor.BuiltIn;
        }

        return RenderPipelineFlavor.Other;
    }

    private static RenderPipelineFlavor GetRenderPipelineFlavor(RenderPipelineAsset renderPipeline)
    {
        if (renderPipeline == null)
        {
            return RenderPipelineFlavor.Unknown;
        }

        string typeName = renderPipeline.GetType().FullName;
        if (string.Equals(typeName, UniversalRenderPipelineAssetTypeName, StringComparison.Ordinal))
        {
            return RenderPipelineFlavor.Universal;
        }

        return string.Equals(typeName, HighDefinitionRenderPipelineAssetTypeName, StringComparison.Ordinal)
            ? RenderPipelineFlavor.HighDefinition
            : RenderPipelineFlavor.Unknown;
    }

    private static RenderPipelineAsset GetGraphicsSettingsDefaultRenderPipeline()
    {
        PropertyInfo property = GetGraphicsSettingsPipelineProperty();
        return property != null ? property.GetValue(null, null) as RenderPipelineAsset : null;
    }

    private static bool SetGraphicsSettingsDefaultRenderPipeline(RenderPipelineAsset renderPipeline)
    {
        PropertyInfo property = GetGraphicsSettingsPipelineProperty();
        if (property == null || !property.CanWrite)
        {
            return false;
        }

        RenderPipelineAsset currentRenderPipeline = property.GetValue(null, null) as RenderPipelineAsset;
        if (currentRenderPipeline == renderPipeline)
        {
            return false;
        }

        property.SetValue(null, renderPipeline, null);
        SetGraphicsSettingsDirty();
        return true;
    }

    private static PropertyInfo GetGraphicsSettingsPipelineProperty()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
        return typeof(GraphicsSettings).GetProperty("defaultRenderPipeline", flags)
               ?? typeof(GraphicsSettings).GetProperty("renderPipelineAsset", flags);
    }

    private static void SetGraphicsSettingsDirty()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
        MethodInfo method = typeof(GraphicsSettings).GetMethod("GetGraphicsSettings", flags);
        UnityEngine.Object graphicsSettings = method != null
            ? method.Invoke(null, null) as UnityEngine.Object
            : null;
        if (graphicsSettings != null)
        {
            EditorUtility.SetDirty(graphicsSettings);
        }
    }

    private static bool IsPackageListedInManifest(string packageName)
    {
        const string manifestPath = "Packages/manifest.json";
        return File.Exists(manifestPath)
               && File.ReadAllText(manifestPath).IndexOf(
                   $"\"{packageName}\"",
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ShouldApplyPipelineImportSettings(RenderPipelineFlavor flavor)
    {
        if (!IsImportablePipelineFlavor(flavor))
        {
            return false;
        }

        if (flavor != RenderPipelineFlavor.BuiltIn
            && HasPipelineImportCompleted(flavor)
            && ArePipelinePackageImportsCurrent(flavor))
        {
            return false;
        }

        return EnsurePipelineImportApproval(flavor);
    }

    private static bool EnsurePipelineImportApproval(RenderPipelineFlavor flavor)
    {
        if (!IsImportablePipelineFlavor(flavor))
        {
            return false;
        }

        string initialPromptAcceptedKey = BuildInitialSetupPromptAcceptedKey(flavor);
        if (!EditorPrefs.GetBool(initialPromptAcceptedKey, false))
        {
            if (Application.isBatchMode
                || SessionState.GetBool(BuildInitialSetupPromptDeclinedKey(flavor), false))
            {
                return false;
            }

            string initialPipelineName = GetPipelineDisplayName(flavor);
            bool installApproved = EditorUtility.DisplayDialog(
                "Minimo Setup",
                $"Minimo detected {initialPipelineName}.\n\nDo you want to install the required packages and import the matching Minimo .unitypackage files?",
                "Install",
                "Not Now");
            if (!installApproved)
            {
                SessionState.SetBool(BuildInitialSetupPromptDeclinedKey(flavor), true);
                return false;
            }

            EditorPrefs.SetBool(initialPromptAcceptedKey, true);
            EditorPrefs.SetBool(BuildPersistentPipelineImportConsentKey(flavor), true);
            EditorPrefs.SetBool(BuildPipelineImportAcceptedKey(flavor), true);
            SessionState.SetBool(BuildInitialSetupPromptDeclinedKey(flavor), false);
        }

        EditorPrefs.SetBool(BuildPersistentPipelineImportConsentKey(flavor), true);
        EditorPrefs.SetBool(BuildPipelineImportAcceptedKey(flavor), true);
        SessionState.SetBool(BuildPipelineImportPromptRequiredKey(flavor), false);
        SessionState.SetBool(BuildPipelineImportSkippedKey(flavor), false);
        return true;
    }

    private static string GetPipelineDisplayName(RenderPipelineFlavor flavor)
    {
        return flavor switch
        {
            RenderPipelineFlavor.Universal => "Universal Render Pipeline",
            RenderPipelineFlavor.HighDefinition => "High Definition Render Pipeline",
            RenderPipelineFlavor.BuiltIn => "Built-In Render Pipeline",
            RenderPipelineFlavor.Other => "Other Render Pipeline",
            _ => "Unknown Render Pipeline"
        };
    }

    private static RenderPipelineFlavor[] GetImportablePipelineFlavors()
    {
        return new[]
        {
            RenderPipelineFlavor.Universal,
            RenderPipelineFlavor.HighDefinition,
            RenderPipelineFlavor.BuiltIn,
            RenderPipelineFlavor.Other
        };
    }

    private static bool IsImportablePipelineFlavor(RenderPipelineFlavor flavor)
    {
        return flavor == RenderPipelineFlavor.Universal
               || flavor == RenderPipelineFlavor.HighDefinition
               || flavor == RenderPipelineFlavor.BuiltIn
               || flavor == RenderPipelineFlavor.Other;
    }

    private static bool EnsureGlobalVolumeProfileOnPipelineAsset(
        RenderPipelineAsset renderPipeline,
        UnityEngine.Object volumeProfile)
    {
        if (renderPipeline == null || volumeProfile == null)
        {
            return false;
        }

        SerializedObject serializedPipeline = new SerializedObject(renderPipeline);
        SerializedProperty volumeProfileProperty = serializedPipeline.FindProperty("m_VolumeProfile");
        if (volumeProfileProperty == null
            || volumeProfileProperty.objectReferenceValue == volumeProfile)
        {
            return false;
        }

        volumeProfileProperty.objectReferenceValue = volumeProfile;
        serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(renderPipeline);
        return true;
    }

    private static int EnsureUrpGlobalVolumeProfilesInPrefab(GameObject prefabRoot)
    {
        if (prefabRoot == null
            || DetectProjectRenderPipelineFlavor() != RenderPipelineFlavor.Universal
            || !HasPipelineImportApproval(RenderPipelineFlavor.Universal))
        {
            return 0;
        }

        UnityEngine.Object volumeProfile =
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(UrpGlobalVolumeProfilePath);
        if (volumeProfile == null)
        {
            return 0;
        }

        return EnsureUrpGlobalVolumeProfiles(prefabRoot, volumeProfile);
    }

    private static int EnsureUrpGlobalVolumeProfilesInScene(Scene scene)
    {
        if (!scene.IsValid()
            || !scene.isLoaded
            || DetectProjectRenderPipelineFlavor() != RenderPipelineFlavor.Universal
            || !HasPipelineImportApproval(RenderPipelineFlavor.Universal))
        {
            return 0;
        }

        UnityEngine.Object volumeProfile =
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(UrpGlobalVolumeProfilePath);
        if (volumeProfile == null)
        {
            return 0;
        }

        int updatedCount = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            updatedCount += EnsureUrpGlobalVolumeProfiles(roots[i], volumeProfile);
        }

        return updatedCount;
    }

    private static int EnsureUrpGlobalVolumeProfiles(GameObject root, UnityEngine.Object volumeProfile)
    {
        if (root == null || volumeProfile == null)
        {
            return 0;
        }

        int updatedCount = 0;
        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || !IsGlobalVolumeBehaviour(behaviour))
            {
                continue;
            }

            SerializedObject serializedBehaviour = new SerializedObject(behaviour);
            SerializedProperty sharedProfile = serializedBehaviour.FindProperty("sharedProfile");
            if (sharedProfile == null || sharedProfile.objectReferenceValue == volumeProfile)
            {
                continue;
            }

            sharedProfile.objectReferenceValue = volumeProfile;
            serializedBehaviour.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(behaviour);
            updatedCount++;
        }

        return updatedCount;
    }

    private static bool IsGlobalVolumeBehaviour(MonoBehaviour behaviour)
    {
        if (behaviour == null
            || !string.Equals(
                behaviour.GetType().FullName,
                "UnityEngine.Rendering.Volume",
                StringComparison.Ordinal))
        {
            return false;
        }

        SerializedObject serializedBehaviour = new SerializedObject(behaviour);
        SerializedProperty isGlobal = serializedBehaviour.FindProperty("m_IsGlobal");
        return isGlobal != null && isGlobal.boolValue;
    }

    private static bool HasPipelineImportApproval(RenderPipelineFlavor flavor)
    {
        return EditorPrefs.GetBool(BuildPipelineImportAcceptedKey(flavor), false);
    }

    private static bool HasPipelineImportCompleted(RenderPipelineFlavor flavor)
    {
        return EditorPrefs.GetBool(BuildPipelineImportCompletedKey(flavor), false);
    }

    private static void MarkPipelineImportCompleted(RenderPipelineFlavor flavor)
    {
        EditorPrefs.SetBool(BuildPipelineImportCompletedKey(flavor), true);
        SessionState.SetBool(BuildPipelineImportPromptRequiredKey(flavor), false);
    }

    private static bool EnsureRendererAssetSafe(UnityEngine.Object renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        SerializedObject serializedRenderer = new SerializedObject(renderer);
        SerializedProperty rendererFeatures = serializedRenderer.FindProperty("m_RendererFeatures");
        if (rendererFeatures == null || !rendererFeatures.isArray || rendererFeatures.arraySize == 0)
        {
            return false;
        }

        bool hasOnlyUnsafeFeatures = true;
        for (int i = 0; i < rendererFeatures.arraySize; i++)
        {
            UnityEngine.Object feature = rendererFeatures.GetArrayElementAtIndex(i).objectReferenceValue;
            if (!IsUnsafeRendererFeature(feature))
            {
                hasOnlyUnsafeFeatures = false;
                break;
            }
        }

        if (!hasOnlyUnsafeFeatures)
        {
            bool disabledAny = false;
            for (int i = 0; i < rendererFeatures.arraySize; i++)
            {
                UnityEngine.Object feature = rendererFeatures.GetArrayElementAtIndex(i).objectReferenceValue;
                if (IsUnsafeRendererFeature(feature))
                {
                    disabledAny |= DisableRendererFeature(feature);
                }
            }

            return disabledAny;
        }

        rendererFeatures.ClearArray();
        SerializedProperty rendererFeatureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");
        if (rendererFeatureMap != null && rendererFeatureMap.isArray)
        {
            rendererFeatureMap.ClearArray();
        }

        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(renderer);
        return true;
    }

    private static bool IsUnsafeRendererFeature(UnityEngine.Object feature)
    {
        return feature == null
               || string.Equals(feature.GetType().Name, "ScreenSpaceAmbientOcclusion", StringComparison.Ordinal);
    }

    private static bool DisableRendererFeature(UnityEngine.Object feature)
    {
        if (feature == null)
        {
            return false;
        }

        SerializedObject serializedFeature = new SerializedObject(feature);
        SerializedProperty active = serializedFeature.FindProperty("m_Active");
        if (active == null || !active.boolValue)
        {
            return false;
        }

        active.boolValue = false;
        serializedFeature.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(feature);
        return true;
    }

    private static bool EnsureRendererOnPipelineAsset(RenderPipelineAsset renderPipeline, UnityEngine.Object renderer)
    {
        SerializedObject serializedPipeline = new SerializedObject(renderPipeline);
        bool changed = false;

        SerializedProperty rendererDataList = serializedPipeline.FindProperty("m_RendererDataList");
        if (rendererDataList != null && rendererDataList.isArray)
        {
            if (rendererDataList.arraySize == 0)
            {
                rendererDataList.arraySize = 1;
                changed = true;
            }

            SerializedProperty firstRenderer = rendererDataList.GetArrayElementAtIndex(0);
            if (firstRenderer != null && firstRenderer.objectReferenceValue != renderer)
            {
                firstRenderer.objectReferenceValue = renderer;
                changed = true;
            }
        }

        SerializedProperty defaultRendererIndex = serializedPipeline.FindProperty("m_DefaultRendererIndex");
        if (defaultRendererIndex != null && defaultRendererIndex.intValue != 0)
        {
            defaultRendererIndex.intValue = 0;
            changed = true;
        }

        SerializedProperty legacyRendererData = serializedPipeline.FindProperty("m_RendererData");
        if (legacyRendererData != null && legacyRendererData.objectReferenceValue != null)
        {
            legacyRendererData.objectReferenceValue = null;
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(renderPipeline);
        return true;
    }

    private static bool AreRenderPipelineSettingsApplied()
    {
        if (!TryGetRenderPipelineImportSettings(out RenderPipelineImportSettings settings))
        {
            return false;
        }

        bool isApplied = GetGraphicsSettingsDefaultRenderPipeline() == settings.DefaultRenderPipeline
                         && QualitySettings.renderPipeline == settings.QualityRenderPipeline;
        if (settings.Renderer == null)
        {
            return isApplied;
        }

        return isApplied
               && IsRendererAssetSafe(settings.Renderer)
               && IsRendererOnPipelineAsset(settings.DefaultRenderPipeline, settings.Renderer);
    }

    private static bool IsRendererAssetSafe(UnityEngine.Object renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        SerializedObject serializedRenderer = new SerializedObject(renderer);
        SerializedProperty rendererFeatures = serializedRenderer.FindProperty("m_RendererFeatures");
        if (rendererFeatures == null || !rendererFeatures.isArray)
        {
            return true;
        }

        for (int i = 0; i < rendererFeatures.arraySize; i++)
        {
            UnityEngine.Object feature = rendererFeatures.GetArrayElementAtIndex(i).objectReferenceValue;
            if (IsUnsafeRendererFeature(feature))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsRendererOnPipelineAsset(RenderPipelineAsset renderPipeline, UnityEngine.Object renderer)
    {
        if (renderPipeline == null || renderer == null)
        {
            return false;
        }

        SerializedObject serializedPipeline = new SerializedObject(renderPipeline);
        SerializedProperty rendererDataList = serializedPipeline.FindProperty("m_RendererDataList");
        SerializedProperty defaultRendererIndex = serializedPipeline.FindProperty("m_DefaultRendererIndex");
        if (rendererDataList != null && rendererDataList.isArray && rendererDataList.arraySize > 0)
        {
            int rendererIndex = defaultRendererIndex != null
                ? Mathf.Clamp(defaultRendererIndex.intValue, 0, rendererDataList.arraySize - 1)
                : 0;
            SerializedProperty rendererProperty = rendererDataList.GetArrayElementAtIndex(rendererIndex);
            return rendererProperty != null && rendererProperty.objectReferenceValue == renderer;
        }

        SerializedProperty legacyRendererData = serializedPipeline.FindProperty("m_RendererData");
        return legacyRendererData != null && legacyRendererData.objectReferenceValue == renderer;
    }

    #endregion

    private static bool EnsureLayerAndPhysicsSettings()
    {
        bool changed = EnsureLayerSettings();
        changed |= EnsurePhysicsLayerCollisionSettings();
        return changed;
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

    private static bool HasEditorImportSettingsScript()
    {
        return File.Exists(EditorImportSettingsPath)
               || !string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(EditorImportSettingsPath));
    }

    private static string BuildCurrentImportHash()
    {
        return $"{ImportSettingsVersion}:{BuildCurrentImportCompletionStamp()}";
    }

    private static string BuildCompletedImportHashKey()
    {
        return $"Minimo.CharacterImportSettings.CompletedImportHash.{ImportSettingsVersion}.{ComputeStableHash(Application.dataPath)}";
    }

    private static string BuildForceRunKey()
    {
        return $"Minimo.CharacterImportSettings.ForceRun.{ImportSettingsVersion}.{ComputeStableHash(Application.dataPath)}";
    }

    private static string BuildPipelineImportAcceptedKey(RenderPipelineFlavor flavor)
    {
        return $"Minimo.PipelineImport.Accepted.{PipelineImportPromptVersion}.{flavor}.{ComputeStableHash(Application.dataPath)}.{ComputeStableHash(BuildCurrentPipelinePackagePromptStamp(flavor))}";
    }

    private static string BuildPersistentPipelineImportConsentKey(RenderPipelineFlavor flavor)
    {
        string consentVersion = flavor == RenderPipelineFlavor.BuiltIn ? "v3" : "v1";
        return $"Minimo.PipelineImport.PersistentConsent.{consentVersion}.{flavor}.{ComputeStableHash(Application.dataPath)}";
    }

    private static string BuildInitialSetupPromptAcceptedKey(RenderPipelineFlavor flavor)
    {
        return $"Minimo.InitialSetupPrompt.Accepted.v2.{flavor}.{ComputeStableHash(Application.dataPath)}";
    }

    private static string BuildInitialSetupPromptDeclinedKey(RenderPipelineFlavor flavor)
    {
        return $"Minimo.InitialSetupPrompt.Declined.v2.{flavor}.{ComputeStableHash(Application.dataPath)}";
    }

    private static string BuildPipelineImportCompletedKey(RenderPipelineFlavor flavor)
    {
        return $"Minimo.PipelineImport.Completed.{PipelineImportPromptVersion}.{flavor}.{ComputeStableHash(Application.dataPath)}.{ComputeStableHash(BuildCurrentPipelinePackagePromptStamp(flavor))}";
    }

    private static string BuildPipelineImportSkippedKey(RenderPipelineFlavor flavor)
    {
        return $"Minimo.PipelineImport.Skipped.{PipelineImportPromptVersion}.{flavor}.{ComputeStableHash(Application.dataPath)}.{ComputeStableHash(BuildCurrentPipelinePackagePromptStamp(flavor))}";
    }

    private static string BuildPipelinePackageImportHashKey(RenderPipelineFlavor flavor)
    {
        return $"Minimo.PipelinePackages.Imported.{PipelinePackageImportVersion}.{flavor}.{ComputeStableHash(Application.dataPath)}";
    }

    private static string BuildPipelineImportPromptRequiredKey(RenderPipelineFlavor flavor)
    {
        return $"Minimo.PipelineImport.PromptRequired.{PipelineImportPromptVersion}.{flavor}.{ComputeStableHash(Application.dataPath)}";
    }

    private static string BuildPipelinePackageQueuePathsKey(RenderPipelineFlavor flavor)
    {
        return $"Minimo.PipelinePackages.Queue.Paths.{PipelinePackageImportVersion}.{flavor}.{ComputeStableHash(Application.dataPath)}";
    }

    private static string BuildPipelinePackageQueueIndexKey(RenderPipelineFlavor flavor)
    {
        return $"Minimo.PipelinePackages.Queue.Index.{PipelinePackageImportVersion}.{flavor}.{ComputeStableHash(Application.dataPath)}";
    }

    private static string BuildPipelinePackageQueueHashKey(RenderPipelineFlavor flavor)
    {
        return $"Minimo.PipelinePackages.Queue.Hash.{PipelinePackageImportVersion}.{flavor}.{ComputeStableHash(Application.dataPath)}";
    }

    private static string BuildPipelinePackageQueueActiveKey(RenderPipelineFlavor flavor)
    {
        return $"Minimo.PipelinePackages.Queue.Active.{PipelinePackageImportVersion}.{flavor}.{ComputeStableHash(Application.dataPath)}";
    }

    private static string BuildCurrentPipelinePackagePromptStamp(RenderPipelineFlavor flavor)
    {
        string[] packagePaths = FindPipelinePackagePaths(flavor);
        return packagePaths.Length == 0
            ? $"{PipelinePackageImportVersion}:{flavor}:None"
            : BuildPipelinePackageImportHash(flavor, packagePaths);
    }

    private static string BuildCurrentImportCompletionStamp()
    {
        RenderPipelineFlavor activeFlavor = DetectProjectRenderPipelineFlavor();
        return $"{activeFlavor}:{BuildCurrentPipelinePackagePromptStamp(activeFlavor)}";
    }

    private static int ComputeStableHash(string value)
    {
        unchecked
        {
            int hash = 23;
            for (int i = 0; i < value.Length; i++)
            {
                hash = hash * 31 + value[i];
            }

            return hash;
        }
    }

    private enum RenderPipelineFlavor
    {
        Unknown,
        Universal,
        HighDefinition,
        BuiltIn,
        Other
    }

    private readonly struct RenderPipelineImportSettings
    {
        public RenderPipelineImportSettings(
            RenderPipelineFlavor flavor,
            RenderPipelineAsset defaultRenderPipeline,
            RenderPipelineAsset qualityRenderPipeline,
            UnityEngine.Object renderer,
            UnityEngine.Object volumeProfile)
        {
            Flavor = flavor;
            DefaultRenderPipeline = defaultRenderPipeline;
            QualityRenderPipeline = qualityRenderPipeline;
            Renderer = renderer;
            VolumeProfile = volumeProfile;
        }

        public RenderPipelineFlavor Flavor { get; }
        public RenderPipelineAsset DefaultRenderPipeline { get; }
        public RenderPipelineAsset QualityRenderPipeline { get; }
        public UnityEngine.Object Renderer { get; }
        public UnityEngine.Object VolumeProfile { get; }
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
}

public sealed class CharacterImportSettingsPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (CharacterImportSettingsProcessor.IsReimportingCharacterAssets)
        {
            return;
        }

        if (SessionState.GetBool(CharacterImportSettingsProcessor.ImportPostprocessSuppressKey, false))
        {
            return;
        }

        if (ContainsCharacterImportSettingsScript(importedAssets)
            || ContainsCharacterImportSettingsScript(movedAssets)
            || ContainsPipelinePackageAsset(importedAssets)
            || ContainsPipelinePackageAsset(movedAssets))
        {
            CharacterImportSettingsProcessor.ScheduleFromScriptImport();
        }
    }

    private static bool ContainsCharacterImportSettingsScript(string[] assetPaths)
    {
        if (assetPaths == null)
        {
            return false;
        }

        for (int i = 0; i < assetPaths.Length; i++)
        {
            if (string.Equals(
                    assetPaths[i],
                    CharacterImportSettingsProcessor.CharacterImportSettingsPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsPipelinePackageAsset(string[] assetPaths)
    {
        if (assetPaths == null)
        {
            return false;
        }

        const string pipelinePackagesRootPath = "Assets/Minimo/Scene/Visuals/Pipelines/Package Assets";
        for (int i = 0; i < assetPaths.Length; i++)
        {
            string path = assetPaths[i];
            if (string.IsNullOrWhiteSpace(path)
                || !path.Replace('\\', '/').StartsWith(pipelinePackagesRootPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string extension = Path.GetExtension(path);
            if (string.Equals(extension, ".unitypackage", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".package", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

internal static class MinimoImportCompatibility
{
    private const string PostProcessingPackageName = "com.unity.postprocessing";
    private const string PostProcessVolumeTypeName =
        "UnityEngine.Rendering.PostProcessing.PostProcessVolume, Unity.Postprocessing.Runtime";
    private const string BuiltInVolumeProfilePath =
        "Assets/Minimo/Scene/Visuals/Pipelines/Built-In/Global Volume Profile.asset";
    private const string MinimoRootPath = "Assets/Minimo";
    private const string CompatibilityBackupPath = "Library/Minimo/PipelineImportCompatibility";

    private static readonly string[] ProtectedScriptPaths =
    {
        "Assets/Minimo/Scripts/Editor/GlobalImportSettings.cs",
        "Assets/Minimo/Scripts/Editor/EditorImportSettings.cs",
        "Assets/Minimo/Scripts/Editor/CharacterImportSettings.cs"
    };

    private static bool IsEditorUnavailableForSceneEditing()
    {
        return Application.isPlaying
               || EditorApplication.isPlayingOrWillChangePlaymode
               || EditorApplication.isCompiling
               || EditorApplication.isUpdating;
    }

    internal static bool AreBuiltInPostProcessingResourcesReady()
    {
        MethodInfo method = FindGlobalMethod(
            nameof(AreBuiltInPostProcessingResourcesReady),
            0);
        if (method != null)
        {
            try
            {
                return method.Invoke(null, null) is bool ready && ready;
            }
            catch (Exception)
            {
            }
        }

        const string manifestPath = "Packages/manifest.json";
        bool packageInstalled = File.Exists(manifestPath)
                                && File.ReadAllText(manifestPath).IndexOf(
                                    $"\"{PostProcessingPackageName}\"",
                                    StringComparison.OrdinalIgnoreCase) >= 0;
        return packageInstalled && Type.GetType(PostProcessVolumeTypeName, false) != null;
    }

    internal static bool EnsureBuiltInPostProcessingVolumesInMinimoScenes(out bool changed)
    {
        MethodInfo method = FindGlobalMethod(
            nameof(EnsureBuiltInPostProcessingVolumesInMinimoScenes),
            1);
        if (method != null)
        {
            try
            {
                object[] arguments = { false };
                bool succeeded = method.Invoke(null, arguments) is bool result && result;
                changed = arguments[0] is bool methodChanged && methodChanged;
                return succeeded;
            }
            catch (Exception)
            {
            }
        }

        return EnsureBuiltInVolumesLocally(out changed);
    }

    internal static void CapturePipelineImportProtectedState(bool preserveUrpEnvironmentLighting)
    {
        MethodInfo method = FindGlobalMethod(
            nameof(CapturePipelineImportProtectedState),
            1);
        MethodInfo restoreMethod = FindGlobalMethod(
            nameof(RestorePipelineImportProtectedState),
            0);
        if (method != null && restoreMethod != null)
        {
            try
            {
                method.Invoke(null, new object[] { preserveUrpEnvironmentLighting });
                return;
            }
            catch (Exception)
            {
            }
        }

        MethodInfo legacyMethod = FindGlobalMethod("CaptureUrpImportProtectedState", 0);
        MethodInfo legacyRestoreMethod = FindGlobalMethod("RestoreUrpImportProtectedState", 0);
        if (legacyMethod != null && legacyRestoreMethod != null)
        {
            try
            {
                legacyMethod.Invoke(null, null);
                return;
            }
            catch (Exception)
            {
            }
        }

        CaptureScriptsLocally();
    }

    internal static void RestorePipelineImportProtectedState()
    {
        MethodInfo captureMethod = FindGlobalMethod(
            nameof(CapturePipelineImportProtectedState),
            1);
        MethodInfo method = FindGlobalMethod(
            nameof(RestorePipelineImportProtectedState),
            0);
        if (captureMethod != null && method != null)
        {
            try
            {
                method.Invoke(null, null);
                return;
            }
            catch (Exception)
            {
            }
        }

        MethodInfo legacyCaptureMethod = FindGlobalMethod("CaptureUrpImportProtectedState", 0);
        MethodInfo legacyMethod = FindGlobalMethod("RestoreUrpImportProtectedState", 0);
        if (legacyCaptureMethod != null && legacyMethod != null)
        {
            try
            {
                legacyMethod.Invoke(null, null);
                return;
            }
            catch (Exception)
            {
            }
        }

        RestoreScriptsLocally();
    }

    private static MethodInfo FindGlobalMethod(string methodName, int parameterCount)
    {
        MethodInfo[] methods = typeof(GlobalImportSettings).GetMethods(
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < methods.Length; i++)
        {
            if (string.Equals(methods[i].Name, methodName, StringComparison.Ordinal)
                && methods[i].GetParameters().Length == parameterCount)
            {
                return methods[i];
            }
        }

        return null;
    }

    private static bool EnsureBuiltInVolumesLocally(out bool changed)
    {
        changed = false;
        if (IsEditorUnavailableForSceneEditing()
            || !AreBuiltInPostProcessingResourcesReady())
        {
            return false;
        }

        Type volumeType = Type.GetType(PostProcessVolumeTypeName, false);
        UnityEngine.Object volumeProfile =
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(BuiltInVolumeProfilePath);
        if (volumeType == null || volumeProfile == null)
        {
            return false;
        }

        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { MinimoRootPath });
        if (sceneGuids == null || sceneGuids.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < sceneGuids.Length; i++)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
            bool openedForUpdate = false;
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            try
            {
                if (!scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    openedForUpdate = true;
                }

                if (!scene.IsValid()
                    || !scene.isLoaded
                    || !EnsureVolumeInScene(
                        scene,
                        volumeType,
                        volumeProfile,
                        out bool sceneChanged))
                {
                    return false;
                }

                if (sceneChanged)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene))
                    {
                        return false;
                    }

                    changed = true;
                }
            }
            finally
            {
                if (openedForUpdate && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        return true;
    }

    private static bool EnsureVolumeInScene(
        Scene scene,
        Type volumeType,
        UnityEngine.Object volumeProfile,
        out bool changed)
    {
        changed = false;
        Component selectedVolume = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length && selectedVolume == null; i++)
        {
            Component[] volumes = roots[i].GetComponentsInChildren(volumeType, true);
            for (int j = 0; j < volumes.Length; j++)
            {
                SerializedObject serializedVolume = new SerializedObject(volumes[j]);
                SerializedProperty isGlobal = serializedVolume.FindProperty("isGlobal");
                if (isGlobal != null && isGlobal.boolValue)
                {
                    selectedVolume = volumes[j];
                    break;
                }
            }
        }

        if (selectedVolume == null)
        {
            GameObject volumeObject = new GameObject("Global Post Processing Volume");
            SceneManager.MoveGameObjectToScene(volumeObject, scene);
            selectedVolume = volumeObject.AddComponent(volumeType);
            changed = true;
        }

        if (selectedVolume == null)
        {
            return false;
        }

        SerializedObject serializedSelectedVolume = new SerializedObject(selectedVolume);
        SerializedProperty selectedIsGlobal = serializedSelectedVolume.FindProperty("isGlobal");
        SerializedProperty sharedProfile = serializedSelectedVolume.FindProperty("sharedProfile");
        if (selectedIsGlobal == null || sharedProfile == null)
        {
            return false;
        }

        if (!selectedIsGlobal.boolValue)
        {
            selectedIsGlobal.boolValue = true;
            changed = true;
        }

        if (sharedProfile.objectReferenceValue != volumeProfile)
        {
            sharedProfile.objectReferenceValue = volumeProfile;
            changed = true;
        }

        if (serializedSelectedVolume.hasModifiedProperties)
        {
            serializedSelectedVolume.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(selectedVolume);
        }

        return true;
    }

    private static void CaptureScriptsLocally()
    {
        if (Directory.Exists(CompatibilityBackupPath))
        {
            return;
        }

        Directory.CreateDirectory(CompatibilityBackupPath);
        for (int i = 0; i < ProtectedScriptPaths.Length; i++)
        {
            string sourcePath = ProtectedScriptPaths[i];
            if (File.Exists(sourcePath))
            {
                File.Copy(
                    sourcePath,
                    Path.Combine(CompatibilityBackupPath, Path.GetFileName(sourcePath)),
                    true);
            }
        }
    }

    private static void RestoreScriptsLocally()
    {
        if (!Directory.Exists(CompatibilityBackupPath))
        {
            return;
        }

        try
        {
            for (int i = 0; i < ProtectedScriptPaths.Length; i++)
            {
                string destinationPath = ProtectedScriptPaths[i];
                string backupPath = Path.Combine(
                    CompatibilityBackupPath,
                    Path.GetFileName(destinationPath));
                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, destinationPath, true);
                }
            }
        }
        finally
        {
            try
            {
                Directory.Delete(CompatibilityBackupPath, true);
            }
            catch (Exception)
            {
            }
        }
    }
}
#endif
