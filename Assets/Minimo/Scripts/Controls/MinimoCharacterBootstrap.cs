using System.Collections.Generic;
using UnityEngine;

#pragma warning disable 0414
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[AddComponentMenu("Minimo/Character Bootstrap")]
public class MinimoCharacterBootstrap : MonoBehaviour
{
    private const string DefaultCharacterVisualPrefabPath = "Assets/Minimo/Scene/Obstacles/Character.prefab";
    private const string UnityCloneSuffix = "(Clone)";
    private static readonly HashSet<int> RuntimeConfiguredCharacterIds = new HashSet<int>();
    private static int visualInstantiationDepth;

    [Header("Character Binding")]
    [Tooltip("Configures scene character.")]
    [SerializeField] private GameObject sceneCharacter;
    [Tooltip("Configures capsule character name.")]
    [SerializeField] private string capsuleCharacterName = "Capsule";
    [Tooltip("Configures capsule visual scale.")]
    [SerializeField] private Vector3 capsuleVisualScale = Vector3.one;
    [Tooltip("Automatically adds required components in the editor.")]
    [SerializeField] private bool autoConfigureInEditor = true;
    [Tooltip("Automatically refreshes the character in the editor when inspector values change.")]
    [SerializeField] private bool liveUpdateInEditor = true;
    [Tooltip("Configures character controller height.")]
    [SerializeField] private bool createCharacterInEditorIfMissing = false;
    [Tooltip("Configures character controller height.")]
    [SerializeField] private bool createCharacterAtRuntimeIfMissing = false;

    [Header("Character Size")]
    [Tooltip("Configures character controller height.")]
    [SerializeField] [Min(0.5f)] private float characterControllerHeight = 2f;
    [Tooltip("Configures character controller radius.")]
    [SerializeField] [Min(0.1f)] private float characterControllerRadius = 0.42f;
    [Tooltip("Configures snap character to ground.")]
    [SerializeField] private bool snapCharacterToGround = true;
    [Tooltip("Configures ground snap distance.")]
    [SerializeField] [Min(0.5f)] private float groundSnapDistance = 8f;

    [Header("Character Visual")]
    [Tooltip("Configures character visual prefab.")]
    [SerializeField] private GameObject characterVisualPrefab;
    [Tooltip("Configures character visual root name.")]
    [SerializeField] private string characterVisualRootName = "CharacterVisual";
    [Tooltip("Configures character visual local position.")]
    [SerializeField] private Vector3 characterVisualLocalPosition = new Vector3(0f, -1f, 0f);
    [Tooltip("Configures character visual local euler.")]
    [SerializeField] private Vector3 characterVisualLocalEuler = Vector3.zero;
    [Tooltip("Configures character visual local scale.")]
    [SerializeField] private Vector3 characterVisualLocalScale = Vector3.one;
    [Tooltip("Configures fit visual to character controller.")]
    [SerializeField] private bool fitVisualToCharacterController = true;
    [Tooltip("Configures hide primitive renderer when visual present.")]
    [SerializeField] private bool hidePrimitiveRendererWhenVisualPresent = true;
    [Tooltip("Configures ragdoll.")]
    [SerializeField] private bool ragdoll = false;

    [Header("Camera Binding")]
    [Tooltip("Configures auto setup camera.")]
    [SerializeField] private bool autoSetupCamera = false;
    [Tooltip("Configures camera start position.")]
    [SerializeField] private Vector3 cameraStartPosition = new Vector3(0f, 1.8f, -4.5f);
    [Tooltip("Configures camera start euler.")]
    [SerializeField] private Vector3 cameraStartEuler = new Vector3(10f, 0f, 0f);

    [Header("Preset")]
    [Tooltip("Configures default gameplay preset.")]
    [SerializeField] private MinimoTpsPreset defaultGameplayPreset;

#if UNITY_EDITOR
    private bool editorRefreshQueued;
#endif
    private int runtimeClaimedCharacterId = int.MinValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeClaims()
    {
        RuntimeConfiguredCharacterIds.Clear();
    }

