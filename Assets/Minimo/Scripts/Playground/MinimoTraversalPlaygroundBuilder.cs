using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
[ExecuteAlways]
[AddComponentMenu("Minimo/Traversal Playground Builder")]
public class MinimoTraversalPlaygroundBuilder : MonoBehaviour
{
    private const string RootName = "MinimoTraversalPlayground_Root";

    [Header("Build")]
    [SerializeField] private bool autoBuildOnAwake = false;
    [SerializeField] private bool autoBuildOnSceneLoad = false;
    [SerializeField] private bool autoBuildInEditMode = false;
    [SerializeField] private bool rebuildIfRootExists = true;
    [SerializeField] private bool buildExtendedCourse = true;
    [SerializeField] private Vector3 courseOrigin = new Vector3(80f, 0f, 0f);
    [SerializeField] [Min(0.25f)] private float courseScale = 1f;

    [Header("Visual")]
    [SerializeField] private Material overrideMaterial;
    [SerializeField] private bool useSectionColors = true;
    [SerializeField] private bool useMeshColliderOnRamps = true;
    [SerializeField] private bool drawCourseGizmos = true;
    [SerializeField] private Color groundColor = new Color(0.91f, 0.89f, 0.82f, 1f);
    [SerializeField] private Color airDashColor = new Color(0.25f, 0.85f, 0.95f, 1f);
    [SerializeField] private Color diveRollColor = new Color(1f, 0.58f, 0.28f, 1f);
    [SerializeField] private Color slopeColor = new Color(0.35f, 0.92f, 0.52f, 1f);
    [SerializeField] private Color stepColor = new Color(0.78f, 0.47f, 0.98f, 1f);
    [SerializeField] private Color movingPlatformColor = new Color(0.99f, 0.73f, 0.26f, 1f);
    [SerializeField] private Color skyIslandsColor = new Color(0.33f, 0.63f, 1f, 1f);
    [SerializeField] private Color rhythmLaneColor = new Color(1f, 0.31f, 0.67f, 1f);

    [Header("Moving Platform")]
    [SerializeField] [Min(0f)] private float movingPlatformVerticalDistance = 2f;
    [SerializeField] [Min(0.25f)] private float movingPlatformCycleDuration = 3.2f;
    [SerializeField] [Min(0f)] private float movingPlatformBaseRotationSpeed = 90f;

    private bool isBuilding;
    private MaterialPropertyBlock sharedPropertyBlock;
    public bool AutoBuildOnSceneLoad => autoBuildOnSceneLoad;

    private void Awake()
    {
        if (Application.isPlaying && autoBuildOnAwake)
        {
            BuildPlayground();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying || !autoBuildInEditMode)
        {
            return;
        }

        BuildPlayground();
    }
#endif

    [ContextMenu("Build Playground")]
    public void BuildPlayground()
    {
        if (isBuilding)
        {
            return;
        }

        isBuilding = true;
        try
        {
            Transform root = transform.Find(RootName);
            if (root != null && !rebuildIfRootExists)
            {
                EnsureMovingPlatformLane(root);
                return;
            }

            if (root != null)
            {
                DestroySmart(root.gameObject);
            }

            root = new GameObject(RootName).transform;
            root.SetParent(transform, false);
            root.localPosition = courseOrigin;

            BuildGround(root);
            BuildAirDashLane(root);
            BuildDiveAndRollLane(root);
            BuildSlopeSlideLane(root);
            BuildStepLane(root);
            BuildMovingPlatformLane(root);

            if (buildExtendedCourse)
            {
                BuildSkyIslandsLane(root);
                BuildRhythmLane(root);
            }
        }
        finally
        {
            isBuilding = false;
        }
    }

    [ContextMenu("Clear Playground")]
    public void ClearPlayground()
    {
        Transform root = transform.Find(RootName);
        if (root != null)
        {
            DestroySmart(root.gameObject);
        }
    }

    private void BuildGround(Transform root)
    {
        CreateBlock(root, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(240f, 1f, 240f), groundColor);
        CreateBlock(root, "StartPad", new Vector3(-12f, 0.5f, 0f), new Vector3(14f, 1f, 14f), groundColor);
        CreateBlock(root, "CenterHub", new Vector3(16f, 0.5f, 0f), new Vector3(18f, 1f, 18f), Color.Lerp(groundColor, Color.white, 0.12f));
    }

