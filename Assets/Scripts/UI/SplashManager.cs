using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SplashManager : MonoBehaviour
{
    [Header("UI Buttons & Panels")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button closeSettingsButton;

    [Header("Name Change UI")]
    [SerializeField] private Button nameChangeButton;
    [SerializeField] private GameObject nameChangePanel;
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button saveNameButton;
    [SerializeField] private Button cancelNameButton;

    [Header("Settings UI")]
    [SerializeField] private Button soundButton;
    [SerializeField] private Button vibrationButton;

    [Header("Toggle Sprites — Drag from Project")]
    [SerializeField] private Sprite soundOnSprite;        // green toggle image
    [SerializeField] private Sprite soundOffSprite;       // blue/gray toggle image
    [SerializeField] private Sprite vibrationOnSprite;    // green toggle image
    [SerializeField] private Sprite vibrationOffSprite;   // blue/gray toggle image

    [Header("Character Selection UI")]
    [SerializeField] private Button characterButton;
    [SerializeField] private CharacterSelectUI characterSelectUI;

    // Discovered at runtime — the Toggle child inside Sound/Vibration rows
    private Toggle soundToggle;
    private Toggle vibrationToggle;

    private CharacterNameTag previewNameTag;
    private bool wasSettingsPanelActive = false;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // Force initialize AudioManager right here on first launch in SplashScene
        if (AudioManager.Instance != null) { /* ensures instance is ready immediately */ }
    }

    private void Update()
    {
        // Whenever settings panel becomes active (even if opened by custom Animator or Unity Inspector OnClick)
        if (settingsPanel == null) EnsureSettingsPanelBinding();

        if (settingsPanel != null)
        {
            bool isActive = settingsPanel.activeInHierarchy;
            if (isActive && !wasSettingsPanelActive)
            {
                EnsureSettingsPanelBinding();
                BindToggles();
                RefreshSoundVisuals();
                RefreshVibrationVisuals();
            }
            wasSettingsPanelActive = isActive;
        }
    }

    private void Start()
    {
        // 0. Ensure UI Canvas is Screen Space - Camera so 3D PreviewBean is visible in front of background plane
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas != null && Camera.main != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 15f;
        }

        // 1. Setup Character Selection Controls
        SetupCharacterControls();

        // 2. Setup Head NameTag on PreviewBean
        SetupPreviewBeanNameTag();

        // 3. Close shop and hide 3D preview bean by default on Main Menu startup
        if (characterSelectUI != null)
        {
            characterSelectUI.CloseShop();
        }
        else
        {
            GameObject previewBean = GameObject.Find("PreviewBean");
            if (previewBean != null) previewBean.SetActive(false);
            Transform howToPlay = transform.Find("How To play");
            if (howToPlay == null) howToPlay = transform.Find("HowToPlay");
            if (howToPlay != null) howToPlay.gameObject.SetActive(true);
        }

        // 2. Auto-find Settings panel if not assigned in Inspector
        if (settingsPanel == null)
        {
            // Search all children including inactive
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                string n = child.name.ToLower().Replace(" ", "").Replace("_", "");
                if (n == "settingspanel" || n == "settingpanel")
                {
                    settingsPanel = child.gameObject;
                    break;
                }
            }
        }

        // 3. Auto-find buttons inside settingsPanel
        if (settingsPanel != null)
        {
            Button[] btns = settingsPanel.GetComponentsInChildren<Button>(true);
            foreach (var btn in btns)
            {
                string bName = btn.name.ToLower();
                if (closeSettingsButton == null && (bName.Equals("close") || bName.Contains("cancel") || bName.Equals("x")))
                    closeSettingsButton = btn;
                else if (soundButton == null && bName.Contains("sound"))
                    soundButton = btn;
                else if (vibrationButton == null && bName.Contains("vibrat"))
                    vibrationButton = btn;
            }
        }

        // 4. Find play / settings buttons if not assigned
        if (playButton == null)
        {
            Transform t = transform.Find("PlayButton");
            if (t != null) playButton = t.GetComponent<Button>();
        }
        if (settingsButton == null)
        {
            Transform t = transform.Find("Settings Button");
            if (t == null) t = transform.Find("SettingsButton");
            if (t != null) settingsButton = t.GetComponent<Button>();
        }
        if (nameChangeButton == null)
        {
            Transform t = transform.Find("Name_change");
            if (t != null) nameChangeButton = t.GetComponent<Button>();
        }
        if (nameChangePanel == null)
        {
            Transform t = transform.Find("NameChangePanel");
            if (t != null) nameChangePanel = t.gameObject;
        }

        // 5. Bind button listeners
        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayClicked);
        }
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(OpenSettings);
        }
        if (closeSettingsButton != null)
        {
            closeSettingsButton.onClick.RemoveAllListeners();
            closeSettingsButton.onClick.AddListener(CloseSettings);
        }
        if (nameChangeButton != null)
        {
            nameChangeButton.onClick.RemoveAllListeners();
            nameChangeButton.onClick.AddListener(OpenNameChangeDialog);
        }

        EnsureNameChangeBinding();

        // 6. Setup Character Selection Controls
        SetupCharacterControls();

        // 7. Bind toggle and button controls before hiding
        EnsureSettingsPanelBinding();
        BindToggles();

        // Ensure panels are hidden initially
        if (Application.isPlaying && settingsPanel != null)
            settingsPanel.SetActive(false);
        if (Application.isPlaying && nameChangePanel != null)
            nameChangePanel.SetActive(false);

        // Refresh visuals next frame as well (ensures Unity built-in components don't reset state)
        StartCoroutine(BindTogglesNextFrame());
    }

    // ─────────────────────────────────────────────────────────────
    //  TOGGLE / BUTTON IMAGE SWAP (Sound & Vibration)
    // ─────────────────────────────────────────────────────────────

    private IEnumerator BindTogglesNextFrame()
    {
        yield return null; // wait 1 frame
        BindToggles();
        RefreshSoundVisuals();
        RefreshVibrationVisuals();
    }

    private void BindToggles()
    {
        // 1. SOUND BINDING
        // soundButton can be the 'toggle' GameObject (with Button component) or the parent Sound row
        if (soundButton != null)
        {
            soundButton.onClick.RemoveAllListeners();
            soundButton.onClick.AddListener(OnSoundClicked);
            soundToggle = soundButton.GetComponentInChildren<Toggle>(true);
        }

        // If a Toggle component exists on or under soundButton or settingsPanel
        if (soundToggle == null && settingsPanel != null)
        {
            foreach (var t in settingsPanel.GetComponentsInChildren<Toggle>(true))
            {
                string tName = t.name.ToLower();
                Transform p = t.transform.parent;
                string pName = p != null ? p.name.ToLower() : "";
                if (tName.Contains("sound") || pName.Contains("sound") || t.name == "toggle")
                {
                    soundToggle = t;
                    break;
                }
            }
        }

        if (soundToggle != null)
        {
            soundToggle.transition = Selectable.Transition.None;
            if (soundToggle.graphic != null) soundToggle.graphic.enabled = false;
            soundToggle.onValueChanged.RemoveAllListeners();
            soundToggle.onValueChanged.AddListener((isOn) =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.SetSoundEnabled(isOn);
                RefreshSoundVisuals();
            });
        }

        // 2. VIBRATION BINDING
        if (vibrationButton != null)
        {
            vibrationButton.onClick.RemoveAllListeners();
            vibrationButton.onClick.AddListener(OnVibrationClicked);
            vibrationToggle = vibrationButton.GetComponentInChildren<Toggle>(true);
        }

        if (vibrationToggle == null && settingsPanel != null)
        {
            foreach (var t in settingsPanel.GetComponentsInChildren<Toggle>(true))
            {
                if (t == soundToggle) continue;
                string tName = t.name.ToLower();
                Transform p = t.transform.parent;
                string pName = p != null ? p.name.ToLower() : "";
                if (tName.Contains("vibrat") || pName.Contains("vibrat") || t.name.Contains("(1)"))
                {
                    vibrationToggle = t;
                    break;
                }
            }
        }

        if (vibrationToggle != null)
        {
            vibrationToggle.transition = Selectable.Transition.None;
            if (vibrationToggle.graphic != null) vibrationToggle.graphic.enabled = false;
            vibrationToggle.onValueChanged.RemoveAllListeners();
            vibrationToggle.onValueChanged.AddListener((isOn) =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.SetVibrationEnabled(isOn);
                RefreshVibrationVisuals();
            });
        }

        // 3. Fallback: also bind any additional button inside Sound / Vibration rows
        if (settingsPanel != null)
        {
            foreach (var btn in settingsPanel.GetComponentsInChildren<Button>(true))
            {
                if (btn == closeSettingsButton || btn == soundButton || btn == vibrationButton) continue;
                string bName = btn.name.ToLower();
                Transform p = btn.transform.parent;
                string pName = p != null ? p.name.ToLower() : "";

                if (bName.Contains("sound") || pName.Contains("sound"))
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(OnSoundClicked);
                }
                else if (bName.Contains("vibrat") || pName.Contains("vibrat"))
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(OnVibrationClicked);
                }
            }
        }

        // Apply initial visual state
        RefreshSoundVisuals();
        RefreshVibrationVisuals();
    }

    private bool IsSoundActive => AudioManager.Instance != null 
        ? AudioManager.Instance.IsSoundEnabled 
        : (PlayerPrefs.GetInt("SoundEnabled", 1) == 1);

    private bool IsVibrationActive => AudioManager.Instance != null 
        ? AudioManager.Instance.IsVibrationEnabled 
        : (PlayerPrefs.GetInt("VibrationEnabled", 1) == 1);

    private void OnSoundClicked()
    {
        bool next = !IsSoundActive;
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetSoundEnabled(next);
        else
        {
            PlayerPrefs.SetInt("SoundEnabled", next ? 1 : 0);
            PlayerPrefs.Save();
        }
        if (soundToggle != null) soundToggle.SetIsOnWithoutNotify(next);
        RefreshSoundVisuals();
    }

    private void OnVibrationClicked()
    {
        bool next = !IsVibrationActive;
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetVibrationEnabled(next);
        else
        {
            PlayerPrefs.SetInt("VibrationEnabled", next ? 1 : 0);
            PlayerPrefs.Save();
        }
        if (vibrationToggle != null) vibrationToggle.SetIsOnWithoutNotify(next);
        RefreshVibrationVisuals();
    }

    public void RefreshSoundVisuals()
    {
        bool isOn = IsSoundActive;
        Sprite target = isOn ? soundOnSprite : soundOffSprite;
        if (target == null) return;

        if (soundButton != null)
            ApplySpriteToHierarchy(soundButton.gameObject, target);

        if (soundToggle != null && soundToggle.gameObject != (soundButton != null ? soundButton.gameObject : null))
            ApplySpriteToHierarchy(soundToggle.gameObject, target);

        // Scan settingsPanel for any image on GameObject named "toggle" under Sound
        if (settingsPanel != null)
        {
            foreach (var img in settingsPanel.GetComponentsInChildren<Image>(true))
            {
                string iName = img.gameObject.name.ToLower();
                Transform p = img.transform.parent;
                string pName = p != null ? p.name.ToLower() : "";
                if (iName.Contains("toggle") && pName.Contains("sound"))
                {
                    ApplySpriteToImage(img, target);
                }
            }
        }
    }

    public void RefreshVibrationVisuals()
    {
        bool isOn = IsVibrationActive;
        Sprite target = isOn ? vibrationOnSprite : vibrationOffSprite;
        if (target == null) return;

        if (vibrationButton != null)
            ApplySpriteToHierarchy(vibrationButton.gameObject, target);

        if (vibrationToggle != null && vibrationToggle.gameObject != (vibrationButton != null ? vibrationButton.gameObject : null))
            ApplySpriteToHierarchy(vibrationToggle.gameObject, target);

        if (settingsPanel != null)
        {
            foreach (var img in settingsPanel.GetComponentsInChildren<Image>(true))
            {
                string iName = img.gameObject.name.ToLower();
                Transform p = img.transform.parent;
                string pName = p != null ? p.name.ToLower() : "";
                if (iName.Contains("toggle") && pName.Contains("vibrat"))
                {
                    ApplySpriteToImage(img, target);
                }
            }
        }
    }

    private void ApplySpriteToHierarchy(GameObject root, Sprite sprite)
    {
        if (root == null || sprite == null) return;

        Selectable sel = root.GetComponent<Selectable>();
        if (sel != null) sel.transition = Selectable.Transition.None;

        Toggle tog = root.GetComponent<Toggle>();
        if (tog != null && tog.graphic != null) tog.graphic.enabled = false;

        Image[] imgs = root.GetComponentsInChildren<Image>(true);
        foreach (var img in imgs)
        {
            ApplySpriteToImage(img, sprite);
        }
    }

    private void ApplySpriteToImage(Image img, Sprite sprite)
    {
        if (img == null || sprite == null) return;
        img.sprite = sprite;
        img.overrideSprite = sprite;
        img.color = Color.white;
    }

    // ─────────────────────────────────────────────────────────────
    //  SETTINGS PANEL
    // ─────────────────────────────────────────────────────────────

    public void EnsureSettingsPanelBinding()
    {
        if (settingsPanel == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                string n = child.name.ToLower().Replace(" ", "").Replace("_", "");
                if (n == "settingspanel" || n == "settingpanel" || n == "settings")
                {
                    settingsPanel = child.gameObject;
                    break;
                }
            }
        }

        if (settingsPanel == null) return;

        // Auto-find all close button candidates recursively under settingsPanel
        Transform[] allChildren = settingsPanel.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
        {
            string nameLower = child.name.ToLower();
            if (nameLower.Equals("close") || nameLower.Contains("cancel") || nameLower.Equals("x") || nameLower.Contains("redclose") || nameLower.Contains("closebtn") || nameLower.Contains("btnclose"))
            {
                Button btn = child.GetComponent<Button>();
                if (btn == null)
                {
                    btn = child.gameObject.AddComponent<Button>();
                }

                Graphic graphic = child.GetComponent<Graphic>();
                if (graphic != null)
                {
                    graphic.raycastTarget = true;
                }

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(CloseSettings);

                if (closeSettingsButton == null)
                {
                    closeSettingsButton = btn;
                }
                Debug.Log($"[SplashManager] Bound CloseSettings listener to button: '{child.name}'");
            }
        }

        if (closeSettingsButton != null)
        {
            closeSettingsButton.onClick.RemoveAllListeners();
            closeSettingsButton.onClick.AddListener(CloseSettings);
        }
    }

    public void OpenSettings()
    {
        EnsureSettingsPanelBinding();
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            EnsureSettingsPanelBinding();
            BindToggles();
            RefreshSoundVisuals();
            RefreshVibrationVisuals();
        }
    }

    public void CloseSettings()
    {
        EnsureSettingsPanelBinding();
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
            Debug.Log("[SplashManager] Settings panel closed successfully!");
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  MISC
    // ─────────────────────────────────────────────────────────────

    public void OnPlayClicked()
    {
        SceneManager.LoadScene("GameplayScene");
    }

    private void SetupPreviewBeanNameTag()
    {
        GameObject previewBean = GameObject.Find("PreviewBean");
        if (previewBean != null)
        {
            previewNameTag = previewBean.GetComponent<CharacterNameTag>();
            if (previewNameTag == null)
                previewNameTag = previewBean.AddComponent<CharacterNameTag>();
            string savedName = PlayerPrefs.GetString("PlayerName", "Player");
            previewNameTag.Setup(savedName, new Color(0.33f, 0.92f, 0.22f), 1.70f);

            // Apply player's chosen character (Police / Bean) to PreviewBean
            string selectedId = CharacterDatabase.GetSelectedCharacterId();
            if (characterSelectUI != null)
                characterSelectUI.ApplyCharacterToPreview(selectedId);
            else
            {
                Animator a;
                CharacterDatabase.SpawnVisual(selectedId, previewBean.transform, out a);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  CHARACTER SELECTION
    // ─────────────────────────────────────────────────────────────

    public void OpenCharacterSelect()
    {
        if (characterSelectUI != null)
        {
            characterSelectUI.OpenSelectScreen();
        }
    }

    private void SetupCharacterControls()
    {
        if (characterSelectUI == null)
        {
            characterSelectUI = GetComponentInChildren<CharacterSelectUI>(true);
            if (characterSelectUI == null)
            {
                characterSelectUI = gameObject.AddComponent<CharacterSelectUI>();
            }
        }

        if (characterButton == null)
        {
            Transform t = transform.Find("CharacterButton");
            if (t == null) t = transform.Find("ShopButton");
            if (t == null) t = transform.Find("Shop Button");
            if (t != null) characterButton = t.GetComponent<Button>();
        }

        if (characterButton == null)
        {
            CreateDynamicCharacterButton();
        }

        if (characterButton != null)
        {
            Image img = characterButton.GetComponent<Image>();
            if (img != null)
            {
                Sprite shopSprite = Resources.Load<Sprite>("Shop");
#if UNITY_EDITOR
                if (shopSprite == null)
                {
                    shopSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Background/Shop.png");
                }
#endif
                if (shopSprite != null)
                {
                    img.sprite = shopSprite;
                    img.color = Color.white;
                }
            }

            characterButton.onClick.RemoveAllListeners();
            characterButton.onClick.AddListener(OpenCharacterSelect);
        }
    }

    private void CreateDynamicCharacterButton()
    {
        GameObject btnObj = new GameObject("CharacterButton");
        btnObj.transform.SetParent(transform, false);

        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(90f, -88f);
        rt.sizeDelta = new Vector2(105f, 105f);

        Image img = btnObj.AddComponent<Image>();
        img.color = Color.white;

        Sprite shopSprite = Resources.Load<Sprite>("Shop");
#if UNITY_EDITOR
        if (shopSprite == null)
        {
            shopSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Background/Shop.png");
        }
#endif
        if (shopSprite != null)
        {
            img.sprite = shopSprite;
        }

        characterButton = btnObj.AddComponent<Button>();
    }

    // ─────────────────────────────────────────────────────────────
    //  NAME CHANGE
    // ─────────────────────────────────────────────────────────────

    private string lastTypedName = "";

    public void EnsureNameChangeBinding()
    {
        // 1. Auto-find Name_change button on main UI Canvas
        if (nameChangeButton == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                string n = child.name.ToLower().Replace(" ", "").Replace("_", "");
                if (n == "namechange" || n == "name" || n == "changename" || n.Contains("namechange"))
                {
                    Button b = child.GetComponent<Button>();
                    if (b == null) b = child.gameObject.AddComponent<Button>();
                    nameChangeButton = b;
                    break;
                }
            }
        }

        if (nameChangeButton != null)
        {
            Graphic g = nameChangeButton.GetComponent<Graphic>();
            if (g != null) g.raycastTarget = true;

            nameChangeButton.onClick.RemoveAllListeners();
            nameChangeButton.onClick.AddListener(OpenNameChangeDialog);
        }

        // 2. Auto-find NameChangePanel
        if (nameChangePanel == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                string n = child.name.ToLower().Replace(" ", "").Replace("_", "");
                if (n == "namechangepanel" || n == "namepanel" || n == "changenamepanel")
                {
                    nameChangePanel = child.gameObject;
                    break;
                }
            }
        }

        if (nameChangePanel == null) return;

        // 3. Auto-find TMP_InputField inside panel
        if (nameInputField == null)
        {
            nameInputField = nameChangePanel.GetComponentInChildren<TMP_InputField>(true);
        }

        if (nameInputField != null)
        {
            nameInputField.interactable = true;
            nameInputField.shouldHideMobileInput = false;

            Graphic inputGraphic = nameInputField.targetGraphic;
            if (inputGraphic == null) inputGraphic = nameInputField.GetComponent<Graphic>();
            if (inputGraphic != null) inputGraphic.raycastTarget = true;

            // Real-time text tracking as user types on mobile keyboard
            nameInputField.onValueChanged.RemoveAllListeners();
            nameInputField.onValueChanged.AddListener((val) =>
            {
                if (!string.IsNullOrEmpty(val))
                    lastTypedName = val;
            });

            // Bind mobile soft keyboard "Done/Enter" action
            nameInputField.onEndEdit.RemoveAllListeners();
            nameInputField.onEndEdit.AddListener((val) =>
            {
                if (!string.IsNullOrEmpty(val))
                    lastTypedName = val;
                SaveName();
            });
        }

        // 4. Auto-find Save and Cancel buttons inside NameChangePanel recursively
        Transform[] allChildren = nameChangePanel.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
        {
            string nameLower = child.name.ToLower();
            if (nameLower.Contains("save") || nameLower.Contains("ok") || nameLower.Contains("submit") || nameLower.Contains("confirm"))
            {
                Button btn = child.GetComponent<Button>();
                if (btn == null) btn = child.gameObject.AddComponent<Button>();
                Graphic g = child.GetComponent<Graphic>();
                if (g != null) g.raycastTarget = true;

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(SaveName);
                saveNameButton = btn;
            }
            else if (nameLower.Contains("cancel") || nameLower.Contains("close") || nameLower.Equals("x") || nameLower.Contains("red"))
            {
                Button btn = child.GetComponent<Button>();
                if (btn == null) btn = child.gameObject.AddComponent<Button>();
                Graphic g = child.GetComponent<Graphic>();
                if (g != null) g.raycastTarget = true;

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(CloseNameDialog);
                cancelNameButton = btn;
            }
        }
    }

    public void OpenNameChangeDialog()
    {
        EnsureNameChangeBinding();

        if (nameChangePanel == null)
            CreateDynamicNameChangePanel();

        EnsureNameChangeBinding();

        if (nameChangePanel != null)
        {
            nameChangePanel.SetActive(true);
            string currentName = PlayerPrefs.GetString("PlayerName", "Player");
            lastTypedName = currentName;

            if (nameInputField != null)
            {
                nameInputField.text = currentName;
                if (nameInputField.textComponent != null)
                    nameInputField.textComponent.text = currentName;

                nameInputField.Select();
                nameInputField.ActivateInputField();
            }
        }
    }

    public void SaveName()
    {
        string newName = "";

        // 1. Priority 1: Check live lastTypedName variable from input field
        if (!string.IsNullOrWhiteSpace(lastTypedName))
        {
            newName = lastTypedName.Trim();
        }

        // 2. Priority 2: Check nameInputField.text
        if (string.IsNullOrWhiteSpace(newName) && nameInputField != null && !string.IsNullOrWhiteSpace(nameInputField.text))
        {
            newName = nameInputField.text.Trim();
        }

        // 3. Priority 3: Check textComponent text directly
        if (string.IsNullOrWhiteSpace(newName) && nameInputField != null && nameInputField.textComponent != null && !string.IsNullOrWhiteSpace(nameInputField.textComponent.text))
        {
            newName = nameInputField.textComponent.text.Trim();
        }

        // Fallback default
        if (string.IsNullOrWhiteSpace(newName))
        {
            newName = PlayerPrefs.GetString("PlayerName", "Player");
        }

        if (newName.Length > 12) newName = newName.Substring(0, 12);
        if (string.IsNullOrWhiteSpace(newName)) newName = "Player";

        // Save permanently to PlayerPrefs
        PlayerPrefs.SetString("PlayerName", newName);
        PlayerPrefs.Save();
        lastTypedName = newName;

        // Update preview name tag on 3D bean
        if (previewNameTag != null)
            previewNameTag.Setup(newName, new Color(0.33f, 0.92f, 0.22f), 1.70f);

        // Update all active 3D name tags in scene
        foreach (var tag in FindObjectsByType<CharacterNameTag>(FindObjectsSortMode.None))
        {
            tag.Setup(newName, new Color(0.33f, 0.92f, 0.22f), 1.70f);
        }

        // Update all UI text elements in Canvas displaying player name
        RefreshAllUIPlayerNameTexts(newName);

        if (nameChangePanel != null)
            nameChangePanel.SetActive(false);

        Debug.Log($"[SplashManager] SUCCESS! Saved new PlayerName: '{newName}'");
    }

    public void RefreshAllUIPlayerNameTexts(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName)) playerName = PlayerPrefs.GetString("PlayerName", "Player");

        foreach (var tmp in FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None))
        {
            string tmpName = tmp.name.ToLower();
            Transform p = tmp.transform.parent;
            string pName = p != null ? p.name.ToLower() : "";

            if (tmpName.Contains("playernametext") || tmpName.Contains("nametext") || tmpName == "playername" || pName.Contains("name_change") || pName.Contains("namechange"))
            {
                tmp.text = playerName;
            }
        }
    }

    public void CloseNameDialog()
    {
        if (nameChangePanel != null)
            nameChangePanel.SetActive(false);
    }

    private void BindNamePanelButtons()
    {
        EnsureNameChangeBinding();
    }

    private void CreateDynamicNameChangePanel()
    {
        nameChangePanel = new GameObject("NameChangePanel");
        nameChangePanel.transform.SetParent(transform, false);

        RectTransform panelRt = nameChangePanel.AddComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.sizeDelta = Vector2.zero;

        Image panelBg = nameChangePanel.AddComponent<Image>();
        panelBg.color = new Color(0.05f, 0.08f, 0.14f, 0.85f);

        GameObject cardObj = new GameObject("Card");
        cardObj.transform.SetParent(nameChangePanel.transform, false);

        RectTransform cardRt = cardObj.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(750f, 420f);
        cardRt.anchoredPosition = Vector2.zero;

        Image cardImg = cardObj.AddComponent<Image>();
        cardImg.color = new Color(0.12f, 0.18f, 0.28f, 0.98f);

        // Title
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(700f, 80f);
        titleRt.anchoredPosition = new Vector2(0, -30f);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "ENTER PLAYER NAME";
        titleText.fontSize = 42;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(1.0f, 0.88f, 0.20f);

        // Input Field
        GameObject inputObj = new GameObject("NameInputField");
        inputObj.transform.SetParent(cardObj.transform, false);
        RectTransform inputRt = inputObj.AddComponent<RectTransform>();
        inputRt.anchorMin = new Vector2(0.5f, 0.5f);
        inputRt.anchorMax = new Vector2(0.5f, 0.5f);
        inputRt.pivot = new Vector2(0.5f, 0.5f);
        inputRt.sizeDelta = new Vector2(600f, 90f);
        inputRt.anchoredPosition = new Vector2(0, 15f);
        inputObj.AddComponent<Image>().color = new Color(0.06f, 0.09f, 0.15f, 1.0f);

        GameObject inputTextObj = new GameObject("Text");
        inputTextObj.transform.SetParent(inputObj.transform, false);
        RectTransform inputTextRt = inputTextObj.AddComponent<RectTransform>();
        inputTextRt.anchorMin = Vector2.zero;
        inputTextRt.anchorMax = Vector2.one;
        inputTextRt.sizeDelta = new Vector2(-40f, 0);
        inputTextRt.anchoredPosition = Vector2.zero;
        TextMeshProUGUI inputText = inputTextObj.AddComponent<TextMeshProUGUI>();
        inputText.fontSize = 38;
        inputText.fontStyle = FontStyles.Bold;
        inputText.alignment = TextAlignmentOptions.Center;
        inputText.color = Color.white;

        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(inputObj.transform, false);
        RectTransform placeholderRt = placeholderObj.AddComponent<RectTransform>();
        placeholderRt.anchorMin = Vector2.zero;
        placeholderRt.anchorMax = Vector2.one;
        placeholderRt.sizeDelta = new Vector2(-40f, 0);
        placeholderRt.anchoredPosition = Vector2.zero;
        TextMeshProUGUI placeholderText = placeholderObj.AddComponent<TextMeshProUGUI>();
        placeholderText.text = "Enter Name...";
        placeholderText.fontSize = 38;
        placeholderText.fontStyle = FontStyles.Italic;
        placeholderText.alignment = TextAlignmentOptions.Center;
        placeholderText.color = new Color(0.6f, 0.6f, 0.6f, 0.7f);

        nameInputField = inputObj.AddComponent<TMP_InputField>();
        nameInputField.textComponent = inputText;
        nameInputField.placeholder = placeholderText;
        nameInputField.characterLimit = 12;

        // Save Button
        GameObject saveBtnObj = new GameObject("SaveButton");
        saveBtnObj.transform.SetParent(cardObj.transform, false);
        RectTransform saveRt = saveBtnObj.AddComponent<RectTransform>();
        saveRt.anchorMin = new Vector2(0.5f, 0f);
        saveRt.anchorMax = new Vector2(0.5f, 0f);
        saveRt.pivot = new Vector2(0.5f, 0f);
        saveRt.sizeDelta = new Vector2(240f, 85f);
        saveRt.anchoredPosition = new Vector2(-140f, 35f);
        saveBtnObj.AddComponent<Image>().color = new Color(0.15f, 0.80f, 0.35f);
        saveNameButton = saveBtnObj.AddComponent<Button>();
        saveNameButton.onClick.AddListener(SaveName);
        GameObject saveTextObj = new GameObject("Text");
        saveTextObj.transform.SetParent(saveBtnObj.transform, false);
        RectTransform saveTextRt = saveTextObj.AddComponent<RectTransform>();
        saveTextRt.anchorMin = Vector2.zero;
        saveTextRt.anchorMax = Vector2.one;
        saveTextRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI saveTmp = saveTextObj.AddComponent<TextMeshProUGUI>();
        saveTmp.text = "SAVE";
        saveTmp.fontSize = 34;
        saveTmp.fontStyle = FontStyles.Bold;
        saveTmp.alignment = TextAlignmentOptions.Center;
        saveTmp.color = Color.white;

        // Cancel Button
        GameObject cancelBtnObj = new GameObject("CancelButton");
        cancelBtnObj.transform.SetParent(cardObj.transform, false);
        RectTransform cancelRt = cancelBtnObj.AddComponent<RectTransform>();
        cancelRt.anchorMin = new Vector2(0.5f, 0f);
        cancelRt.anchorMax = new Vector2(0.5f, 0f);
        cancelRt.pivot = new Vector2(0.5f, 0f);
        cancelRt.sizeDelta = new Vector2(240f, 85f);
        cancelRt.anchoredPosition = new Vector2(140f, 35f);
        cancelBtnObj.AddComponent<Image>().color = new Color(0.85f, 0.25f, 0.25f);
        cancelNameButton = cancelBtnObj.AddComponent<Button>();
        cancelNameButton.onClick.AddListener(CloseNameDialog);
        GameObject cancelTextObj = new GameObject("Text");
        cancelTextObj.transform.SetParent(cancelBtnObj.transform, false);
        RectTransform cancelTextRt = cancelTextObj.AddComponent<RectTransform>();
        cancelTextRt.anchorMin = Vector2.zero;
        cancelTextRt.anchorMax = Vector2.one;
        cancelTextRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI cancelTmp = cancelTextObj.AddComponent<TextMeshProUGUI>();
        cancelTmp.text = "CANCEL";
        cancelTmp.fontSize = 34;
        cancelTmp.fontStyle = FontStyles.Bold;
        cancelTmp.alignment = TextAlignmentOptions.Center;
        cancelTmp.color = Color.white;
    }
}
