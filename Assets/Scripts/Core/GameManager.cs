using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Waiting, Playing, GameOver, Victory }

    [Header("Game Configuration")]
    [SerializeField] private int botCount = 4;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject botPrefab;

    [Header("Dependencies")]
    [SerializeField] private PlatformGridGenerator gridGenerator;
    [SerializeField] private VirtualJoystick joystick;
    [SerializeField] private FollowCamera followCamera;
    [SerializeField] private UIManager uiManager;

    [Header("Bot Colors & Names")]
    [SerializeField] private Color[] botColors = new Color[]
    {
        new Color(0.95f, 0.25f, 0.20f), // Vibrant Red
        new Color(0.60f, 0.20f, 0.85f), // Royal Purple
        new Color(1.00f, 0.55f, 0.00f), // Neon Orange
        new Color(0.98f, 0.85f, 0.10f), // Sunny Yellow
        new Color(0.20f, 0.80f, 0.35f)  // Emerald Green
    };

    [SerializeField] private string[] randomBotNames = new string[]
    {
        "Shadow", "Speedy", "Blaze", "Thunder", "Ninja",
        "Viper", "Turbo", "Cosmo", "Titan", "Phantom",
        "Ace", "Pixel", "Maverick", "Rex", "Flash",
        "Volt", "Zane", "Nova", "Apex", "Kratos"
    };

    private GameState currentState = GameState.Waiting;
    private List<GameObject> aliveParticipants = new List<GameObject>();
    private int totalParticipants = 0;
    private GameObject playerInstance;

    public GameState CurrentState => currentState;
    public int AliveCount => aliveParticipants.Count;
    public int TotalParticipants => totalParticipants;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Mobile performance: Target 60 FPS and prevent screen sleep
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    private void Start()
    {
        StartNewMatch();
    }

    public void StartNewMatch()
    {
        currentState = GameState.Waiting;
        aliveParticipants.Clear();

        Vector3 playerSpawn;
        List<Vector3> botSpawns;
        
        if (gridGenerator == null)
        {
            gridGenerator = FindObjectOfType<PlatformGridGenerator>();
        }

        LevelConfig levelConfig = LevelManager.Instance != null ? LevelManager.Instance.GetCurrentLevelConfig() : LevelGenerator.GetConfigForLevel(1);
        botCount = levelConfig.botCount;

        // Setup Level Sky, Floating Islands & Theme Colors
        SetupLevelEnvironment(levelConfig.levelNumber);

        // 1. Generate Multi-Layer Platform Arena with LevelConfig & Gaps
        gridGenerator.GenerateGridForLevel(levelConfig, out playerSpawn, out botSpawns);

        float bottomEliminationY = gridGenerator.BottomLayerY;

        string playerName = PlayerPrefs.GetString("PlayerName", "Player");

        // 2. Spawn Player
        if (playerInstance != null) Destroy(playerInstance);

        if (playerPrefab != null)
        {
            playerInstance = Instantiate(playerPrefab, playerSpawn, Quaternion.identity);

            // Apply player's selected character model (e.g. Police Officer, Classic Bean)
            string selectedCharId = CharacterDatabase.GetSelectedCharacterId();
            if (selectedCharId != CharacterDatabase.DEFAULT_CHAR_ID)
            {
                for (int i = playerInstance.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = playerInstance.transform.GetChild(i);
                    if (child.name.StartsWith("Character3DVisual") || child.name.Contains("Bean") || 
                        child.name.Contains("Police") || child.name.Contains("Clone") || child.name.Contains("CH_"))
                    {
                        child.gameObject.SetActive(false);
                        child.name = "DestroyedOldVisual";
                        Destroy(child.gameObject);
                    }
                }

                Animator charAnim;
                GameObject visual = CharacterDatabase.SpawnVisual(selectedCharId, playerInstance.transform, out charAnim);
                Character3DAnimator animBridge = playerInstance.GetComponent<Character3DAnimator>();
                if (animBridge != null && charAnim != null)
                {
                    animBridge.SetupAnimator(charAnim, playerInstance.GetComponent<Rigidbody>());
                }
            }
        }
        else
        {
            playerInstance = CreateCharacterBean(playerSpawn, playerName, new Color(0.12f, 0.65f, 1.0f)); // Blue Bean Player
        }

        playerInstance.name = playerName;

        // Apply saved custom PlayerName to head NameTag
        CharacterNameTag playerTag = playerInstance.GetComponent<CharacterNameTag>();
        if (playerTag == null) playerTag = playerInstance.AddComponent<CharacterNameTag>();
        playerTag.Setup(playerName, new Color(0.33f, 0.92f, 0.22f), 1.80f);

        PlayerController pc = playerInstance.GetComponent<PlayerController>();
        if (pc == null) pc = playerInstance.AddComponent<PlayerController>();
        pc.Initialize(joystick, bottomEliminationY);

        aliveParticipants.Add(playerInstance);

        if (followCamera != null)
        {
            followCamera.SetMinCameraY(bottomEliminationY + 6.0f);
            followCamera.SetTarget(playerInstance.transform);
        }

        List<string> namePool = new List<string>(randomBotNames);
        for (int i = 0; i < namePool.Count; i++)
        {
            int rnd = Random.Range(i, namePool.Count);
            string temp = namePool[i];
            namePool[i] = namePool[rnd];
            namePool[rnd] = temp;
        }

        // 3. Spawn Bots
        for (int i = 0; i < botSpawns.Count; i++)
        {
            GameObject botObj;
            Color botColor = botColors[i % botColors.Length];
            string botName = (i < namePool.Count) ? namePool[i] : $"Bot {i + 1}";

            if (botPrefab != null)
            {
                botObj = Instantiate(botPrefab, botSpawns[i], Quaternion.identity);
            }
            else
            {
                botObj = CreateCharacterBean(botSpawns[i], botName, botColor);
            }

            botObj.name = botName;

            BotController bc = botObj.GetComponent<BotController>();
            if (bc == null) bc = botObj.AddComponent<BotController>();
            bc.Initialize(botName, botColor, bottomEliminationY);

            aliveParticipants.Add(botObj);
        }

        totalParticipants = aliveParticipants.Count;

        if (uiManager != null)
        {
            uiManager.SetLevelTitle(levelConfig.levelNumber);
            uiManager.UpdateAliveCount(aliveParticipants.Count, totalParticipants);
            uiManager.HideGameOver();
            uiManager.HideVictory();
        }

        currentState = GameState.Playing;
    }

    public void OnPlayerEliminated(GameObject playerObj)
    {
        if (currentState != GameState.Playing) return;

        aliveParticipants.Remove(playerObj);
        int rank = aliveParticipants.Count + 1;

        currentState = GameState.GameOver;
        GameDataManager.RecordMatchResult(rank, totalParticipants, false);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayGameOver();
        }

        if (uiManager != null)
        {
            uiManager.UpdateAliveCount(aliveParticipants.Count, totalParticipants);
        }

        StartCoroutine(TransitionToSceneRoutine("GameOverScene", 0.1f));
    }

    public void OnBotEliminated(GameObject botObj)
    {
        if (currentState != GameState.Playing) return;

        aliveParticipants.Remove(botObj);

        if (uiManager != null)
        {
            uiManager.UpdateAliveCount(aliveParticipants.Count, totalParticipants);
            uiManager.ShowEliminationToast(botObj != null ? botObj.name : "Bot");
        }

        // Check Victory condition: only player remains!
        if (aliveParticipants.Count == 1 && aliveParticipants[0] == playerInstance)
        {
            currentState = GameState.Victory;
            GameDataManager.RecordMatchResult(1, totalParticipants, true);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayVictory();
            }

            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.AdvanceToNextLevel();
            }

            // Immediately transition to dedicated WinScene without showing in-game VictoryPanel
            StartCoroutine(TransitionToSceneRoutine("WinScene", 0.2f));
        }
    }

    private IEnumerator TransitionToSceneRoutine(string sceneName, float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneName);
    }

    public void RestartMatch()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private GameObject CreateCharacterBean(Vector3 spawnPos, string name, Color bodyColor)
    {
        GameObject character = CharacterModelBuilder.BuildRunnerCharacter(name, bodyColor, name == "Player");
        character.transform.position = spawnPos;
        return character;
    }

    private Sprite GetLevelBackgroundSprite(int levelNumber)
    {
        int themeIdx = (levelNumber - 1) % 6;
        string resPath = $"Backgrounds/Bg_Level_{themeIdx + 1}";

        // 1. Primary: Load Sprite from Resources (works on Mobile & Standalone build)
        Sprite bgSprite = Resources.Load<Sprite>(resPath);

        // 2. Secondary: If texture imported as Default Texture, load Texture2D & convert to Sprite
        if (bgSprite == null)
        {
            Texture2D tex = Resources.Load<Texture2D>(resPath);
            if (tex != null)
            {
                bgSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }

#if UNITY_EDITOR
        // 3. Editor fallback
        if (bgSprite == null)
        {
            string[] editorAssetPaths = new string[]
            {
                "Assets/UI/Background/Background/ChatGPT Image Sep 30, 2026, 04_55_23 PM.png",
                "Assets/UI/Background/Background/ChatGPT Image Sep 30, 2026, 04_56_02 PM.png",
                "Assets/UI/Background/Background/ChatGPT Image Sep 30, 2026, 04_58_57 PM.png",
                "Assets/UI/Background/Background/ChatGPT Image Sep 30, 2026, 05_25_58 PM.png",
                "Assets/UI/Background/Background/ChatGPT Image Sep 30, 2026, 05_27_54 PM.png",
                "Assets/UI/Background/Background/ChatGPT Image Sep 30, 2026, 05_30_02 PM.png"
            };

            if (themeIdx >= 0 && themeIdx < editorAssetPaths.Length)
            {
                bgSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(editorAssetPaths[themeIdx]);
            }
        }
#endif

        return bgSprite;
    }

    private void SetupLevelEnvironment(int levelNumber)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        int themeIdx = (levelNumber - 1) % 6;
        Color skyColor;
        Color[] floorColors;

        switch (themeIdx)
        {
            case 0: // Level 1: Classic Blue Sky & Floating Platforms
                skyColor = new Color(0.12f, 0.58f, 0.95f);
                floorColors = new Color[] {
                    new Color(0.88f, 0.38f, 0.48f), // Coral Pink
                    new Color(0.20f, 0.75f, 0.95f), // Cyan Blue
                    new Color(0.98f, 0.70f, 0.15f)  // Sun Gold
                };
                break;

            case 1: // Level 2: Lush Green Waterfall & Sky Islands
                skyColor = new Color(0.25f, 0.70f, 0.95f);
                floorColors = new Color[] {
                    new Color(0.16f, 0.80f, 0.45f), // Emerald Green
                    new Color(0.18f, 0.65f, 0.92f), // Aqua Blue
                    new Color(0.85f, 0.88f, 0.20f)  // Spring Lime
                };
                break;

            case 2: // Level 3: Rainbow Sun & Crown Arch
                skyColor = new Color(0.35f, 0.65f, 0.98f);
                floorColors = new Color[] {
                    new Color(0.65f, 0.22f, 0.92f), // Royal Purple
                    new Color(0.20f, 0.60f, 0.98f), // Electric Blue
                    new Color(1.00f, 0.45f, 0.15f)  // Neon Orange
                };
                break;

            case 3: // Level 4: Sunset Lava & Fiery Sky
                skyColor = new Color(0.90f, 0.32f, 0.18f);
                floorColors = new Color[] {
                    new Color(0.95f, 0.25f, 0.18f), // Fiery Crimson
                    new Color(0.24f, 0.21f, 0.26f), // Dark Obsidian
                    new Color(1.00f, 0.62f, 0.10f)  // Lava Gold
                };
                break;

            case 4: // Level 5: Snowy Ice Mountain Winter
                skyColor = new Color(0.35f, 0.58f, 0.88f);
                floorColors = new Color[] {
                    new Color(0.48f, 0.82f, 0.98f), // Ice Cyan
                    new Color(0.20f, 0.45f, 0.82f), // Frost Blue
                    new Color(0.92f, 0.96f, 1.00f)  // Snow White
                };
                break;

            default: // Level 6: Sakura Cherry Blossom Petals
                skyColor = new Color(0.52f, 0.72f, 0.98f);
                floorColors = new Color[] {
                    new Color(0.96f, 0.48f, 0.70f), // Blossom Pink
                    new Color(0.90f, 0.30f, 0.60f), // Magenta Rose
                    new Color(0.40f, 0.90f, 0.72f)  // Mint Leaf
                };
                break;
        }

        // Apply Camera Sky Color
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = skyColor;

        // Apply Dynamic Floor Colors to PlatformGridGenerator
        if (gridGenerator != null)
        {
            gridGenerator.SetLayerNormalColors(floorColors);
        }

        // Load exact background sprite for this level
        Sprite bgSprite = GetLevelBackgroundSprite(levelNumber);

        if (bgSprite != null)
        {
            Transform existingBg = cam.transform.Find("CameraBackgroundPlane");
            GameObject bgObj = existingBg != null ? existingBg.gameObject : new GameObject("CameraBackgroundPlane");
            bgObj.transform.SetParent(cam.transform, false);

            float bgDistance = 45f;
            bgObj.transform.localPosition = new Vector3(0, -2f, bgDistance);
            bgObj.transform.localRotation = Quaternion.identity;

            SpriteRenderer bgSr = bgObj.GetComponent<SpriteRenderer>();
            if (bgSr == null) bgSr = bgObj.AddComponent<SpriteRenderer>();
            bgSr.sprite = bgSprite;
            bgSr.sortingOrder = -300;
            bgSr.color = Color.white; // Crisp high-res artwork

            float fovRad = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float bgHeight = 2f * bgDistance * Mathf.Tan(fovRad);
            float bgWidth = bgHeight * cam.aspect;

            Vector2 spriteSize = bgSprite.bounds.size;
            if (spriteSize.x > 0 && spriteSize.y > 0)
            {
                float scaleFactor = Mathf.Max(bgWidth / spriteSize.x, bgHeight / spriteSize.y) * 1.35f;
                bgObj.transform.localScale = new Vector3(scaleFactor, scaleFactor, 1f);
            }
        }
    }
}