    private void BuildAirDashLane(Transform root)
    {
        Transform lane = CreateGroup(root, "AirDashLane", new Vector3(0f, 0f, -42f));
        CreateBlock(lane, "RunUp", new Vector3(-18f, 0.5f, 0f), new Vector3(14f, 1f, 12f), airDashColor);

        for (int i = 0; i < 12; i++)
        {
            float z = i * 6f;
            float y = 1.6f + Mathf.Sin(i * 0.9f) * 1.3f;
            float x = (i % 3 - 1) * 3.6f;
            Color tint = Color.Lerp(airDashColor, Color.white, (i % 4) * 0.12f);
            CreateBlock(lane, $"DashPad_{i}", new Vector3(x, y, z), new Vector3(5f, 0.55f, 4.6f), tint);

            if (i % 2 == 0)
            {
                CreateBlock(lane, $"DashGateL_{i}", new Vector3(x - 3.2f, y + 1.35f, z), new Vector3(0.45f, 2.6f, 0.45f), tint);
                CreateBlock(lane, $"DashGateR_{i}", new Vector3(x + 3.2f, y + 1.35f, z), new Vector3(0.45f, 2.6f, 0.45f), tint);
            }
        }

        CreateRamp(lane, "DashExitRamp", new Vector3(0f, 1.2f, 74f), new Vector3(10f, 1f, 20f), new Vector3(-21f, 0f, 0f), airDashColor);
    }

    private void BuildDiveAndRollLane(Transform root)
    {
        Transform lane = CreateGroup(root, "DiveRollLane", new Vector3(46f, 0f, 0f));
        CreateBlock(lane, "TowerColumn", new Vector3(0f, 6f, 0f), new Vector3(5f, 12f, 5f), diveRollColor);
        CreateBlock(lane, "TowerTop", new Vector3(0f, 12.5f, 0f), new Vector3(11f, 1f, 11f), Color.Lerp(diveRollColor, Color.white, 0.18f));
        CreateBlock(lane, "LandingStrip", new Vector3(0f, 0.5f, 24f), new Vector3(14f, 1f, 46f), diveRollColor);

        for (int i = 0; i < 7; i++)
        {
            float z = 7f + i * 5.3f;
            float side = i % 2 == 0 ? -1f : 1f;
            float height = 0.9f + (i % 3) * 0.45f;
            CreateBlock(lane, $"DiveRollObstacle_{i}", new Vector3(side * 3.8f, height, z), new Vector3(1.9f, 1.8f + i * 0.08f, 2.2f), Color.Lerp(diveRollColor, Color.black, 0.1f));
        }

        CreateRamp(lane, "RollExitRamp", new Vector3(0f, 1f, 48f), new Vector3(12f, 1f, 16f), new Vector3(-18f, 0f, 0f), diveRollColor);
    }

    private void BuildSlopeSlideLane(Transform root)
    {
        Transform lane = CreateGroup(root, "SlopeSlideLane", new Vector3(-58f, 0f, -4f));
        CreateBlock(lane, "SlopeStart", new Vector3(0f, 0.5f, -30f), new Vector3(18f, 1f, 12f), slopeColor);
        CreateRamp(lane, "MainSlope", new Vector3(0f, 8.2f, 2f), new Vector3(18f, 1f, 48f), new Vector3(-30f, 0f, 0f), slopeColor);
        CreateRamp(lane, "SecondSlope", new Vector3(0f, 4.6f, 33f), new Vector3(18f, 1f, 26f), new Vector3(-21f, 0f, 0f), Color.Lerp(slopeColor, Color.white, 0.16f));
        CreateBlock(lane, "FlatRunout", new Vector3(0f, 0.5f, 58f), new Vector3(20f, 1f, 32f), slopeColor);
        CreateRamp(lane, "BhopKicker", new Vector3(0f, 1.35f, 79f), new Vector3(13f, 1f, 11f), new Vector3(-19f, 0f, 0f), slopeColor);

        for (int i = 0; i < 8; i++)
        {
            float z = -12f + i * 10f;
            float y = 1.2f + i * 1.05f;
            CreateBlock(lane, $"SlopeRailL_{i}", new Vector3(-9.8f, y, z), new Vector3(0.65f, 2f, 6f), Color.Lerp(slopeColor, Color.black, 0.16f));
            CreateBlock(lane, $"SlopeRailR_{i}", new Vector3(9.8f, y, z), new Vector3(0.65f, 2f, 6f), Color.Lerp(slopeColor, Color.black, 0.16f));
        }
    }

