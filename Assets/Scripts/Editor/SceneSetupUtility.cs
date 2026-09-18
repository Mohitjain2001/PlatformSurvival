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
    [MenuItem("Tools/Setup All Project Scenes")]
    public static void SetupAllScenes()
    {
        string scenesDir = "Assets/Scenes";
        if (!Directory.Exists(scenesDir)) Directory.CreateDirectory(scenesDir);

        SetupSplashScene();
        SetupGameplayScene();

        // Configure EditorBuildSettings
        EditorBuildSettingsScene[] buildScenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/SplashScene.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/GameplayScene.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true)
        };
        EditorBuildSettings.scenes = buildScenes;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("All Project Scenes (SplashScene, GameplayScene) successfully created, arranged, and added to Build Settings!");
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

        // Decorative Player Bean
        GameObject bean = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bean.name = "PreviewBean";
        bean.transform.position = new Vector3(0, 1.2f, 0);
        bean.GetComponent<MeshRenderer>().material.color = new Color(0.12f, 0.65f, 1.0f);

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
        cameraObj.transform.position = new Vector3(0, 15.0f, -11.0f);
        cameraObj.transform.rotation = Quaternion.Euler(52f, 0f, 0f);

        // 3. Setup Grid Generator
        GameObject gridObj = new GameObject("GridGenerator");
        PlatformGridGenerator gridGenerator = gridObj.AddComponent<PlatformGridGenerator>();

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

        // 5a. Joystick UI
        GameObject joystickObj = CreateUIElement("VirtualJoystick", canvasObj.transform);
        RectTransform joyRect = joystickObj.GetComponent<RectTransform>();
        joyRect.anchorMin = new Vector2(0f, 0f);
        joyRect.anchorMax = new Vector2(0f, 0f);
        joyRect.pivot = new Vector2(0f, 0f);
        joyRect.anchoredPosition = new Vector2(80f, 80f);
        joyRect.sizeDelta = new Vector2(320f, 320f);

        Image joyBgImage = joystickObj.AddComponent<Image>();
        joyBgImage.color = new Color(0f, 0f, 0f, 0.35f);

        GameObject handleObj = CreateUIElement("Handle", joystickObj.transform);
        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = new Vector2(130f, 130f);

        Image handleImage = handleObj.AddComponent<Image>();
        handleImage.color = new Color(1f, 1f, 1f, 0.65f);

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

        SerializedObject soGM = new SerializedObject(gm);
        soGM.FindProperty("gridGenerator").objectReferenceValue = gridGenerator;
        soGM.FindProperty("joystick").objectReferenceValue = joystickScript;
        soGM.FindProperty("followCamera").objectReferenceValue = followCamera;
        soGM.FindProperty("uiManager").objectReferenceValue = uiManager;
        soGM.ApplyModifiedProperties();

        // Save both GameplayScene and SampleScene
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/GameplayScene.unity");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/SampleScene.unity");
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
