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

        // 1. Generate Multi-Layer Platform Arena with LevelConfig & Gaps
        gridGenerator.GenerateGridForLevel(levelConfig, out playerSpawn, out botSpawns);

        float bottomEliminationY = gridGenerator.BottomLayerY;

        string playerName = PlayerPrefs.GetString("PlayerName", "Player");

        // 2. Spawn Player
        if (playerInstance != null) Destroy(playerInstance);

        if (playerPrefab != null)
        {
            playerInstance = Instantiate(playerPrefab, playerSpawn, Quaternion.identity);
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

            if (uiManager != null)
            {
                uiManager.ShowVictory();
            }

            StartCoroutine(TransitionToSceneRoutine("WinScene", 1.5f));
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
}
