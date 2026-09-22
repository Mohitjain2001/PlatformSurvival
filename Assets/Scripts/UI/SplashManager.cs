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

    private CharacterNameTag previewNameTag;

    private void Start()
    {
        // 1. Setup Head NameTag on PreviewBean in SplashScene
        SetupPreviewBeanNameTag();

        // 2. Auto-find Settings references if not assigned in Inspector
        if (playButton == null)
        {
            Transform tPlay = transform.Find("PlayButton");
            if (tPlay != null) playButton = tPlay.GetComponent<Button>();
        }

        if (settingsButton == null)
        {
            Transform tSet = transform.Find("Settings Button");
            if (tSet != null) settingsButton = tSet.GetComponent<Button>();
        }

        if (settingsPanel == null)
        {
            Transform tPan = transform.Find("Settings panel");
            if (tPan != null) settingsPanel = tPan.gameObject;
        }

        if (settingsPanel != null && closeSettingsButton == null)
        {
            Button[] btns = settingsPanel.GetComponentsInChildren<Button>(true);
            foreach (var btn in btns)
            {
                if (btn.name.Equals("Close", System.StringComparison.OrdinalIgnoreCase))
                {
                    closeSettingsButton = btn;
                    break;
                }
            }
        }

        // 3. Auto-find Name_change button & panel
        if (nameChangeButton == null)
        {
            Transform tName = transform.Find("Name_change");
            if (tName != null) nameChangeButton = tName.GetComponent<Button>();
        }

        if (nameChangePanel == null)
        {
            Transform tNamePanel = transform.Find("NameChangePanel");
            if (tNamePanel != null) nameChangePanel = tNamePanel.gameObject;
        }

        // 4. Bind Listeners
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

        // Auto-bind Save and Cancel buttons inside NameChangePanel
        BindNamePanelButtons();

        // 5. Ensure Panels are hidden initially
        if (Application.isPlaying && settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        if (Application.isPlaying && nameChangePanel != null)
        {
            nameChangePanel.SetActive(false);
        }
    }

    private void BindNamePanelButtons()
    {
        if (nameChangePanel == null)
        {
            Transform tNamePanel = transform.Find("NameChangePanel");
            if (tNamePanel != null) nameChangePanel = tNamePanel.gameObject;
        }

        if (nameChangePanel == null) return;

        if (nameInputField == null)
        {
            nameInputField = nameChangePanel.GetComponentInChildren<TMP_InputField>(true);
        }

        Button[] btns = nameChangePanel.GetComponentsInChildren<Button>(true);
        foreach (var btn in btns)
        {
            string bName = btn.name.ToLower();
            if (saveNameButton == null && (bName.Contains("save") || bName.Contains("ok")))
            {
                saveNameButton = btn;
            }
            else if (cancelNameButton == null && (bName.Contains("cancel") || bName.Contains("close")))
            {
                cancelNameButton = btn;
            }

            Graphic[] graphics = btn.GetComponentsInChildren<Graphic>(true);
            foreach (var g in graphics)
            {
                if (g.gameObject != btn.gameObject)
                {
                    g.raycastTarget = false;
                }
                else
                {
                    g.raycastTarget = true;
                }
            }
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

    private void SetupPreviewBeanNameTag()
    {
        GameObject previewBean = GameObject.Find("PreviewBean");
        if (previewBean != null)
        {
            previewNameTag = previewBean.GetComponent<CharacterNameTag>();
            if (previewNameTag == null)
            {
                previewNameTag = previewBean.AddComponent<CharacterNameTag>();
            }
            string savedName = PlayerPrefs.GetString("PlayerName", "Player");
            previewNameTag.Setup(savedName, new Color(0.33f, 0.92f, 0.22f), 1.70f);
        }
    }

    public void OnPlayClicked()
    {
        SceneManager.LoadScene("GameplayScene");
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    public void OpenNameChangeDialog()
    {
        if (nameChangePanel == null)
        {
            CreateDynamicNameChangePanel();
        }

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
        {
            newName = nameInputField.text.Trim();
        }

        // Limit name length to 12 chars
        if (newName.Length > 12)
        {
            newName = newName.Substring(0, 12);
        }

        PlayerPrefs.SetString("PlayerName", newName);
        PlayerPrefs.Save();

        // Update preview character head text
        if (previewNameTag != null)
        {
            previewNameTag.Setup(newName, new Color(0.33f, 0.92f, 0.22f), 1.70f);
        }

        if (nameChangePanel != null)
        {
            nameChangePanel.SetActive(false);
        }
    }

    public void CloseNameDialog()
    {
        if (nameChangePanel != null)
        {
            nameChangePanel.SetActive(false);
        }
    }

    private void CreateDynamicNameChangePanel()
    {
        // 1. Overlay Panel Background
        nameChangePanel = new GameObject("NameChangePanel");
        nameChangePanel.transform.SetParent(transform, false);

        RectTransform panelRt = nameChangePanel.AddComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.sizeDelta = Vector2.zero;

        Image panelBg = nameChangePanel.AddComponent<Image>();
        panelBg.color = new Color(0.05f, 0.08f, 0.14f, 0.85f); // Soft dark modal dimming

        // 2. Card Dialog Container
        GameObject cardObj = new GameObject("Card");
        cardObj.transform.SetParent(nameChangePanel.transform, false);

        RectTransform cardRt = cardObj.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(750f, 420f);
        cardRt.anchoredPosition = Vector2.zero;

        Image cardImg = cardObj.AddComponent<Image>();
        cardImg.color = new Color(0.12f, 0.18f, 0.28f, 0.98f); // Dark Slate Card

        // 3. Header Title Text
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

        // 4. Input Field Container
        GameObject inputObj = new GameObject("NameInputField");
        inputObj.transform.SetParent(cardObj.transform, false);

        RectTransform inputRt = inputObj.AddComponent<RectTransform>();
        inputRt.anchorMin = new Vector2(0.5f, 0.5f);
        inputRt.anchorMax = new Vector2(0.5f, 0.5f);
        inputRt.pivot = new Vector2(0.5f, 0.5f);
        inputRt.sizeDelta = new Vector2(600f, 90f);
        inputRt.anchoredPosition = new Vector2(0, 15f);

        Image inputBg = inputObj.AddComponent<Image>();
        inputBg.color = new Color(0.06f, 0.09f, 0.15f, 1.0f);

        // Input Field Text Child
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

        // Input Field Placeholder Child
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

        // 5. Save Button (Green)
        GameObject saveBtnObj = new GameObject("SaveButton");
        saveBtnObj.transform.SetParent(cardObj.transform, false);

        RectTransform saveRt = saveBtnObj.AddComponent<RectTransform>();
        saveRt.anchorMin = new Vector2(0.5f, 0f);
        saveRt.anchorMax = new Vector2(0.5f, 0f);
        saveRt.pivot = new Vector2(0.5f, 0f);
        saveRt.sizeDelta = new Vector2(240f, 85f);
        saveRt.anchoredPosition = new Vector2(-140f, 35f);

        Image saveImg = saveBtnObj.AddComponent<Image>();
        saveImg.color = new Color(0.15f, 0.80f, 0.35f);

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

        // 6. Cancel Button (Gray/Red)
        GameObject cancelBtnObj = new GameObject("CancelButton");
        cancelBtnObj.transform.SetParent(cardObj.transform, false);

        RectTransform cancelRt = cancelBtnObj.AddComponent<RectTransform>();
        cancelRt.anchorMin = new Vector2(0.5f, 0f);
        cancelRt.anchorMax = new Vector2(0.5f, 0f);
        cancelRt.pivot = new Vector2(0.5f, 0f);
        cancelRt.sizeDelta = new Vector2(240f, 85f);
        cancelRt.anchoredPosition = new Vector2(140f, 35f);

        Image cancelImg = cancelBtnObj.AddComponent<Image>();
        cancelImg.color = new Color(0.85f, 0.25f, 0.25f);

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
