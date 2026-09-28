using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime controller for the Character Shop UI matching reference concept media_1790512342334.jpg:
/// - Top Showroom 3D model navigation via Left [<] and Right [>] arrow buttons.
/// - Showroom Name Pill and Green "(✓) EQUIPPED" Status Badge.
/// - 4x2 Grid card selection with glowing yellow borders, lock badges (🔒), and checkmark badges (✓).
/// - Watch Ad button ("TRY THIS CHARACTER FOR 1 MATCH") & Level Unlock button ("UNLOCK AT LEVEL X").
/// - Preserves user-assigned custom character images set in the Unity Inspector.
/// </summary>
public class CharacterSelectUI : MonoBehaviour
{
    [Header("Preview Target")]
    [SerializeField] private GameObject previewRoot;

    // Showroom Controls
    private Button leftArrowBtn;
    private Button rightArrowBtn;
    private TextMeshProUGUI showroomNameText;
    private TextMeshProUGUI showroomEquippedText;
    private Button showroomEquipBtn;
    private Button closeBtn;

    // Action Buttons
    private Button watchAdBtn;
    private Button levelUnlockBtn;
    private TextMeshProUGUI levelUnlockText;

    // Grid Elements
    private List<CharacterData> characters;
    private int selectedIndex = 0;
    private bool isOpen = true;

    [Header("Showroom Rotation")]
    [SerializeField] private float autoRotationSpeed = 35f;
    [SerializeField] private bool enableDragRotation = true;
    private float lastMouseX;
    private bool isDragging = false;

    private void Awake()
    {
        characters = CharacterDatabase.GetAllCharacters();
        BindUIReferences();
    }

