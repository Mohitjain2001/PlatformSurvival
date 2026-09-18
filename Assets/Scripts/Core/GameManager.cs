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
        // Root Character GameObject with Collider and Rigidbody
        GameObject character = new GameObject(name);
        character.transform.position = spawnPos;

        CapsuleCollider capCol = character.AddComponent<CapsuleCollider>();
        capCol.center = new Vector3(0, 1.0f, 0);
        capCol.radius = 0.45f;
        capCol.height = 2.0f;

        // Visual Body Root
        GameObject bodyRoot = new GameObject("BodyVisual");
        bodyRoot.transform.SetParent(character.transform, false);
        bodyRoot.transform.localPosition = new Vector3(0, 0.9f, 0);

        Material mainMat = new Material(Shader.Find("Standard"));
        mainMat.color = bodyColor;

        Material skinMat = new Material(Shader.Find("Standard"));
        skinMat.color = new Color(0.98f, 0.82f, 0.68f); // Soft skin tone for hands/limbs

        Material darkMat = new Material(Shader.Find("Standard"));
        darkMat.color = new Color(0.2f, 0.2f, 0.2f); // Shoes / eyes

        // 1. Torso
        GameObject torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        torso.name = "Torso";
        DestroyImmediate(torso.GetComponent<Collider>());
        torso.transform.SetParent(bodyRoot.transform, false);
        torso.transform.localPosition = Vector3.zero;
        torso.transform.localScale = new Vector3(0.75f, 0.75f, 0.65f);
        torso.GetComponent<MeshRenderer>().material = mainMat;

        // 2. Head / Visor
        GameObject visor = new GameObject("VisorFace");
        visor.transform.SetParent(bodyRoot.transform, false);
        visor.transform.localPosition = new Vector3(0, 0.42f, 0.32f);
        visor.transform.localScale = new Vector3(0.42f, 0.32f, 1f);

        MeshFilter visorMf = visor.AddComponent<MeshFilter>();
        GameObject tempQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        visorMf.sharedMesh = tempQuad.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(tempQuad);

        MeshRenderer visorMr = visor.AddComponent<MeshRenderer>();
        visorMr.material = new Material(Shader.Find("Standard"));
        visorMr.material.color = new Color(0.96f, 0.96f, 0.96f);

        // Eyes
        GameObject eyeL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        DestroyImmediate(eyeL.GetComponent<Collider>());
        eyeL.name = "EyeL";
        eyeL.transform.SetParent(visor.transform, false);
        eyeL.transform.localPosition = new Vector3(-0.22f, 0.05f, -0.05f);
        eyeL.transform.localScale = new Vector3(0.18f, 0.26f, 0.18f);
        eyeL.GetComponent<MeshRenderer>().material = darkMat;

        GameObject eyeR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        DestroyImmediate(eyeR.GetComponent<Collider>());
        eyeR.name = "EyeR";
        eyeR.transform.SetParent(visor.transform, false);
        eyeR.transform.localPosition = new Vector3(0.22f, 0.05f, -0.05f);
        eyeR.transform.localScale = new Vector3(0.18f, 0.26f, 0.18f);
        eyeR.GetComponent<MeshRenderer>().material = darkMat;

        // 3. Left Arm Pivot & Mesh
        GameObject lArmPivot = new GameObject("ArmPivot_L");
        lArmPivot.transform.SetParent(bodyRoot.transform, false);
        lArmPivot.transform.localPosition = new Vector3(-0.48f, 0.35f, 0f);

        GameObject lArmMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        DestroyImmediate(lArmMesh.GetComponent<Collider>());
        lArmMesh.transform.SetParent(lArmPivot.transform, false);
        lArmMesh.transform.localPosition = new Vector3(0, -0.32f, 0);
        lArmMesh.transform.localScale = new Vector3(0.22f, 0.35f, 0.22f);
        lArmMesh.GetComponent<MeshRenderer>().material = mainMat;

        // 4. Right Arm Pivot & Mesh
        GameObject rArmPivot = new GameObject("ArmPivot_R");
        rArmPivot.transform.SetParent(bodyRoot.transform, false);
        rArmPivot.transform.localPosition = new Vector3(0.48f, 0.35f, 0f);

        GameObject rArmMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        DestroyImmediate(rArmMesh.GetComponent<Collider>());
        rArmMesh.transform.SetParent(rArmPivot.transform, false);
        rArmMesh.transform.localPosition = new Vector3(0, -0.32f, 0);
        rArmMesh.transform.localScale = new Vector3(0.22f, 0.35f, 0.22f);
        rArmMesh.GetComponent<MeshRenderer>().material = mainMat;

        // 5. Left Leg Pivot & Mesh
        GameObject lLegPivot = new GameObject("LegPivot_L");
        lLegPivot.transform.SetParent(character.transform, false);
        lLegPivot.transform.localPosition = new Vector3(-0.24f, 0.55f, 0f);

        GameObject lLegMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        DestroyImmediate(lLegMesh.GetComponent<Collider>());
        lLegMesh.transform.SetParent(lLegPivot.transform, false);
        lLegMesh.transform.localPosition = new Vector3(0, -0.28f, 0);
        lLegMesh.transform.localScale = new Vector3(0.24f, 0.30f, 0.24f);
        lLegMesh.GetComponent<MeshRenderer>().material = mainMat;

        // Left Foot / Shoe
        GameObject lFoot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        DestroyImmediate(lFoot.GetComponent<Collider>());
        lFoot.transform.SetParent(lLegPivot.transform, false);
        lFoot.transform.localPosition = new Vector3(0, -0.52f, 0.08f);
        lFoot.transform.localScale = new Vector3(0.26f, 0.16f, 0.35f);
        lFoot.GetComponent<MeshRenderer>().material = darkMat;

        // 6. Right Leg Pivot & Mesh
        GameObject rLegPivot = new GameObject("LegPivot_R");
        rLegPivot.transform.SetParent(character.transform, false);
        rLegPivot.transform.localPosition = new Vector3(0.24f, 0.55f, 0f);

        GameObject rLegMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        DestroyImmediate(rLegMesh.GetComponent<Collider>());
        rLegMesh.transform.SetParent(rLegPivot.transform, false);
        rLegMesh.transform.localPosition = new Vector3(0, -0.28f, 0);
        rLegMesh.transform.localScale = new Vector3(0.24f, 0.30f, 0.24f);
        rLegMesh.GetComponent<MeshRenderer>().material = mainMat;

        // Right Foot / Shoe
        GameObject rFoot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        DestroyImmediate(rFoot.GetComponent<Collider>());
        rFoot.transform.SetParent(rLegPivot.transform, false);
        rFoot.transform.localPosition = new Vector3(0, -0.52f, 0.08f);
        rFoot.transform.localScale = new Vector3(0.26f, 0.16f, 0.35f);
        rFoot.GetComponent<MeshRenderer>().material = darkMat;

        // 7. Add Procedural Animation Controller
        CharacterProceduralAnimator animator = character.AddComponent<CharacterProceduralAnimator>();
        animator.SetupLimbs(bodyRoot.transform, lArmPivot.transform, rArmPivot.transform, lLegPivot.transform, rLegPivot.transform);

        character.AddComponent<CharacterSquashAndStretch>();

        return character;
    }
}
