using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneSetupUtility
{
    [InitializeOnLoadMethod]
    private static void AutoEnsureSceneBaked()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string tilePrefabPath = "Assets/Prefabs/PlatformTile.prefab";
            if (!File.Exists(tilePrefabPath))
            {
                Debug.Log("[SceneSetupUtility] First-time setup: Baking assets, prefabs, and arena in GameplayScene...");
                SetupAllScenes();
            }
        };
    }

    [MenuItem("Tools/Bake Arena In GameplayScene")]
    public static void BakeArenaInGameplayScene()
    {
        EnsureAssetsAndPrefabsExist();
        SetupGameplayScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Arena successfully baked in GameplayScene!");
    }

    public static void EnsureAssetsAndPrefabsExist()
    {
        if (!Directory.Exists("Assets/Models")) Directory.CreateDirectory("Assets/Models");
        if (!Directory.Exists("Assets/Materials")) Directory.CreateDirectory("Assets/Materials");
        if (!Directory.Exists("Assets/Prefabs")) Directory.CreateDirectory("Assets/Prefabs");

        // 1. Hexagon Tile Mesh Asset
        Mesh hexMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/HexagonTileMesh.asset");
        if (hexMesh == null)
        {
            hexMesh = PlatformGridGenerator.CreateHexagonMesh(1.2f - 0.08f, 0.4f);
            hexMesh.name = "HexagonTileMesh";
            AssetDatabase.CreateAsset(hexMesh, "Assets/Models/HexagonTileMesh.asset");
        }

        // 2. Materials
        Shader stdShader = Shader.Find("Standard");
        Material matCoral = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tile_Layer1_Coral.mat");
        if (matCoral == null)
        {
            matCoral = new Material(stdShader) { color = new Color(0.88f, 0.38f, 0.48f) };
            AssetDatabase.CreateAsset(matCoral, "Assets/Materials/Tile_Layer1_Coral.mat");
        }
        Material matCyan = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tile_Layer2_Cyan.mat");
        if (matCyan == null)
        {
            matCyan = new Material(stdShader) { color = new Color(0.20f, 0.75f, 0.95f) };
            AssetDatabase.CreateAsset(matCyan, "Assets/Materials/Tile_Layer2_Cyan.mat");
        }
        Material matGold = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tile_Layer3_Gold.mat");
        if (matGold == null)
        {
            matGold = new Material(stdShader) { color = new Color(0.98f, 0.70f, 0.15f) };
            AssetDatabase.CreateAsset(matGold, "Assets/Materials/Tile_Layer3_Gold.mat");
        }

        // 3. Platform Tile Prefab
        GameObject tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlatformTile.prefab");
        if (tilePrefab == null)
        {
            GameObject tempTile = new GameObject("PlatformTile");
            MeshFilter mf = tempTile.AddComponent<MeshFilter>();
            mf.sharedMesh = hexMesh;
            MeshRenderer mr = tempTile.AddComponent<MeshRenderer>();
            mr.sharedMaterial = matCoral;
            MeshCollider mc = tempTile.AddComponent<MeshCollider>();
            mc.sharedMesh = hexMesh;
            mc.convex = true;
            PlatformTile pt = tempTile.AddComponent<PlatformTile>();
            pt.SetColors(new Color(0.88f, 0.38f, 0.48f), new Color(1.0f, 0.45f, 0.0f), new Color(0.95f, 0.15f, 0.15f));
            PrefabUtility.SaveAsPrefabAsset(tempTile, "Assets/Prefabs/PlatformTile.prefab");
            Object.DestroyImmediate(tempTile);
        }

        // 4. Player and Bot Prefabs
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        if (playerPrefab == null)
        {
            GameObject tempPlayer = CharacterModelBuilder.BuildRunnerCharacter("Player", new Color(0.12f, 0.65f, 1.0f), true);
            tempPlayer.AddComponent<PlayerController>();
            PrefabUtility.SaveAsPrefabAsset(tempPlayer, "Assets/Prefabs/Player.prefab");
            Object.DestroyImmediate(tempPlayer);
        }
        GameObject botPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bot.prefab");
        if (botPrefab == null)
        {
            GameObject tempBot = CharacterModelBuilder.BuildRunnerCharacter("Bot", new Color(0.95f, 0.25f, 0.20f), false);
            tempBot.AddComponent<BotController>();
            PrefabUtility.SaveAsPrefabAsset(tempBot, "Assets/Prefabs/Bot.prefab");
            Object.DestroyImmediate(tempBot);
        }

        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Setup All Project Scenes")]
    public static void SetupAllScenes()
    {
        string scenesDir = "Assets/Scenes";
        if (!Directory.Exists(scenesDir)) Directory.CreateDirectory(scenesDir);

        EnsureAssetsAndPrefabsExist();

        SetupSplashScene();
        SetupGameplayScene();
        SetupWinScene();
        SetupGameOverScene();

        // Configure EditorBuildSettings for all 4 distinct scenes
        EditorBuildSettingsScene[] buildScenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/SplashScene.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/GameplayScene.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/WinScene.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/GameOverScene.unity", true)
        };
        EditorBuildSettings.scenes = buildScenes;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("All 4 Scenes (Splash, Gameplay, Win, GameOver) successfully created, linked, and added to Build Settings!");
    }

    public static void SetupSplashScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Lighting & Camera
        GameObject lightObj = new GameObject("Directional Light");
        Light lightComp = lightObj.AddComponent<Light>();
        lightComp.type = LightType.Directional;
        lightComp.intensity = 1.25f;
        lightComp.color = new Color(1.0f, 0.96f, 0.90f);
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        GameObject cameraObj = new GameObject("Main Camera");
        cameraObj.tag = "MainCamera";
        Camera cameraComp = cameraObj.AddComponent<Camera>();
        cameraComp.clearFlags = CameraClearFlags.SolidColor;
        cameraComp.backgroundColor = new Color(0.35f, 0.65f, 0.95f);
        cameraObj.AddComponent<AudioListener>();
        cameraObj.transform.position = new Vector3(0, 5.0f, -8.0f);
        cameraObj.transform.rotation = Quaternion.Euler(30f, 0f, 0f);

        // 2. Decorative 3D Platform Preview
        GameObject platformObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        platformObj.name = "PreviewPlatform";
        platformObj.transform.position = new Vector3(0, 0, 0);
        platformObj.transform.localScale = new Vector3(4.5f, 0.3f, 4.5f);
        platformObj.GetComponent<MeshRenderer>().material.color = new Color(0.20f, 0.75f, 0.95f);

        // Decorative 3D Runner Character
        GameObject bean = CharacterModelBuilder.BuildRunnerCharacter("PreviewBean", new Color(0.12f, 0.65f, 1.0f));
        bean.transform.position = new Vector3(0, 0.15f, 0);
        Object.DestroyImmediate(bean.GetComponent<Rigidbody>());

        // 3. UI Canvas
        GameObject eventSystemObj = new GameObject("EventSystem");
        eventSystemObj.AddComponent<EventSystem>();
        eventSystemObj.AddComponent<StandaloneInputModule>();

        GameObject canvasObj = new GameObject("UI Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();
        SplashManager splashMgr = canvasObj.AddComponent<SplashManager>();

        // Title Text
        TextMeshProUGUI title = CreateTMPText("Title", canvasObj.transform, "FALL SURVIVAL 3D", 70, new Vector2(0, 480f));
        title.color = new Color(1.0f, 0.88f, 0.20f);

        // Subtitle
        TextMeshProUGUI subtitle = CreateTMPText("Subtitle", canvasObj.transform, "LAST PLAYER STANDING WINS!", 34, new Vector2(0, 390f));
        subtitle.color = Color.white;

        // Instructions Card Panel
        GameObject guidePanel = CreatePanel("GuidePanel", canvasObj.transform, new Color(0.08f, 0.12f, 0.18f, 0.75f));
        RectTransform guideRt = guidePanel.GetComponent<RectTransform>();
        guideRt.anchorMin = new Vector2(0.5f, 0.5f);
        guideRt.anchorMax = new Vector2(0.5f, 0.5f);
        guideRt.pivot = new Vector2(0.5f, 0.5f);
        guideRt.anchoredPosition = new Vector2(0, 60f);
        guideRt.sizeDelta = new Vector2(800f, 380f);

        string guideString = "HOW TO PLAY\n\n" +
                             "• Drag Joystick to Move freely\n" +
                             "• Auto-Jump across gaps automatically\n" +
                             "• Platforms shake & fall when stepped on\n" +
                             "• 3 Floor Levels — Survive to the bottom!";
        TextMeshProUGUI guideText = CreateTMPText("GuideText", guidePanel.transform, guideString, 32, Vector2.zero);
        guideText.alignment = TextAlignmentOptions.Center;

        // Play Button
        Button playBtn = CreateButton("PlayButton", canvasObj.transform, "PLAY GAME", new Vector2(0, -380f), new Color(0.15f, 0.80f, 0.35f));
        playBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(500f, 130f);

        SerializedObject soSplash = new SerializedObject(splashMgr);
        soSplash.FindProperty("playButton").objectReferenceValue = playBtn;
        soSplash.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/SplashScene.unity");
    }

    public static void SetupGameplayScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Setup Directional Light
        GameObject lightObj = new GameObject("Directional Light");
        Light lightComp = lightObj.AddComponent<Light>();
        lightComp.type = LightType.Directional;
        lightComp.intensity = 1.25f;
        lightComp.color = new Color(1.0f, 0.96f, 0.90f);
        lightComp.shadows = LightShadows.Soft;
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // 2. Setup Main Camera & FollowCamera
        GameObject cameraObj = new GameObject("Main Camera");
        cameraObj.tag = "MainCamera";
        Camera cameraComp = cameraObj.AddComponent<Camera>();
        cameraComp.clearFlags = CameraClearFlags.SolidColor;
        cameraComp.backgroundColor = new Color(0.42f, 0.68f, 0.95f);
        cameraComp.fieldOfView = 65f;
        cameraObj.AddComponent<AudioListener>();
        FollowCamera followCamera = cameraObj.AddComponent<FollowCamera>();
        cameraObj.transform.position = new Vector3(0, 11.5f, -7.8f);
        cameraObj.transform.rotation = Quaternion.Euler(49f, 0f, 0f);

        // 3. Setup Grid Generator & Pre-Bake 3-Floor Hexagon Arena into the Scene!
        GameObject gridObj = new GameObject("Arena_GridGenerator");
        PlatformGridGenerator gridGenerator = gridObj.AddComponent<PlatformGridGenerator>();

        GameObject tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlatformTile.prefab");
        Material matCoral = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tile_Layer1_Coral.mat");
        Material matCyan = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tile_Layer2_Cyan.mat");
        Material matGold = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tile_Layer3_Gold.mat");
        Material[] layerMats = new Material[] { matCoral, matCyan, matGold };

        Color[] layerColors = new Color[]
        {
            new Color(0.88f, 0.38f, 0.48f),
            new Color(0.20f, 0.75f, 0.95f),
            new Color(0.98f, 0.70f, 0.15f)
        };
        Color tileWarning = new Color(1.0f, 0.45f, 0.0f);
        Color tileDanger = new Color(0.95f, 0.15f, 0.15f);

        int gridRadius = 6;
        float effectiveRadius = 1.2f;
        float xSpacing = Mathf.Sqrt(3) * effectiveRadius;
        float zSpacing = 1.5f * effectiveRadius;
        float layerSpacing = 12.0f;

        for (int layer = 0; layer < 3; layer++)
        {
            float layerY = -layer * layerSpacing;
            GameObject layerParent = new GameObject($"Layer_{layer + 1}");
            layerParent.transform.SetParent(gridObj.transform);

            Material layerMat = layerMats[layer % layerMats.Length];
            Color baseColor = layerColors[layer % layerColors.Length];

            for (int q = -gridRadius; q <= gridRadius; q++)
            {
                int r1 = Mathf.Max(-gridRadius, -q - gridRadius);
                int r2 = Mathf.Min(gridRadius, -q + gridRadius);

                for (int r = r1; r <= r2; r++)
                {
                    float x = xSpacing * (q + r / 2.0f);
                    float z = zSpacing * r;
                    Vector3 pos = new Vector3(x, layerY, z);

                    GameObject tileObj;
                    if (tilePrefab != null)
                    {
                        tileObj = (GameObject)PrefabUtility.InstantiatePrefab(tilePrefab, layerParent.transform);
                        tileObj.transform.position = pos;
                        tileObj.name = $"HexTile_{layer + 1}_{q}_{r}";
                    }
                    else
                    {
                        tileObj = new GameObject($"HexTile_{layer + 1}_{q}_{r}");
                        tileObj.transform.SetParent(layerParent.transform);
                        tileObj.transform.position = pos;
                    }

                    if (layerMat != null)
                    {
                        MeshRenderer mr = tileObj.GetComponent<MeshRenderer>();
                        if (mr != null) mr.sharedMaterial = layerMat;
                    }

                    PlatformTile tile = tileObj.GetComponent<PlatformTile>();
                    if (tile == null) tile = tileObj.AddComponent<PlatformTile>();
                    tile.SetColors(baseColor, tileWarning, tileDanger);
                    tile.SetInitialPosition(pos);
                }
            }
        }

        SerializedObject soGrid = new SerializedObject(gridGenerator);
        soGrid.FindProperty("tilePrefab").objectReferenceValue = tilePrefab;
        soGrid.ApplyModifiedProperties();

        // 4. Setup EventSystem
        GameObject eventSystemObj = new GameObject("EventSystem");
        eventSystemObj.AddComponent<EventSystem>();
        eventSystemObj.AddComponent<StandaloneInputModule>();

        // 5. Setup Canvas & UI
        GameObject canvasObj = new GameObject("UI Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();
        UIManager uiManager = canvasObj.AddComponent<UIManager>();

        // 5a. Invisible Full-Screen Touch Zone (Zero UI icons)
        GameObject joystickObj = CreateUIElement("VirtualJoystick", canvasObj.transform);
        RectTransform joyRect = joystickObj.GetComponent<RectTransform>();
        joyRect.anchorMin = Vector2.zero;
        joyRect.anchorMax = Vector2.one;
        joyRect.pivot = new Vector2(0.5f, 0.5f);
        joyRect.anchoredPosition = Vector2.zero;
        joyRect.sizeDelta = Vector2.zero;

        Image joyBgImage = joystickObj.AddComponent<Image>();
        joyBgImage.color = Color.clear; // Transparent raycast target

        GameObject handleObj = CreateUIElement("Handle", joystickObj.transform);
        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = Vector2.zero;

        Image handleImage = handleObj.AddComponent<Image>();
        handleImage.color = Color.clear;
        handleObj.SetActive(false);

        VirtualJoystick joystickScript = joystickObj.AddComponent<VirtualJoystick>();

        // 5b. Alive Count Text
        GameObject aliveTextObj = CreateUIElement("AliveCountText", canvasObj.transform);
        RectTransform aliveRect = aliveTextObj.GetComponent<RectTransform>();
        aliveRect.anchorMin = new Vector2(0.5f, 1f);
        aliveRect.anchorMax = new Vector2(0.5f, 1f);
        aliveRect.pivot = new Vector2(0.5f, 1f);
        aliveRect.anchoredPosition = new Vector2(0, -70f);
        aliveRect.sizeDelta = new Vector2(600f, 100f);

        TextMeshProUGUI aliveTmp = aliveTextObj.AddComponent<TextMeshProUGUI>();
        aliveTmp.text = "ALIVE: 5 / 5";
        aliveTmp.fontSize = 54;
        aliveTmp.fontStyle = FontStyles.Bold;
        aliveTmp.alignment = TextAlignmentOptions.Center;
        aliveTmp.color = Color.white;

        // 5c. Game Over Panel (Hidden by default in edit mode)
        GameObject gameOverObj = CreatePanel("GameOverPanel", canvasObj.transform, new Color(0.12f, 0.05f, 0.05f, 0.90f));
        TextMeshProUGUI gameOverTitle = CreateTMPText("Title", gameOverObj.transform, "GAME OVER", 64, new Vector2(0, 200f));
        TextMeshProUGUI gameOverRank = CreateTMPText("RankText", gameOverObj.transform, "ELIMINATED!", 42, new Vector2(0, 80f));
        Button gameOverRetryBtn = CreateButton("RetryButton", gameOverObj.transform, "TRY AGAIN", new Vector2(0, -60f), new Color(0.2f, 0.8f, 0.4f));
        Button gameOverMenuBtn = CreateButton("MenuButton", gameOverObj.transform, "MAIN MENU", new Vector2(0, -180f), new Color(0.35f, 0.55f, 0.85f));
        gameOverObj.SetActive(false); // Clean edit mode view

        // 5d. Victory Panel (Hidden by default in edit mode)
        GameObject victoryObj = CreatePanel("VictoryPanel", canvasObj.transform, new Color(0.08f, 0.32f, 0.12f, 0.90f));
        TextMeshProUGUI victoryTitle = CreateTMPText("Title", victoryObj.transform, "VICTORY!\nLAST PLAYER STANDING!", 60, new Vector2(0, 180f));
        Button victoryRetryBtn = CreateButton("RetryButton", victoryObj.transform, "PLAY AGAIN", new Vector2(0, -60f), new Color(0.98f, 0.80f, 0.15f));
        Button victoryMenuBtn = CreateButton("MenuButton", victoryObj.transform, "MAIN MENU", new Vector2(0, -180f), new Color(0.35f, 0.55f, 0.85f));
        victoryObj.SetActive(false); // Clean edit mode view

        // Link UIManager
        SerializedObject soUI = new SerializedObject(uiManager);
        soUI.FindProperty("aliveCountText").objectReferenceValue = aliveTmp;
        soUI.FindProperty("gameOverPanel").objectReferenceValue = gameOverObj;
        soUI.FindProperty("gameOverTitleText").objectReferenceValue = gameOverTitle;
        soUI.FindProperty("rankText").objectReferenceValue = gameOverRank;
        soUI.FindProperty("retryGameOverButton").objectReferenceValue = gameOverRetryBtn;
        soUI.FindProperty("menuGameOverButton").objectReferenceValue = gameOverMenuBtn;
        soUI.FindProperty("victoryPanel").objectReferenceValue = victoryObj;
        soUI.FindProperty("victoryTitleText").objectReferenceValue = victoryTitle;
        soUI.FindProperty("retryVictoryButton").objectReferenceValue = victoryRetryBtn;
        soUI.FindProperty("menuVictoryButton").objectReferenceValue = victoryMenuBtn;
        soUI.ApplyModifiedProperties();

        // 6. Setup GameManager
        GameObject gmObj = new GameObject("GameManager");
        GameManager gm = gmObj.AddComponent<GameManager>();

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        GameObject botPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bot.prefab");

        SerializedObject soGM = new SerializedObject(gm);
        soGM.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
        soGM.FindProperty("botPrefab").objectReferenceValue = botPrefab;
        soGM.FindProperty("gridGenerator").objectReferenceValue = gridGenerator;
        soGM.FindProperty("joystick").objectReferenceValue = joystickScript;
        soGM.FindProperty("followCamera").objectReferenceValue = followCamera;
        soGM.FindProperty("uiManager").objectReferenceValue = uiManager;
        soGM.ApplyModifiedProperties();

        // Save GameplayScene
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/GameplayScene.unity");
    }

    public static void SetupWinScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Lighting & Camera
        GameObject lightObj = new GameObject("Directional Light");
        Light lightComp = lightObj.AddComponent<Light>();
        lightComp.type = LightType.Directional;
        lightComp.intensity = 1.3f;
        lightComp.color = new Color(1.0f, 0.95f, 0.85f);
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        GameObject cameraObj = new GameObject("Main Camera");
        cameraObj.tag = "MainCamera";
        Camera cameraComp = cameraObj.AddComponent<Camera>();
        cameraComp.clearFlags = CameraClearFlags.SolidColor;
        cameraComp.backgroundColor = new Color(0.15f, 0.65f, 0.45f); // Vibrant winner emerald
        cameraObj.AddComponent<AudioListener>();
        cameraObj.transform.position = new Vector3(0, 3.5f, -6.0f);
        cameraObj.transform.rotation = Quaternion.Euler(20f, 0f, 0f);

        // 2. Winner Podium 3D Setup
        GameObject podiumObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        podiumObj.name = "VictoryPodium";
        podiumObj.transform.position = new Vector3(0, 0, 0);
        podiumObj.transform.localScale = new Vector3(3.2f, 0.4f, 3.2f);
        podiumObj.GetComponent<MeshRenderer>().material.color = new Color(1.0f, 0.84f, 0.0f); // Gold podium

        // Winner 3D Runner Character
        GameObject winnerBean = CharacterModelBuilder.BuildRunnerCharacter("WinnerBean", new Color(0.12f, 0.65f, 1.0f));
        winnerBean.transform.position = new Vector3(0, 0.4f, 0);
        winnerBean.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        Object.DestroyImmediate(winnerBean.GetComponent<Rigidbody>());

        // 3. UI Canvas
        GameObject eventSystemObj = new GameObject("EventSystem");
        eventSystemObj.AddComponent<EventSystem>();
        eventSystemObj.AddComponent<StandaloneInputModule>();

        GameObject canvasObj = new GameObject("UI Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();
        WinSceneManager winMgr = canvasObj.AddComponent<WinSceneManager>();

        // Header Title
        TextMeshProUGUI title = CreateTMPText("VictoryTitle", canvasObj.transform, "VICTORY!", 72, new Vector2(0, 520f));
        title.color = new Color(1.0f, 0.90f, 0.20f);

        // Subtitle
        TextMeshProUGUI subtitle = CreateTMPText("Subtitle", canvasObj.transform, "LAST PLAYER STANDING!", 36, new Vector2(0, 430f));
        subtitle.color = Color.white;

        // Buttons
        Button playAgainBtn = CreateButton("PlayAgainButton", canvasObj.transform, "PLAY AGAIN", new Vector2(0, -420f), new Color(0.20f, 0.85f, 0.40f));
        Button menuBtn = CreateButton("MenuButton", canvasObj.transform, "MAIN MENU", new Vector2(0, -560f), new Color(0.35f, 0.55f, 0.85f));

        // Connect references
        SerializedObject soWin = new SerializedObject(winMgr);
        soWin.FindProperty("titleText").objectReferenceValue = title;
        soWin.FindProperty("subtitleText").objectReferenceValue = subtitle;
        soWin.FindProperty("playAgainButton").objectReferenceValue = playAgainBtn;
        soWin.FindProperty("mainMenuButton").objectReferenceValue = menuBtn;
        soWin.FindProperty("winnerBeanTransform").objectReferenceValue = winnerBean.transform;
        soWin.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/WinScene.unity");
    }

    public static void SetupGameOverScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Lighting & Camera
        GameObject lightObj = new GameObject("Directional Light");
        Light lightComp = lightObj.AddComponent<Light>();
        lightComp.type = LightType.Directional;
        lightComp.intensity = 1.0f;
        lightComp.color = new Color(0.9f, 0.85f, 0.85f);
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        GameObject cameraObj = new GameObject("Main Camera");
        cameraObj.tag = "MainCamera";
        Camera cameraComp = cameraObj.AddComponent<Camera>();
        cameraComp.clearFlags = CameraClearFlags.SolidColor;
        cameraComp.backgroundColor = new Color(0.18f, 0.12f, 0.16f); // Moody dark plum/red
        cameraObj.AddComponent<AudioListener>();
        cameraObj.transform.position = new Vector3(0, 3.5f, -6.0f);
        cameraObj.transform.rotation = Quaternion.Euler(20f, 0f, 0f);

        // 2. Fallen platform visual
        GameObject fallenHex = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fallenHex.name = "FallenHex";
        fallenHex.transform.position = new Vector3(0, -0.5f, 0);
        fallenHex.transform.localScale = new Vector3(3.5f, 0.2f, 3.5f);
        fallenHex.GetComponent<MeshRenderer>().material.color = new Color(0.85f, 0.25f, 0.25f);

        // 3. UI Canvas
        GameObject eventSystemObj = new GameObject("EventSystem");
        eventSystemObj.AddComponent<EventSystem>();
        eventSystemObj.AddComponent<StandaloneInputModule>();

        GameObject canvasObj = new GameObject("UI Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();
        GameOverSceneManager gameOverMgr = canvasObj.AddComponent<GameOverSceneManager>();

        // Header Title
        TextMeshProUGUI title = CreateTMPText("GameOverTitle", canvasObj.transform, "ELIMINATED!", 72, new Vector2(0, 480f));
        title.color = new Color(0.95f, 0.35f, 0.35f);

        // Rank info
        TextMeshProUGUI rank = CreateTMPText("RankText", canvasObj.transform, "You were eliminated!", 42, new Vector2(0, 360f));
        rank.color = Color.white;

        // Tip text panel
        GameObject tipCard = CreatePanel("TipCard", canvasObj.transform, new Color(0.12f, 0.08f, 0.12f, 0.85f));
        RectTransform tipRt = tipCard.GetComponent<RectTransform>();
        tipRt.anchorMin = new Vector2(0.5f, 0.5f);
        tipRt.anchorMax = new Vector2(0.5f, 0.5f);
        tipRt.sizeDelta = new Vector2(750f, 220f);
        tipRt.anchoredPosition = new Vector2(0, 40f);

        TextMeshProUGUI tip = CreateTMPText("TipText", tipCard.transform, "PRO TIP:\nDon't stop moving!\nHexagons drop shortly after you touch them!", 32, Vector2.zero);
        tip.color = new Color(0.90f, 0.90f, 0.90f);

        // Buttons
        Button retryBtn = CreateButton("RetryButton", canvasObj.transform, "TRY AGAIN", new Vector2(0, -320f), new Color(0.20f, 0.80f, 0.40f));
        Button menuBtn = CreateButton("MenuButton", canvasObj.transform, "MAIN MENU", new Vector2(0, -460f), new Color(0.35f, 0.55f, 0.85f));

        // Connect references
        SerializedObject soGameOver = new SerializedObject(gameOverMgr);
        soGameOver.FindProperty("titleText").objectReferenceValue = title;
        soGameOver.FindProperty("rankText").objectReferenceValue = rank;
        soGameOver.FindProperty("tipText").objectReferenceValue = tip;
        soGameOver.FindProperty("retryButton").objectReferenceValue = retryBtn;
        soGameOver.FindProperty("mainMenuButton").objectReferenceValue = menuBtn;
        soGameOver.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/GameOverScene.unity");
    }

    private static GameObject CreateUIElement(string name, Transform parent)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        return obj;
    }

    private static GameObject CreatePanel(string name, Transform parent, Color bgColor)
    {
        GameObject panel = CreateUIElement(name, parent);
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        Image img = panel.AddComponent<Image>();
        img.color = bgColor;
        return panel;
    }

    private static TextMeshProUGUI CreateTMPText(string name, Transform parent, string text, float fontSize, Vector2 anchoredPos)
    {
        GameObject txtObj = CreateUIElement(name, parent);
        RectTransform rt = txtObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(850f, 180f);

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        return tmp;
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 anchoredPos, Color btnColor)
    {
        GameObject btnObj = CreateUIElement(name, parent);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(420f, 100f);

        Image img = btnObj.AddComponent<Image>();
        img.color = btnColor;

        Button btn = btnObj.AddComponent<Button>();

        GameObject txtObj = CreateUIElement("Text", btnObj.transform);
        RectTransform txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 38;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return btn;
    }
}