    private void Update()
    {
        if (!isOpen) return;

        if (previewRoot == null) previewRoot = GameObject.Find("PreviewBean");
        if (previewRoot == null) return;

        // Interactive Touch / Mouse Drag Rotation
        if (enableDragRotation)
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (Input.mousePosition.y > Screen.height * 0.40f)
                {
                    isDragging = true;
                    lastMouseX = Input.mousePosition.x;
                }
            }
            if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
            }

            if (isDragging && Input.GetMouseButton(0))
            {
                float deltaX = Input.mousePosition.x - lastMouseX;
                lastMouseX = Input.mousePosition.x;
                previewRoot.transform.Rotate(Vector3.up, -deltaX * 0.5f, Space.World);
                return;
            }
        }

        // Auto Turntable Rotation
        previewRoot.transform.Rotate(Vector3.up, autoRotationSpeed * Time.deltaTime, Space.World);
    }

    private void Start()
    {
        if (previewRoot == null)
        {
            previewRoot = GameObject.Find("PreviewBean");
        }

        string currentId = CharacterDatabase.GetSelectedCharacterId();
        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i].id == currentId)
            {
                selectedIndex = i;
                break;
            }
        }

        ApplyCharacterToPreview(currentId);
        RefreshGrid();
        UpdateShowroomUI();
    }

    private void OnEnable()
    {
        BindUIReferences();
    }

    private void BindUIReferences()
    {
        // Find and bind all Close buttons recursively anywhere under Canvas or ShopWindowRoot
        Transform searchRoot = transform.parent != null ? transform.parent : transform;
        Transform[] allTransforms = searchRoot.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in allTransforms)
        {
            string tName = t.gameObject.name.ToLower();
            if (tName.Contains("close") || tName.Contains("redclose") || tName == "x" || tName.Contains("cancel"))
            {
                Button b = t.GetComponent<Button>();
                if (b == null)
                {
                    b = t.gameObject.AddComponent<Button>();
                }
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(CloseShop);
                Debug.Log($"[CharacterSelectUI] Bound Close listener to button: '{t.gameObject.name}'");
            }
        }

        // Navigation Arrows
        Transform leftObj = transform.Find("LeftArrowButton");
        if (leftObj != null)
        {
            leftArrowBtn = leftObj.GetComponent<Button>();
            if (leftArrowBtn != null) leftArrowBtn.onClick.AddListener(() => NavigateCharacter(-1));
        }

        Transform rightObj = transform.Find("RightArrowButton");
        if (rightObj != null)
        {
            rightArrowBtn = rightObj.GetComponent<Button>();
            if (rightArrowBtn != null) rightArrowBtn.onClick.AddListener(() => NavigateCharacter(1));
        }

        // Showroom Name & Badge
        Transform namePill = transform.Find("ShowroomNamePill");
        if (namePill != null) showroomNameText = namePill.GetComponentInChildren<TextMeshProUGUI>();

        Transform eqBadge = transform.Find("ShowroomEquippedBadge");
        if (eqBadge != null)
        {
            showroomEquippedText = eqBadge.GetComponentInChildren<TextMeshProUGUI>();
            showroomEquipBtn = eqBadge.GetComponent<Button>();
            if (showroomEquipBtn != null) showroomEquipBtn.onClick.AddListener(OnEquipButtonClicked);
        }

        // Action Buttons
        Transform actionBar = transform.Find("BottomDrawerPanel/ActionBar");
        if (actionBar != null)
        {
            Transform adObj = actionBar.Find("WatchAdButton");
            if (adObj != null)
            {
                watchAdBtn = adObj.GetComponent<Button>();
                if (watchAdBtn != null) watchAdBtn.onClick.AddListener(OnWatchAdClicked);
            }

            Transform unObj = actionBar.Find("LevelUnlockButton");
            if (unObj != null)
            {
                levelUnlockBtn = unObj.GetComponent<Button>();
                if (levelUnlockBtn != null) levelUnlockBtn.onClick.AddListener(OnLevelUnlockClicked);
                levelUnlockText = unObj.GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        // Cards Grid
        Transform grid = transform.Find("BottomDrawerPanel/SkyBluePanel/CardsGrid");
        if (grid != null)
        {
            for (int i = 0; i < grid.childCount; i++)
            {
                Transform child = grid.GetChild(i);
                int index = i;
                if (child.name.Contains("_"))
                {
                    string[] parts = child.name.Split('_');
                    if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out int parsedIndex))
                    {
                        index = parsedIndex;
                    }
                }

                Button cardBtn = child.GetComponent<Button>();
                if (cardBtn != null)
                {
                    cardBtn.onClick.RemoveAllListeners();
                    cardBtn.onClick.AddListener(() => OnCardClicked(index));
                }
            }
        }
    }

    public void OpenSelectScreen()
    {
        OpenShop();
    }

    public void CloseSelectScreen()
    {
        CloseShop();
    }

    public void OpenShop()
    {
        isOpen = true;
        gameObject.SetActive(true);

        // Show 3D preview bean when shop is open
        if (previewRoot == null) previewRoot = GameObject.Find("PreviewBean");
        if (previewRoot != null) previewRoot.SetActive(true);

        // Hide main menu static poster so 3D showroom model is clear
        SetMainMenuPosterActive(false);

        RefreshGrid();
        UpdateShowroomUI();
    }

    public void CloseShop()
    {
        isOpen = false;
        gameObject.SetActive(false);

        // Hide 3D preview bean when shop is closed
        if (previewRoot == null) previewRoot = GameObject.Find("PreviewBean");
        if (previewRoot != null) previewRoot.SetActive(false);

        // Re-enable main menu static poster for clean splash screen
        SetMainMenuPosterActive(true);
    }

    private void SetMainMenuPosterActive(bool active)
    {
        Transform canvasT = transform.parent;
        if (canvasT == null)
        {
            Canvas c = GetComponentInParent<Canvas>();
            if (c != null) canvasT = c.transform;
        }
        if (canvasT != null)
        {
            Transform howToPlay = canvasT.Find("How To play");
            if (howToPlay == null) howToPlay = canvasT.Find("HowToPlay");
            if (howToPlay != null)
            {
                howToPlay.gameObject.SetActive(active);
            }
        }
    }

    private void NavigateCharacter(int direction)
    {
        if (characters == null || characters.Count == 0) return;
        selectedIndex = (selectedIndex + direction + characters.Count) % characters.Count;
        SelectAndEquipCurrentIndex();
    }

    private void OnCardClicked(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= characters.Count) return;
        selectedIndex = cardIndex;
        SelectAndEquipCurrentIndex();
    }

    private void OnEquipButtonClicked()
    {
        SelectAndEquipCurrentIndex();
    }

    private void OnWatchAdClicked()
    {
        if (selectedIndex < 0 || selectedIndex >= characters.Count) return;
        CharacterData data = characters[selectedIndex];
        CharacterDatabase.AddCoins(150);
        CharacterDatabase.UnlockCharacter(data.id);
        CharacterDatabase.SelectCharacter(data.id);
        ApplyCharacterToPreview(data.id);
        Debug.Log($"[CharacterSelectUI] Watch Ad clicked! Character '{data.displayName}' unlocked & equipped!");
        UpdateShowroomUI();
        RefreshGrid();
    }

    private void OnLevelUnlockClicked()
    {
        if (selectedIndex < 0 || selectedIndex >= characters.Count) return;
        CharacterData data = characters[selectedIndex];
        CharacterDatabase.UnlockCharacter(data.id);
        CharacterDatabase.SelectCharacter(data.id);
        ApplyCharacterToPreview(data.id);
        Debug.Log($"[CharacterSelectUI] Level Unlock clicked! Character '{data.displayName}' unlocked & equipped!");
        UpdateShowroomUI();
        RefreshGrid();
    }

    private void SelectAndEquipCurrentIndex()
    {
        if (selectedIndex < 0 || selectedIndex >= characters.Count) return;
        CharacterData data = characters[selectedIndex];

        CharacterDatabase.UnlockCharacter(data.id);
        CharacterDatabase.SelectCharacter(data.id);
        ApplyCharacterToPreview(data.id);

        Debug.Log($"[CharacterSelectUI] Selected character: {data.displayName}");

        UpdateShowroomUI();
        RefreshGrid();
    }

    private void UpdateShowroomUI()
    {
        if (selectedIndex < 0 || selectedIndex >= characters.Count) return;
        CharacterData data = characters[selectedIndex];

        if (showroomNameText != null) showroomNameText.text = data.displayName.ToUpper();
        if (showroomEquippedText != null) showroomEquippedText.text = "✓ EQUIPPED";

        if (levelUnlockText != null)
        {
            int levelReq = (selectedIndex + 1) * 5;
            levelUnlockText.text = $"🔒 UNLOCK AT\n<size=26>LEVEL {levelReq}</size>";
        }
    }

    private void RefreshGrid()
    {
        Transform grid = transform.Find("BottomDrawerPanel/SkyBluePanel/CardsGrid");
        if (grid == null) return;

        string currentEquippedId = CharacterDatabase.GetSelectedCharacterId();

        for (int i = 0; i < grid.childCount; i++)
        {
            Transform slot = grid.GetChild(i);
            int index = i;
            if (slot.name.Contains("_"))
            {
                string[] parts = slot.name.Split('_');
                if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out int parsedIndex))
                {
                    index = parsedIndex;
                }
            }

            Image bgImg = slot.GetComponent<Image>();
            Transform icon = slot.Find("ItemIcon");
            Image iconImg = icon != null ? icon.GetComponent<Image>() : null;

            if (index < characters.Count)
            {
                CharacterData data = characters[index];
                bool isUnlocked = CharacterDatabase.IsCharacterUnlocked(data.id);
                bool isEquipped = (data.id == currentEquippedId);

                // Update card frame border if default card sprite
                if (bgImg != null)
                {
                    if (bgImg.sprite == null || bgImg.sprite == ShopUIAssets.GetSelectedCardBg() || bgImg.sprite == ShopUIAssets.GetLockedCardBg())
                    {
                        bgImg.sprite = isEquipped ? ShopUIAssets.GetSelectedCardBg() : ShopUIAssets.GetLockedCardBg();
                    }
                }

                // Preserve custom assigned icon sprite from inspector
                if (iconImg != null)
                {
                    if (iconImg.sprite == null)
                    {
                        iconImg.sprite = (isEquipped || isUnlocked) ? ShopUIAssets.GetGoldCoin() : ShopUIAssets.GetGoldCoin();
                    }
                    iconImg.color = (isUnlocked || isEquipped) ? Color.white : new Color(0.7f, 0.7f, 0.7f, 0.9f);
                    iconImg.gameObject.SetActive(true);
                }

                // Toggle selection frame / checkmark child GameObject ("salect", "select", "CheckmarkBadge")
                for (int c = 0; c < slot.childCount; c++)
                {
                    Transform child = slot.GetChild(c);
                    string childNameLower = child.name.ToLower();
                    if (childNameLower.Contains("salect") || childNameLower.Contains("select") || childNameLower.Contains("checkmark"))
                    {
                        child.gameObject.SetActive(isEquipped);
                    }
                }

                // Lock badge (hidden when equipped/unlocked)
                Transform lockChild = slot.Find("LockBadge");
                if (lockChild != null) lockChild.gameObject.SetActive(!isEquipped && !isUnlocked);

                // Bottom Name & Status Pill
                Transform pillChild = slot.Find("BottomNamePill");
                if (pillChild != null)
                {
                    TextMeshProUGUI pTxt = pillChild.GetComponentInChildren<TextMeshProUGUI>();
                    if (pTxt != null)
                    {
                        string statusTag = isEquipped ? "<color=#00FF66>EQUIPPED</color>" : $"UNLOCK AT LEVEL {(index + 1) * 5}";
                        pTxt.text = $"<size=20><b>{data.displayName.ToUpper()}</b></size>\n<size=12>{statusTag}</size>";
                    }
                }
            }
        }
    }

    public void ApplyCharacterToPreview(string characterId)
    {
        if (previewRoot == null) previewRoot = GameObject.Find("PreviewBean");
        if (previewRoot != null)
        {
            CharacterModelBuilder.BuildCharacterModel(previewRoot, characterId);
        }
    }
}
