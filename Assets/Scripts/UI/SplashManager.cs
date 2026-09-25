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
        if (settingsPanel != null)
        {
            bool isActive = settingsPanel.activeInHierarchy;
            if (isActive && !wasSettingsPanelActive)
            {
                BindToggles();
                RefreshSoundVisuals();
                RefreshVibrationVisuals();
            }
            wasSettingsPanelActive = isActive;
        }
    }

    private void Start()
    {
        // 1. Setup Head NameTag on PreviewBean
        SetupPreviewBeanNameTag();

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

        BindNamePanelButtons();

        // 6. Bind toggle and button controls before hiding
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

    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            BindToggles();
            RefreshSoundVisuals();
            RefreshVibrationVisuals();
        }
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
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
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  NAME CHANGE
    // ─────────────────────────────────────────────────────────────

    public void OpenNameChangeDialog()
    {
        if (nameChangePanel == null)
            CreateDynamicNameChangePanel();

        BindNamePanelButtons();

        if (nameChangePanel != null)
        {
            nameChangePanel.SetActive(true);
            if (nameInputField != null)
            {
                nameInputField.text = PlayerPrefs.GetString("PlayerName", "Player");
                nameInputField.Select();
                nameInputField.ActivateInputField();
            }
        }
    }

    public void SaveName()
    {
        string newName = "Player";
        if (nameInputField != null && !string.IsNullOrWhiteSpace(nameInputField.text))
            newName = nameInputField.text.Trim();
        if (newName.Length > 12) newName = newName.Substring(0, 12);

        PlayerPrefs.SetString("PlayerName", newName);
        PlayerPrefs.Save();

        if (previewNameTag != null)
            previewNameTag.Setup(newName, new Color(0.33f, 0.92f, 0.22f), 1.70f);

        if (nameChangePanel != null)
            nameChangePanel.SetActive(false);
    }

    public void CloseNameDialog()
    {
        if (nameChangePanel != null)
            nameChangePanel.SetActive(false);
    }

    private void BindNamePanelButtons()
    {
        if (nameChangePanel == null)
        {
            Transform t = transform.Find("NameChangePanel");
            if (t != null) nameChangePanel = t.gameObject;
        }
        if (nameChangePanel == null) return;

        if (nameInputField == null)
            nameInputField = nameChangePanel.GetComponentInChildren<TMP_InputField>(true);

        Button[] btns = nameChangePanel.GetComponentsInChildren<Button>(true);
        foreach (var btn in btns)
        {
            string bName = btn.name.ToLower();
            if (saveNameButton == null && (bName.Contains("save") || bName.Contains("ok")))
                saveNameButton = btn;
            else if (cancelNameButton == null && (bName.Contains("cancel") || bName.Contains("close")))
                cancelNameButton = btn;
        }

        if (saveNameButton != null)
        {
            saveNameButton.onClick.RemoveAllListeners();
            saveNameButton.onClick.AddListener(SaveName);
        }
        if (cancelNameButton != null)
        {
            cancelNameButton.onClick.RemoveAllListeners();
            cancelNameButton.onClick.AddListener(CloseNameDialog);
        }
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