    private void Awake()
    {
        if (ShouldSkipAwakeForVisualInstance())
        {
            enabled = false;
            return;
        }

        SanitizeConfiguration();

        GameObject character = ResolveCharacter();
        if (character == null)
        {
            return;
        }

        if (!TryClaimRuntimeCharacter(character))
        {
            if (ragdoll)
            {
                ApplyRagdollColliderStateToExistingVisual(character);
            }
            return;
        }

        ConfigureCharacterForTps(character, true, false);
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        ClearQueuedEditorRefresh();
#endif

        if (runtimeClaimedCharacterId == int.MinValue)
        {
            return;
        }

        RuntimeConfiguredCharacterIds.Remove(runtimeClaimedCharacterId);
        runtimeClaimedCharacterId = int.MinValue;
    }

#if UNITY_EDITOR
    private void OnEnable()
    {
        QueueEditorRefresh();
    }

    private void OnValidate()
    {
        if (Application.isPlaying
            || EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling
            || EditorApplication.isUpdating
            || EditorUtility.IsPersistent(this)
            || !autoConfigureInEditor)
        {
            return;
        }

        SanitizeConfiguration();
        QueueEditorRefresh();
    }

    private void QueueEditorRefresh()
    {
        if (Application.isPlaying
            || EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling
            || EditorApplication.isUpdating
            || EditorUtility.IsPersistent(this)
            || !autoConfigureInEditor
            || !liveUpdateInEditor
            || editorRefreshQueued)
        {
            return;
        }

        editorRefreshQueued = true;
        EditorApplication.delayCall += PerformQueuedEditorRefresh;
    }

    private void ClearQueuedEditorRefresh()
    {
        if (!editorRefreshQueued)
        {
            return;
        }

        editorRefreshQueued = false;
        EditorApplication.delayCall -= PerformQueuedEditorRefresh;
    }

    private void PerformQueuedEditorRefresh()
    {
        editorRefreshQueued = false;
        EditorApplication.delayCall -= PerformQueuedEditorRefresh;

        if (this == null
            || Application.isPlaying
            || EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling
            || EditorApplication.isUpdating
            || EditorUtility.IsPersistent(this)
            || !autoConfigureInEditor
            || !liveUpdateInEditor)
        {
            return;
        }

        GameObject character = ResolveCharacter();

        if (character != null)
        {
            ConfigureCharacterForTps(character, false, false);
            EditorUtility.SetDirty(character);
        }

        EditorUtility.SetDirty(this);
    }
#endif

    [ContextMenu("Setup Scene Character")]
    private void SetupSceneCharacter()
    {
        GameObject character = ResolveCharacter();

        if (character == null)
        {
            return;
        }

        if (!TryClaimRuntimeCharacter(character))
        {
            return;
        }

        ConfigureCharacterForTps(character, true, false);
    }

    private bool TryClaimRuntimeCharacter(GameObject character)
    {
        if (!Application.isPlaying || character == null)
        {
            return true;
        }

        int characterId = character.GetHashCode();
        if (runtimeClaimedCharacterId == characterId)
        {
            return true;
        }

        if (RuntimeConfiguredCharacterIds.Contains(characterId))
        {
            // When multiple bootstrap components target the same character, the first claim wins.
            // Keep the duplicate component enabled, but skip this setup pass.
            return false;
        }

        RuntimeConfiguredCharacterIds.Add(characterId);
        runtimeClaimedCharacterId = characterId;
        return true;
    }

    private bool ShouldSkipAwakeForVisualInstance()
    {
        if (visualInstantiationDepth > 0)
        {
            return true;
        }

        if (transform.parent == null)
        {
            return false;
        }

        bool hasGameplayComponents = GetComponent<CharacterController>() != null || GetComponent<MinimoTpsController>() != null;
        if (!hasGameplayComponents)
        {
            return false;
        }

        Transform parent = transform.parent;
        return parent.GetComponentInParent<CharacterController>() != null
            || parent.GetComponentInParent<MinimoTpsController>() != null;
    }

    private GameObject ResolveCharacter()
    {
        if (sceneCharacter != null)
        {
            return sceneCharacter;
        }

        if (IsSceneCharacterCandidate(gameObject))
        {
            sceneCharacter = gameObject;
            return sceneCharacter;
        }

        if (!string.IsNullOrWhiteSpace(capsuleCharacterName))
        {
            sceneCharacter = GameObject.Find(capsuleCharacterName);
            if (sceneCharacter != null)
            {
                return sceneCharacter;
            }
        }

        MinimoTpsController tpsController = MinimoUnityCompatibility.FindFirstObjectByType<MinimoTpsController>();
        if (tpsController != null && IsSceneCharacterCandidate(tpsController.gameObject))
        {
            sceneCharacter = tpsController.gameObject;
            return sceneCharacter;
        }

        CharacterController[] characterControllers =
            MinimoUnityCompatibility.FindObjectsByType<CharacterController>(true);
        for (int i = 0; i < characterControllers.Length; i++)
        {
            CharacterController controller = characterControllers[i];
            if (controller == null || !IsSceneCharacterCandidate(controller.gameObject))
            {
                continue;
            }

            sceneCharacter = controller.gameObject;
            return sceneCharacter;
        }

        return null;
    }

