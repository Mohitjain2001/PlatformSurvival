using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
[DefaultExecutionOrder(-10000)]
public sealed class MinimoPlaygroundCharacterTransfer : MonoBehaviour
{
    private const string RuntimeHostName = "MinimoPlaygroundCharacterTransfer_Runtime";
    private const string CustomizationSceneName = "Customization";
    private const string CustomizationScenePath = "Assets/Minimo/Scene/Customization/Customization.unity";
    private const string PlaygroundSceneName = "Playground";
    private const string PlaygroundScenePath = "Assets/Minimo/Scene/Playground/Playground.unity";
    private const string PlayButtonObjectName = "PlayScene";
    private const string PlayButtonSceneObjectName = "PlayButton";
    private const string CharacterVisualRootName = "CharacterVisual";
    private const string UnityCloneSuffix = "(Clone)";
    private const KeyCode GamepadBackButton = KeyCode.JoystickButton6;
    private const KeyCode GamepadMenuButton = KeyCode.JoystickButton7;
    private const int PlaygroundApplyRetryFrames = 8;

    private static readonly MinimoCharacterCustomizer.PartSlot[] OrderedSlots =
    {
        MinimoCharacterCustomizer.PartSlot.Hair,
        MinimoCharacterCustomizer.PartSlot.Face,
        MinimoCharacterCustomizer.PartSlot.ShoulderLeft,
        MinimoCharacterCustomizer.PartSlot.ShoulderRight,
        MinimoCharacterCustomizer.PartSlot.ArmLeft,
        MinimoCharacterCustomizer.PartSlot.ArmRight,
        MinimoCharacterCustomizer.PartSlot.HandLeft,
        MinimoCharacterCustomizer.PartSlot.HandRight,
        MinimoCharacterCustomizer.PartSlot.Hips,
        MinimoCharacterCustomizer.PartSlot.Torso,
        MinimoCharacterCustomizer.PartSlot.LegUpperLeft,
        MinimoCharacterCustomizer.PartSlot.LegUpperRight,
        MinimoCharacterCustomizer.PartSlot.BootsLeft,
        MinimoCharacterCustomizer.PartSlot.BootsRight
    };

    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

    private sealed class CustomizationVisualSnapshot
    {
        public readonly List<PartVisualSnapshot> parts = new List<PartVisualSnapshot>();
        public readonly List<PropInstanceSnapshot> props = new List<PropInstanceSnapshot>();
        public GameObject propHost;

        public void Dispose()
        {
            for (int i = 0; i < parts.Count; i++)
            {
                PartVisualSnapshot part = parts[i];
                if (part?.runtimeMaterials == null)
                {
                    continue;
                }

                for (int materialIndex = 0; materialIndex < part.runtimeMaterials.Length; materialIndex++)
                {
                    DestroyUnityObject(part.runtimeMaterials[materialIndex]);
                }
            }

            if (propHost != null)
            {
                DestroyUnityObject(propHost);
                propHost = null;
            }

            parts.Clear();
            props.Clear();
        }
    }

    private sealed class PartVisualSnapshot
    {
        public MinimoCharacterCustomizer.PartSlot slot;
        public int variantIndex;
        public bool rendererEnabled = true;
        public Mesh sharedMesh;
        public Material[] sharedMaterials = Array.Empty<Material>();
        public Bounds localBounds;
        public string rootBoneName;
        public string[] boneNames = Array.Empty<string>();
        public readonly List<PartColorSnapshot> colors = new List<PartColorSnapshot>();
        public Material[] runtimeMaterials = Array.Empty<Material>();
        public MaterialPropertyBlock[] propertyBlocks = Array.Empty<MaterialPropertyBlock>();
    }

    private readonly struct PartColorSnapshot
    {
        public readonly int materialIndex;
        public readonly Color color;

        public PartColorSnapshot(int materialIndex, Color color)
        {
            this.materialIndex = materialIndex;
            this.color = color;
        }
    }

    private sealed class PropInstanceSnapshot
    {
        public string rigName;
        public GameObject propRoot;
    }

    private static MinimoPlaygroundCharacterTransfer runtimeInstance;

