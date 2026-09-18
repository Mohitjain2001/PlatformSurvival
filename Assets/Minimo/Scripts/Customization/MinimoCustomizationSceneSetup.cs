using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Minimo/Customization Scene Setup")]
public class MinimoCustomizationSceneSetup : MonoBehaviour
{
    private static readonly string[] CharacterPrefabAssetPaths =
    {
        "Assets/Minimo/Scene/Obstacles/Character.prefab"
    };
    private const string CharacterRootName = "Character_Center";
    private const string CharacterFallbackName = "Character";
    private const string CustomizeCharacterName = "CustomizeCharacterPrefab";
    private const string GroundName = "Ground_Preview";
    private const string BackdropName = "Backdrop_Wall";
    private const string FillLightLeftName = "Fill_Light_Left";
    private const string FillLightRightName = "Fill_Light_Right";
    private const string CanvasName = "CustomizationCanvas";

    [Header("Scene Build")]
    [SerializeField] private GameObject characterPrefab;
    [SerializeField] private bool rebuildInEditorOnLoad = true;
    [SerializeField] private bool includeGround = true;
    [SerializeField] private bool forceDefaultOnPlay = true;

    [Header("Runtime References")]
    [SerializeField] private MinimoCharacterCustomizer customizer;
    [SerializeField] private MinimoCustomizationUIController uiController;
    [SerializeField] private Canvas customizationCanvas;
    [SerializeField] private bool sceneInitialized;

    private bool isBuilding;

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            BuildOrRefreshScene();
            return;
        }

        if (rebuildInEditorOnLoad)
        {
            BuildOrRefreshScene();
        }
    }

    private void Start()
    {
        if (Application.isPlaying && !sceneInitialized)
        {
            BuildOrRefreshScene();
        }
    }

    [ContextMenu("Customization Scene/Build Or Refresh")]
    public void BuildOrRefreshScene()
    {
        if (isBuilding)
        {
            return;
        }

        isBuilding = true;
        try
        {
            EnsureCharacterPrefabReference();
            EnsureCamera();
            EnsureLighting();
            if (includeGround)
            {
                EnsureEnvironmentProps();
            }

            EnsureEventSystem();
            EnsureCharacter();
            EnsureUI();
            MinimoUiInputCompatibility.EnsureSceneUiInput();
            sceneInitialized = true;
        }
        finally
        {
            isBuilding = false;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
        }
#endif
    }

    private void EnsureCharacterPrefabReference()
    {
        if (characterPrefab != null)
        {
            return;
        }

#if UNITY_EDITOR
        for (int i = 0; i < CharacterPrefabAssetPaths.Length; i++)
        {
            characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabAssetPaths[i]);
            if (characterPrefab != null)
            {
                break;
            }
        }
