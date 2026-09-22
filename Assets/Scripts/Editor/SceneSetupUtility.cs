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
        AssetDatabase.Refresh();
        EnsureAssetsAndPrefabsExist();
        SetupGameplayScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Arena successfully baked in GameplayScene!");
    }

    [MenuItem("Tools/Bake Arena With Kenney Hexagon Kit")]
    public static void BakeArenaWithKenneyHexagonKit()
    {
        AssetDatabase.Refresh();
        EnsureAssetsAndPrefabsExist();
        EnsureKenneyPrefabsExist(true);
        SetupGameplayScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Arena successfully baked with Kenney Hexagon Kit in GameplayScene!");
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

    public static GameObject[] EnsureKenneyPrefabsExist(bool forceRecreate = false)
    {
        string grassFbx = "Assets/KenneyHexagonKit/FBX format/grass.fbx";
        string sandFbx = "Assets/KenneyHexagonKit/FBX format/sand.fbx";
        string stoneFbx = "Assets/KenneyHexagonKit/FBX format/stone.fbx";
        string colormapPath = "Assets/KenneyHexagonKit/FBX format/Textures/colormap.png";

        if (!File.Exists(grassFbx)) return null;

        Texture2D colormap = AssetDatabase.LoadAssetAtPath<Texture2D>(colormapPath);

        GameObject grassPrefab = CreateOrUpdateKenneyPrefab(grassFbx, "Assets/Prefabs/KenneyTile_Grass.prefab", "KenneyTile_Grass", colormap, forceRecreate);
        GameObject sandPrefab = CreateOrUpdateKenneyPrefab(sandFbx, "Assets/Prefabs/KenneyTile_Sand.prefab", "KenneyTile_Sand", colormap, forceRecreate);
        GameObject stonePrefab = CreateOrUpdateKenneyPrefab(stoneFbx, "Assets/Prefabs/KenneyTile_Stone.prefab", "KenneyTile_Stone", colormap, forceRecreate);

        if (grassPrefab != null && sandPrefab != null && stonePrefab != null)
        {
            return new GameObject[] { grassPrefab, sandPrefab, stonePrefab };
        }
        return null;
    }

    private static GameObject CreateOrUpdateKenneyPrefab(string fbxPath, string prefabPath, string prefabName, Texture2D colormap, bool forceRecreate)
    {
        if (!forceRecreate)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null) return existing;
        }

        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbxAsset == null) return null;

        GameObject instance = Object.Instantiate(fbxAsset);
        instance.name = prefabName;
        // Scale Kenney hexagon (width 1.0) by 2.0 to match effectiveRadius 1.2f grid with ~0.08m clean gap
        instance.transform.localScale = new Vector3(2.0f, 2.0f, 2.0f);
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;

        // Ensure convex MeshCollider on all child mesh filters
        MeshFilter[] mfs = instance.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in mfs)
        {
            if (mf.sharedMesh != null && mf.gameObject.GetComponent<Collider>() == null)
            {
                MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = true;
            }
        }

        // Apply material with colormap palette texture
        if (colormap != null)
        {
            Material kenneyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Kenney_Colormap.mat");
            if (kenneyMat == null)
            {
                Shader s = Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse");
                kenneyMat = new Material(s)
                {
                    mainTexture = colormap,
                    color = Color.white
                };
                AssetDatabase.CreateAsset(kenneyMat, "Assets/Materials/Kenney_Colormap.mat");
            }
            MeshRenderer[] mrs = instance.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in mrs)
            {
                mr.sharedMaterial = kenneyMat;
            }
        }

        // Add PlatformTile logic
        PlatformTile pt = instance.GetComponent<PlatformTile>();
        if (pt == null) pt = instance.AddComponent<PlatformTile>();
        pt.SetColors(Color.white, new Color(1.0f, 0.45f, 0.0f), new Color(0.95f, 0.15f, 0.15f));

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);
        return savedPrefab;
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

        // 2. Decorative 3D Platform Preview (Kenney Hexagon Tile)
        GameObject kenneyGrass = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/KenneyTile_Grass.prefab");
        GameObject platformObj;
        if (kenneyGrass != null)
        {
            platformObj = (GameObject)PrefabUtility.InstantiatePrefab(kenneyGrass);
            platformObj.name = "PreviewPlatform";
            platformObj.transform.position = Vector3.zero;
            platformObj.transform.localScale = new Vector3(5.5f, 3.5f, 5.5f);
        }
        else
        {
            Mesh hexMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/HexagonTileMesh.asset");
            if (hexMesh == null) hexMesh = PlatformGridGenerator.CreateHexagonMesh(1.2f, 0.4f);
            platformObj = new GameObject("PreviewPlatform");
            platformObj.transform.position = Vector3.zero;
            platformObj.transform.localScale = new Vector3(4.0f, 2.0f, 4.0f);
            MeshFilter mf = platformObj.AddComponent<MeshFilter>();
            mf.sharedMesh = hexMesh;
            MeshRenderer mr = platformObj.AddComponent<MeshRenderer>();
            Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tile_Layer1_Coral.mat");
            mr.sharedMaterial = mat != null ? mat : new Material(Shader.Find("Standard")) { color = new Color(0.20f, 0.75f, 0.95f) };
        }

        // Decorative 3D Runner Character standing on top of Hexagon Tile
        GameObject bean = CharacterModelBuilder.BuildRunnerCharacter("PreviewBean", new Color(0.12f, 0.65f, 1.0f));
        bean.transform.position = new Vector3(0, 0.70f, 0);
        Object.DestroyImmediate(bean.GetComponent<Rigidbody>());

        CharacterNameTag beanTag = bean.GetComponent<CharacterNameTag>();
        if (beanTag == null) beanTag = bean.AddComponent<CharacterNameTag>();
        beanTag.Setup(PlayerPrefs.GetString("PlayerName", "Player"), new Color(0.33f, 0.92f, 0.22f), 1.70f);

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

        Transform tSettingsBtn = canvasObj.transform.Find("Settings Button");
        Transform tSettingsPanel = canvasObj.transform.Find("Settings panel");
        Transform tNameBtn = canvasObj.transform.Find("Name_change");

        Button settingsBtn = tSettingsBtn != null ? tSettingsBtn.GetComponent<Button>() : null;
        GameObject settingsPan = tSettingsPanel != null ? tSettingsPanel.gameObject : null;
        Button nameBtn = tNameBtn != null ? tNameBtn.GetComponent<Button>() : null;

        Button closeBtn = null;
        if (settingsPan != null)
        {
            Button[] btns = settingsPan.GetComponentsInChildren<Button>(true);
            foreach (var b in btns)
            {
                if (b.name.Equals("Close", System.StringComparison.OrdinalIgnoreCase))
                {
                    closeBtn = b;
                    break;
                }
            }
        }

        SerializedObject soSplash = new SerializedObject(splashMgr);
        soSplash.FindProperty("playButton").objectReferenceValue = playBtn;
        if (settingsBtn != null) soSplash.FindProperty("settingsButton").objectReferenceValue = settingsBtn;
        if (settingsPan != null) soSplash.FindProperty("settingsPanel").objectReferenceValue = settingsPan;
        if (closeBtn != null) soSplash.FindProperty("closeSettingsButton").objectReferenceValue = closeBtn;
        if (nameBtn != null) soSplash.FindProperty("nameChangeButton").objectReferenceValue = nameBtn;
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
        cameraComp.backgroundColor = new Color(0.16f, 0.11f, 0.28f); // Deep Twilight Royal Violet (Stunning contrast for Pink, Cyan & Gold tiles)
        cameraComp.fieldOfView = 65f;
        cameraObj.AddComponent<AudioListener>();
        FollowCamera followCamera = cameraObj.AddComponent<FollowCamera>();
        cameraObj.transform.position = new Vector3(0, 11.5f, -7.8f);
        cameraObj.transform.rotation = Quaternion.Euler(49f, 0f, 0f);

        // Stylized atmospheric depth fog
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.16f, 0.11f, 0.28f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 22f;
        RenderSettings.fogEndDistance = 55f;

        // 3. Setup Grid Generator & Pre-Bake 3-Floor Hexagon Arena into the Scene!
        GameObject gridObj = new GameObject("Arena_GridGenerator");
        PlatformGridGenerator gridGenerator = gridObj.AddComponent<PlatformGridGenerator>();

        GameObject[] kenneyPrefabs = EnsureKenneyPrefabsExist();
        bool useKenney = (kenneyPrefabs != null && kenneyPrefabs.Length >= 3);

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
        float layerSpacing = 12.0f;

        for (int layer = 0; layer < 3; layer++)
        {
            float layerY = -layer * layerSpacing;
            GameObject layerParent = new GameObject($"Layer_{layer + 1}");
            layerParent.transform.SetParent(gridObj.transform);

            Material layerMat = layerMats[layer % layerMats.Length];
            Color baseColor = layerColors[layer % layerColors.Length];
            GameObject layerPrefab = useKenney ? kenneyPrefabs[layer] : tilePrefab;

            float scaleX = (layerPrefab != null) ? layerPrefab.transform.localScale.x : 2.0f;
            float effectiveRadius = scaleX * (1.0f / Mathf.Sqrt(3)) * 1.04f;
            float xSpacing = Mathf.Sqrt(3) * effectiveRadius;
            float zSpacing = 1.5f * effectiveRadius;

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
                    if (layerPrefab != null)
                    {
                        tileObj = (GameObject)PrefabUtility.InstantiatePrefab(layerPrefab, layerParent.transform);
                        tileObj.transform.position = pos;
                        tileObj.name = $"HexTile_{layer + 1}_{q}_{r}";
                    }
                    else
                    {
                        tileObj = new GameObject($"HexTile_{layer + 1}_{q}_{r}");
                        tileObj.transform.SetParent(layerParent.transform);
                        tileObj.transform.position = pos;
                    }

                    if (!useKenney && layerMat != null)
                    {
                        MeshRenderer mr = tileObj.GetComponent<MeshRenderer>();
                        if (mr != null) mr.sharedMaterial = layerMat;
                    }

                    PlatformTile tile = tileObj.GetComponent<PlatformTile>();
                    if (tile == null) tile = tileObj.AddComponent<PlatformTile>();
                    Color normalCol = useKenney ? Color.white : baseColor;
                    tile.SetColors(normalCol, tileWarning, tileDanger);
                    tile.SetInitialPosition(pos);
                    tile.SetInitialScale(tileObj.transform.localScale);
                }
            }
        }

        SerializedObject soGrid = new SerializedObject(gridGenerator);
        if (useKenney)
        {
            SerializedProperty propLayers = soGrid.FindProperty("layerTilePrefabs");
            propLayers.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                propLayers.GetArrayElementAtIndex(i).objectReferenceValue = kenneyPrefabs[i];
            }
            soGrid.FindProperty("useNaturalModelColors").boolValue = true;
        }
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

        // 5b. Level Title & Alive Count Text
        GameObject levelTextObj = CreateUIElement("LevelTitleText", canvasObj.transform);
        RectTransform levelRect = levelTextObj.GetComponent<RectTransform>();
        levelRect.anchorMin = new Vector2(0.5f, 1f);
        levelRect.anchorMax = new Vector2(0.5f, 1f);
        levelRect.pivot = new Vector2(0.5f, 1f);
        levelRect.anchoredPosition = new Vector2(0, -40f);
        levelRect.sizeDelta = new Vector2(600f, 70f);

        TextMeshProUGUI levelTmp = levelTextObj.AddComponent<TextMeshProUGUI>();
        levelTmp.text = "LEVEL 1";
        levelTmp.fontSize = 46;
        levelTmp.fontStyle = FontStyles.Bold;
        levelTmp.alignment = TextAlignmentOptions.Center;
        levelTmp.color = new Color(1.0f, 0.88f, 0.20f); // Golden Yellow Level Header

        GameObject aliveTextObj = CreateUIElement("AliveCountText", canvasObj.transform);
        RectTransform aliveRect = aliveTextObj.GetComponent<RectTransform>();
        aliveRect.anchorMin = new Vector2(0.5f, 1f);
        aliveRect.anchorMax = new Vector2(0.5f, 1f);
        aliveRect.pivot = new Vector2(0.5f, 1f);
        aliveRect.anchoredPosition = new Vector2(0, -110f);
        aliveRect.sizeDelta = new Vector2(600f, 70f);

        TextMeshProUGUI aliveTmp = aliveTextObj.AddComponent<TextMeshProUGUI>();
        aliveTmp.text = "ALIVE: 5 / 5";
        aliveTmp.fontSize = 44;
        aliveTmp.fontStyle = FontStyles.Bold;
        aliveTmp.alignment = TextAlignmentOptions.Center;
        aliveTmp.color = Color.white;

        // 5b-2. Elimination Toast Text
        GameObject toastTextObj = CreateUIElement("ToastText", canvasObj.transform);
        RectTransform toastRect = toastTextObj.GetComponent<RectTransform>();
        toastRect.anchorMin = new Vector2(0.5f, 1f);
        toastRect.anchorMax = new Vector2(0.5f, 1f);
        toastRect.pivot = new Vector2(0.5f, 1f);
        toastRect.anchoredPosition = new Vector2(0, -180f);
        toastRect.sizeDelta = new Vector2(800f, 80f);

        TextMeshProUGUI toastTmp = toastTextObj.AddComponent<TextMeshProUGUI>();
        toastTmp.text = "";
        toastTmp.fontSize = 42;
        toastTmp.fontStyle = FontStyles.Bold;
        toastTmp.alignment = TextAlignmentOptions.Center;
        toastTmp.color = new Color(1.0f, 0.45f, 0.20f, 1.0f);
        toastTextObj.SetActive(false);

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
        soUI.FindProperty("levelTitleText").objectReferenceValue = levelTmp;
        soUI.FindProperty("aliveCountText").objectReferenceValue = aliveTmp;
        soUI.FindProperty("toastText").objectReferenceValue = toastTmp;
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

        // 6. Setup LevelManager & GameManager
        GameObject levelMgrObj = new GameObject("LevelManager");
        levelMgrObj.AddComponent<LevelManager>();

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
