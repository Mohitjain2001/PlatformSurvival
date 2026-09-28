#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bakes the Character Shop UI directly into SplashScene.unity matching reference concept media_1790511363312.png:
/// - Top Header: "CHARACTER SHOP" glossy title banner with Crown & Red (X) Close button.
/// - Upper Showroom: Left [<] and Right [>] Navigation Arrow buttons, Name Pill & Green "(✓) EQUIPPED" Badge.
/// - Bottom Panel: 4x2 Grid of character cards with Lock icons (🔒), Level unlock tags, Yellow selection borders, and Green checkmarks (✓).
/// - Bottom Action Bar: Yellow "WATCH AD" button + Dark "UNLOCK AT LEVEL X" button.
/// </summary>
public static class ShopHierarchyBaker
{
// [MenuItem("Tools/Bake Shop UI Into Hierarchy")]
    public static void EnsureShopHierarchyInScene()
    {
        // Load SplashScene if needed
        UnityEngine.SceneManagement.Scene currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!currentScene.name.Contains("Splash"))
        {
            string splashPath = "Assets/Scenes/SplashScene.unity";
            if (File.Exists(splashPath) && !Application.isPlaying)
            {
                currentScene = EditorSceneManager.OpenScene(splashPath, OpenSceneMode.Single);
            }
        }

        // Find/Create Canvas
        Canvas mainCanvas = Object.FindFirstObjectByType<Canvas>();
        if (mainCanvas == null)
        {
            GameObject canvasObj = new GameObject("UI Canvas");
            mainCanvas = canvasObj.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler cs = canvasObj.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1080, 1920);
            cs.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Clear existing ShopWindowRoot
        Transform oldShop = mainCanvas.transform.Find("ShopWindowRoot");
        if (oldShop != null) Object.DestroyImmediate(oldShop.gameObject);

        // Build Hierarchy
        GameObject shopRoot = CreateShopHierarchy(mainCanvas.gameObject);

        // Save Scene
        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorSceneManager.SaveScene(currentScene);