#endif
    }

    private static void EnsureCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            camera = MinimoUnityCompatibility.FindFirstObjectByType<Camera>();
        }

        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            camera = cameraObject.GetComponent<Camera>();
        }

        camera.transform.position = new Vector3(0f, 1.55f, -4.35f);
        camera.transform.rotation = Quaternion.Euler(9f, 0f, 0f);
        camera.fieldOfView = 34f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 200f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.07f, 0.12f, 0.18f, 1f);
    }

    private static void EnsureLighting()
    {
        Light directional = FindOrCreateDirectionalLight();
        directional.transform.rotation = Quaternion.Euler(46f, -32f, 0f);
        directional.type = LightType.Directional;
        directional.intensity = 0.95f;
        directional.color = Color.white;

        Light leftFill = FindOrCreateNamedSpot(FillLightLeftName, new Vector3(-2.2f, 1.8f, -1.4f), Color.white, 0.25f);
        leftFill.transform.LookAt(new Vector3(0f, 1f, 0f));

        Light rightFill = FindOrCreateNamedSpot(FillLightRightName, new Vector3(2.3f, 1.7f, -1.2f), Color.white, 0.2f);
        rightFill.transform.LookAt(new Vector3(0f, 1f, 0f));
    }

    private static Light FindOrCreateDirectionalLight()
    {
        Light[] lights = MinimoUnityCompatibility.FindObjectsByType<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            Light candidate = lights[i];
            if (candidate != null && candidate.type == LightType.Directional)
            {
                return candidate;
            }
        }

        GameObject lightObject = new GameObject("Directional Light", typeof(Light));
        Light directional = lightObject.GetComponent<Light>();
        directional.type = LightType.Directional;
        return directional;
    }

    private static Light FindOrCreateNamedSpot(string name, Vector3 position, Color color, float intensity)
    {
        GameObject obj = GameObject.Find(name);
        if (obj == null)
        {
            obj = new GameObject(name, typeof(Light));
        }

        obj.transform.position = position;
        Light light = obj.GetComponent<Light>();
        light.type = LightType.Spot;
        light.range = 8f;
        light.spotAngle = 72f;
        light.intensity = intensity;
        light.color = color;
        light.shadows = LightShadows.None;
        return light;
    }

    private static void EnsureEnvironmentProps()
    {
        GameObject ground = GameObject.Find(GroundName);
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = GroundName;
        }

        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(1.4f, 1f, 1.4f);
        AssignSolidMaterial(ground, "MAT_CustomizationGround", new Color(0.12f, 0.16f, 0.21f, 1f));

        GameObject wall = GameObject.Find(BackdropName);
        if (wall == null)
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = BackdropName;
        }

        wall.transform.position = new Vector3(0f, 2.4f, 2.65f);
        wall.transform.localScale = new Vector3(8f, 4.8f, 0.2f);
        AssignSolidMaterial(wall, "MAT_CustomizationBackdrop", new Color(0.09f, 0.13f, 0.19f, 1f));
    }

    private static void AssignSolidMaterial(GameObject target, string materialName, Color color)
    {
        if (target == null || !target.TryGetComponent(out Renderer renderer))
        {
            return;
        }

        Material material = renderer.sharedMaterial;
        if (material == null || material.shader == null || !material.name.StartsWith(materialName, System.StringComparison.Ordinal))
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader) { name = materialName };
            renderer.sharedMaterial = material;
        }

        material.color = color;
    }

    private static void EnsureEventSystem()
    {
        MinimoUiInputCompatibility.EnsureSceneUiInput();
    }

    private void EnsureCharacter()
    {
        if (customizer == null)
        {
            customizer = MinimoUnityCompatibility.FindFirstObjectByType<MinimoCharacterCustomizer>();
        }

        if (customizer == null)
        {
            Transform characterRoot = ResolveExistingSceneCharacterRoot();
            if (characterRoot != null)
            {
                customizer = characterRoot.GetComponent<MinimoCharacterCustomizer>();
                if (customizer == null)
                {
                    customizer = characterRoot.gameObject.AddComponent<MinimoCharacterCustomizer>();
                }
            }
            else
            {
            }
        }

        if (customizer == null)
        {
            return;
        }

        customizer.transform.position = Vector3.zero;
        customizer.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            customizer.EditorLoadVariantsFromCasualFolder();
            EditorUtility.SetDirty(customizer);
        }