    private bool IsSceneCharacterCandidate(GameObject candidate)
    {
        if (candidate == null || !candidate.scene.IsValid() || !candidate.scene.isLoaded)
        {
            return false;
        }

        if (candidate.GetComponent<CharacterController>() != null
            || candidate.GetComponent<MinimoTpsController>() != null)
        {
            return true;
        }

        string visualRootName = string.IsNullOrWhiteSpace(characterVisualRootName)
            ? "CharacterVisual"
            : characterVisualRootName;
        return candidate.transform.Find(visualRootName) != null;
    }

    private void ConfigureCharacterForTps(GameObject character, bool setupCameraBinding, bool forceGroundSnap)
    {
        SanitizeConfiguration();
        ApplyConfiguredScale(character);

        if (!character.TryGetComponent(out CharacterController controller))
        {
            controller = character.AddComponent<CharacterController>();
        }

        if (character.TryGetComponent(out CapsuleCollider primitiveCapsuleCollider))
        {
            primitiveCapsuleCollider.enabled = false;
        }

        controller.center = Vector3.zero;
        controller.radius = Mathf.Max(0.1f, characterControllerRadius);
        controller.height = Mathf.Max(controller.radius * 2f + 0.05f, characterControllerHeight);
        controller.stepOffset = Mathf.Min(0.4f, controller.height * 0.5f);
        controller.slopeLimit = 50f;
        controller.skinWidth = 0.04f;
        controller.minMoveDistance = 0f;

        bool hasVisual = EnsureCharacterVisual(character, controller);
        if (forceGroundSnap)
        {
            SnapCharacterToGroundIfEnabled(character.transform, controller);
        }

        if (character.TryGetComponent(out MeshRenderer primitiveRenderer))
        {
            primitiveRenderer.enabled = !hidePrimitiveRendererWhenVisualPresent || !hasVisual;
        }

        if (!character.TryGetComponent(out MinimoTpsController tpsController))
        {
            tpsController = character.AddComponent<MinimoTpsController>();
        }
        tpsController.RefreshControllerShapeCacheFromCurrentController();

        if (!character.TryGetComponent(out MinimoMovingPlatformRider _))
        {
            character.AddComponent<MinimoMovingPlatformRider>();
        }

        Animator characterAnimator = ResolveCharacterAnimator(character, hasVisual);
        if (characterAnimator != null)
        {
            tpsController.ConfigureAnimator(characterAnimator, true);
        }

        if (defaultGameplayPreset != null)
        {
            tpsController.SetPreset(defaultGameplayPreset, false);
            tpsController.ApplyAssignedPresetIfEnabled();
        }

        if (!setupCameraBinding)
        {
            return;
        }

        Camera sceneCamera = Camera.main;
        if (sceneCamera == null)
        {
            sceneCamera = GetComponent<Camera>();
        }

        if (sceneCamera != null)
        {
            tpsController.SetupCamera(sceneCamera.transform, autoSetupCamera, cameraStartPosition, cameraStartEuler);
        }
    }