    private void BuildStepLane(Transform root)
    {
        Transform lane = CreateGroup(root, "StepLane", new Vector3(0f, 0f, 46f));
        CreateBlock(lane, "StepRunUp", new Vector3(0f, 0.5f, -12f), new Vector3(14f, 1f, 14f), stepColor);

        float x = -7.2f;
        for (int i = 0; i < 20; i++)
        {
            float y = 0.2f + i * 0.13f;
            float z = i * 2.3f;
            float h = 0.35f + i * 0.05f;
            Color tint = Color.Lerp(stepColor, Color.white, (i % 6) * 0.08f);
            CreateBlock(lane, $"Step_{i}", new Vector3(x, y, z), new Vector3(2.2f, h, 2f), tint);
            x += 0.78f;
        }

        CreateBlock(lane, "StepFinish", new Vector3(8.4f, 3.8f, 42f), new Vector3(14f, 1f, 10f), stepColor);
    }

    private void BuildMovingPlatformLane(Transform root)
    {
        Transform lane = CreateGroup(root, "MovingPlatformLane", new Vector3(38f, 0f, 24f));
        CreateBlock(lane, "MovingPlatformStart", new Vector3(-8f, 0.5f, -2f), new Vector3(8f, 1f, 8f), movingPlatformColor);
        CreateBlock(lane, "MovingPlatformFinish", new Vector3(17f, 4f, 10f), new Vector3(9f, 1f, 9f), movingPlatformColor);

        float phaseStep = movingPlatformCycleDuration * 0.32f;
        CreateMovingPlatform(lane, "TraversalPlatform_A", new Vector3(-1f, 1.3f, 0f), new Vector3(4.8f, 0.75f, 4.8f), movingPlatformColor, 0f, movingPlatformBaseRotationSpeed);
        CreateMovingPlatform(lane, "TraversalPlatform_B", new Vector3(6.5f, 2.2f, 3f), new Vector3(4.6f, 0.75f, 4.6f), movingPlatformColor, phaseStep, -movingPlatformBaseRotationSpeed * 0.9f);
        CreateMovingPlatform(lane, "TraversalPlatform_C", new Vector3(13.2f, 3f, 6.8f), new Vector3(5f, 0.75f, 5f), movingPlatformColor, phaseStep * 2f, movingPlatformBaseRotationSpeed * 1.1f);
    }

    private void EnsureMovingPlatformLane(Transform root)
    {
        Transform existingLane = root.Find("MovingPlatformLane");
        if (existingLane != null)
        {
            DestroySmart(existingLane.gameObject);
        }

        BuildMovingPlatformLane(root);
    }

    private void BuildSkyIslandsLane(Transform root)
    {
        Transform lane = CreateGroup(root, "SkyIslandsLane", new Vector3(78f, 0f, -44f));
        CreateBlock(lane, "SkyStart", new Vector3(-8f, 1f, -4f), new Vector3(10f, 2f, 10f), skyIslandsColor);

        for (int i = 0; i < 16; i++)
        {
            float arcT = i / 15f;
            float x = Mathf.Lerp(-4f, 38f, arcT);
            float z = Mathf.Lerp(0f, 62f, arcT) + Mathf.Sin(i * 0.8f) * 2.8f;
            float y = 3f + Mathf.Sin(arcT * Mathf.PI * 1.2f) * 6f + (i % 2 == 0 ? 0.6f : -0.25f);
            float s = 3.4f + (i % 3) * 0.55f;
            CreateBlock(lane, $"SkyIsland_{i}", new Vector3(x, y, z), new Vector3(s, 0.7f, s), Color.Lerp(skyIslandsColor, Color.white, arcT * 0.35f));
        }

        CreateRamp(lane, "SkyExitRamp", new Vector3(42f, 2.2f, 66f), new Vector3(9f, 1f, 14f), new Vector3(-18f, 14f, 0f), skyIslandsColor);
    }