#endif

        if ((forceDefaultOnPlay && Application.isPlaying) || !sceneInitialized)
        {
            customizer.ResetToDefaultCharacter();
        }
        else
        {
            customizer.ApplyCurrentSelection();
        }

        EnsureRotateComponent(customizer.transform);
    }

    private static Transform ResolveExistingSceneCharacterRoot()
    {
        GameObject namedRoot = GameObject.Find(CharacterRootName);
        if (namedRoot != null)
        {
            return namedRoot.transform;
        }

        GameObject characterByName = GameObject.Find(CharacterFallbackName);
        if (characterByName != null)
        {
            return characterByName.transform;
        }

        GameObject customizeCharacter = GameObject.Find(CustomizeCharacterName);
        if (customizeCharacter != null)
        {
            return customizeCharacter.transform;
        }

        MinimoTpsController tpsController = MinimoUnityCompatibility.FindFirstObjectByType<MinimoTpsController>();
        if (tpsController != null)
        {
            return tpsController.transform;
        }

        SkinnedMeshRenderer skinnedRenderer =
            MinimoUnityCompatibility.FindFirstObjectByType<SkinnedMeshRenderer>(true);
        if (skinnedRenderer != null)
        {
            return skinnedRenderer.transform.root;
        }

        Animator animator = MinimoUnityCompatibility.FindFirstObjectByType<Animator>(true);
        if (animator != null)
        {
            return animator.transform.root;
        }

        return null;
    }

    private static void EnsureRotateComponent(Transform characterRoot)
    {
        if (characterRoot == null)
        {
            return;
        }

        MinimoCharacterDragRotate rotate = characterRoot.GetComponent<MinimoCharacterDragRotate>();
        if (rotate == null)
        {
            rotate = characterRoot.gameObject.AddComponent<MinimoCharacterDragRotate>();
        }

        if (!characterRoot.TryGetComponent(out BoxCollider box))
        {
            box = characterRoot.gameObject.AddComponent<BoxCollider>();
        }

        Bounds bounds = default;
        bool hasBounds = false;
        Renderer[] renderers = characterRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
        {
            Vector3 localCenter = characterRoot.InverseTransformPoint(bounds.center);
            Vector3 localSize = characterRoot.InverseTransformVector(bounds.size);
            box.center = localCenter;
            box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z)) * 1.05f;
        }
    }

    private void EnsureUI()
    {
        if (customizationCanvas == null)
        {
            customizationCanvas = ResolveExistingCustomizationCanvas();
        }

        bool useSceneAuthoredUi = IsSceneAuthoredCustomizationCanvas(customizationCanvas);
        if (customizationCanvas != null && !sceneInitialized && !useSceneAuthoredUi)
        {
            DestroyGameObjectSafely(customizationCanvas.gameObject);
            customizationCanvas = null;
        }

        if (useSceneAuthoredUi)
        {
            if (uiController == null)
            {
                uiController = customizationCanvas.GetComponentInChildren<MinimoCustomizationUIController>(true);
            }

            if (uiController == null)
            {
                uiController = MinimoUnityCompatibility.FindFirstObjectByType<MinimoCustomizationUIController>();
            }

            if (uiController != null)
            {
                uiController.RefreshFromCustomizer();
            }

            return;
        }

        Button btnSetPrev;
        Button btnSetNext;
        Button btnResetDefault;
        Button btnRandomize;
        Text txtSetName;
        Text txtHelp;
        Text txtTitle;
        RectTransform regionList;

        if (customizationCanvas == null)
        {
            Font font = ResolveFont();
            customizationCanvas = CreateCanvas(font, out btnSetPrev, out btnSetNext, out btnResetDefault, out btnRandomize, out txtSetName, out txtHelp, out txtTitle, out regionList);
        }
        else
        {
            btnSetPrev = FindChildByName<Button>(customizationCanvas.transform, "BtnSetPrev");
            btnSetNext = FindChildByName<Button>(customizationCanvas.transform, "BtnSetNext");
            btnResetDefault = FindChildByName<Button>(customizationCanvas.transform, "BtnResetDefault");
            btnRandomize = FindChildByName<Button>(customizationCanvas.transform, "BtnRandomize");
            txtSetName = FindChildByName<Text>(customizationCanvas.transform, "TxtSetName");
            txtHelp = FindChildByName<Text>(customizationCanvas.transform, "TxtHelp");
            txtTitle = FindChildByName<Text>(customizationCanvas.transform, "TxtTitle");
            regionList = FindChildByName<RectTransform>(customizationCanvas.transform, "RegionList");

            bool invalid =
                btnSetPrev == null || btnSetNext == null ||
                btnResetDefault == null || btnRandomize == null ||
                txtSetName == null || txtHelp == null ||
                txtTitle == null || regionList == null;

            if (invalid)
            {
                DestroyGameObjectSafely(customizationCanvas.gameObject);
                customizationCanvas = null;
                Font font = ResolveFont();
                customizationCanvas = CreateCanvas(font, out btnSetPrev, out btnSetNext, out btnResetDefault, out btnRandomize, out txtSetName, out txtHelp, out txtTitle, out regionList);
            }
        }

        if (uiController == null)
        {
            uiController = MinimoUnityCompatibility.FindFirstObjectByType<MinimoCustomizationUIController>();
        }

        if (uiController == null)
        {
            GameObject controllerObject = new GameObject("CustomizationUIController");
            controllerObject.transform.SetParent(customizationCanvas.transform, false);
            uiController = controllerObject.AddComponent<MinimoCustomizationUIController>();
        }

        uiController.Configure(customizer, regionList, btnSetPrev, btnSetNext, btnResetDefault, btnRandomize, txtSetName, txtHelp, txtTitle);
        uiController.RefreshFromCustomizer();
    }

    private static Canvas ResolveExistingCustomizationCanvas()
    {
        Canvas[] canvases = MinimoUnityCompatibility.FindObjectsByType<Canvas>(true);
        Canvas fallback = null;
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
            {
                continue;
            }

            if (canvas.GetComponentInChildren<MinimoCustomizationUIController>(true) != null)
            {
                fallback ??= canvas;
            }

            if (string.Equals(canvas.name, "General UI Canvas", System.StringComparison.OrdinalIgnoreCase))
            {
                return canvas;
            }

            if (string.Equals(canvas.name, CanvasName, System.StringComparison.OrdinalIgnoreCase))
            {
                fallback ??= canvas;
            }
        }

        return fallback;
    }

    private static bool IsSceneAuthoredCustomizationCanvas(Canvas canvas)
    {
        if (canvas == null)
        {
            return false;
        }

        if (string.Equals(canvas.name, "General UI Canvas", System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return canvas.GetComponentInChildren<MinimoCustomizationUIController>(true) != null
               && (FindChildByName<RectTransform>(canvas.transform, "BodyParts") != null
                   || FindChildByName<RectTransform>(canvas.transform, "PartsPanel") != null
                   || FindChildByName<RectTransform>(canvas.transform, "SelectedPartsGroupPopup") != null);
    }

    private static void DestroyGameObjectSafely(GameObject target)
    {
        if (target == null)
        {
            return;
        }

#if UNITY_EDITOR
        ClearEditorSelectionIfContains(target.transform);
#endif

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

#if UNITY_EDITOR
    private static void ClearEditorSelectionIfContains(Transform target)
    {
        if (target == null)
        {
            return;
        }

        Object[] selectedObjects = Selection.objects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            return;
        }

        for (int i = 0; i < selectedObjects.Length; i++)
        {
            Object selectedObject = selectedObjects[i];
            Transform selectedTransform = selectedObject switch
            {
                GameObject selectedGameObject => selectedGameObject.transform,
                Component selectedComponent => selectedComponent.transform,
                _ => null
            };

            if (selectedTransform == null)
            {
                continue;
            }

            if (selectedTransform == target || selectedTransform.IsChildOf(target))
            {
                Selection.activeObject = null;
                Selection.objects = System.Array.Empty<Object>();
                return;
            }
        }
    }
#endif

    private static Canvas CreateCanvas(
        Font font,
        out Button btnSetPrev,
        out Button btnSetNext,
        out Button btnResetDefault,
        out Button btnRandomize,
        out Text txtSetName,
        out Text txtHelp,
        out Text txtTitle,
        out RectTransform regionList)
    {
        GameObject canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform panel = CreateRect("HUDPanel", canvas.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(640f, 840f), new Vector2(-24f, 0f));
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.02f, 0.05f, 0.1f, 0.94f);

        VerticalLayoutGroup panelLayout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(16, 16, 16, 14);
        panelLayout.spacing = 10f;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = false;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        txtTitle = CreateText(panel, "TxtTitle", "Minimo Character Customization", font, 30, TextAnchor.MiddleCenter, new Color(0.95f, 0.98f, 1f, 1f));
        AddLayout(txtTitle.rectTransform, 52f);

        RectTransform setRow = CreateRect("RowSet", panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        AddLayout(setRow, 56f);
        HorizontalLayoutGroup setRowLayout = setRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        setRowLayout.spacing = 8f;
        setRowLayout.childControlWidth = false;
        setRowLayout.childControlHeight = true;
        setRowLayout.childForceExpandWidth = false;
        setRowLayout.childForceExpandHeight = true;

        btnSetPrev = CreateButton(setRow, "BtnSetPrev", "<", font, 56f);
        txtSetName = CreateText(setRow, "TxtSetName", "Set: Default", font, 22, TextAnchor.MiddleCenter, new Color(0.95f, 0.98f, 1f, 1f));
        LayoutElement setLabelLayout = txtSetName.gameObject.AddComponent<LayoutElement>();
        setLabelLayout.preferredWidth = 330f;
        setLabelLayout.flexibleWidth = 1f;
        btnSetNext = CreateButton(setRow, "BtnSetNext", ">", font, 56f);

        RectTransform actionRow = CreateRect("RowActions", panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        AddLayout(actionRow, 54f);
        HorizontalLayoutGroup actionLayout = actionRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        actionLayout.spacing = 10f;
        actionLayout.childControlWidth = false;
        actionLayout.childControlHeight = true;
        actionLayout.childForceExpandWidth = true;
        actionLayout.childForceExpandHeight = true;

        btnResetDefault = CreateButton(actionRow, "BtnResetDefault", "Reset Default", font, 0f);
        btnRandomize = CreateButton(actionRow, "BtnRandomize", "Randomize", font, 0f);

        RectTransform regionPanel = CreateRect("RegionPanel", panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        AddLayout(regionPanel, 630f, true);
        Image regionPanelBg = regionPanel.gameObject.AddComponent<Image>();
        regionPanelBg.color = new Color(0.06f, 0.1f, 0.17f, 0.88f);

        RectTransform regionTitle = CreateRect("TxtRegions", regionPanel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 34f), new Vector2(0f, -2f));
        Text regionTitleText = regionTitle.gameObject.AddComponent<Text>();
        regionTitleText.font = font;
        regionTitleText.fontSize = 20;
        regionTitleText.alignment = TextAnchor.MiddleCenter;
        regionTitleText.color = new Color(0.86f, 0.93f, 1f, 1f);
        regionTitleText.text = "Region Controls";
        regionTitleText.raycastTarget = false;
        AddOutline(regionTitleText);

        RectTransform scrollRoot = CreateRect("RegionScroll", regionPanel, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0f), new Vector2(-14f, -50f), new Vector2(0f, 6f));
        ScrollRect scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.135f;
        scrollRect.scrollSensitivity = 24f;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        RectTransform viewport = CreateRect("Viewport", scrollRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
        RectMask2D mask = viewport.gameObject.AddComponent<RectMask2D>();
        _ = mask;

        regionList = CreateRect("RegionList", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-10f, 0f), new Vector2(0f, 0f));
        VerticalLayoutGroup regionLayout = regionList.gameObject.AddComponent<VerticalLayoutGroup>();
        regionLayout.padding = new RectOffset(4, 4, 4, 4);
        regionLayout.spacing = 6f;
        regionLayout.childControlWidth = true;
        regionLayout.childControlHeight = true;
        regionLayout.childForceExpandWidth = true;
        regionLayout.childForceExpandHeight = false;
        ContentSizeFitter fitter = regionList.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect.viewport = viewport;
        scrollRect.content = regionList;

        txtHelp = CreateText(panel, "TxtHelp", "Tip: Drag the character to rotate.", font, 18, TextAnchor.MiddleCenter, new Color(0.87f, 0.93f, 1f, 1f));
        AddLayout(txtHelp.rectTransform, 30f);

        return canvas;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 anchoredPosition)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        return rect;
    }

    private static Button CreateButton(Transform parent, string name, string label, Font font, float width)
    {
        RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.18f, 0.26f, 0.38f, 0.98f);

        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.18f, 0.26f, 0.38f, 1f);
        colors.highlightedColor = new Color(0.31f, 0.42f, 0.57f, 1f);
        colors.pressedColor = new Color(0.12f, 0.18f, 0.28f, 1f);
        colors.disabledColor = new Color(0.08f, 0.1f, 0.14f, 0.6f);
        button.colors = colors;

        LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
        if (width > 0f)
        {
            layout.preferredWidth = width;
            layout.minWidth = width;
        }
        else
        {
            layout.flexibleWidth = 1f;
        }

        Text text = CreateText(rect, "Label", label, font, 20, TextAnchor.MiddleCenter, new Color(0.95f, 0.98f, 1f, 1f));
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private static Text CreateText(Transform parent, string name, string value, Font font, int size, TextAnchor anchor, Color color)
    {
        RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Text text = rect.gameObject.AddComponent<Text>();
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = color;
        text.raycastTarget = false;
        AddOutline(text);
        return text;
    }

    private static void AddOutline(Text text)
    {
        Outline outline = text.gameObject.GetComponent<Outline>();
        if (outline == null)
        {
            outline = text.gameObject.AddComponent<Outline>();
        }

        outline.effectColor = new Color(0f, 0f, 0f, 0.78f);
        outline.effectDistance = new Vector2(1f, -1f);
    }

    private static void AddLayout(RectTransform rect, float preferredHeight, bool flexibleHeight = false)
    {
        LayoutElement layout = rect.gameObject.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = rect.gameObject.AddComponent<LayoutElement>();
        }

        layout.preferredHeight = preferredHeight;
        layout.minHeight = preferredHeight;
        layout.flexibleHeight = flexibleHeight ? 1f : 0f;
    }

    private static T FindChildByName<T>(Transform root, string name) where T : Component
    {
        if (root == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        T[] components = root.GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component != null && string.Equals(component.name, name, System.StringComparison.Ordinal))
            {
                return component;
            }
        }

        return null;
    }

    private static Font ResolveFont()
    {
        Font font = TryGetBuiltinFont("LegacyRuntime.ttf");
        if (font != null)
        {
            return font;
        }

        font = TryGetBuiltinFont("Arial.ttf");
        if (font != null)
        {
            return font;
        }

        throw new UnityException("Could not resolve built-in UI font.");
    }

    private static Font TryGetBuiltinFont(string resourceName)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            return null;
        }

        try
        {
            return Resources.GetBuiltinResource<Font>(resourceName);
        }
        catch (System.ArgumentException)
        {
            return null;
        }
    }
}

