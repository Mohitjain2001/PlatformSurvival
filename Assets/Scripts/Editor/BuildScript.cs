using System.IO;
using UnityEditor;
using UnityEngine;

public class BuildScript
{
    [MenuItem("Build/Build Android APK")]
    public static void BuildAndroid()
    {
        // 1. Ensure scene setup is ran before build
        SceneSetupUtility.SetupAllScenes();

        // 2. Configure Player Settings
        PlayerSettings.companyName = "PlatformGames";
        PlayerSettings.productName = "PlatformSurvival";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.platformgames.platformsurvival");
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

        // 3. Define Output Path
        string buildDir = "Builds";
        if (!Directory.Exists(buildDir))
        {
            Directory.CreateDirectory(buildDir);
        }
        string apkPath = Path.Combine(buildDir, "PlatformSurvival.apk");

        // 4. Define Build Scenes in Order (Splash -> Gameplay -> Win -> GameOver)
        string[] scenes = new string[] 
        { 
            "Assets/Scenes/SplashScene.unity",
            "Assets/Scenes/GameplayScene.unity",
            "Assets/Scenes/WinScene.unity",
            "Assets/Scenes/GameOverScene.unity"
        };

        // 5. Build Options
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = apkPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        Debug.Log("Starting Android APK Build to: " + apkPath);
        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        var summary = report.summary;

        if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log("Android Build succeeded! Total size: " + summary.totalSize + " bytes.");
        }
        else if (summary.result == UnityEditor.Build.Reporting.BuildResult.Failed)
        {
            Debug.LogError("Android Build failed with " + summary.totalErrors + " errors.");
        }
    }
}
