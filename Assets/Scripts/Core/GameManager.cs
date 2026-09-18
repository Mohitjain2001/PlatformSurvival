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
        new Color(0.95f, 0.25f, 0.20f), // Vibrant Red
        new Color(0.60f, 0.20f, 0.85f), // Royal Purple
        new Color(1.00f, 0.55f, 0.00f), // Neon Orange
        new Color(0.98f, 0.85f, 0.10f), // Sunny Yellow
        new Color(0.20f, 0.80f, 0.35f)  // Emerald Green
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

        Vector3 playerSpawn;
        List<Vector3> botSpawns;
        
        if (gridGenerator == null)
        {
            gridGenerator = FindObjectOfType<PlatformGridGenerator>();
        }

        // 1. Generate Multi-Layer Platform Arena
        gridGenerator.GenerateGrid(out playerSpawn, out botSpawns, botCount);

        float bottomEliminationY = gridGenerator.BottomLayerY;

        // 2. Spawn Player
        if (playerInstance != null) Destroy(playerInstance);

        if (playerPrefab != null)
        {
            playerInstance = Instantiate(playerPrefab, playerSpawn, Quaternion.identity);
        }
        else
        {
            playerInstance = CreateCharacterBean(playerSpawn, "Player", new Color(0.12f, 0.65f, 1.0f)); // Blue Bean Player
        }

        PlayerController pc = playerInstance.GetComponent<PlayerController>();
        if (pc == null) pc = playerInstance.AddComponent<PlayerController>();
        pc.Initialize(joystick, bottomEliminationY);

        aliveParticipants.Add(playerInstance);

        if (followCamera != null)
        {
            followCamera.SetMinCameraY(bottomEliminationY + 6.0f);
            followCamera.SetTarget(playerInstance.transform);
        }

        // 3. Spawn Bots
        for (int i = 0; i < botSpawns.Count; i++)
        {
            GameObject botObj;
            Color botColor = botColors[i % botColors.Length];

            if (botPrefab != null)
            {
                botObj = Instantiate(botPrefab, botSpawns[i], Quaternion.identity);
            }
            else
            {
                botObj = CreateCharacterBean(botSpawns[i], "Bot_" + (i + 1), botColor);
            }

            BotController bc = botObj.GetComponent<BotController>();
            if (bc == null) bc = botObj.AddComponent<BotController>();
            bc.Initialize("Bot " + (i + 1), botColor, bottomEliminationY);

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

        currentState = GameState.GameOver;
        GameDataManager.RecordMatchResult(rank, totalParticipants, false);

        if (uiManager != null)
        {
            uiManager.UpdateAliveCount(aliveParticipants.Count, totalParticipants);
            uiManager.ShowGameOver(rank, totalParticipants);
        }

        StartCoroutine(TransitionToSceneRoutine("GameOverScene", 1.5f));
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
            GameDataManager.RecordMatchResult(1, totalParticipants, true);

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
        GameObject character = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        character.name = name;
        character.transform.position = spawnPos;

        MeshRenderer mr = character.GetComponent<MeshRenderer>();
        mr.material = new Material(Shader.Find("Standard"));
        mr.material.color = bodyColor;

        // Add cute Face / Visor (white oval with black eyes) like Fall Guys beans!
        GameObject visor = new GameObject("VisorFace");
        visor.transform.SetParent(character.transform);
        visor.transform.localPosition = new Vector3(0, 0.45f, 0.48f);
        visor.transform.localScale = new Vector3(0.45f, 0.35f, 1f);
        visor.transform.localRotation = Quaternion.Euler(0, 0, 0);

        MeshFilter visorMf = visor.AddComponent<MeshFilter>();
        GameObject tempQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        visorMf.sharedMesh = tempQuad.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(tempQuad);

        MeshRenderer visorMr = visor.AddComponent<MeshRenderer>();
        visorMr.material = new Material(Shader.Find("Standard"));
        visorMr.material.color = new Color(0.95f, 0.95f, 0.95f); // Off-white visor faceplate

        // Left Eye
        GameObject eyeL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        DestroyImmediate(eyeL.GetComponent<Collider>());
        eyeL.name = "EyeL";
        eyeL.transform.SetParent(visor.transform);
        eyeL.transform.localPosition = new Vector3(-0.22f, 0.05f, -0.05f);
        eyeL.transform.localScale = new Vector3(0.18f, 0.25f, 0.18f);
        eyeL.GetComponent<MeshRenderer>().material.color = Color.black;

        // Right Eye
        GameObject eyeR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        DestroyImmediate(eyeR.GetComponent<Collider>());
        eyeR.name = "EyeR";
        eyeR.transform.SetParent(visor.transform);
        eyeR.transform.localPosition = new Vector3(0.22f, 0.05f, -0.05f);
        eyeR.transform.localScale = new Vector3(0.18f, 0.25f, 0.18f);
        eyeR.GetComponent<MeshRenderer>().material.color = Color.black;

        character.AddComponent<CharacterSquashAndStretch>();

        return character;
    }
}
