using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SceneSetupUtility
{
    [MenuItem("Tools/Setup Gameplay Scene")]
    public static void SetupScene()
    {
        string scenePath = "Assets/Scenes/SampleScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // Clear existing scene hierarchy except main camera and light if present
        GameObject[] rootObjects = scene.GetRootGameObjects();
        foreach (GameObject obj in rootObjects)
        {
            Object.DestroyImmediate(obj);
        }

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
        cameraComp.backgroundColor = new Color(0.42f, 0.68f, 0.95f); // Sky Blue
        cameraObj.AddComponent<AudioListener>();
        FollowCamera followCamera = cameraObj.AddComponent<FollowCamera>();
        cameraObj.transform.position = new Vector3(0, 8.5f, -7.0f);
        cameraObj.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

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
        joyRect.sizeDelta = new Vector2(300f, 300f);

        Image joyBgImage = joystickObj.AddComponent<Image>();
        joyBgImage.color = new Color(0f, 0f, 0f, 0.35f);

        GameObject handleObj = CreateUIElement("Handle", joystickObj.transform);
        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = new Vector2(120f, 120f);

        Image handleImage = handleObj.AddComponent<Image>();
        handleImage.color = new Color(1f, 1f, 1f, 0.65f);

        VirtualJoystick joystickScript = joystickObj.AddComponent<VirtualJoystick>();

        // 5b. Alive Count Text
        GameObject aliveTextObj = CreateUIElement("AliveCountText", canvasObj.transform);
        RectTransform aliveRect = aliveTextObj.GetComponent<RectTransform>();
        aliveRect.anchorMin = new Vector2(0.5f, 1f);
        aliveRect.anchorMax = new Vector2(0.5f, 1f);
        aliveRect.pivot = new Vector2(0.5f, 1f);
        aliveRect.anchoredPosition = new Vector2(0, -60f);
        aliveRect.sizeDelta = new Vector2(600f, 100f);

        TextMeshProUGUI aliveTmp = aliveTextObj.AddComponent<TextMeshProUGUI>();
        aliveTmp.text = "ALIVE: 5 / 5";
        aliveTmp.fontSize = 54;
        aliveTmp.fontStyle = FontStyles.Bold;
        aliveTmp.alignment = TextAlignmentOptions.Center;
        aliveTmp.color = Color.white;

        // 5c. Game Over Panel
        GameObject gameOverObj = CreatePanel("GameOverPanel", canvasObj.transform, new Color(0.1f, 0.05f, 0.05f, 0.85f));
        TextMeshProUGUI gameOverTitle = CreateTMPText("Title", gameOverObj.transform, "GAME OVER", 64, new Vector2(0, 150f));
        TextMeshProUGUI gameOverRank = CreateTMPText("RankText", gameOverObj.transform, "ELIMINATED!", 42, new Vector2(0, 30f));
        Button gameOverRetryBtn = CreateButton("RetryButton", gameOverObj.transform, "RETRY", new Vector2(0, -120f));

        // 5d. Victory Panel
        GameObject victoryObj = CreatePanel("VictoryPanel", canvasObj.transform, new Color(0.1f, 0.35f, 0.15f, 0.85f));
        TextMeshProUGUI victoryTitle = CreateTMPText("Title", victoryObj.transform, "VICTORY!\nLAST PLAYER STANDING!", 60, new Vector2(0, 120f));
        Button victoryRetryBtn = CreateButton("RetryButton", victoryObj.transform, "PLAY AGAIN", new Vector2(0, -120f));

        // Set UIManager references via SerializedObject
        SerializedObject soUI = new SerializedObject(uiManager);
        soUI.FindProperty("aliveCountText").objectReferenceValue = aliveTmp;
        soUI.FindProperty("gameOverPanel").objectReferenceValue = gameOverObj;
        soUI.FindProperty("gameOverTitleText").objectReferenceValue = gameOverTitle;
        soUI.FindProperty("rankText").objectReferenceValue = gameOverRank;
        soUI.FindProperty("retryGameOverButton").objectReferenceValue = gameOverRetryBtn;
        soUI.FindProperty("victoryPanel").objectReferenceValue = victoryObj;
        soUI.FindProperty("victoryTitleText").objectReferenceValue = victoryTitle;
        soUI.FindProperty("retryVictoryButton").objectReferenceValue = victoryRetryBtn;
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

        // Save scene
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Gameplay Scene Setup Complete and Saved!");
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
        rt.sizeDelta = new Vector2(800f, 150f);

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        return tmp;
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 anchoredPos)
    {
        GameObject btnObj = CreateUIElement(name, parent);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(350f, 90f);

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.2f, 0.8f, 0.4f);

        Button btn = btnObj.AddComponent<Button>();

        GameObject txtObj = CreateUIElement("Text", btnObj.transform);
        RectTransform txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 36;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return btn;
    }
}