        Debug.Log("[ShopHierarchyBaker] Baked Character Shop UI into Hierarchy matching media_1790511363312.png!");
    }

    public static GameObject CreateShopHierarchy(GameObject canvasParent)
    {
        Sprite sBanner = ShopUIAssets.GetCharacterShopBanner();
        Sprite sArrowLeft = ShopUIAssets.GetNavArrowLeft();
        Sprite sArrowRight = ShopUIAssets.GetNavArrowRight();
        Sprite sRedClose = ShopUIAssets.GetRedCloseBtn();
        Sprite sNamePill = ShopUIAssets.GetNamePill();
        Sprite sEquippedBadge = ShopUIAssets.GetEquippedBadge();
        Sprite sPanelBg = ShopUIAssets.GetMainPanelBg();
        Sprite sSelectedCard = ShopUIAssets.GetSelectedCardBg();
        Sprite sLockedCard = ShopUIAssets.GetLockedCardBg();
        Sprite sLockIcon = ShopUIAssets.GetLockIcon();
        Sprite sCheckmark = ShopUIAssets.GetCheckmarkBadge();
        Sprite sWatchAdBtn = ShopUIAssets.GetWatchAdBtn();
        Sprite sLevelUnlockBtn = ShopUIAssets.GetLevelUnlockBtn();
        Sprite sGoldCoin = ShopUIAssets.GetGoldCoin();

        // Main Root
        GameObject shopRoot = new GameObject("ShopWindowRoot");
        shopRoot.transform.SetParent(canvasParent.transform, false);
        RectTransform rootRt = shopRoot.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        // Controller Component
        CharacterSelectUI controller = shopRoot.AddComponent<CharacterSelectUI>();

        // ─────────────────────────────────────────────────────────────
        // 1. TOP HEADER ("CHARACTER SHOP" BANNER & CLOSE BUTTON)
        // ─────────────────────────────────────────────────────────────
        GameObject bannerObj = new GameObject("CharacterShopBanner");
        bannerObj.transform.SetParent(shopRoot.transform, false);
        RectTransform bannerRt = bannerObj.AddComponent<RectTransform>();
        bannerRt.anchorMin = new Vector2(0.5f, 1f);
        bannerRt.anchorMax = new Vector2(0.5f, 1f);
        bannerRt.anchoredPosition = new Vector2(0, -65);
        bannerRt.sizeDelta = new Vector2(480, 96);

        Image bannerImg = bannerObj.AddComponent<Image>();
        bannerImg.sprite = sBanner;
        bannerImg.type = Image.Type.Sliced;

        GameObject bannerTextObj = new GameObject("BannerTitleText");
        bannerTextObj.transform.SetParent(bannerObj.transform, false);
        RectTransform titleRt = bannerTextObj.AddComponent<RectTransform>();
        titleRt.anchorMin = Vector2.zero;
        titleRt.anchorMax = Vector2.one;
        TextMeshProUGUI titleTxt = bannerTextObj.AddComponent<TextMeshProUGUI>();
        titleTxt.text = "CHARACTER SHOP";
        titleTxt.fontSize = 42;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = new Color(1.00f, 0.85f, 0.10f);

        // Close Button (Red X)
        GameObject closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(shopRoot.transform, false);
        RectTransform closeRt = closeBtnObj.AddComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1f, 1f);
        closeRt.anchorMax = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-55, -65);
        closeRt.sizeDelta = new Vector2(88, 88);
        Image closeImg = closeBtnObj.AddComponent<Image>();
        closeImg.sprite = sRedClose;
        closeBtnObj.AddComponent<Button>();

        // ─────────────────────────────────────────────────────────────
        // 2. SHOWROOM NAVIGATION CONTROLS (TOP 3D PREVIEW AREA)
        // ─────────────────────────────────────────────────────────────
        // Left Arrow [<]
        GameObject leftArrowObj = new GameObject("LeftArrowButton");
        leftArrowObj.transform.SetParent(shopRoot.transform, false);
        RectTransform leftRt = leftArrowObj.AddComponent<RectTransform>();
        leftRt.anchorMin = new Vector2(0.5f, 0.72f);
        leftRt.anchorMax = new Vector2(0.5f, 0.72f);
        leftRt.anchoredPosition = new Vector2(-380, 0);
        leftRt.sizeDelta = new Vector2(88, 88);
        Image leftImg = leftArrowObj.AddComponent<Image>();
        leftImg.sprite = sArrowLeft;
        leftArrowObj.AddComponent<Button>();

        // Right Arrow [>]
        GameObject rightArrowObj = new GameObject("RightArrowButton");
        rightArrowObj.transform.SetParent(shopRoot.transform, false);
        RectTransform rightRt = rightArrowObj.AddComponent<RectTransform>();
        rightRt.anchorMin = new Vector2(0.5f, 0.72f);
        rightRt.anchorMax = new Vector2(0.5f, 0.72f);
        rightRt.anchoredPosition = new Vector2(380, 0);
        rightRt.sizeDelta = new Vector2(88, 88);
        Image rightImg = rightArrowObj.AddComponent<Image>();
        rightImg.sprite = sArrowRight;
        rightArrowObj.AddComponent<Button>();

        // Character Name Pill
        GameObject namePillObj = new GameObject("ShowroomNamePill");
        namePillObj.transform.SetParent(shopRoot.transform, false);
        RectTransform namePillRt = namePillObj.AddComponent<RectTransform>();
        namePillRt.anchorMin = new Vector2(0.5f, 0.56f);
        namePillRt.anchorMax = new Vector2(0.5f, 0.56f);
        namePillRt.anchoredPosition = new Vector2(0, 30);
        namePillRt.sizeDelta = new Vector2(260, 54);
        Image namePillImg = namePillObj.AddComponent<Image>();
        namePillImg.sprite = sNamePill;
        namePillImg.type = Image.Type.Sliced;

        GameObject nameTxtObj = new GameObject("NameText");
        nameTxtObj.transform.SetParent(namePillObj.transform, false);
        RectTransform nameTxtRt = nameTxtObj.AddComponent<RectTransform>();
        nameTxtRt.anchorMin = Vector2.zero;
        nameTxtRt.anchorMax = Vector2.one;
        TextMeshProUGUI sNameTxt = nameTxtObj.AddComponent<TextMeshProUGUI>();
        sNameTxt.text = "DEFAULT";
        sNameTxt.fontSize = 28;
        sNameTxt.fontStyle = FontStyles.Bold;
        sNameTxt.alignment = TextAlignmentOptions.Center;
        sNameTxt.color = Color.white;

        // Equipped Badge / Select Button
        GameObject equippedObj = new GameObject("ShowroomEquippedBadge");
        equippedObj.transform.SetParent(shopRoot.transform, false);
        RectTransform eqRt = equippedObj.AddComponent<RectTransform>();
        eqRt.anchorMin = new Vector2(0.5f, 0.56f);
        eqRt.anchorMax = new Vector2(0.5f, 0.56f);
        eqRt.anchoredPosition = new Vector2(0, -30);
        eqRt.sizeDelta = new Vector2(240, 48);
        Image eqImg = equippedObj.AddComponent<Image>();
        eqImg.sprite = sEquippedBadge;
        eqImg.type = Image.Type.Sliced;

        GameObject eqTxtObj = new GameObject("EquippedText");
        eqTxtObj.transform.SetParent(equippedObj.transform, false);
        RectTransform eqTxtRt = eqTxtObj.AddComponent<RectTransform>();
        eqTxtRt.anchorMin = Vector2.zero;
        eqTxtRt.anchorMax = Vector2.one;
        TextMeshProUGUI eqTxt = eqTxtObj.AddComponent<TextMeshProUGUI>();
        eqTxt.text = "✓ EQUIPPED";
        eqTxt.fontSize = 24;
        eqTxt.fontStyle = FontStyles.Bold;
        eqTxt.alignment = TextAlignmentOptions.Center;
        eqTxt.color = Color.white;
        equippedObj.AddComponent<Button>();

        // ─────────────────────────────────────────────────────────────
        // 3. BOTTOM SHOP GRID CONTAINER (LOWER 52% OF SCREEN)
        // ─────────────────────────────────────────────────────────────
        GameObject drawer = new GameObject("BottomDrawerPanel");
        drawer.transform.SetParent(shopRoot.transform, false);
        RectTransform drawerRt = drawer.AddComponent<RectTransform>();
        drawerRt.anchorMin = Vector2.zero;
        drawerRt.anchorMax = new Vector2(1f, 0.52f);
        drawerRt.offsetMin = Vector2.zero;
        drawerRt.offsetMax = Vector2.zero;

        GameObject panelObj = new GameObject("SkyBluePanel");
        panelObj.transform.SetParent(drawer.transform, false);
        RectTransform panelRt = panelObj.AddComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = new Vector2(15, 120);
        panelRt.offsetMax = new Vector2(-15, -15);
        Image panelImg = panelObj.AddComponent<Image>();
        panelImg.sprite = sPanelBg;
        panelImg.type = Image.Type.Sliced;

        // Grid (4 columns x 2 rows)
        GameObject gridObj = new GameObject("CardsGrid");
        gridObj.transform.SetParent(panelObj.transform, false);
        RectTransform gridRt = gridObj.AddComponent<RectTransform>();
        gridRt.anchorMin = Vector2.zero;
        gridRt.anchorMax = Vector2.one;
        gridRt.offsetMin = new Vector2(20, 20);
        gridRt.offsetMax = new Vector2(-20, -20);

        GridLayoutGroup glg = gridObj.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(215, 290);
        glg.spacing = new Vector2(20, 20);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 4;
        glg.childAlignment = TextAnchor.MiddleCenter;

        string[] names = { "DEFAULT", "SOLDIER", "GIRL", "WRESTLER", "STYLISH", "NINJA", "ASTRONAUT", "ALIEN" };
        string[] unlocks = { "EQUIPPED", "UNLOCK AT LEVEL 5", "UNLOCK AT LEVEL 10", "UNLOCK AT LEVEL 15", "UNLOCK AT LEVEL 20", "UNLOCK AT LEVEL 25", "UNLOCK AT LEVEL 30", "UNLOCK AT LEVEL 35" };

        for (int i = 0; i < 8; i++)
        {
            CreateConceptCardSlot(gridObj, i, (i == 0), sSelectedCard, sLockedCard, sLockIcon, sCheckmark, names[i], unlocks[i]);
        }

        // ─────────────────────────────────────────────────────────────
        // 4. BOTTOM ACTION BAR (WATCH AD & UNLOCK AT LEVEL BUTTONS)
        // ─────────────────────────────────────────────────────────────
        GameObject actionBar = new GameObject("ActionBar");
        actionBar.transform.SetParent(drawer.transform, false);
        RectTransform actionRt = actionBar.AddComponent<RectTransform>();
        actionRt.anchorMin = Vector2.zero;
        actionRt.anchorMax = new Vector2(1f, 0f);
        actionRt.pivot = new Vector2(0.5f, 0f);
        actionRt.anchoredPosition = new Vector2(0, 15);
        actionRt.sizeDelta = new Vector2(0, 100);

        HorizontalLayoutGroup actionHlg = actionBar.AddComponent<HorizontalLayoutGroup>();
        actionHlg.spacing = 30;
        actionHlg.childAlignment = TextAnchor.MiddleCenter;
        actionHlg.childControlWidth = false;
        actionHlg.childControlHeight = false;

        // Left Action: Yellow Watch Ad Button
        GameObject watchAdBtn = new GameObject("WatchAdButton");
        watchAdBtn.transform.SetParent(actionBar.transform, false);
        watchAdBtn.AddComponent<RectTransform>().sizeDelta = new Vector2(460, 96);
        Image adImg = watchAdBtn.AddComponent<Image>();
        adImg.sprite = sWatchAdBtn;
        adImg.type = Image.Type.Sliced;
        watchAdBtn.AddComponent<Button>();

        GameObject adTxtObj = new GameObject("Text");
        adTxtObj.transform.SetParent(watchAdBtn.transform, false);
        RectTransform adTxtRt = adTxtObj.AddComponent<RectTransform>();
        adTxtRt.anchorMin = Vector2.zero;
        adTxtRt.anchorMax = Vector2.one;
        TextMeshProUGUI adTxt = adTxtObj.AddComponent<TextMeshProUGUI>();
        adTxt.text = "WATCH AD\n<size=18>TRY THIS CHARACTER FOR 1 MATCH</size>";
        adTxt.fontSize = 24;
        adTxt.fontStyle = FontStyles.Bold;
        adTxt.alignment = TextAlignmentOptions.Center;
        adTxt.color = new Color(0.15f, 0.15f, 0.20f);

        // Right Action: Dark Unlock Level Button
        GameObject unlockLevelBtn = new GameObject("LevelUnlockButton");
        unlockLevelBtn.transform.SetParent(actionBar.transform, false);
        unlockLevelBtn.AddComponent<RectTransform>().sizeDelta = new Vector2(460, 96);
        Image unImg = unlockLevelBtn.AddComponent<Image>();
        unImg.sprite = sLevelUnlockBtn;
        unImg.type = Image.Type.Sliced;
        unlockLevelBtn.AddComponent<Button>();

        GameObject unTxtObj = new GameObject("Text");
        unTxtObj.transform.SetParent(unlockLevelBtn.transform, false);
        RectTransform unTxtRt = unTxtObj.AddComponent<RectTransform>();
        unTxtRt.anchorMin = Vector2.zero;
        unTxtRt.anchorMax = Vector2.one;
        TextMeshProUGUI unTxt = unTxtObj.AddComponent<TextMeshProUGUI>();
        unTxt.text = "🔒 UNLOCK AT\n<size=26>LEVEL 5</size>";
        unTxt.fontSize = 20;
        unTxt.fontStyle = FontStyles.Bold;
        unTxt.alignment = TextAlignmentOptions.Center;
        unTxt.color = new Color(1.00f, 0.85f, 0.10f);

        return shopRoot;
    }

    private static GameObject CreateConceptCardSlot(GameObject parent, int index, bool isSelected, Sprite selBg, Sprite lockBg, Sprite lockIcon, Sprite checkmark, string name, string unlockTag)
    {
        GameObject slot = new GameObject("CardSlot_" + index);
        slot.transform.SetParent(parent.transform, false);

        Image bg = slot.AddComponent<Image>();
        bg.sprite = isSelected ? selBg : lockBg;
        bg.type = Image.Type.Sliced;

        // 3D Portrait / Icon
        GameObject iconObj = new GameObject("ItemIcon");
        iconObj.transform.SetParent(slot.transform, false);
        RectTransform iconRt = iconObj.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.22f);
        iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = new Vector2(10, 10);
        iconRt.offsetMax = new Vector2(-10, -10);
        Image img = iconObj.AddComponent<Image>();
        img.preserveAspect = true;

        // Checkmark Badge (Top-Right)
        GameObject checkObj = new GameObject("CheckmarkBadge");
        checkObj.transform.SetParent(slot.transform, false);
        RectTransform checkRt = checkObj.AddComponent<RectTransform>();
        checkRt.anchorMin = Vector2.one;
        checkRt.anchorMax = Vector2.one;
        checkRt.anchoredPosition = new Vector2(-8, -8);
        checkRt.sizeDelta = new Vector2(52, 52);
        Image checkImg = checkObj.AddComponent<Image>();
        checkImg.sprite = checkmark;
        checkObj.SetActive(isSelected);

        // Lock Badge (Top-Right when locked)
        GameObject lockObj = new GameObject("LockBadge");
        lockObj.transform.SetParent(slot.transform, false);
        RectTransform lockRt = lockObj.AddComponent<RectTransform>();
        lockRt.anchorMin = Vector2.one;
        lockRt.anchorMax = Vector2.one;
        lockRt.anchoredPosition = new Vector2(-8, -8);
        lockRt.sizeDelta = new Vector2(48, 48);
        Image lockImg = lockObj.AddComponent<Image>();
        lockImg.sprite = lockIcon;
        lockObj.SetActive(!isSelected);

        // Bottom Name & Unlock Status Pill
        GameObject bottomPill = new GameObject("BottomNamePill");
        bottomPill.transform.SetParent(slot.transform, false);
        RectTransform pillRt = bottomPill.AddComponent<RectTransform>();
        pillRt.anchorMin = new Vector2(0f, 0f);
        pillRt.anchorMax = new Vector2(1f, 0.26f);
        pillRt.offsetMin = new Vector2(6, 6);
        pillRt.offsetMax = new Vector2(-6, 0);

        Image pillBg = bottomPill.AddComponent<Image>();
        pillBg.sprite = ShopUIAssets.GetNamePill();
        pillBg.type = Image.Type.Sliced;

        GameObject pillTxtObj = new GameObject("PillText");
        pillTxtObj.transform.SetParent(bottomPill.transform, false);
        RectTransform pTxtRt = pillTxtObj.AddComponent<RectTransform>();
        pTxtRt.anchorMin = Vector2.zero;
        pTxtRt.anchorMax = Vector2.one;
        TextMeshProUGUI pTxt = pillTxtObj.AddComponent<TextMeshProUGUI>();
        pTxt.text = $"<size=20><b>{name}</b></size>\n<size=12><color=#FFD700>{unlockTag}</color></size>";
        pTxt.alignment = TextAlignmentOptions.Center;

        slot.AddComponent<Button>();
        return slot;
    }
}
#endif
