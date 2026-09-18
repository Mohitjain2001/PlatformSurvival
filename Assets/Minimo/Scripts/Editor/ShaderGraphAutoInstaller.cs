#if UNITY_EDITOR

using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

[InitializeOnLoad]
internal static class ShaderGraphAutoInstaller
{
    private const string PackageName = "com.unity.shadergraph";
    private const string ScriptFileName = "ShaderGraphAutoInstaller.cs";

    private const int MaximumRetryCount = 15;
    private const double RetryDelaySeconds = 1.0;

    private static ListRequest listRequest;
    private static AddRequest addRequest;

    private static bool operationStarted;
    private static bool waitingForRetry;

    private static int retryCount;
    private static double nextRetryTime;

    private static string InstallationStartedKey =>
        "ShaderGraphAutoInstaller.InstallationStarted."
        + UnityEngine.Application.dataPath;

    static ShaderGraphAutoInstaller()
    {
        EditorApplication.delayCall -= StartOperation;
        EditorApplication.delayCall += StartOperation;
    }

    private static void StartOperation()
    {
        if (operationStarted)
            return;

        operationStarted = true;

        EditorApplication.update -= UpdateOperation;
        EditorApplication.update += UpdateOperation;

        RequestInstalledPackageList();
    }

    private static void RequestInstalledPackageList()
    {
        if (listRequest != null || addRequest != null)
            return;

        waitingForRetry = false;

        try
        {
            listRequest = Client.List(
                offlineMode: true,
                includeIndirectDependencies: true
            );
        }
        catch
        {
            ScheduleRetry();
        }
    }

    private static void UpdateOperation()
    {
        if (listRequest != null)
        {
            if (!listRequest.IsCompleted)
                return;

            ListRequest completedRequest = listRequest;
            listRequest = null;

            HandlePackageList(completedRequest);
            return;
        }

        if (addRequest != null)
        {
            if (!addRequest.IsCompleted)
                return;

            AddRequest completedRequest = addRequest;
            addRequest = null;

            HandlePackageInstallation(completedRequest);
            return;
        }

        if (waitingForRetry &&
            EditorApplication.timeSinceStartup >= nextRetryTime)
        {
            waitingForRetry = false;
            RequestInstalledPackageList();
        }
    }

    private static void HandlePackageList(ListRequest request)
    {
        if (request.Status != StatusCode.Success)
        {
            ScheduleRetry();
            return;
        }

        bool shaderGraphInstalled = IsShaderGraphInstalled(request);

        if (shaderGraphInstalled)
        {
            bool installedByThisScript =
                EditorPrefs.GetBool(InstallationStartedKey, false);

            if (installedByThisScript)
            {
                EditorPrefs.DeleteKey(InstallationStartedKey);
                StopPackageOperations();
                QueueSelfDeletion();
            }
            else
            {
                StopPackageOperations();
            }

            return;
        }

        EditorPrefs.SetBool(InstallationStartedKey, true);
        InstallShaderGraph();
    }

    private static bool IsShaderGraphInstalled(ListRequest request)
    {
        if (request.Result == null)
            return false;

        foreach (var packageInfo in request.Result)
        {
            if (string.Equals(
                    packageInfo.name,
                    PackageName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void InstallShaderGraph()
    {
        if (addRequest != null)
            return;

        try
        {
            addRequest = Client.Add(PackageName);
        }
        catch
        {
            EditorPrefs.DeleteKey(InstallationStartedKey);
            ScheduleRetry();
        }
    }

    private static void HandlePackageInstallation(AddRequest request)
    {
        if (request.Status != StatusCode.Success)
        {
            EditorPrefs.DeleteKey(InstallationStartedKey);
            StopPackageOperations();
            return;
        }

        retryCount = 0;
        waitingForRetry = true;
        nextRetryTime =
            EditorApplication.timeSinceStartup + 0.5;
    }

    private static void ScheduleRetry()
    {
        listRequest = null;
        addRequest = null;

        retryCount++;

        if (retryCount > MaximumRetryCount)
        {
            EditorPrefs.DeleteKey(InstallationStartedKey);
            StopPackageOperations();
            return;
        }

        waitingForRetry = true;
        nextRetryTime =
            EditorApplication.timeSinceStartup + RetryDelaySeconds;
    }

    private static void StopPackageOperations()
    {
        EditorApplication.update -= UpdateOperation;

        listRequest = null;
        addRequest = null;

        waitingForRetry = false;
        operationStarted = false;
    }

    private static void QueueSelfDeletion()
    {
        EditorApplication.delayCall -= BeginWaitingForEditor;
        EditorApplication.delayCall += BeginWaitingForEditor;
    }

    private static void BeginWaitingForEditor()
    {
        EditorApplication.update -= DeleteWhenEditorIsReady;
        EditorApplication.update += DeleteWhenEditorIsReady;
    }

    private static void DeleteWhenEditorIsReady()
    {
        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            return;
        }

        EditorApplication.update -= DeleteWhenEditorIsReady;
        DeleteInstallerScript();
    }

    private static void DeleteInstallerScript()
    {
        string scriptPath = FindInstallerScriptPath();

        if (string.IsNullOrEmpty(scriptPath))
            return;

        AssetDatabase.DeleteAsset(scriptPath);
    }

    private static string FindInstallerScriptPath()
    {
        string[] scriptGuids = AssetDatabase.FindAssets(
            "ShaderGraphAutoInstaller t:MonoScript"
        );

        string filenameFallback = null;
        int matchingFilenameCount = 0;

        foreach (string guid in scriptGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (string.IsNullOrEmpty(path))
                continue;

            MonoScript monoScript =
                AssetDatabase.LoadAssetAtPath<MonoScript>(path);

            if (monoScript != null &&
                monoScript.GetClass() ==
                typeof(ShaderGraphAutoInstaller))
            {
                return path;
            }

            if (string.Equals(
                    Path.GetFileName(path),
                    ScriptFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                filenameFallback = path;
                matchingFilenameCount++;
            }
        }

        return matchingFilenameCount == 1
            ? filenameFallback
            : null;
    }
}

#endif