    private Button boundPlayButton;
    private Coroutine pendingPlayLoadCoroutine;
    private Coroutine pendingPlaygroundApplyCoroutine;
    private Coroutine pendingSceneLoadCoroutine;
    private GameObject carriedCharacterVisual;
    private CustomizationVisualSnapshot carriedCustomizationSnapshot;
    private bool transferPending;
    private bool returnToCustomizationPending;
    private bool playButtonPressPending;
    private bool sceneLoadPending;
    private MaterialPropertyBlock sharedPropertyBlock;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureRuntimeInstance();
    }

    private static void EnsureRuntimeInstance()
    {
        if (runtimeInstance != null)
        {
            return;
        }

        GameObject host = new GameObject(RuntimeHostName);
        DontDestroyOnLoad(host);
        runtimeInstance = host.AddComponent<MinimoPlaygroundCharacterTransfer>();
    }

    private void Awake()
    {
        if (runtimeInstance != null && runtimeInstance != this)
        {
            Destroy(gameObject);
            return;
        }

        runtimeInstance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryBindPlayButton(SceneManager.GetActiveScene());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnbindPlayButton();
        if (pendingPlaygroundApplyCoroutine != null)
        {
            StopCoroutine(pendingPlaygroundApplyCoroutine);
            pendingPlaygroundApplyCoroutine = null;
        }

        if (pendingSceneLoadCoroutine != null)
        {
            StopCoroutine(pendingSceneLoadCoroutine);
            pendingSceneLoadCoroutine = null;
        }

        ClearCarriedCustomizationSnapshot();
        ClearCarriedCharacterVisual();
    }

    public static bool HasPendingCustomizationReturn()
    {
        return runtimeInstance != null
               && runtimeInstance.returnToCustomizationPending
               && (runtimeInstance.carriedCustomizationSnapshot != null
                   || runtimeInstance.carriedCharacterVisual != null);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode _mode)
    {
        TryBindPlayButton(scene);

        if (transferPending && scene.name.Equals(PlaygroundSceneName, StringComparison.OrdinalIgnoreCase))
        {
            HideDefaultPlaygroundVisualWhileTransferPending();
            if (ApplyCarriedCharacterToPlayground(logWarnings: false, clearOnFailure: false))
            {
                return;
            }

            if (pendingPlaygroundApplyCoroutine != null)
            {
                StopCoroutine(pendingPlaygroundApplyCoroutine);
            }

            pendingPlaygroundApplyCoroutine = StartCoroutine(ApplyCarriedCharacterToPlaygroundDeferred());
            return;
        }

        if (returnToCustomizationPending && scene.name.Equals(CustomizationSceneName, StringComparison.OrdinalIgnoreCase))
        {
            StartCoroutine(ApplyCarriedCharacterToCustomizationDeferred());
        }
    }

    private void Update()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid()
            || !activeScene.isLoaded
            || !activeScene.name.Equals(PlaygroundSceneName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!IsReturnToCustomizationInputPressed())
        {
            return;
        }

        HandleReturnToCustomizationRequested();
    }

    private static bool IsReturnToCustomizationInputPressed()
    {
        if (MinimoInputBridge.GetKeyDown(KeyCode.Escape)
            || MinimoInputBridge.GetKeyDown(GamepadBackButton)
            || MinimoInputBridge.GetKeyDown(GamepadMenuButton))
        {
            return true;
        }

#if ENABLE_INPUT_SYSTEM
        Gamepad gamepad = Gamepad.current;
        if (gamepad != null
            && ((gamepad.selectButton != null && gamepad.selectButton.wasPressedThisFrame)
                || (gamepad.startButton != null && gamepad.startButton.wasPressedThisFrame)))
        {
            return true;
        }
#endif

        return false;
    }

    private void TryBindPlayButton(Scene scene)
    {
        UnbindPlayButton();

        if (!scene.IsValid()
            || !scene.isLoaded
            || !scene.name.Equals(CustomizationSceneName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Button playButton = FindPlayButtonInScene();
        if (playButton == null)
        {
            return;
        }

        boundPlayButton = playButton;
        boundPlayButton.onClick.RemoveListener(HandlePlayButtonPressed);
        boundPlayButton.onClick.AddListener(HandlePlayButtonPressed);
    }

    private void UnbindPlayButton()
    {
        if (boundPlayButton == null)
        {
            return;
        }

        boundPlayButton.onClick.RemoveListener(HandlePlayButtonPressed);
        boundPlayButton = null;
    }

    private static Button FindPlayButtonInScene()
    {
        Button namedButton = FindButtonBySceneName(PlayButtonObjectName);
        if (namedButton != null)
        {
            return namedButton;
        }

        namedButton = FindButtonBySceneName(PlayButtonSceneObjectName);
        if (namedButton != null)
        {
            return namedButton;
        }

        Button[] allButtons = MinimoUnityCompatibility.FindObjectsByType<Button>(true);
        for (int i = 0; i < allButtons.Length; i++)
        {
            Button candidate = allButtons[i];
            if (candidate == null)
            {
                continue;
            }

            string name = candidate.gameObject.name;
            if (name.Equals(PlayButtonObjectName, StringComparison.OrdinalIgnoreCase)
                || name.Equals(PlayButtonSceneObjectName, StringComparison.OrdinalIgnoreCase)
                || name.Equals("Play", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static Button FindButtonBySceneName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject root = GameObject.Find(objectName);
        if (root == null)
        {
            return null;
        }

        Button onRoot = root.GetComponent<Button>();
        if (onRoot != null)
        {
            return onRoot;
        }

        return root.GetComponentInChildren<Button>(true);
    }

    private void HandlePlayButtonPressed()
    {
        if (playButtonPressPending || transferPending || sceneLoadPending)
        {
            return;
        }

        playButtonPressPending = true;
        if (pendingPlayLoadCoroutine != null)
        {
            StopCoroutine(pendingPlayLoadCoroutine);
        }

        pendingPlayLoadCoroutine = StartCoroutine(HandlePlayButtonPressedDeferred());
    }

    private IEnumerator HandlePlayButtonPressedDeferred()
    {
        yield return null;
        yield return new WaitForSecondsRealtime(0.06f);

        pendingPlayLoadCoroutine = null;
        CaptureCurrentCustomizationCharacter();
        returnToCustomizationPending = false;
        LoadPlaygroundScene();
    }

    private void CaptureCurrentCustomizationCharacter()
    {
        ClearCarriedCustomizationSnapshot();
        ClearCarriedCharacterVisual();

        transferPending = false;
        returnToCustomizationPending = false;

        MinimoCharacterCustomizer customizer =
            MinimoUnityCompatibility.FindFirstObjectByType<MinimoCharacterCustomizer>();
        if (customizer == null || customizer.gameObject == null)
        {
            return;
        }

        carriedCustomizationSnapshot = CaptureCustomizerVisualSnapshot(customizer);
        transferPending = carriedCustomizationSnapshot != null;
    }

    private void HandleReturnToCustomizationRequested()
    {
        if (transferPending || returnToCustomizationPending || sceneLoadPending)
        {
            return;
        }

        CaptureCurrentPlaygroundCharacter();
        returnToCustomizationPending = true;
        LoadCustomizationScene();
    }

    private void CaptureCurrentPlaygroundCharacter()
    {
        ClearCarriedCustomizationSnapshot();
        ClearCarriedCharacterVisual();

        transferPending = false;

        GameObject sourceVisual = ResolveCurrentPlaygroundVisual();
        if (sourceVisual == null)
        {
            return;
        }

        MinimoCharacterCustomizer sourceCustomizer = sourceVisual.GetComponent<MinimoCharacterCustomizer>();
        sourceCustomizer ??= sourceVisual.GetComponentInChildren<MinimoCharacterCustomizer>(true);
        if (sourceCustomizer == null)
        {
            return;
        }

        carriedCustomizationSnapshot = CaptureCustomizerVisualSnapshot(sourceCustomizer);
    }

    private static GameObject ResolveCurrentPlaygroundVisual()
    {
        GameObject controlledCharacter = ResolveControlledPlaygroundCharacter();
        if (controlledCharacter == null)
        {
            return null;
        }

        Transform visualRoot = controlledCharacter.transform.Find(CharacterVisualRootName);
        if (visualRoot == null)
        {
            return null;
        }

        return visualRoot.gameObject;
    }

    private IEnumerator ApplyCarriedCharacterToPlaygroundDeferred()
    {
        transferPending = false;

        for (int attempt = 0; attempt < PlaygroundApplyRetryFrames; attempt++)
        {
            HideDefaultPlaygroundVisualWhileTransferPending();
            bool isFinalAttempt = attempt == PlaygroundApplyRetryFrames - 1;
            if (ApplyCarriedCharacterToPlayground(logWarnings: isFinalAttempt, clearOnFailure: isFinalAttempt))
            {
                pendingPlaygroundApplyCoroutine = null;
                yield break;
            }

            yield return null;
        }

        pendingPlaygroundApplyCoroutine = null;
    }

    private static void HideDefaultPlaygroundVisualWhileTransferPending()
    {
        GameObject controlledCharacter = ResolveControlledPlaygroundCharacter();
        Transform visualRoot = controlledCharacter != null
            ? controlledCharacter.transform.Find(CharacterVisualRootName)
            : null;
        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(false);
        }
    }

    private bool ApplyCarriedCharacterToPlayground(bool logWarnings, bool clearOnFailure)
    {
        transferPending = false;

        if (carriedCustomizationSnapshot == null)
        {
            return true;
        }

        GameObject controlledCharacter = ResolveControlledPlaygroundCharacter();
        MinimoTpsController controller = controlledCharacter != null
            ? controlledCharacter.GetComponent<MinimoTpsController>()
            : null;
        if (controlledCharacter == null)
        {
            if (clearOnFailure)
            {
                ClearCarriedCustomizationSnapshot();
            }

            return false;
        }

        if (!TryApplyCustomizationSnapshotToExistingPlaygroundRig(controlledCharacter, controller))
        {
            if (clearOnFailure)
            {
                ClearCarriedCustomizationSnapshot();
            }

            return false;
        }

        HideControlledPrimitiveVisuals(controlledCharacter);
        ClearCarriedCustomizationSnapshot();
        return true;
    }

    private bool TryApplyCustomizationSnapshotToExistingPlaygroundRig(
        GameObject controlledCharacter,
        MinimoTpsController controller)
    {
        if (controlledCharacter == null || carriedCustomizationSnapshot == null)
        {
            return false;
        }

        Transform existingVisual = controlledCharacter.transform.Find(CharacterVisualRootName);
        if (existingVisual == null)
        {
            return false;
        }

        MinimoCharacterCustomizer targetCustomizer = ResolveOrCreatePlaygroundCustomizer(existingVisual);
        if (targetCustomizer == null)
        {
            return false;
        }

        existingVisual.gameObject.SetActive(true);
        targetCustomizer.enabled = true;
        targetCustomizer.PrepareRuntimeTransferTarget();
        ApplyCustomizerVisualSnapshot(carriedCustomizationSnapshot, targetCustomizer);
        DisableCustomizationOnlyComponents(existingVisual.gameObject);

        Animator animator = existingVisual.GetComponentInChildren<Animator>(true);
        if (controller != null && animator != null)
        {
            controller.ConfigureAnimator(animator, true);
        }

        return true;
    }

    private static MinimoCharacterCustomizer ResolveOrCreatePlaygroundCustomizer(Transform visualRoot)
    {
        if (visualRoot == null)
        {
            return null;
        }

        MinimoCharacterCustomizer customizer = visualRoot.GetComponent<MinimoCharacterCustomizer>();
        customizer ??= visualRoot.GetComponentInChildren<MinimoCharacterCustomizer>(true);
        if (customizer == null)
        {
            customizer = visualRoot.gameObject.AddComponent<MinimoCharacterCustomizer>();
        }

        return customizer;
    }

    private static void HideControlledPrimitiveVisuals(GameObject controlledCharacter)
    {
        if (controlledCharacter == null)
        {
            return;
        }

        if (controlledCharacter.TryGetComponent(out MeshRenderer primitiveRenderer))
        {
            primitiveRenderer.enabled = false;
        }

        if (controlledCharacter.TryGetComponent(out CapsuleCollider primitiveCollider))
        {
            primitiveCollider.enabled = false;
        }
    }

    private CustomizationVisualSnapshot CaptureCustomizerVisualSnapshot(MinimoCharacterCustomizer source)
    {
        if (source == null)
        {
            return null;
        }

        CustomizationVisualSnapshot snapshot = new CustomizationVisualSnapshot();
        for (int i = 0; i < OrderedSlots.Length; i++)
        {
            snapshot.parts.Add(CapturePartVisualSnapshot(source, OrderedSlots[i]));
        }

        CapturePropInstanceSnapshots(source, snapshot);
        return snapshot;
    }

    private PartVisualSnapshot CapturePartVisualSnapshot(
        MinimoCharacterCustomizer source,
        MinimoCharacterCustomizer.PartSlot slot)
    {
        PartVisualSnapshot part = new PartVisualSnapshot()
        {
            slot = slot,
            variantIndex = source.GetPartVariant(slot)
        };

        if (!source.TryGetPartRenderer(slot, out SkinnedMeshRenderer sourceRenderer) || sourceRenderer == null)
        {
            return part;
        }

        part.rendererEnabled = sourceRenderer.enabled;
        part.sharedMesh = sourceRenderer.sharedMesh;
        part.sharedMaterials = sourceRenderer.sharedMaterials != null
            ? sourceRenderer.sharedMaterials.ToArray()
            : Array.Empty<Material>();
        part.localBounds = sourceRenderer.localBounds;
        part.rootBoneName = sourceRenderer.rootBone != null ? sourceRenderer.rootBone.name : string.Empty;

        Transform[] sourceBones = sourceRenderer.bones;
        if (sourceBones != null && sourceBones.Length > 0)
        {
            part.boneNames = new string[sourceBones.Length];
            for (int i = 0; i < sourceBones.Length; i++)
            {
                part.boneNames[i] = sourceBones[i] != null ? sourceBones[i].name : string.Empty;
            }
        }

        Material[] sharedMaterials = sourceRenderer.sharedMaterials;
        int sharedMaterialCount = sharedMaterials != null ? sharedMaterials.Length : 0;
        for (int materialIndex = 0; materialIndex < sharedMaterialCount; materialIndex++)
        {
            Material sourceMaterial = sharedMaterials[materialIndex];
            if (TryResolveEffectiveBaseColor(sourceRenderer, sourceMaterial, materialIndex, out Color color))
            {
                part.colors.Add(new PartColorSnapshot(materialIndex, color));
            }
        }

        part.runtimeMaterials = CloneMaterialsForSnapshot(GetSafeSourceRuntimeMaterials(sourceRenderer));
        part.propertyBlocks = CaptureMaterialPropertyBlocks(sourceRenderer, sharedMaterialCount);
        return part;
    }

    private static Material[] CloneMaterialsForSnapshot(Material[] sourceMaterials)
    {
        if (sourceMaterials == null || sourceMaterials.Length == 0)
        {
            return Array.Empty<Material>();
        }

        Material[] clones = new Material[sourceMaterials.Length];
        for (int i = 0; i < sourceMaterials.Length; i++)
        {
            Material sourceMaterial = sourceMaterials[i];
            clones[i] = sourceMaterial != null ? new Material(sourceMaterial) : null;
        }

        return clones;
    }

    private static MaterialPropertyBlock[] CaptureMaterialPropertyBlocks(
        Renderer sourceRenderer,
        int materialCount)
    {
        if (sourceRenderer == null || materialCount <= 0)
        {
            return Array.Empty<MaterialPropertyBlock>();
        }

        MaterialPropertyBlock[] blocks = new MaterialPropertyBlock[materialCount];
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
        {
            block.Clear();
            sourceRenderer.GetPropertyBlock(block, materialIndex);
            if (block.isEmpty)
            {
                continue;
            }

            MaterialPropertyBlock captured = new MaterialPropertyBlock();
            sourceRenderer.GetPropertyBlock(captured, materialIndex);
            blocks[materialIndex] = captured;
        }

        return blocks;
    }

    private static void CapturePropInstanceSnapshots(
        MinimoCharacterCustomizer sourceCustomizer,
        CustomizationVisualSnapshot snapshot)
    {
        if (sourceCustomizer == null || snapshot == null)
        {
            return;
        }

        Transform sourceRoot = sourceCustomizer.transform;
        if (sourceRoot == null)
        {
            return;
        }

        Transform[] sourceTransforms = sourceRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < sourceTransforms.Length; i++)
        {
            Transform sourceRig = sourceTransforms[i];
            if (sourceRig == null)
            {
                continue;
            }

            for (int childIndex = 0; childIndex < sourceRig.childCount; childIndex++)
            {
                Transform sourcePropRoot = sourceRig.GetChild(childIndex);
                if (sourcePropRoot == null || !IsPropInstanceName(sourcePropRoot.name))
                {
                    continue;
                }

                GameObject host = EnsureSnapshotPropHost(snapshot);
                GameObject propClone = Instantiate(sourcePropRoot.gameObject, host.transform, false);
                if (propClone == null)
                {
                    continue;
                }

                propClone.name = StripUnityCloneSuffix(sourcePropRoot.name);
                StripUnityCloneSuffixesRecursively(propClone.transform);
                Transform propCloneTransform = propClone.transform;
                propCloneTransform.localPosition = sourcePropRoot.localPosition;
                propCloneTransform.localRotation = sourcePropRoot.localRotation;
                propCloneTransform.localScale = sourcePropRoot.localScale;
                propClone.SetActive(sourcePropRoot.gameObject.activeSelf);

                CopyPropChildHierarchyState(sourcePropRoot, propCloneTransform);
                CopyRendererStateBetweenPropInstances(sourcePropRoot, propCloneTransform);

                snapshot.props.Add(new PropInstanceSnapshot
                {
                    rigName = sourceRig.name,
                    propRoot = propClone
                });
            }
        }
    }

    private static GameObject EnsureSnapshotPropHost(CustomizationVisualSnapshot snapshot)
    {
        if (snapshot.propHost != null)
        {
            return snapshot.propHost;
        }

        GameObject host = new GameObject("MinimoCustomizationPropSnapshot");
        host.SetActive(false);
        DontDestroyOnLoad(host);
        snapshot.propHost = host;
        return host;
    }

    private void ApplyCustomizerVisualSnapshot(
        CustomizationVisualSnapshot snapshot,
        MinimoCharacterCustomizer target)
    {
        if (snapshot == null || target == null)
        {
            return;
        }

        for (int i = 0; i < snapshot.parts.Count; i++)
        {
            PartVisualSnapshot part = snapshot.parts[i];
            if (part == null)
            {
                continue;
            }

            target.SetPartVariant(part.slot, part.variantIndex);
        }

        target.ApplyCurrentSelection();
        ApplyPartMeshSnapshots(snapshot, target);

        for (int i = 0; i < snapshot.parts.Count; i++)
        {
            PartVisualSnapshot part = snapshot.parts[i];
            if (part == null)
            {
                continue;
            }

            for (int colorIndex = 0; colorIndex < part.colors.Count; colorIndex++)
            {
                PartColorSnapshot color = part.colors[colorIndex];
                target.SetPartColor(part.slot, color.materialIndex, color.color);
            }
        }

        ApplyPropSnapshotsToCustomizer(snapshot, target);
        ApplyRuntimeMaterialSnapshots(snapshot, target);
        ApplyMaterialPropertyBlockSnapshots(snapshot, target);
        ApplyRendererEnabledSnapshots(snapshot, target);
    }

    private static void ApplyPartMeshSnapshots(
        CustomizationVisualSnapshot snapshot,
        MinimoCharacterCustomizer target)
    {
        for (int i = 0; i < snapshot.parts.Count; i++)
        {
            PartVisualSnapshot part = snapshot.parts[i];
            if (part == null)
            {
                continue;
            }

            target.ApplyExternalPartRendererState(
                part.slot,
                part.sharedMesh,
                part.sharedMaterials,
                part.localBounds,
                part.rootBoneName,
                part.boneNames,
                part.rendererEnabled);
        }
    }

    private static void ApplyRuntimeMaterialSnapshots(
        CustomizationVisualSnapshot snapshot,
        MinimoCharacterCustomizer target)
    {
        for (int i = 0; i < snapshot.parts.Count; i++)
        {
            PartVisualSnapshot part = snapshot.parts[i];
            if (part == null
                || part.runtimeMaterials == null
                || part.runtimeMaterials.Length == 0
                || !target.TryGetPartRenderer(part.slot, out SkinnedMeshRenderer targetRenderer)
                || targetRenderer == null)
            {
                continue;
            }

            Material[] targetMaterials = targetRenderer.materials;
            int targetCount = targetMaterials != null ? targetMaterials.Length : 0;
            int materialCount = Mathf.Max(targetCount, part.runtimeMaterials.Length);
            if (materialCount <= 0)
            {
                continue;
            }

            Material[] updatedMaterials = new Material[materialCount];
            for (int materialIndex = 0; materialIndex < targetCount; materialIndex++)
            {
                updatedMaterials[materialIndex] = targetMaterials[materialIndex];
            }

            for (int materialIndex = 0; materialIndex < part.runtimeMaterials.Length; materialIndex++)
            {
                Material sourceMaterial = part.runtimeMaterials[materialIndex];
                if (sourceMaterial == null)
                {
                    continue;
                }

                if (updatedMaterials[materialIndex] == null)
                {
                    updatedMaterials[materialIndex] = new Material(sourceMaterial);
                }
                else
                {
                    updatedMaterials[materialIndex].CopyPropertiesFromMaterial(sourceMaterial);
                }
            }

            targetRenderer.materials = updatedMaterials;
        }
    }

    private static void ApplyMaterialPropertyBlockSnapshots(
        CustomizationVisualSnapshot snapshot,
        MinimoCharacterCustomizer target)
    {
        for (int i = 0; i < snapshot.parts.Count; i++)
        {
            PartVisualSnapshot part = snapshot.parts[i];
            if (part == null
                || part.propertyBlocks == null
                || part.propertyBlocks.Length == 0
                || !target.TryGetPartRenderer(part.slot, out SkinnedMeshRenderer targetRenderer)
                || targetRenderer == null)
            {
                continue;
            }

            for (int materialIndex = 0; materialIndex < part.propertyBlocks.Length; materialIndex++)
            {
                targetRenderer.SetPropertyBlock(part.propertyBlocks[materialIndex], materialIndex);
            }
        }
    }

    private static void ApplyRendererEnabledSnapshots(
        CustomizationVisualSnapshot snapshot,
        MinimoCharacterCustomizer target)
    {
        for (int i = 0; i < snapshot.parts.Count; i++)
        {
            PartVisualSnapshot part = snapshot.parts[i];
            if (part == null
                || !target.TryGetPartRenderer(part.slot, out SkinnedMeshRenderer targetRenderer)
                || targetRenderer == null)
            {
                continue;
            }

            targetRenderer.enabled = part.rendererEnabled;
        }
    }

    private static void ApplyPropSnapshotsToCustomizer(
        CustomizationVisualSnapshot snapshot,
        MinimoCharacterCustomizer targetCustomizer)
    {
        if (snapshot == null || targetCustomizer == null)
        {
            return;
        }

        ClearAllPropsFromCharacter(targetCustomizer);
        if (snapshot.props.Count == 0)
        {
            return;
        }

        Transform targetRoot = targetCustomizer.transform;
        for (int i = 0; i < snapshot.props.Count; i++)
        {
            PropInstanceSnapshot prop = snapshot.props[i];
            if (prop == null || prop.propRoot == null || string.IsNullOrWhiteSpace(prop.rigName))
            {
                continue;
            }

            Transform targetRig = FindDescendantTransformByName(targetRoot, prop.rigName);
            if (targetRig == null)
            {
                string normalizedRigName = NormalizeBodyRegionKey(prop.rigName);
                targetRig = FindDescendantTransformByNormalizedName(targetRoot, normalizedRigName);
            }

            if (targetRig == null)
            {
                continue;
            }

            GameObject propClone = Instantiate(prop.propRoot, targetRig, false);
            if (propClone == null)
            {
                continue;
            }

            propClone.name = StripUnityCloneSuffix(prop.propRoot.name);
            StripUnityCloneSuffixesRecursively(propClone.transform);
            Transform propCloneTransform = propClone.transform;
            Transform sourceTransform = prop.propRoot.transform;
            propCloneTransform.localPosition = sourceTransform.localPosition;
            propCloneTransform.localRotation = sourceTransform.localRotation;
            propCloneTransform.localScale = sourceTransform.localScale;
            propClone.SetActive(prop.propRoot.activeSelf);

            CopyPropChildHierarchyState(sourceTransform, propCloneTransform);
            CopyRendererStateBetweenPropInstances(sourceTransform, propCloneTransform);
        }
    }

    private void ClearCarriedCustomizationSnapshot()
    {
        if (carriedCustomizationSnapshot == null)
        {
            return;
        }

        carriedCustomizationSnapshot.Dispose();
        carriedCustomizationSnapshot = null;
    }

    private void ClearCarriedCharacterVisual()
    {
        if (carriedCharacterVisual == null)
        {
            return;
        }

        DestroyUnityObject(carriedCharacterVisual);
        carriedCharacterVisual = null;
    }

    private static void DestroyUnityObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
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
        while (result.EndsWith(UnityCloneSuffix, StringComparison.Ordinal))
        {
            result = result.Substring(0, result.Length - UnityCloneSuffix.Length).TrimEnd();
        }

        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }

    private static GameObject ResolveControlledPlaygroundCharacter()
    {
        MinimoTpsController controller = MinimoUnityCompatibility.FindFirstObjectByType<MinimoTpsController>();
        if (controller != null && controller.gameObject != null)
        {
            return controller.gameObject;
        }

        CharacterController[] controllers = MinimoUnityCompatibility.FindObjectsByType<CharacterController>(true);
        GameObject fallbackCharacter = null;
        for (int i = 0; i < controllers.Length; i++)
        {
            CharacterController current = controllers[i];
            if (current == null || current.gameObject == null)
            {
                continue;
            }

            GameObject candidate = current.gameObject;
            if (!candidate.scene.IsValid() || !candidate.scene.isLoaded)
            {
                continue;
            }

            if (candidate.transform.Find(CharacterVisualRootName) != null)
            {
                return candidate;
            }

            fallbackCharacter ??= candidate;
        }

        if (fallbackCharacter != null)
        {
            return fallbackCharacter;
        }

        return GameObject.Find("Capsule");
    }

    private IEnumerator ApplyCarriedCharacterToCustomizationDeferred()
    {
        yield return null;
        ApplyCarriedCharacterToCustomization();
    }

    private void ApplyCarriedCharacterToCustomization()
    {
        returnToCustomizationPending = false;
        transferPending = false;

        if (carriedCustomizationSnapshot != null)
        {
            MinimoCharacterCustomizer snapshotTargetCustomizer =
                MinimoUnityCompatibility.FindFirstObjectByType<MinimoCharacterCustomizer>();
            if (snapshotTargetCustomizer == null)
            {
                ClearCarriedCustomizationSnapshot();
                return;
            }

            ApplyCustomizerVisualSnapshot(carriedCustomizationSnapshot, snapshotTargetCustomizer);

            MinimoCustomizationUIController snapshotUiController =
                MinimoUnityCompatibility.FindFirstObjectByType<MinimoCustomizationUIController>();
            if (snapshotUiController != null)
            {
                snapshotUiController.RefreshAfterTransferredCharacterVisualApplied();
            }

            ClearCarriedCustomizationSnapshot();
            return;
        }

        if (carriedCharacterVisual == null)
        {
            return;
        }

        MinimoCharacterCustomizer sourceCustomizer = carriedCharacterVisual.GetComponent<MinimoCharacterCustomizer>();
        sourceCustomizer ??= carriedCharacterVisual.GetComponentInChildren<MinimoCharacterCustomizer>(true);
        MinimoCharacterCustomizer targetCustomizer =
            MinimoUnityCompatibility.FindFirstObjectByType<MinimoCharacterCustomizer>();
        if (sourceCustomizer == null || targetCustomizer == null)
        {
            ClearCarriedCharacterVisual();
            return;
        }

        CopyFullCustomizerVisualState(sourceCustomizer, targetCustomizer);

        MinimoCustomizationUIController uiController =
            MinimoUnityCompatibility.FindFirstObjectByType<MinimoCustomizationUIController>();
        if (uiController != null)
        {
            uiController.RefreshAfterTransferredCharacterVisualApplied();
        }

        ClearCarriedCharacterVisual();
    }

    private void CopyFullCustomizerVisualState(MinimoCharacterCustomizer source, MinimoCharacterCustomizer target)
    {
        if (source == null || target == null)
        {
            return;
        }

        CopyCustomizerState(source, target);
        target.ApplyCurrentSelection();
        CopyPartRendererVisualState(source, target);
        CopyPropInstancesBetweenCustomizers(source, target);
        CopyRuntimeMaterialProperties(source, target);
        CopyMaterialPropertyBlocks(source, target);
        CopyRendererEnabledFlags(source, target);
    }

    private static void CopyPartRendererVisualState(
        MinimoCharacterCustomizer source,
        MinimoCharacterCustomizer target)
    {
        for (int i = 0; i < OrderedSlots.Length; i++)
        {
            MinimoCharacterCustomizer.PartSlot slot = OrderedSlots[i];
            if (!source.TryGetPartRenderer(slot, out SkinnedMeshRenderer sourceRenderer)
                || sourceRenderer == null)
            {
                continue;
            }

            Transform[] sourceBones = sourceRenderer.bones;
            string[] boneNames = sourceBones != null
                ? sourceBones.Select(bone => bone != null ? bone.name : string.Empty).ToArray()
                : Array.Empty<string>();
            target.ApplyExternalPartRendererState(
                slot,
                sourceRenderer.sharedMesh,
                sourceRenderer.sharedMaterials != null
                    ? sourceRenderer.sharedMaterials.ToArray()
                    : Array.Empty<Material>(),
                sourceRenderer.localBounds,
                sourceRenderer.rootBone != null ? sourceRenderer.rootBone.name : string.Empty,
                boneNames,
                sourceRenderer.enabled);
        }
    }

    private void CopyCustomizerState(MinimoCharacterCustomizer source, MinimoCharacterCustomizer target)
    {
        if (source == null || target == null)
        {
            return;
        }

        for (int i = 0; i < OrderedSlots.Length; i++)
        {
            MinimoCharacterCustomizer.PartSlot slot = OrderedSlots[i];
            target.SetPartVariant(slot, source.GetPartVariant(slot));
        }

        target.ApplyCurrentSelection();

        sharedPropertyBlock ??= new MaterialPropertyBlock();
        for (int i = 0; i < OrderedSlots.Length; i++)
        {
            MinimoCharacterCustomizer.PartSlot slot = OrderedSlots[i];
            if (!source.TryGetPartRenderer(slot, out SkinnedMeshRenderer sourceRenderer)
                || sourceRenderer == null
                || !target.TryGetPartRenderer(slot, out SkinnedMeshRenderer targetRenderer)
                || targetRenderer == null)
            {
                continue;
            }

            Material[] sourceMaterials = sourceRenderer.sharedMaterials;
            Material[] targetMaterials = targetRenderer.sharedMaterials;
            int materialCount = Math.Min(
                sourceMaterials != null ? sourceMaterials.Length : 0,
                targetMaterials != null ? targetMaterials.Length : 0);
            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                Material sourceMaterial = sourceMaterials[materialIndex];
                if (!TryResolveEffectiveBaseColor(sourceRenderer, sourceMaterial, materialIndex, out Color color))
                {
                    continue;
                }

                target.SetPartColor(slot, materialIndex, color);
            }
        }
    }

    private bool TryResolveEffectiveBaseColor(
        SkinnedMeshRenderer sourceRenderer,
        Material sourceMaterial,
        int materialIndex,
        out Color color)
    {
        color = Color.white;
        if (sourceRenderer == null || sourceMaterial == null)
        {
            return false;
        }

        sharedPropertyBlock ??= new MaterialPropertyBlock();
        sharedPropertyBlock.Clear();
        sourceRenderer.GetPropertyBlock(sharedPropertyBlock, materialIndex);
        if (sharedPropertyBlock != null && !sharedPropertyBlock.isEmpty)
        {
            if (sourceMaterial.HasProperty(ColorPropertyId))
            {
                color = sharedPropertyBlock.GetColor(ColorPropertyId);
                return true;
            }

            if (sourceMaterial.HasProperty(BaseColorPropertyId))
            {
                color = sharedPropertyBlock.GetColor(BaseColorPropertyId);
                return true;
            }
        }

        if (sourceMaterial.HasProperty(ColorPropertyId))
        {
            color = sourceMaterial.GetColor(ColorPropertyId);
            return true;
        }

        if (sourceMaterial.HasProperty(BaseColorPropertyId))
        {
            color = sourceMaterial.GetColor(BaseColorPropertyId);
            return true;
        }

        return false;
    }

    private static void CopyRuntimeMaterialProperties(MinimoCharacterCustomizer source, MinimoCharacterCustomizer target)
    {
        if (source == null || target == null)
        {
            return;
        }

        for (int i = 0; i < OrderedSlots.Length; i++)
        {
            MinimoCharacterCustomizer.PartSlot slot = OrderedSlots[i];
            if (!source.TryGetPartRenderer(slot, out SkinnedMeshRenderer sourceRenderer)
                || sourceRenderer == null
                || !target.TryGetPartRenderer(slot, out SkinnedMeshRenderer targetRenderer)
                || targetRenderer == null)
            {
                continue;
            }

            Material[] sourceMaterials = sourceRenderer.materials;
            Material[] targetMaterials = targetRenderer.materials;
            int materialCount = Math.Min(
                sourceMaterials != null ? sourceMaterials.Length : 0,
                targetMaterials != null ? targetMaterials.Length : 0);
            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                Material sourceMaterial = sourceMaterials[materialIndex];
                Material targetMaterial = targetMaterials[materialIndex];
                if (sourceMaterial == null || targetMaterial == null)
                {
                    continue;
                }

                targetMaterial.CopyPropertiesFromMaterial(sourceMaterial);
            }

            targetRenderer.materials = targetMaterials;
        }
    }

    private void CopyMaterialPropertyBlocks(MinimoCharacterCustomizer source, MinimoCharacterCustomizer target)
    {
        if (source == null || target == null)
        {
            return;
        }

        sharedPropertyBlock ??= new MaterialPropertyBlock();
        for (int i = 0; i < OrderedSlots.Length; i++)
        {
            MinimoCharacterCustomizer.PartSlot slot = OrderedSlots[i];
            if (!source.TryGetPartRenderer(slot, out SkinnedMeshRenderer sourceRenderer)
                || sourceRenderer == null
                || !target.TryGetPartRenderer(slot, out SkinnedMeshRenderer targetRenderer)
                || targetRenderer == null)
            {
                continue;
            }

            Material[] sourceMaterials = sourceRenderer.sharedMaterials;
            Material[] targetMaterials = targetRenderer.sharedMaterials;
            int materialCount = Math.Min(
                sourceMaterials != null ? sourceMaterials.Length : 0,
                targetMaterials != null ? targetMaterials.Length : 0);
            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                sharedPropertyBlock.Clear();
                sourceRenderer.GetPropertyBlock(sharedPropertyBlock, materialIndex);
                if (sharedPropertyBlock.isEmpty)
                {
                    targetRenderer.SetPropertyBlock(null, materialIndex);
                }
                else
                {
                    targetRenderer.SetPropertyBlock(sharedPropertyBlock, materialIndex);
                }
            }
        }
    }

    private static void CopyRendererEnabledFlags(MinimoCharacterCustomizer source, MinimoCharacterCustomizer target)
    {
        if (source == null || target == null)
        {
            return;
        }

        for (int i = 0; i < OrderedSlots.Length; i++)
        {
            MinimoCharacterCustomizer.PartSlot slot = OrderedSlots[i];
            if (!source.TryGetPartRenderer(slot, out SkinnedMeshRenderer sourceRenderer)
                || sourceRenderer == null
                || !target.TryGetPartRenderer(slot, out SkinnedMeshRenderer targetRenderer)
                || targetRenderer == null)
            {
                continue;
            }

            targetRenderer.enabled = sourceRenderer.enabled;
        }
    }

    private static void CopyPropInstancesBetweenCustomizers(
        MinimoCharacterCustomizer sourceCustomizer,
        MinimoCharacterCustomizer targetCustomizer)
    {
        if (sourceCustomizer == null || targetCustomizer == null)
        {
            return;
        }

        ClearAllPropsFromCharacter(targetCustomizer);

        Transform sourceRoot = sourceCustomizer.transform;
        Transform targetRoot = targetCustomizer.transform;
        if (sourceRoot == null || targetRoot == null)
        {
            return;
        }

        Transform[] sourceTransforms = sourceRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < sourceTransforms.Length; i++)
        {
            Transform sourceRig = sourceTransforms[i];
            if (sourceRig == null)
            {
                continue;
            }

            bool hasPropChild = false;
            for (int childIndex = 0; childIndex < sourceRig.childCount; childIndex++)
            {
                Transform sourceChild = sourceRig.GetChild(childIndex);
                if (sourceChild != null && IsPropInstanceName(sourceChild.name))
                {
                    hasPropChild = true;
                    break;
                }
            }

            if (!hasPropChild)
            {
                continue;
            }

            Transform targetRig = FindDescendantTransformByName(targetRoot, sourceRig.name);
            if (targetRig == null)
            {
                string normalizedRigName = NormalizeBodyRegionKey(sourceRig.name);
                targetRig = FindDescendantTransformByNormalizedName(targetRoot, normalizedRigName);
            }

            if (targetRig == null)
            {
                continue;
            }

            for (int childIndex = 0; childIndex < sourceRig.childCount; childIndex++)
            {
                Transform sourcePropRoot = sourceRig.GetChild(childIndex);
                if (sourcePropRoot == null || !IsPropInstanceName(sourcePropRoot.name))
                {
                    continue;
                }

                GameObject propClone = Instantiate(sourcePropRoot.gameObject, targetRig, false);
                if (propClone == null)
                {
                    continue;
                }

                propClone.name = StripUnityCloneSuffix(sourcePropRoot.name);
                StripUnityCloneSuffixesRecursively(propClone.transform);
                Transform propCloneTransform = propClone.transform;
                propCloneTransform.localPosition = sourcePropRoot.localPosition;
                propCloneTransform.localRotation = sourcePropRoot.localRotation;
                propCloneTransform.localScale = sourcePropRoot.localScale;
                propClone.SetActive(sourcePropRoot.gameObject.activeSelf);

                // Root carries rig placement; child hierarchy carries visual state.
                CopyPropChildHierarchyState(sourcePropRoot, propCloneTransform);
                CopyRendererStateBetweenPropInstances(sourcePropRoot, propCloneTransform);
            }
        }
    }

    private static void ClearAllPropsFromCharacter(MinimoCharacterCustomizer targetCustomizer)
    {
        if (targetCustomizer == null)
        {
            return;
        }

        Transform[] transforms = targetCustomizer.GetComponentsInChildren<Transform>(true);
        List<GameObject> toDestroy = new List<GameObject>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];
            if (current == null)
            {
                continue;
            }

            for (int childIndex = 0; childIndex < current.childCount; childIndex++)
            {
                Transform child = current.GetChild(childIndex);
                if (child != null && IsPropInstanceName(child.name))
                {
                    toDestroy.Add(child.gameObject);
                }
            }
        }

        for (int i = 0; i < toDestroy.Count; i++)
        {
            GameObject target = toDestroy[i];
            if (target == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                target.transform.SetParent(null, false);
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }

    private static void CopyRendererStateBetweenPropInstances(Transform sourceRoot, Transform targetRoot)
    {
        if (sourceRoot == null || targetRoot == null)
        {
            return;
        }

        Transform sourceVisualRoot = ResolvePropVisualStateRoot(sourceRoot);
        Transform targetVisualRoot = ResolvePropVisualStateRoot(targetRoot);
        if (sourceVisualRoot == null || targetVisualRoot == null)
        {
            return;
        }

        Renderer[] sourceRenderers = sourceVisualRoot.GetComponentsInChildren<Renderer>(true);
        Renderer[] targetRenderers = targetVisualRoot.GetComponentsInChildren<Renderer>(true);
        int rendererCount = Mathf.Min(sourceRenderers.Length, targetRenderers.Length);
        if (rendererCount <= 0)
        {
            return;
        }

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        for (int rendererIndex = 0; rendererIndex < rendererCount; rendererIndex++)
        {
            Renderer sourceRenderer = sourceRenderers[rendererIndex];
            Renderer targetRenderer = targetRenderers[rendererIndex];
            if (sourceRenderer == null || targetRenderer == null)
            {
                continue;
            }

            targetRenderer.enabled = sourceRenderer.enabled;
            targetRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
            targetRenderer.receiveShadows = sourceRenderer.receiveShadows;

            Material[] sourceSharedMaterials = sourceRenderer.sharedMaterials;
            Material[] sourceRuntimeMaterials = GetSafeSourceRuntimeMaterials(sourceRenderer);

            if (sourceSharedMaterials != null)
            {
                Material[] targetSharedMaterials = new Material[sourceSharedMaterials.Length];
                for (int materialIndex = 0; materialIndex < sourceSharedMaterials.Length; materialIndex++)
                {
                    targetSharedMaterials[materialIndex] = sourceSharedMaterials[materialIndex];
                }

                targetRenderer.sharedMaterials = targetSharedMaterials;
            }

            int runtimeCount = sourceRuntimeMaterials != null ? sourceRuntimeMaterials.Length : 0;
            int sharedCount = sourceSharedMaterials != null ? sourceSharedMaterials.Length : 0;
            int materialCount = Mathf.Max(runtimeCount, sharedCount);
            if (materialCount > 0)
            {
                Material[] targetRuntimeMaterials = new Material[materialCount];
                for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
                {
                    Material authoredMaterial = materialIndex < sharedCount
                        ? sourceSharedMaterials[materialIndex]
                        : null;
                    Material runtimeMaterial = materialIndex < runtimeCount
                        ? sourceRuntimeMaterials[materialIndex]
                        : null;
                    Material baseMaterial = authoredMaterial != null ? authoredMaterial : runtimeMaterial;
                    if (baseMaterial == null)
                    {
                        targetRuntimeMaterials[materialIndex] = null;
                        continue;
                    }

                    Material clonedMaterial = new Material(baseMaterial);
                    if (runtimeMaterial != null)
                    {
                        clonedMaterial.CopyPropertiesFromMaterial(runtimeMaterial);
                    }

                    targetRuntimeMaterials[materialIndex] = clonedMaterial;
                }

                targetRenderer.materials = targetRuntimeMaterials;
            }

            int sourceSharedCount = sourceRenderer.sharedMaterials != null ? sourceRenderer.sharedMaterials.Length : 0;
            for (int materialIndex = 0; materialIndex < sourceSharedCount; materialIndex++)
            {
                block.Clear();
                sourceRenderer.GetPropertyBlock(block, materialIndex);
                if (block.isEmpty)
                {
                    targetRenderer.SetPropertyBlock(null, materialIndex);
                }
                else
                {
                    targetRenderer.SetPropertyBlock(block, materialIndex);
                }
            }
        }
    }

    private static Transform ResolvePropVisualStateRoot(Transform propRoot)
    {
        if (propRoot == null)
        {
            return null;
        }

        return propRoot.childCount > 0 ? propRoot.GetChild(0) : propRoot;
    }

    private static void CopyPropChildHierarchyState(Transform sourcePropRoot, Transform targetPropRoot)
    {
        if (sourcePropRoot == null || targetPropRoot == null)
        {
            return;
        }

        int childCount = Math.Min(sourcePropRoot.childCount, targetPropRoot.childCount);
        for (int i = 0; i < childCount; i++)
        {
            Transform sourceChild = sourcePropRoot.GetChild(i);
            Transform targetChild = targetPropRoot.GetChild(i);
            CopyTransformHierarchyStateRecursive(sourceChild, targetChild);
        }
    }

    private static void CopyTransformHierarchyStateRecursive(Transform source, Transform target)
    {
        if (source == null || target == null)
        {
            return;
        }

        target.localPosition = source.localPosition;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
        target.gameObject.SetActive(source.gameObject.activeSelf);

        int childCount = Math.Min(source.childCount, target.childCount);
        for (int i = 0; i < childCount; i++)
        {
            CopyTransformHierarchyStateRecursive(source.GetChild(i), target.GetChild(i));
        }
    }

    private static Material[] GetSafeSourceRuntimeMaterials(Renderer renderer)
    {
        if (renderer == null)
        {
            return Array.Empty<Material>();
        }

        GameObject rendererObject = renderer.gameObject;
        bool canAccessInstanceMaterials = rendererObject != null
            && rendererObject.scene.IsValid()
            && rendererObject.scene.isLoaded;
        if (canAccessInstanceMaterials)
        {
            try
            {
                Material[] runtimeMaterials = renderer.materials;
                if (runtimeMaterials != null)
                {
                    return runtimeMaterials;
                }
            }
            catch
            {
                // Accessing .materials on prefab assets throws in editor.
            }
        }

        return renderer.sharedMaterials ?? Array.Empty<Material>();
    }

    private static Transform FindDescendantTransformByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform candidate = descendants[i];
            if (candidate != null && string.Equals(candidate.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static Transform FindDescendantTransformByNormalizedName(Transform root, string normalizedName)
    {
        if (root == null || string.IsNullOrWhiteSpace(normalizedName))
        {
            return null;
        }

        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform candidate = descendants[i];
            if (candidate == null)
            {
                continue;
            }

            string candidateName = NormalizeBodyRegionKey(candidate.name);
            if (string.Equals(candidateName, normalizedName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string NormalizeBodyRegionKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.Trim().ToUpperInvariant();
        normalized = normalized
            .Replace('-', '_')
            .Replace(' ', '_')
            .Replace('/', '_')
            .Replace('\\', '_');

        while (normalized.IndexOf("__", StringComparison.Ordinal) >= 0)
        {
            normalized = normalized.Replace("__", "_");
        }

        return normalized.Trim('_');
    }

    private static bool IsPropInstanceName(string objectName)
    {
        return !string.IsNullOrWhiteSpace(objectName)
               && objectName.IndexOf("_prop-", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void LoadPlaygroundScene()
    {
        BeginSceneLoad(PlaygroundScenePath, PlaygroundSceneName);
    }

    private void LoadCustomizationScene()
    {
        BeginSceneLoad(CustomizationScenePath, CustomizationSceneName);
    }

    private void BeginSceneLoad(string scenePath, string sceneName)
    {
        if (sceneLoadPending)
        {
            return;
        }

        if (pendingSceneLoadCoroutine != null)
        {
            StopCoroutine(pendingSceneLoadCoroutine);
        }

        pendingSceneLoadCoroutine = StartCoroutine(LoadSceneAsyncDeferred(scenePath, sceneName));
    }

    private IEnumerator LoadSceneAsyncDeferred(string scenePath, string sceneName)
    {
        sceneLoadPending = true;
        yield return null;

        ThreadPriority previousPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = ThreadPriority.Low;
        try
        {
            AsyncOperation loadOperation = CreateSceneLoadOperation(scenePath, sceneName);
            if (loadOperation == null)
            {
#if UNITY_EDITOR
                EditorSceneManager.LoadSceneInPlayMode(
                    scenePath,
                    new LoadSceneParameters(LoadSceneMode.Single));
#endif
                yield break;
            }

            loadOperation.allowSceneActivation = false;
            while (loadOperation.progress < 0.9f)
            {
                yield return null;
            }

            yield return null;
            loadOperation.allowSceneActivation = true;
            while (!loadOperation.isDone)
            {
                yield return null;
            }
        }
        finally
        {
            Application.backgroundLoadingPriority = previousPriority;
            pendingSceneLoadCoroutine = null;
            sceneLoadPending = false;
            playButtonPressPending = false;
        }
    }

    private static AsyncOperation CreateSceneLoadOperation(string scenePath, string sceneName)
    {
        int buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);
        if (buildIndex >= 0)
        {
            return SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
        }

        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }

        return null;
    }

    private static void DisableCustomizationOnlyComponents(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        MinimoCharacterCustomizer[] customizers = root.GetComponentsInChildren<MinimoCharacterCustomizer>(true);
        for (int i = 0; i < customizers.Length; i++)
        {
            if (customizers[i] != null)
            {
                customizers[i].enabled = false;
            }
        }

        MinimoCharacterDragRotate[] dragRotateScripts = root.GetComponentsInChildren<MinimoCharacterDragRotate>(true);
        for (int i = 0; i < dragRotateScripts.Length; i++)
        {
            if (dragRotateScripts[i] != null)
            {
                dragRotateScripts[i].enabled = false;
            }
        }
    }
}
