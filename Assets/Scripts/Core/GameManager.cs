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

    [Header("Bot Colors")]
    [SerializeField] private Color[] botColors = new Color[]
    {
        new Color(0.95f, 0.26f, 0.21f), // Red
        new Color(0.61f, 0.15f, 0.69f), // Purple
        new Color(1.00f, 0.60f, 0.00f), // Orange
        new Color(1.00f, 0.92f, 0.23f), // Yellow
        new Color(0.30f, 0.69f, 0.31f)  // Green
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
    }

    private void Start()
    {
        StartNewMatch();
    }

    public void StartNewMatch()
    {
        currentState = GameState.Waiting;
        aliveParticipants.Clear();

        // 1. Generate Platform Grid
        Vector3 playerSpawn;
        List<Vector3> botSpawns;
        
        if (gridGenerator == null)
        {
            gridGenerator = FindObjectOfType<PlatformGridGenerator>();
        }

        gridGenerator.GenerateGrid(out playerSpawn, out botSpawns, botCount);

        // 2. Spawn Player
        if (playerInstance != null) Destroy(playerInstance);

        if (playerPrefab != null)
        {
            playerInstance = Instantiate(playerPrefab, playerSpawn, Quaternion.identity);
        }
        else
        {
            playerInstance = CreatePrimitivePlayer(playerSpawn);
        }

        PlayerController pc = playerInstance.GetComponent<PlayerController>();
        if (pc != null) pc.Initialize(joystick);

        aliveParticipants.Add(playerInstance);

        if (followCamera != null)
        {
            followCamera.SetTarget(playerInstance.transform);
        }

        // 3. Spawn Bots
        for (int i = 0; i < botSpawns.Count; i++)
        {
            GameObject botObj;
            if (botPrefab != null)
            {
                botObj = Instantiate(botPrefab, botSpawns[i], Quaternion.identity);
            }
            else
            {
                botObj = CreatePrimitiveBot(botSpawns[i]);
            }

            BotController bc = botObj.GetComponent<BotController>();
            Color color = botColors[i % botColors.Length];
            if (bc != null) bc.Initialize("Bot " + (i + 1), color);

            aliveParticipants.Add(botObj);
        }

        totalParticipants = aliveParticipants.Count;

        if (uiManager != null)
        {
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

        if (uiManager != null)
        {
            uiManager.UpdateAliveCount(aliveParticipants.Count, totalParticipants);
            uiManager.ShowGameOver(rank, totalParticipants);
        }

        currentState = GameState.GameOver;
    }

    public void OnBotEliminated(GameObject botObj)
    {
        if (currentState != GameState.Playing) return;

        aliveParticipants.Remove(botObj);

        if (uiManager != null)
        {
            uiManager.UpdateAliveCount(aliveParticipants.Count, totalParticipants);
        }

        // Check Victory condition: only player remains!
        if (aliveParticipants.Count == 1 && aliveParticipants[0] == playerInstance)
        {
            currentState = GameState.Victory;
            if (uiManager != null)
            {
                uiManager.ShowVictory();
            }
        }
    }

    public void RestartMatch()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private GameObject CreatePrimitivePlayer(Vector3 spawnPos)
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.tag = "Player";
        player.transform.position = spawnPos;

        MeshRenderer mr = player.GetComponent<MeshRenderer>();
        mr.material = new Material(Shader.Find("Standard"));
        mr.material.color = new Color(0.1f, 0.6f, 1.0f); // Bright Blue

        player.AddComponent<CharacterSquashAndStretch>();
        player.AddComponent<PlayerController>();

        return player;
    }

    private GameObject CreatePrimitiveBot(Vector3 spawnPos)
    {
        GameObject bot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bot.name = "Bot";
        bot.tag = "Bot";
        bot.transform.position = spawnPos;

        MeshRenderer mr = bot.GetComponent<MeshRenderer>();
        mr.material = new Material(Shader.Find("Standard"));

        bot.AddComponent<CharacterSquashAndStretch>();
        bot.AddComponent<BotController>();

        return bot;
    }
}
