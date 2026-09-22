using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformGridGenerator : MonoBehaviour
{
    [Header("Grid Configuration")]
    [SerializeField] private int gridRadius = 6; // Hexagonal grid radius
    [SerializeField] private float tileOuterRadius = 1.2f;
    [SerializeField] private float tileHeight = 0.4f;
    [SerializeField] private float tileSpacing = 0.08f;

    [Header("Multi-Layer Setup")]
    [SerializeField] private int layerCount = 3; // 3 vertical floors like Fall Race 3D!
    [SerializeField] private float layerSpacing = 12.0f; // Vertical distance between floors (generous gap so upper floors never clip camera)

    [Header("Layer Colors")]
    [SerializeField] private Color[] layerNormalColors = new Color[]
    {
        new Color(0.88f, 0.38f, 0.48f), // Layer 1: Coral / Pink (matching user reference image!)
        new Color(0.20f, 0.75f, 0.95f), // Layer 2: Bright Blue / Cyan
        new Color(0.98f, 0.70f, 0.15f)  // Layer 3: Golden Yellow / Orange
    };

    [SerializeField] private Color tileWarningColor = new Color(1.0f, 0.45f, 0.0f); // Warning Orange
    [SerializeField] private Color tileDangerColor = new Color(0.95f, 0.15f, 0.15f); // Red

    [Header("Tile Prefab Configuration")]
    [SerializeField] private GameObject[] layerTilePrefabs; // e.g. [0]=Grass, [1]=Sand, [2]=Stone
    [SerializeField] private GameObject tilePrefab; // Fallback single tile prefab
    [SerializeField] private bool useNaturalModelColors = true; // True for Kenney textures, false for solid layer colors

    private List<PlatformTile> generatedTiles = new List<PlatformTile>();
    private Mesh hexMesh;

    public int LayerCount => layerCount;
    public float BottomLayerY => -(layerCount - 1) * layerSpacing - 4.0f;

    public List<PlatformTile> ActiveTiles
    {
        get
        {
            generatedTiles.RemoveAll(t => t == null || !t.IsAvailable);
            return generatedTiles;
        }
    }

    public static PlatformGridGenerator Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        if (hexMesh == null)
        {
            hexMesh = CreateHexagonMesh(tileOuterRadius - tileSpacing, tileHeight);
        }
    }

    public float CalculateEffectiveRadius(int layerIndex = 0)
    {
        GameObject prefabToUse = null;
        if (layerTilePrefabs != null && layerIndex < layerTilePrefabs.Length && layerTilePrefabs[layerIndex] != null)
        {
            prefabToUse = layerTilePrefabs[layerIndex];
        }
        else if (tilePrefab != null)
        {
            prefabToUse = tilePrefab;
        }

        if (prefabToUse != null)
        {
            float scaleX = prefabToUse.transform.localScale.x;
            return scaleX * (1.0f / Mathf.Sqrt(3)) * 1.04f;
        }

        return tileOuterRadius;
    }

    public void GenerateGrid(out Vector3 playerSpawn, out List<Vector3> botSpawns, int botCount)
    {
        LevelConfig defaultConfig = new LevelConfig
        {
            botCount = botCount,
            floorCount = layerCount,
            gridRadius = gridRadius
        };
        GenerateGridForLevel(defaultConfig, out playerSpawn, out botSpawns);
    }

    public void GenerateGridForLevel(LevelConfig config, out Vector3 playerSpawn, out List<Vector3> botSpawns)
    {
        int effectiveLayerCount = config != null ? config.floorCount : layerCount;
        int effectiveRadiusCount = config != null ? config.gridRadius : gridRadius;
        int botCount = config != null ? config.botCount : 4;
        layerCount = effectiveLayerCount;

        // 1. Check if scene ALREADY has pre-baked tiles in hierarchy (only use static pre-baked in edit mode preview)
        PlatformTile[] existingTiles = GetComponentsInChildren<PlatformTile>(true);
        if (!Application.isPlaying && existingTiles.Length > 0)
        {
            generatedTiles.Clear();
            List<Vector3> topSpawnPoints = new List<Vector3>();

            for (int i = 0; i < existingTiles.Length; i++)
            {
                PlatformTile tile = existingTiles[i];
                tile.ResetTile();
                generatedTiles.Add(tile);

                // Top layer tiles (Y near 0) are spawn candidates
                if (Mathf.Abs(tile.Position.y) < 1.0f)
                {
                    topSpawnPoints.Add(tile.Position + Vector3.up * 2.0f);
                }
            }

            // Shuffle top layer spawn points
            for (int i = 0; i < topSpawnPoints.Count; i++)
            {
                int rnd = Random.Range(i, topSpawnPoints.Count);
                Vector3 temp = topSpawnPoints[i];
                topSpawnPoints[i] = topSpawnPoints[rnd];
                topSpawnPoints[rnd] = temp;
            }

            playerSpawn = Vector3.up * 2.0f;
            botSpawns = new List<Vector3>();

            int sIdx = 0;
            if (topSpawnPoints.Count > 0)
            {
                playerSpawn = topSpawnPoints[0];
                sIdx = 1;
            }

            for (int i = 0; i < botCount; i++)
            {
                if (sIdx < topSpawnPoints.Count)
                {
                    botSpawns.Add(topSpawnPoints[sIdx]);
                    sIdx++;
                }
                else
                {
                    botSpawns.Add(Vector3.up * 2.0f + new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f)));
                }
            }
            return;
        }

        // 2. Procedural generation at runtime with floor patterns & gaps
        ClearGrid();
        generatedTiles.Clear();

        List<Vector3> topLayerSpawnPoints = new List<Vector3>();

        for (int layer = 0; layer < effectiveLayerCount; layer++)
        {
            float effectiveRadius = CalculateEffectiveRadius(layer);
            float xSpacing = Mathf.Sqrt(3) * effectiveRadius;
            float zSpacing = 1.5f * effectiveRadius;

            float layerY = -layer * layerSpacing;
            GameObject layerParent = new GameObject($"Layer_{layer + 1}");
            layerParent.transform.SetParent(transform);

            Color baseColor = layerNormalColors[layer % layerNormalColors.Length];

            FloorPattern pattern = FloorPattern.Solid;
            if (config != null && config.floorPatterns != null && config.floorPatterns.Length > 0)
            {
                pattern = config.floorPatterns[layer % config.floorPatterns.Length];
            }

            for (int q = -effectiveRadiusCount; q <= effectiveRadiusCount; q++)
            {
                int r1 = Mathf.Max(-effectiveRadiusCount, -q - effectiveRadiusCount);
                int r2 = Mathf.Min(effectiveRadiusCount, -q + effectiveRadiusCount);

                for (int r = r1; r <= r2; r++)
                {
                    // Check if tile should be spawned or left as a gap ("gali")!
                    if (!LevelGenerator.ShouldSpawnTile(q, r, effectiveRadiusCount, pattern))
                    {
                        continue; // SKIP TILE -> CREATES GAP IN PLATFORM!
                    }

                    float x = xSpacing * (q + r / 2.0f);
                    float z = zSpacing * r;

                    Vector3 pos = new Vector3(x, layerY, z);
                    GameObject tileObj = CreateTileObject(pos, layerParent.transform, layer);
                    PlatformTile tile = tileObj.GetComponent<PlatformTile>();
                    if (tile == null) tile = tileObj.AddComponent<PlatformTile>();
                    Color normalCol = useNaturalModelColors ? Color.white : baseColor;
                    tile.SetColors(normalCol, tileWarningColor, tileDangerColor);
                    tile.SetInitialPosition(pos);
                    tile.SetInitialScale(tileObj.transform.localScale);

                    generatedTiles.Add(tile);

                    if (layer == 0)
                    {
                        topLayerSpawnPoints.Add(pos + Vector3.up * 2.0f);
                    }
                }
            }
        }

        // Shuffle top layer spawn points for Player and Bots
        for (int i = 0; i < topLayerSpawnPoints.Count; i++)
        {
            int rnd = Random.Range(i, topLayerSpawnPoints.Count);
            Vector3 temp = topLayerSpawnPoints[i];
            topLayerSpawnPoints[i] = topLayerSpawnPoints[rnd];
            topLayerSpawnPoints[rnd] = temp;
        }

        playerSpawn = Vector3.up * 2.0f;
        botSpawns = new List<Vector3>();

        int spawnIndex = 0;
        if (topLayerSpawnPoints.Count > 0)
        {
            playerSpawn = topLayerSpawnPoints[0];
            spawnIndex = 1;
        }

        for (int i = 0; i < botCount; i++)
        {
            if (spawnIndex < topLayerSpawnPoints.Count)
            {
                botSpawns.Add(topLayerSpawnPoints[spawnIndex]);
                spawnIndex++;
            }
            else
            {
                botSpawns.Add(Vector3.up * 2.0f + new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f)));
            }
        }
    }

    [ContextMenu("Bake Grid in Scene")]
    public void BakeGridInScene()
    {
        ClearGrid();

        if (hexMesh == null)
        {
            hexMesh = CreateHexagonMesh(tileOuterRadius - tileSpacing, tileHeight);
        }

        for (int layer = 0; layer < layerCount; layer++)
        {
            float effectiveRadius = CalculateEffectiveRadius(layer);
            float xSpacing = Mathf.Sqrt(3) * effectiveRadius;
            float zSpacing = 1.5f * effectiveRadius;

            float layerY = -layer * layerSpacing;
            GameObject layerParent = new GameObject($"Layer_{layer + 1}");
            layerParent.transform.SetParent(transform);

            Color baseColor = layerNormalColors[layer % layerNormalColors.Length];

            for (int q = -gridRadius; q <= gridRadius; q++)
                {
                int r1 = Mathf.Max(-gridRadius, -q - gridRadius);
                int r2 = Mathf.Min(gridRadius, -q + gridRadius);

                for (int r = r1; r <= r2; r++)
                {
                    float x = xSpacing * (q + r / 2.0f);
                    float z = zSpacing * r;

                    Vector3 pos = new Vector3(x, layerY, z);
                    GameObject tileObj = CreateTileObject(pos, layerParent.transform, layer);
                    PlatformTile tile = tileObj.GetComponent<PlatformTile>();
                    if (tile == null) tile = tileObj.AddComponent<PlatformTile>();
                    Color normalCol = useNaturalModelColors ? Color.white : baseColor;
                    tile.SetColors(normalCol, tileWarningColor, tileDangerColor);
                    tile.SetInitialPosition(pos);
                    tile.SetInitialScale(tileObj.transform.localScale);
                }
            }
        }
        Debug.Log($"Successfully baked {layerCount} floors of Hexagonal Grid into the scene!");
    }

    [ContextMenu("Clear Grid")]
    public void ClearGrid()
    {
        List<GameObject> children = new List<GameObject>();
        foreach (Transform child in transform)
        {
            children.Add(child.gameObject);
        }
        for (int i = children.Count - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(children[i]);
                continue;
            }
#endif
            Destroy(children[i]);
        }
        generatedTiles.Clear();
    }

    private GameObject CreateTileObject(Vector3 position, Transform parent, int layerIndex)
    {
        GameObject prefabToUse = null;
        if (layerTilePrefabs != null && layerTilePrefabs.Length > 0)
        {
            int pIdx = Mathf.Abs(layerIndex) % layerTilePrefabs.Length;
            if (layerTilePrefabs[pIdx] != null)
            {
                prefabToUse = layerTilePrefabs[pIdx];
            }
        }

        if (prefabToUse == null && tilePrefab != null)
        {
            prefabToUse = tilePrefab;
        }

        if (prefabToUse != null)
        {
            GameObject tileObj = Instantiate(prefabToUse, position, Quaternion.identity, parent);
            tileObj.name = $"HexTile_L{layerIndex + 1}";
            return tileObj;
        }

        GameObject fallbackObj = new GameObject($"HexTile_L{layerIndex + 1}");
        fallbackObj.transform.SetParent(parent);
        fallbackObj.transform.position = position;

        MeshFilter mf = fallbackObj.AddComponent<MeshFilter>();
        mf.sharedMesh = hexMesh;

        MeshRenderer mr = fallbackObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(Shader.Find("Standard"));

        MeshCollider mc = fallbackObj.AddComponent<MeshCollider>();
        mc.sharedMesh = hexMesh;
        mc.convex = true;

        fallbackObj.layer = LayerMask.NameToLayer("Default");
        return fallbackObj;
    }

    public static Mesh CreateHexagonMesh(float radius, float height)
    {
        Mesh mesh = new Mesh();
        mesh.name = "ProceduralHexagon";

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector3> normals = new List<Vector3>();

        // Top face center
        vertices.Add(new Vector3(0, height / 2f, 0));
        normals.Add(Vector3.up);

        // Top face perimeter (6 vertices)
        for (int i = 0; i < 6; i++)
        {
            float angleDeg = 60 * i - 30;
            float angleRad = Mathf.Deg2Rad * angleDeg;
            vertices.Add(new Vector3(radius * Mathf.Cos(angleRad), height / 2f, radius * Mathf.Sin(angleRad)));
            normals.Add(Vector3.up);
        }

        // Top face triangles
        for (int i = 1; i <= 6; i++)
        {
            int next = (i % 6) + 1;
            triangles.Add(0);
            triangles.Add(i);
            triangles.Add(next);
        }

        // Bottom face center
        int bottomCenterIndex = vertices.Count;
        vertices.Add(new Vector3(0, -height / 2f, 0));
        normals.Add(Vector3.down);

        // Bottom face perimeter (6 vertices)
        int bottomStart = vertices.Count;
        for (int i = 0; i < 6; i++)
        {
            float angleDeg = 60 * i - 30;
            float angleRad = Mathf.Deg2Rad * angleDeg;
            vertices.Add(new Vector3(radius * Mathf.Cos(angleRad), -height / 2f, radius * Mathf.Sin(angleRad)));
            normals.Add(Vector3.down);
        }

        // Bottom face triangles
        for (int i = 0; i < 6; i++)
        {
            int curr = bottomStart + i;
            int next = bottomStart + ((i + 1) % 6);
            triangles.Add(bottomCenterIndex);
            triangles.Add(next);
            triangles.Add(curr);
        }

        // Side faces
        for (int i = 0; i < 6; i++)
        {
            int topCurr = i + 1;
            int topNext = (i % 6) + 1;
            int botCurr = bottomStart + i;
            int botNext = bottomStart + ((i + 1) % 6);

            Vector3 edge1 = vertices[topNext] - vertices[topCurr];
            Vector3 edge2 = vertices[botCurr] - vertices[topCurr];
            Vector3 sideNormal = Vector3.Cross(edge1, edge2).normalized;

            int sideStart = vertices.Count;
            vertices.Add(vertices[topCurr]);
            vertices.Add(vertices[topNext]);
            vertices.Add(vertices[botNext]);
            vertices.Add(vertices[botCurr]);

            for (int k = 0; k < 4; k++) normals.Add(sideNormal);

            triangles.Add(sideStart);
            triangles.Add(sideStart + 1);
            triangles.Add(sideStart + 2);

            triangles.Add(sideStart);
            triangles.Add(sideStart + 2);
            triangles.Add(sideStart + 3);
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}
