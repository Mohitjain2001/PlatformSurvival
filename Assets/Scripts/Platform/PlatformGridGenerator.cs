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
    [SerializeField] private float layerSpacing = 5.0f; // Vertical distance between floors

    [Header("Layer Colors")]
    [SerializeField] private Color[] layerNormalColors = new Color[]
    {
        new Color(0.20f, 0.75f, 0.95f), // Layer 1: Bright Blue / Cyan
        new Color(0.98f, 0.70f, 0.15f), // Layer 2: Golden Yellow / Orange
        new Color(0.90f, 0.25f, 0.75f)  // Layer 3: Pink / Magenta
    };

    [SerializeField] private Color tileWarningColor = new Color(1.0f, 0.45f, 0.0f); // Warning Orange
    [SerializeField] private Color tileDangerColor = new Color(0.95f, 0.15f, 0.15f); // Red

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

    private void Awake()
    {
        hexMesh = CreateHexagonMesh(tileOuterRadius - tileSpacing, tileHeight);
    }

    public void GenerateGrid(out Vector3 playerSpawn, out List<Vector3> botSpawns, int botCount)
    {
        // Clear previous children
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        generatedTiles.Clear();

        float effectiveRadius = tileOuterRadius;
        float xSpacing = Mathf.Sqrt(3) * effectiveRadius;
        float zSpacing = 1.5f * effectiveRadius;

        List<Vector3> topLayerSpawnPoints = new List<Vector3>();

        // Generate Multiple Floors (Layers)
        for (int layer = 0; layer < layerCount; layer++)
        {
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
                    GameObject tileObj = CreateTileObject(pos, layerParent.transform);
                    PlatformTile tile = tileObj.AddComponent<PlatformTile>();
                    tile.SetColors(baseColor, tileWarningColor, tileDangerColor);

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

    private GameObject CreateTileObject(Vector3 position, Transform parent)
    {
        GameObject tileObj = new GameObject("HexTile");
        tileObj.transform.SetParent(parent);
        tileObj.transform.position = position;

        MeshFilter mf = tileObj.AddComponent<MeshFilter>();
        mf.sharedMesh = hexMesh;

        MeshRenderer mr = tileObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(Shader.Find("Standard"));

        MeshCollider mc = tileObj.AddComponent<MeshCollider>();
        mc.sharedMesh = hexMesh;
        mc.convex = true;

        tileObj.layer = LayerMask.NameToLayer("Default");
        return tileObj;
    }

    private Mesh CreateHexagonMesh(float radius, float height)
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
        mesh.SetNormals(normals);
        mesh.RecalculateBounds();

        return mesh;
    }
}