    private void BuildRhythmLane(Transform root)
    {
        Transform lane = CreateGroup(root, "RhythmLane", new Vector3(-92f, 0f, 48f));
        CreateBlock(lane, "RhythmStart", new Vector3(0f, 0.5f, -12f), new Vector3(13f, 1f, 13f), rhythmLaneColor);

        for (int i = 0; i < 22; i++)
        {
            float x = (i % 2 == 0 ? -3.4f : 3.4f);
            float y = 0.9f + (i % 5) * 0.35f;
            float z = i * 3.9f;
            float w = i % 3 == 0 ? 5.2f : 4.2f;
            float d = i % 4 == 0 ? 6.1f : 4.4f;
            Color tint = Color.Lerp(rhythmLaneColor, Color.white, (i % 7) * 0.07f);
            CreateBlock(lane, $"RhythmPad_{i}", new Vector3(x, y, z), new Vector3(w, 0.7f, d), tint);
        }

        CreateBlock(lane, "RhythmFinish", new Vector3(0f, 2.2f, 92f), new Vector3(14f, 1f, 16f), rhythmLaneColor);
    }

    private Transform CreateGroup(Transform parent, string name, Vector3 localPosition)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition * courseScale;
        return go.transform;
    }

    private GameObject CreateBlock(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color tint)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        Transform tr = block.transform;
        tr.SetParent(parent, false);
        tr.localPosition = localPosition * courseScale;
        tr.localScale = localScale * courseScale;
        ApplySharedMaterial(block, tint);
        return block;
    }

    private GameObject CreateRamp(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Vector3 localEuler, Color tint)
    {
        GameObject ramp = CreateBlock(parent, name, localPosition, localScale, tint);
        ramp.transform.localRotation = Quaternion.Euler(localEuler);

        if (useMeshColliderOnRamps)
        {
            BoxCollider box = ramp.GetComponent<BoxCollider>();
            if (box != null)
            {
                DestroySmart(box);
            }

            MeshFilter filter = ramp.GetComponent<MeshFilter>();
            MeshCollider meshCollider = ramp.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                meshCollider = ramp.AddComponent<MeshCollider>();
            }

            if (filter != null)
            {
                meshCollider.sharedMesh = filter.sharedMesh;
            }

            meshCollider.convex = false;
        }

        return ramp;
    }

    private GameObject CreateMovingPlatform(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color tint, float phaseOffset, float rotationSpeed)
    {
        GameObject platformRoot = new GameObject(name);
        Transform rootTransform = platformRoot.transform;
        rootTransform.SetParent(parent, false);
        rootTransform.localPosition = localPosition * courseScale;
        rootTransform.localRotation = Quaternion.identity;
        rootTransform.localScale = Vector3.one;

        GameObject surface = CreateBlock(rootTransform, "Surface", Vector3.zero, localScale, tint);
        surface.name = "Surface";

        MinimoMovingPlatform movingPlatform = platformRoot.AddComponent<MinimoMovingPlatform>();
        movingPlatform.ApplySettings(rotationSpeed, movingPlatformVerticalDistance * courseScale, movingPlatformCycleDuration, phaseOffset);
        return platformRoot;
    }

    private void ApplySharedMaterial(GameObject go, Color tint)
    {
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            return;
        }

        if (overrideMaterial != null)
        {
            renderer.sharedMaterial = overrideMaterial;
        }

        if (!useSectionColors)
        {
            return;
        }

        if (sharedPropertyBlock == null)
        {
            sharedPropertyBlock = new MaterialPropertyBlock();
        }

        sharedPropertyBlock.Clear();
        sharedPropertyBlock.SetColor("_BaseColor", tint);
        sharedPropertyBlock.SetColor("_Color", tint);
        renderer.SetPropertyBlock(sharedPropertyBlock);
    }

    private static void DestroySmart(UnityEngine.Object obj)
    {
        if (obj == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(obj);
        }
        else
        {
            DestroyImmediate(obj);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawCourseGizmos)
        {
            return;
        }

        Gizmos.color = Color.Lerp(Color.cyan, Color.white, 0.4f);
        Gizmos.DrawWireCube(courseOrigin + new Vector3(0f, 4f, 0f), new Vector3(240f, 8f, 240f) * courseScale);
    }
}

public static class MinimoTraversalPlaygroundAutoBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureBuilderForTraversalScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path))
        {
            return;
        }

        string normalizedPath = scene.path.Replace('\\', '/');
        if (!normalizedPath.EndsWith("/Minimo/Scene/Playground/Playground.unity", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        MinimoTraversalPlaygroundBuilder existing =
            MinimoUnityCompatibility.FindFirstObjectByType<MinimoTraversalPlaygroundBuilder>();
        if (existing == null || !existing.AutoBuildOnSceneLoad)
        {
            return;
        }

        existing.BuildPlayground();
    }
}