internal static class MinimoUiInputCompatibility
{
    private const string InputSystemUiInputModuleFullName =
        "UnityEngine.InputSystem.UI.InputSystemUIInputModule";
    private const string InputSystemUiInputModuleAssemblyName =
        "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem";

    public static EventSystem EnsureSceneUiInput()
    {
        EventSystem eventSystem = ResolveEventSystem();
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        if (!eventSystem.gameObject.activeSelf)
        {
            eventSystem.gameObject.SetActive(true);
        }

        eventSystem.enabled = true;
        EventSystem.current = eventSystem;
        EnsureInputModules(eventSystem);
        EnsureCanvasRaycasters();
        return eventSystem;
    }

    private static EventSystem ResolveEventSystem()
    {
        EventSystem current = EventSystem.current;
        if (current != null && current.gameObject.activeInHierarchy)
        {
            return current;
        }

        EventSystem[] eventSystems = MinimoUnityCompatibility.FindObjectsByType<EventSystem>(true);
        EventSystem fallback = null;
        for (int i = 0; i < eventSystems.Length; i++)
        {
            EventSystem candidate = eventSystems[i];
            if (candidate == null)
            {
                continue;
            }

            fallback ??= candidate;
            if (candidate.isActiveAndEnabled)
            {
                return candidate;
            }
        }

        return fallback;
    }