    private bool EnsureCharacterVisual(GameObject character, CharacterController controller)
    {
        if (character == null)
        {
            return false;
        }

        string visualRootName = string.IsNullOrWhiteSpace(characterVisualRootName) ? "CharacterVisual" : characterVisualRootName;
        Transform visualRoot = character.transform.Find(visualRootName);

        if (visualRoot == null)
        {
            visualRoot = FindNestedReusableCharacterVisual(character.transform, visualRootName);
        }

        if (visualRoot == null)
        {
            visualRoot = ResolveReusableSceneCharacterVisual(character, visualRootName);
        }

        if (visualRoot == null)
        {
            GameObject visualPrefab = ResolveCharacterVisualPrefab();
            if (visualPrefab == null)
            {
                return false;
            }

            GameObject visualSource = ResolveCharacterVisualSource(visualPrefab, visualRootName);
            if (visualSource == null)
            {
                return false;
            }

            GameObject visualInstance = InstantiateVisualSource(visualSource, character.transform);

            if (visualInstance == null)
            {
                return false;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(visualInstance, "Create Minimo Character Visual");
            }
#endif

            StripGameplayComponentsFromVisualInstance(visualInstance);

            visualRoot = visualInstance.transform;
            StripUnityCloneSuffixesRecursively(visualRoot);
        }
        else if (visualRoot.parent != character.transform)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.SetTransformParent(visualRoot, character.transform, "Use Scene Minimo Character Visual");
            }
            else
#endif
            {
                visualRoot.SetParent(character.transform, false);
            }
        }

        StripGameplayComponentsFromVisualInstance(visualRoot.gameObject);

        visualRoot.name = visualRootName;
        visualRoot.localPosition = characterVisualLocalPosition;
        visualRoot.localRotation = Quaternion.Euler(characterVisualLocalEuler);
        visualRoot.localScale = SanitizeScale(characterVisualLocalScale);

        SetChildCollidersEnabled(visualRoot, ragdoll);

        if (fitVisualToCharacterController)
        {
            FitVisualToController(character.transform, visualRoot, controller.height, controller.center.y);
        }

        return true;
    }

    private static Transform FindNestedReusableCharacterVisual(Transform characterRoot, string visualRootName)
    {
        if (characterRoot == null || string.IsNullOrWhiteSpace(visualRootName))
        {
            return null;
        }

        return FindNestedReusableCharacterVisual(characterRoot, characterRoot, visualRootName);
    }

    private static Transform FindNestedReusableCharacterVisual(
        Transform current,
        Transform characterRoot,
        string visualRootName)
    {
        for (int i = 0; i < current.childCount; i++)
        {
            Transform child = current.GetChild(i);
            if (string.Equals(child.name, visualRootName, System.StringComparison.OrdinalIgnoreCase)
                && IsReusableSceneCharacterVisual(child, characterRoot))
            {
                return child;
            }

            Transform nested = FindNestedReusableCharacterVisual(child, characterRoot, visualRootName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static Transform ResolveReusableSceneCharacterVisual(GameObject character, string visualRootName)
    {
        if (character == null || string.IsNullOrWhiteSpace(visualRootName))
        {
            return null;
        }

        UnityEngine.SceneManagement.Scene scene = character.scene;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null)
            {
                continue;
            }

            Transform match = FindReusableSceneCharacterVisual(root.transform, character.transform, visualRootName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static Transform FindReusableSceneCharacterVisual(
        Transform current,
        Transform characterRoot,
        string visualRootName)
    {
        if (current == null || characterRoot == null)
        {
            return null;
        }

        if (current == characterRoot || current.IsChildOf(characterRoot))
        {
            return null;
        }

        if (string.Equals(current.name, visualRootName, System.StringComparison.OrdinalIgnoreCase)
            && IsReusableSceneCharacterVisual(current, characterRoot))
        {
            return current;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            Transform match = FindReusableSceneCharacterVisual(current.GetChild(i), characterRoot, visualRootName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static bool IsReusableSceneCharacterVisual(Transform candidate, Transform characterRoot)
    {
        if (candidate == null || characterRoot == null)
        {
            return false;
        }

        CharacterController owningController = candidate.GetComponentInParent<CharacterController>();
        if (owningController != null
            && owningController.transform != characterRoot
            && !owningController.transform.IsChildOf(candidate))
        {
            return false;
        }

        MinimoTpsController owningTpsController = candidate.GetComponentInParent<MinimoTpsController>();
        if (owningTpsController != null
            && owningTpsController.transform != characterRoot
            && !owningTpsController.transform.IsChildOf(candidate))
        {
            return false;
        }

        return candidate.GetComponentInChildren<Animator>(true) != null
            || candidate.GetComponentInChildren<Renderer>(true) != null
            || candidate.GetComponentInChildren<MinimoCharacterCustomizer>(true) != null;
    }

    private GameObject ResolveCharacterVisualSource(GameObject visualPrefab, string visualRootName)
    {
        if (visualPrefab == null)
        {
            return null;
        }

        if (!HasGameplaySetupComponents(visualPrefab))
        {
            return visualPrefab;
        }

        Transform nestedVisualRoot = visualPrefab.transform.Find(visualRootName);
        return nestedVisualRoot != null ? nestedVisualRoot.gameObject : visualPrefab;
    }

    private static bool HasGameplaySetupComponents(GameObject target)
    {
        return target != null
            && (target.GetComponent<MinimoCharacterBootstrap>() != null
                || target.GetComponent<CharacterController>() != null
                || target.GetComponent<MinimoTpsController>() != null);
    }

    private static void StripUnityCloneSuffixesRecursively(Transform root)
    {
        if (root == null)
        {
            return;
        }

        root.gameObject.name = StripUnityCloneSuffix(root.gameObject.name);
        for (int i = 0; i < root.childCount; i++)
        {
            StripUnityCloneSuffixesRecursively(root.GetChild(i));
        }
    }

    private static string StripUnityCloneSuffix(string value, string fallback = "Object")
    {
        string result = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        while (result.EndsWith(UnityCloneSuffix, System.StringComparison.Ordinal))
        {
            result = result.Substring(0, result.Length - UnityCloneSuffix.Length).TrimEnd();
        }

        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }

    private GameObject InstantiateVisualSource(GameObject visualSource, Transform parent)
    {
        try
        {
            visualInstantiationDepth++;

#if UNITY_EDITOR
            if (!Application.isPlaying && visualSource.transform.parent == null)
            {
                GameObject prefabInstance = PrefabUtility.InstantiatePrefab(visualSource, parent) as GameObject;
                if (prefabInstance != null)
                {
                    return prefabInstance;
                }
            }
#endif

            return Instantiate(visualSource, parent);
        }
        finally
        {
            visualInstantiationDepth--;
        }
    }

    private void StripGameplayComponentsFromVisualInstance(GameObject visualInstance)
    {
        if (visualInstance == null)
        {
            return;
        }

        MinimoCharacterBootstrap[] bootstraps = visualInstance.GetComponentsInChildren<MinimoCharacterBootstrap>(true);
        for (int i = 0; i < bootstraps.Length; i++)
        {
            DestroyVisualOnlyComponent(bootstraps[i]);
        }

        MinimoTpsController[] tpsControllers = visualInstance.GetComponentsInChildren<MinimoTpsController>(true);
        for (int i = 0; i < tpsControllers.Length; i++)
        {
            DestroyVisualOnlyComponent(tpsControllers[i]);
        }

        MinimoMovingPlatformRider[] platformRiders = visualInstance.GetComponentsInChildren<MinimoMovingPlatformRider>(true);
        for (int i = 0; i < platformRiders.Length; i++)
        {
            DestroyVisualOnlyComponent(platformRiders[i]);
        }

        CharacterController[] characterControllers = visualInstance.GetComponentsInChildren<CharacterController>(true);
        for (int i = 0; i < characterControllers.Length; i++)
        {
            DestroyVisualOnlyComponent(characterControllers[i]);
        }
    }

    private void DestroyVisualOnlyComponent(Component component)
    {
        if (component == null || component == this)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.DestroyObjectImmediate(component);
            return;
        }
#endif

        if (component is Behaviour behaviour)
        {
            behaviour.enabled = false;
        }
        else if (component is Collider collider)
        {
            collider.enabled = false;
        }

        Destroy(component);
    }

    private GameObject ResolveCharacterVisualPrefab()
    {
        if (characterVisualPrefab != null)
        {
            return characterVisualPrefab;
        }

#if UNITY_EDITOR
        characterVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultCharacterVisualPrefabPath);
        if (characterVisualPrefab != null)
        {
            EditorUtility.SetDirty(this);
        }
#endif
        return characterVisualPrefab;
    }

    private void ApplyRagdollColliderStateToExistingVisual(GameObject character)
    {
        if (character == null)
        {
            return;
        }

        string visualRootName = string.IsNullOrWhiteSpace(characterVisualRootName) ? "CharacterVisual" : characterVisualRootName;
        Transform visualRoot = character.transform.Find(visualRootName);
        if (visualRoot == null)
        {
            return;
        }

        SetChildCollidersEnabled(visualRoot, true);
    }

    private static void SetChildCollidersEnabled(Transform visualRoot, bool ragdollEnabled)
    {
        Collider[] colliders = visualRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = ragdollEnabled;
        }
    }

    private static void FitVisualToController(Transform characterRoot, Transform visualRoot, float controllerHeight, float controllerCenterY)
    {
        if (controllerHeight <= 0.01f || !TryGetVisualBottom(characterRoot, visualRoot, out float visualBottom))
        {
            return;
        }

        float desiredBottom = controllerCenterY - controllerHeight * 0.5f;
        float heightOffset = desiredBottom - visualBottom;
        visualRoot.localPosition += Vector3.up * heightOffset;
    }

    private static bool TryGetVisualBottom(Transform characterRoot, Transform visualRoot, out float visualBottom)
    {
        Animator animator = visualRoot.GetComponentInChildren<Animator>(true);
        if (animator != null && animator.isHuman)
        {
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            Transform rightToes = animator.GetBoneTransform(HumanBodyBones.RightToes);

            bool foundBone = false;
            float minY = float.PositiveInfinity;
            foundBone |= TryAccumulateBoneY(characterRoot, leftFoot, ref minY);
            foundBone |= TryAccumulateBoneY(characterRoot, rightFoot, ref minY);
            foundBone |= TryAccumulateBoneY(characterRoot, leftToes, ref minY);
            foundBone |= TryAccumulateBoneY(characterRoot, rightToes, ref minY);

            if (foundBone)
            {
                visualBottom = minY;
                return true;
            }
        }

        if (TryGetRendererBounds(visualRoot, out Bounds bounds))
        {
            visualBottom = characterRoot.InverseTransformPoint(bounds.min).y;
            return true;
        }

        visualBottom = 0f;
        return false;
    }

    private static bool TryAccumulateBoneY(Transform characterRoot, Transform bone, ref float minY)
    {
        if (bone == null)
        {
            return false;
        }

        float y = characterRoot.InverseTransformPoint(bone.position).y;
        if (y < minY)
        {
            minY = y;
        }

        return true;
    }

    private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        bounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private void SnapCharacterToGroundIfEnabled(Transform characterTransform, CharacterController controller)
    {
        if (!snapCharacterToGround || controller == null)
        {
            return;
        }

        float castHeight = Mathf.Max(1f, controller.height + 0.25f);
        float castDistance = Mathf.Max(groundSnapDistance, controller.height + 0.5f) + castHeight;
        Vector3 origin = characterTransform.position + Vector3.up * castHeight;
        Ray ray = new Ray(origin, Vector3.down);
        RaycastHit[] hits = Physics.RaycastAll(ray, castDistance, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            return;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null)
            {
                continue;
            }

            if (hit.collider.transform.IsChildOf(characterTransform))
            {
                continue;
            }

            float localBottom = controller.center.y - controller.height * 0.5f;
            Vector3 position = characterTransform.position;
            position.y = hit.point.y - localBottom;
            characterTransform.position = position;
            return;
        }
    }

    private Animator ResolveCharacterAnimator(GameObject character, bool preferVisualRoot)
    {
        if (preferVisualRoot)
        {
            string visualRootName = string.IsNullOrWhiteSpace(characterVisualRootName) ? "CharacterVisual" : characterVisualRootName;
            Transform visualRoot = character.transform.Find(visualRootName);
            if (visualRoot != null)
            {
                Animator visualAnimator = visualRoot.GetComponentInChildren<Animator>(true);
                if (visualAnimator != null)
                {
                    return visualAnimator;
                }
            }
        }

        return character.GetComponentInChildren<Animator>(true);
    }

    private void SanitizeConfiguration()
    {
        if (!IsValidScale(capsuleVisualScale))
        {
            capsuleVisualScale = Vector3.one;
        }

        if (!IsValidScale(characterVisualLocalScale))
        {
            characterVisualLocalScale = Vector3.one;
        }

        characterControllerHeight = Mathf.Max(0.5f, characterControllerHeight);
        characterControllerRadius = Mathf.Max(0.1f, characterControllerRadius);
        groundSnapDistance = Mathf.Max(0.5f, groundSnapDistance);
    }

    private static bool IsValidScale(Vector3 value)
    {
        return value.x > 0.0001f && value.y > 0.0001f && value.z > 0.0001f;
    }

    private static Vector3 SanitizeScale(Vector3 value)
    {
        return IsValidScale(value) ? value : Vector3.one;
    }

    private void ApplyConfiguredScale(GameObject character)
    {
        character.transform.localScale = SanitizeScale(capsuleVisualScale);
    }
}