    private static void EnsureInputModules(EventSystem eventSystem)
    {
        if (eventSystem == null)
        {
            return;
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        StandaloneInputModule standaloneModule =
            eventSystem.GetComponent<StandaloneInputModule>()
            ?? eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        standaloneModule.enabled = true;
        SetInputSystemUiInputModuleEnabled(eventSystem, false);
#elif ENABLE_INPUT_SYSTEM
        BaseInputModule inputSystemModule = EnsureInputSystemUiInputModule(eventSystem);
        if (inputSystemModule != null)
        {
            inputSystemModule.enabled = true;
            ConfigureInputSystemUiInputModule(inputSystemModule);
        }
#else
        BaseInputModule module = eventSystem.GetComponent<BaseInputModule>();
        if (module != null)
        {
            module.enabled = true;
        }
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static BaseInputModule EnsureInputSystemUiInputModule(EventSystem eventSystem)
    {
        BaseInputModule existing = FindInputSystemUiInputModule(eventSystem);
        if (existing != null)
        {
            return existing;
        }

        System.Type moduleType = ResolveInputSystemUiInputModuleType();
        return moduleType != null
            ? eventSystem.gameObject.AddComponent(moduleType) as BaseInputModule
            : null;
    }

    private static BaseInputModule FindInputSystemUiInputModule(EventSystem eventSystem)
    {
        BaseInputModule[] modules = eventSystem.GetComponents<BaseInputModule>();
        for (int i = 0; i < modules.Length; i++)
        {
            BaseInputModule module = modules[i];
            if (module != null
                && string.Equals(
                    module.GetType().FullName,
                    InputSystemUiInputModuleFullName,
                    System.StringComparison.Ordinal))
            {
                return module;
            }
        }

        return null;
    }

    private static System.Type ResolveInputSystemUiInputModuleType()
    {
        return System.Type.GetType(InputSystemUiInputModuleAssemblyName);
    }

    private static void ConfigureInputSystemUiInputModule(BaseInputModule module)
    {
        if (module == null)
        {
            return;
        }

        System.Type type = module.GetType();
        System.Reflection.PropertyInfo actionsAssetProperty = type.GetProperty(
            "actionsAsset",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        object actionsAsset = actionsAssetProperty != null
            ? actionsAssetProperty.GetValue(module, null)
            : null;
        if (actionsAsset != null)
        {
            return;
        }

        System.Reflection.MethodInfo assignDefaultActions = type.GetMethod(
            "AssignDefaultActions",
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic);
        assignDefaultActions?.Invoke(module, null);
    }
#endif

    private static void SetInputSystemUiInputModuleEnabled(EventSystem eventSystem, bool enabled)
    {
        if (eventSystem == null)
        {
            return;
        }

        BaseInputModule[] modules = eventSystem.GetComponents<BaseInputModule>();
        for (int i = 0; i < modules.Length; i++)
        {
            BaseInputModule module = modules[i];
            if (module == null
                || !string.Equals(
                    module.GetType().FullName,
                    InputSystemUiInputModuleFullName,
                    System.StringComparison.Ordinal))
            {
                continue;
            }

            module.enabled = enabled;
        }
    }

    private static void EnsureCanvasRaycasters()
    {
        Canvas[] canvases = MinimoUnityCompatibility.FindObjectsByType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
            {
                continue;
            }

            GraphicRaycaster raycaster =
                canvas.GetComponent<GraphicRaycaster>()
                ?? canvas.gameObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = true;

            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera == null)
            {
                canvas.worldCamera = Camera.main ?? MinimoUnityCompatibility.FindFirstObjectByType<Camera>();
            }
        }
    }
}
