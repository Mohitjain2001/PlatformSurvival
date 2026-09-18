using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[AddComponentMenu("Minimo/Character Customizer")]
public class MinimoCharacterCustomizer : MonoBehaviour
{
    public enum PartSlot
    {
        ArmLeft = 0,
        ArmRight = 1,
        ShoulderLeft = 2,
        ShoulderRight = 3,
        HandLeft = 4,
        HandRight = 5,
        Hips = 6,
        LegUpperLeft = 7,
        LegUpperRight = 8,
        BootsLeft = 9,
        BootsRight = 10,
        Torso = 11,
        Hair = 12,
        Face = 13,
        Props = 14
    }

    [Serializable]
    public sealed class VariantDefinition
    {
        [Tooltip("Configures display name.")]
        public string variantId = "FIGURE_SET_01";

        [Tooltip("Configures display name.")]
        public string displayName = "Figure Set 01";

        [Tooltip("Configures source character.")]
        public GameObject sourceCharacter;
    }

    [Serializable]
    private sealed class SlotPrefabSource
    {
        public PartSlot slot;
        [Tooltip("Configures folder path.")]
        public string folderPath;
        public List<GameObject> prefabs = new List<GameObject>();
        public List<string> prefabCategories = new List<string>();
    }

    [Serializable]
    private sealed class PartBinding
    {
        public PartSlot slot;
        public Transform slotRoot;
        public SkinnedMeshRenderer targetRenderer;

        [NonSerialized] public bool defaultCaptured;
        [NonSerialized] public bool defaultEnabled;
        [NonSerialized] public Mesh defaultMesh;
        [NonSerialized] public Material[] defaultMaterials;
        [NonSerialized] public Transform defaultRootBone;
        [NonSerialized] public Transform[] defaultBones;
        [NonSerialized] public Bounds defaultLocalBounds;
    }

    [Serializable]
    private sealed class PartSelection
    {
        public PartSlot slot;
        [Tooltip("Configures class.")]
        public int variantIndex;
    }

    [Serializable]
    private sealed class PartColorSelection
    {
        public PartSlot slot;
        [Tooltip("Configures use custom color.")]
        public int materialIndex = -1;
        public bool useCustomColor;
        public Color color = Color.white;
    }

    private static readonly PartSlot[] OrderedSlots =
    {
        PartSlot.ArmLeft,
        PartSlot.ArmRight,
        PartSlot.ShoulderLeft,
        PartSlot.ShoulderRight,
        PartSlot.HandLeft,
        PartSlot.HandRight,
        PartSlot.Hips,
        PartSlot.LegUpperLeft,
        PartSlot.LegUpperRight,
        PartSlot.BootsLeft,
        PartSlot.BootsRight,
        PartSlot.Torso,
        PartSlot.Hair,
        PartSlot.Face
    };

    private static readonly Dictionary<PartSlot, string> SlotDisplayNames = new Dictionary<PartSlot, string>()
    {
        [PartSlot.ArmLeft] = "Arm_L",
        [PartSlot.ArmRight] = "Arm_R",
        [PartSlot.ShoulderLeft] = "Shoulder_L",
        [PartSlot.ShoulderRight] = "Shoulder_R",
        [PartSlot.HandLeft] = "Hand_L",
        [PartSlot.HandRight] = "Hand_R",
        [PartSlot.Hips] = "Hips",
        [PartSlot.LegUpperLeft] = "Leg_Upper_L",
        [PartSlot.LegUpperRight] = "Leg_Upper_R",
        [PartSlot.BootsLeft] = "Boots_L",
        [PartSlot.BootsRight] = "Boots_R",
        [PartSlot.Torso] = "Torso",
        [PartSlot.Hair] = "Hair",
        [PartSlot.Face] = "Face"
    };

    private const string SlotsRootName = "CustomizationSlots";
    private const string DefaultPartsRootName = "CH_Default_GRP";
#if UNITY_EDITOR
    private static readonly string[] EditorCasualFolderCandidates =
    {
        "Assets/Minimo/Character/Categories/Folders/Casual"
    };
#endif
    private static readonly Dictionary<PartSlot, string[]> CustomizeFolderNamesBySlot = new Dictionary<PartSlot, string[]>()
    {
        [PartSlot.ShoulderLeft] = new[] { "ShoulderLeft" },
        [PartSlot.ShoulderRight] = new[] { "ShoulderRight" },
        [PartSlot.ArmLeft] = new[] { "ArmLeft" },
        [PartSlot.ArmRight] = new[] { "ArmRight" },
        [PartSlot.HandLeft] = new[] { "HandsLeft", "HandLeft" },
        [PartSlot.HandRight] = new[] { "HandsRight", "HandRight" },
        [PartSlot.Hips] = new[] { "Hips" },
        [PartSlot.LegUpperLeft] = new[] { "LegLeft" },
        [PartSlot.LegUpperRight] = new[] { "LegRight" },
        [PartSlot.BootsLeft] = new[] { "ShoeLeft" },
        [PartSlot.BootsRight] = new[] { "ShoeRight" },
        [PartSlot.Torso] = new[] { "Chests", "Chest" },
        [PartSlot.Hair] = new[] { "Hairs" },
        [PartSlot.Face] = new[] { "Heads", "Faces", "Face" }
    };

    [Header("Setup")]
    [SerializeField] private bool autoBindExistingRenderers = true;
    [SerializeField] private bool normalizeChildNames = true;
    [SerializeField] private bool applyOnStart = false;
    [SerializeField] private int defaultVariantForAllParts = 0;
    [SerializeField] private bool keepAssignedMaterialsOnApply = true;

    [Header("Data")]
    [SerializeField] private List<VariantDefinition> variants = new List<VariantDefinition>();
    [SerializeField] private bool useCustomizeFolderSource = true;
    [SerializeField] private string customizeRootFolder = "Assets/Minimo/Customize";
    [SerializeField] private bool autoLoadCustomizeFolderInEditor = true;
    [SerializeField] private List<SlotPrefabSource> customizeSlotSources = new List<SlotPrefabSource>();
    [SerializeField] private List<PartBinding> partBindings = new List<PartBinding>();
    [SerializeField] private List<PartSelection> partSelections = new List<PartSelection>();
    [SerializeField] private List<PartColorSelection> partColorSelections = new List<PartColorSelection>();

    private readonly Dictionary<GameObject, Dictionary<PartSlot, SkinnedMeshRenderer>> sourceCache = new Dictionary<GameObject, Dictionary<PartSlot, SkinnedMeshRenderer>>();
    private readonly Dictionary<(GameObject prefab, PartSlot slot), SkinnedMeshRenderer> sourceRendererCache = new Dictionary<(GameObject prefab, PartSlot slot), SkinnedMeshRenderer>();
    private MaterialPropertyBlock materialPropertyBlock;
    private Dictionary<string, Transform> skeletonMap;
    private bool isDirty = true;
    private static readonly int ColorShaderProperty = Shader.PropertyToID("_Color");
    private static readonly int BaseColorShaderProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int HighlightShaderProperty = Shader.PropertyToID("_Highlight");
    private static readonly int ShadowShaderProperty = Shader.PropertyToID("_Shadow");
    private static readonly Color SkinReferenceBaseColor = new Color32(0xFF, 0xCC, 0xB2, 0xFF);
    private static readonly Color SkinReferenceHighlightColor = new Color32(0x66, 0x4B, 0x3D, 0xFF);
    private static readonly Color SkinReferenceShadowColor = new Color32(0xB2, 0xA1, 0x98, 0xFF);
    private static readonly Color OtherReferenceBaseColor = new Color32(0xAF, 0xBA, 0xF0, 0xFF);
    private static readonly Color OtherReferenceHighlightColor = new Color32(0x33, 0x4D, 0xCC, 0xFF);
    private static readonly Color OtherReferenceShadowColor = new Color32(0x00, 0x19, 0x4C, 0xFF);
#if UNITY_EDITOR
    private const string SavedPrefabCharactersRoot = "Assets/Minimo/Character/SavedPrefabs/Characters";
#endif

    public static IReadOnlyList<PartSlot> Slots => OrderedSlots;
    public bool UsesCustomizeFolderSource => useCustomizeFolderSource;

    private void Reset()
    {
        EnsureSerializationLayout();
        MigrateAwayFromCustomizationSlots();
        AutoBindRenderers();
        SetAllPartsToVariant(defaultVariantForAllParts);
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

#if UNITY_EDITOR
        // PrefabImporter can invoke OnValidate while it is producing an import
        // artifact. AssetDatabase queries and serialized mutations from that
        // context can make two imports of the same prefab produce different
        // results. Prefab Mode objects are not persistent, so authoring there
        // still receives the normal validation pass.
        if (EditorUtility.IsPersistent(this) || PrefabUtility.IsPartOfPrefabAsset(gameObject))
        {
            return;
        }

        bool isSavedPrefabAsset = IsSavedPrefabAsset();
        if (isSavedPrefabAsset)
        {
            return;
        }
#endif

        EnsureSerializationLayout();

#if UNITY_EDITOR
        if (!isSavedPrefabAsset
            && useCustomizeFolderSource
            && autoLoadCustomizeFolderInEditor
            && EditorCustomizeSourcesNeedReload())
        {
            EditorLoadVariantsFromCustomizeFolder();
        }
#endif

        MigrateAwayFromCustomizationSlots();

        if (autoBindExistingRenderers
#if UNITY_EDITOR
            && !isSavedPrefabAsset
#endif
            )
        {
            AutoBindRenderers();
        }
    }

    private void Start()
    {
#if UNITY_EDITOR
        if (useCustomizeFolderSource)
        {
            if (autoLoadCustomizeFolderInEditor || GetCustomizeVariantCount() == 0)
            {
                EditorLoadVariantsFromCustomizeFolder(applyAfterLoad: false);
            }
        }
        else if (variants == null || variants.Count == 0)
        {
            EditorLoadVariantsFromCasualFolder(applyAfterLoad: false);
        }
#endif

        MigrateAwayFromCustomizationSlots();
        if (applyOnStart)
        {
            ApplyCurrentSelection();
        }
    }

    public void PreserveHairDefaultOffOnStart()
    {
        ApplyHairNothingSelection();
    }

    public void PrepareRuntimeTransferTarget()
    {
        EnsureSerializationLayout();
        MigrateAwayFromCustomizationSlots();
        AutoBindRenderers();
#if UNITY_EDITOR
        if (useCustomizeFolderSource)
        {
            if (EditorCustomizeSourcesNeedReload() || GetVariantCount() == 0)
            {
                EditorLoadVariantsFromCustomizeFolder(applyAfterLoad: false);
            }
        }
        else if (variants == null || variants.Count == 0)
        {
            EditorLoadVariantsFromCasualFolder(applyAfterLoad: false);
        }
#endif
        InvalidateBindingCache();
    }

    public bool ApplyExternalPartRendererState(
        PartSlot slot,
        Mesh mesh,
        Material[] sharedMaterials,
        Bounds localBounds,
        string rootBoneName,
        string[] boneNames,
        bool rendererEnabled)
    {
        if (!TryGetOrCreatePartRendererForTransfer(slot, out SkinnedMeshRenderer renderer) || renderer == null)
        {
            return false;
        }

        EnsureBindingCache();
        renderer.sharedMesh = mesh;
        renderer.sharedMaterials = sharedMaterials != null ? sharedMaterials.ToArray() : Array.Empty<Material>();
        renderer.localBounds = localBounds;

        Transform resolvedRootBone = ResolveBoneByName(rootBoneName) ?? renderer.rootBone;
        if (resolvedRootBone != null)
        {
            renderer.rootBone = resolvedRootBone;
        }

        if (boneNames != null && boneNames.Length > 0)
        {
            Transform fallback = renderer.rootBone;
            Transform[] remappedBones = new Transform[boneNames.Length];
            for (int i = 0; i < boneNames.Length; i++)
            {
                remappedBones[i] = ResolveBoneByName(boneNames[i]) ?? fallback;
            }

            renderer.bones = remappedBones;
        }

        renderer.enabled = rendererEnabled && mesh != null;
        return true;
    }

    private void ApplyHairNothingSelection()
    {
        SetPartVariant(PartSlot.Hair, -1);

        PartBinding hairBinding = GetBinding(PartSlot.Hair);
        if (hairBinding?.targetRenderer != null)
        {
            hairBinding.targetRenderer.enabled = false;
        }
    }

#if UNITY_EDITOR
    private bool IsSavedPrefabAsset()
    {
        string assetPath = AssetDatabase.GetAssetPath(gameObject);
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            assetPath = AssetDatabase.GetAssetPath(this);
        }

        assetPath = NormalizeAssetPath(assetPath);
        return !string.IsNullOrWhiteSpace(assetPath)
               && assetPath.StartsWith(SavedPrefabCharactersRoot + "/", StringComparison.OrdinalIgnoreCase)
               && assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
    }
#endif

    public void SetAllPartsToVariant(int variantIndex)
    {
        EnsureSerializationLayout();
        for (int i = 0; i < partSelections.Count; i++)
        {
            PartSelection selection = partSelections[i];
            if (selection == null)
            {
                continue;
            }

            int slotVariantCount = GetVariantCountForSlot(selection.slot);
            selection.variantIndex = variantIndex >= 0 && variantIndex < slotVariantCount
                ? variantIndex
                : -1;
        }

        for (int i = 0; i < partColorSelections.Count; i++)
        {
            PartColorSelection colorSelection = partColorSelections[i];
            if (colorSelection == null)
            {
                continue;
            }

            colorSelection.useCustomColor = false;
        }
    }

    public void ResetToDefaultCharacter()
    {
        SetAllPartsToVariant(-1);
        EnsureSerializationLayout();
        for (int i = 0; i < partColorSelections.Count; i++)
        {
            PartColorSelection selection = partColorSelections[i];
            if (selection == null)
            {
                continue;
            }

            selection.useCustomColor = false;
        }

        ApplyCurrentSelection();
    }

    public void SetPartVariant(PartSlot slot, int variantIndex)
    {
        EnsureSerializationLayout();
        PartSelection selection = GetSelection(slot);
        int slotVariantCount = GetVariantCountForSlot(slot);
        selection.variantIndex = variantIndex >= 0 && variantIndex < slotVariantCount ? variantIndex : -1;

        for (int i = 0; i < partColorSelections.Count; i++)
        {
            PartColorSelection colorSelection = partColorSelections[i];
            if (colorSelection == null || colorSelection.slot != slot)
            {
                continue;
            }

            colorSelection.useCustomColor = false;
        }

        ApplyColorOverride(slot);
    }

    public int GetPartVariant(PartSlot slot)
    {
        EnsureSerializationLayout();
        PartSelection selection = GetSelection(slot);
        return selection != null ? selection.variantIndex : -1;
    }

    public void SetPartColor(PartSlot slot, Color color)
    {
        SetPartColor(slot, -1, color);
    }

    public void SetPartColor(PartSlot slot, int materialIndex, Color color)
    {
        EnsureSerializationLayout();
        PartColorSelection selection = GetColorSelection(slot, materialIndex, true);
        if (selection == null)
        {
            return;
        }

        if (selection.useCustomColor && ColorsApproximatelyEqual(selection.color, color))
        {
            return;
        }

        selection.useCustomColor = true;
        selection.color = color;
        ApplyColorOverride(slot);
    }

    public void SetPartColors(IEnumerable<(PartSlot slot, int materialIndex, Color color)> colorAssignments)
    {
        if (colorAssignments == null)
        {
            return;
        }

        EnsureSerializationLayout();
        List<PartSlot> dirtySlots = new List<PartSlot>();
        foreach ((PartSlot slot, int materialIndex, Color color) assignment in colorAssignments)
        {
            PartColorSelection selection = GetColorSelection(assignment.slot, assignment.materialIndex, true);
            if (selection == null)
            {
                continue;
            }

            if (selection.useCustomColor && ColorsApproximatelyEqual(selection.color, assignment.color))
            {
                continue;
            }

            selection.useCustomColor = true;
            selection.color = assignment.color;
            if (!dirtySlots.Contains(assignment.slot))
            {
                dirtySlots.Add(assignment.slot);
            }
        }

        for (int i = 0; i < dirtySlots.Count; i++)
        {
            ApplyColorOverride(dirtySlots[i]);
        }
    }

    public void ClearPartColor(PartSlot slot)
    {
        EnsureSerializationLayout();
        for (int i = 0; i < partColorSelections.Count; i++)
        {
            PartColorSelection selection = partColorSelections[i];
            if (selection == null || selection.slot != slot)
            {
                continue;
            }

            selection.useCustomColor = false;
        }

        ApplyColorOverride(slot);
    }

    public void ClearPartColor(PartSlot slot, int materialIndex)
    {
        EnsureSerializationLayout();
        PartColorSelection selection = GetColorSelection(slot, materialIndex, false);
        if (selection == null)
        {
            return;
        }

        selection.useCustomColor = false;
        ApplyColorOverride(slot);
    }

    public bool TryGetPartColor(PartSlot slot, out Color color)
    {
        return TryGetPartColor(slot, -1, out color);
    }

    public bool TryGetPartColor(PartSlot slot, int materialIndex, out Color color)
    {
        color = Color.white;
        EnsureSerializationLayout();
        PartColorSelection selection = GetColorSelection(slot, materialIndex, false);
        if ((selection == null || !selection.useCustomColor) && materialIndex >= 0)
        {
            selection = GetColorSelection(slot, -1, false);
        }

        if (selection == null || !selection.useCustomColor)
        {
            return false;
        }

        color = selection.color;
        return true;
    }

    public int GetPartEditableColorCount(PartSlot slot)
    {
        EnsureSerializationLayout();
        PartBinding binding = GetBinding(slot);
        if (binding?.targetRenderer == null)
        {
            return 0;
        }

        Material[] materials = binding.targetRenderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < materials.Length; i++)
        {
            if (MaterialSupportsColor(materials[i]))
            {
                count++;
            }
        }

        return count;
    }

    public bool TryGetPartRenderer(PartSlot slot, out SkinnedMeshRenderer renderer)
    {
        renderer = null;
        EnsureSerializationLayout();
        PartBinding binding = GetBinding(slot);
        if (binding?.targetRenderer == null)
        {
            return false;
        }

        renderer = binding.targetRenderer;
        return true;
    }

    public bool TryGetVariantPartPreviewData(
        int variantIndex,
        PartSlot slot,
        out Mesh mesh,
        out Material[] materials,
        out Bounds localBounds)
    {
        return TryGetVariantPartPreviewData(
            variantIndex,
            slot,
            out mesh,
            out materials,
            out localBounds,
            out _);
    }

    public bool TryGetVariantPartPreviewData(
        int variantIndex,
        PartSlot slot,
        out Mesh mesh,
        out Material[] materials,
        out Bounds localBounds,
        out Quaternion previewRotationHint)
    {
        mesh = null;
        materials = Array.Empty<Material>();
        localBounds = default;
        previewRotationHint = Quaternion.identity;

        if (!TryResolveSourceRenderer(slot, variantIndex, out SkinnedMeshRenderer sourceRenderer))
        {
            return false;
        }

        if (sourceRenderer.sharedMesh == null)
        {
            return false;
        }

        mesh = sourceRenderer.sharedMesh;
        materials = sourceRenderer.sharedMaterials != null ? sourceRenderer.sharedMaterials.ToArray() : Array.Empty<Material>();
        localBounds = sourceRenderer.localBounds.size.sqrMagnitude > 0.000001f
            ? sourceRenderer.localBounds
            : mesh.bounds;
        previewRotationHint = sourceRenderer.transform != null ? sourceRenderer.transform.localRotation : Quaternion.identity;
        return true;
    }

    public bool TryGetVariantSourceCharacter(int variantIndex, out GameObject sourceCharacter)
    {
        sourceCharacter = null;
        if (variantIndex < 0)
        {
            return false;
        }

        if (useCustomizeFolderSource)
        {
            for (int i = 0; i < OrderedSlots.Length; i++)
            {
                if (TryGetCustomizeVariantSourcePrefab(OrderedSlots[i], variantIndex, out sourceCharacter))
                {
                    return sourceCharacter != null;
                }
            }

            return false;
        }

        if (variants == null || variantIndex >= variants.Count)
        {
            return false;
        }

        VariantDefinition variant = variants[variantIndex];
        if (variant == null || variant.sourceCharacter == null)
        {
            return false;
        }

        sourceCharacter = variant.sourceCharacter;
        return true;
    }

    public bool TryGetVariantSourcePrefabForSlot(PartSlot slot, int variantIndex, out GameObject sourcePrefab)
    {
        sourcePrefab = null;
        if (variantIndex < 0)
        {
            return false;
        }

        if (useCustomizeFolderSource)
        {
            return TryGetCustomizeVariantSourcePrefab(slot, variantIndex, out sourcePrefab) && sourcePrefab != null;
        }

        if (variants == null || variantIndex >= variants.Count)
        {
            return false;
        }

        VariantDefinition variant = variants[variantIndex];
        if (variant?.sourceCharacter == null)
        {
            return false;
        }

        sourcePrefab = variant.sourceCharacter;
        return true;
    }

    public int GetVariantCount()
    {
        int customizeCount = GetCustomizeVariantCount();
        if (useCustomizeFolderSource)
        {
            return customizeCount;
        }

        return variants != null ? variants.Count : 0;
    }

    public string GetVariantDisplayName(int variantIndex)
    {
        if (variantIndex < 0)
        {
            return "None";
        }

        if (useCustomizeFolderSource && TryGetCustomizeVariantDisplayName(variantIndex, out string customizeName))
        {
            return customizeName;
        }

        if (useCustomizeFolderSource)
        {
            return variantIndex < GetVariantCount()
                ? $"Set {variantIndex + 1:00}"
                : "None";
        }

        if (variants == null || variantIndex >= variants.Count)
        {
            return $"Set {variantIndex + 1:00}";
        }

        VariantDefinition variant = variants[variantIndex];
        if (variant == null)
        {
            return "None";
        }

        if (!string.IsNullOrWhiteSpace(variant.displayName))
        {
            return variant.displayName;
        }

        if (!string.IsNullOrWhiteSpace(variant.variantId))
        {
            return variant.variantId;
        }

        return $"Variant {variantIndex + 1:00}";
    }

    public List<string> GetCustomizeVariantCategories()
    {
        List<string> result = new List<string>();
        if (!useCustomizeFolderSource || customizeSlotSources == null || customizeSlotSources.Count == 0)
        {
            return result;
        }

        HashSet<string> uniqueCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < customizeSlotSources.Count; i++)
        {
            SlotPrefabSource source = customizeSlotSources[i];
            if (source == null)
            {
                continue;
            }

            if (source.prefabCategories != null)
            {
                for (int j = 0; j < source.prefabCategories.Count; j++)
                {
                    string category = source.prefabCategories[j];
                    if (!string.IsNullOrWhiteSpace(category))
                    {
                        uniqueCategories.Add(category.Trim());
                    }
                }
            }

            if (!TryResolveCategoryFromSlotFolderPath(source.folderPath, out string fallbackCategory))
            {
                continue;
            }

            uniqueCategories.Add(fallbackCategory);
        }

#if UNITY_EDITOR
        EditorAddCustomizeRootFolderCategories(uniqueCategories);
#endif

        result.AddRange(uniqueCategories);
        result.Sort(CompareCustomizeCategoryNames);
        return result;
    }

    public bool TryGetCustomizeVariantCategory(PartSlot slot, int variantIndex, out string categoryName)
    {
        categoryName = string.Empty;
        if (!useCustomizeFolderSource || variantIndex < 0 || !TryGetCustomizeSlotSource(slot, out SlotPrefabSource source) || source == null)
        {
            return false;
        }

        if (source.prefabCategories != null && variantIndex < source.prefabCategories.Count)
        {
            string cachedCategory = source.prefabCategories[variantIndex];
            if (!string.IsNullOrWhiteSpace(cachedCategory))
            {
                categoryName = cachedCategory.Trim();
                return true;
            }
        }

#if UNITY_EDITOR
        if (source.prefabs != null && variantIndex < source.prefabs.Count)
        {
            GameObject prefab = source.prefabs[variantIndex];
            if (prefab != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(prefab);
                if (TryResolveCategoryFromAssetPath(assetPath, out string resolvedCategory))
                {
                    categoryName = resolvedCategory;
                    return true;
                }
            }
        }
#endif

        return false;
    }

    public string GetSlotDisplayName(PartSlot slot)
    {
        if (SlotDisplayNames.TryGetValue(slot, out string display))
        {
            return display;
        }

        return slot.ToString();
    }

    public bool HasDefaultRendererForSlot(PartSlot slot)
    {
        EnsureSerializationLayout();
        PartBinding binding = GetBinding(slot);
        if (binding?.targetRenderer == null)
        {
            return false;
        }

        CaptureBindingDefaults(binding);
        return binding.defaultCaptured && binding.defaultMesh != null;
    }

    public bool TryGetCurrentPartMeshInfo(PartSlot slot, out int vertexCount, out int triangleCount)
    {
        vertexCount = 0;
        triangleCount = 0;

        EnsureSerializationLayout();
        PartBinding binding = GetBinding(slot);
        if (binding?.targetRenderer == null)
        {
            return false;
        }

        SkinnedMeshRenderer renderer = binding.targetRenderer;
        if (!renderer.enabled || renderer.sharedMesh == null)
        {
            return false;
        }

        Mesh mesh = renderer.sharedMesh;
        vertexCount = mesh.vertexCount;
        long indexCount = 0;
        int subMeshCount = mesh.subMeshCount;
        for (int i = 0; i < subMeshCount; i++)
        {
            indexCount += mesh.GetIndexCount(i);
        }

        long triangleTotal = indexCount / 3;
        triangleCount = triangleTotal > int.MaxValue ? int.MaxValue : (int)triangleTotal;
        return true;
    }

    public void ApplyCurrentSelection()
    {
        EnsureSerializationLayout();
        MigrateAwayFromCustomizationSlots();
        EnsureBindingCache();

        for (int i = 0; i < OrderedSlots.Length; i++)
        {
            ApplySelectionForSlot(OrderedSlots[i]);
        }

        ApplyAllColorOverrides();
    }

    public void ApplyPartSelection(PartSlot slot)
    {
        PartSlot[] slots = { slot };
        ApplyPartSelections(slots);
    }

    public void ApplyPartSelections(IEnumerable<PartSlot> slots)
    {
        if (slots == null)
        {
            return;
        }

        List<PartSlot> uniqueSlots = new List<PartSlot>();
        foreach (PartSlot slot in slots)
        {
            if (!uniqueSlots.Contains(slot))
            {
                uniqueSlots.Add(slot);
            }
        }

        if (uniqueSlots.Count == 0)
        {
            return;
        }

        EnsureSerializationLayout();
        MigrateAwayFromCustomizationSlots();
        EnsureBindingCache();

        for (int i = 0; i < uniqueSlots.Count; i++)
        {
            ApplySelectionForSlot(uniqueSlots[i]);
        }

        for (int i = 0; i < uniqueSlots.Count; i++)
        {
            ApplyColorOverride(uniqueSlots[i]);
        }
    }

    private void ApplySelectionForSlot(PartSlot slot)
    {
        PartSelection selection = GetSelection(slot);
        if (selection == null)
        {
            return;
        }

        int slotVariantCount = GetVariantCountForSlot(slot);
        if (selection.variantIndex < 0 || selection.variantIndex >= slotVariantCount)
        {
            // If there is no face variant source, keep the current face renderer as-is
            // so changing other parts won't reset the current head.
            if (slot == PartSlot.Face && slotVariantCount <= 0)
            {
                return;
            }

            RestoreDefaultOrDisable(slot);
            return;
        }

        if (!TryResolveSourceRenderer(slot, selection.variantIndex, out SkinnedMeshRenderer sourceRenderer))
        {
            RestoreDefaultOrDisable(slot);
            return;
        }

        ApplySlot(slot, sourceRenderer);
    }

    public bool TryGetVariantIndexById(string variantId, out int index)
    {
        index = -1;
        if (string.IsNullOrWhiteSpace(variantId))
        {
            return false;
        }

        if (useCustomizeFolderSource)
        {
            if (variantId.StartsWith("SET_", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(variantId.Substring(4), out int setIndex))
            {
                int resolvedIndex = setIndex - 1;
                if (resolvedIndex >= 0 && resolvedIndex < GetVariantCount())
                {
                    index = resolvedIndex;
                    return true;
                }
            }

            return false;
        }

        for (int i = 0; i < variants.Count; i++)
        {
            VariantDefinition candidate = variants[i];
            if (candidate == null)
            {
                continue;
            }

            if (string.Equals(candidate.variantId, variantId, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                return true;
            }
        }

        return false;
    }

    [ContextMenu("Customization/Apply Current Selection")]
    private void ContextApplyCurrentSelection()
    {
        ApplyCurrentSelection();
    }

    [ContextMenu("Customization/Use Default Variant For All Parts")]
    private void ContextUseDefaultVariantForAllParts()
    {
        SetAllPartsToVariant(defaultVariantForAllParts);
        ApplyCurrentSelection();
    }

    [ContextMenu("Customization/Normalize Child Names")]
    private void ContextNormalizeChildNames()
    {
        if (NormalizeChildNamesIfEnabled(true))
        {
            InvalidateBindingCache();
        }
    }

#if UNITY_EDITOR
    private bool EditorCustomizeSourcesNeedReload()
    {
        if (!useCustomizeFolderSource)
        {
            return false;
        }

        EnsureCustomizeSourceLayout();
        for (int i = 0; i < customizeSlotSources.Count; i++)
        {
            SlotPrefabSource source = customizeSlotSources[i];
            if (source == null)
            {
                continue;
            }

            if (source.prefabs != null && source.prefabs.Count > 0)
            {
                bool hasMissingPrefabReference = false;
                for (int j = 0; j < source.prefabs.Count; j++)
                {
                    if (source.prefabs[j] == null)
                    {
                        hasMissingPrefabReference = true;
                        break;
                    }
                }

                if (!hasMissingPrefabReference)
                {
                    continue;
                }
            }

            if (!string.IsNullOrWhiteSpace(source.folderPath) && AssetDatabase.IsValidFolder(source.folderPath))
            {
                return true;
            }
        }

        return false;
    }

    [ContextMenu("Customization/Load Variants From Customize Folder")]
    private void ContextLoadVariantsFromCustomizeFolder()
    {
        EditorLoadVariantsFromCustomizeFolder();
    }

    public void EditorLoadVariantsFromCustomizeFolder(bool applyAfterLoad = true)
    {
        if (IsSavedPrefabAsset())
        {
            return;
        }

        EnsureCustomizeSourceLayout();

        for (int i = 0; i < customizeSlotSources.Count; i++)
        {
            SlotPrefabSource source = customizeSlotSources[i];
            if (source == null)
            {
                continue;
            }

            source.prefabs ??= new List<GameObject>();
            source.prefabCategories ??= new List<string>();
            source.prefabs.Clear();
            source.prefabCategories.Clear();
            if (string.IsNullOrWhiteSpace(source.folderPath) || !AssetDatabase.IsValidFolder(source.folderPath))
            {
                continue;
            }

            EditorLoadPrefabsForSlotSource(source);
        }

        sourceRendererCache.Clear();
        sourceCache.Clear();
        int variantCount = GetVariantCount();
        defaultVariantForAllParts = variantCount > 0 ? 0 : -1;
        if (applyAfterLoad)
        {
            SetAllPartsToVariant(defaultVariantForAllParts);
            ApplyCurrentSelection();
        }
        EditorUtility.SetDirty(this);
    }

    private static void EditorLoadPrefabsForSlotSource(SlotPrefabSource source)
    {
        if (source == null || string.IsNullOrWhiteSpace(source.folderPath) || !AssetDatabase.IsValidFolder(source.folderPath))
        {
            return;
        }

        source.prefabs ??= new List<GameObject>();
        source.prefabCategories ??= new List<string>();

        List<string> candidateSlotFolders = EditorGetOrderedCustomizeSlotFolders(source.slot, source.folderPath);
        if (candidateSlotFolders.Count == 0)
        {
            candidateSlotFolders.Add(source.folderPath);
        }

        HashSet<string> addedAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < candidateSlotFolders.Count; i++)
        {
            string slotFolder = candidateSlotFolders[i];
            TryResolveCategoryFromSlotFolderPath(slotFolder, out string categoryName);

            List<string> orderedPaths = EditorGetOrderedPrefabAssetPaths(slotFolder);
            for (int j = 0; j < orderedPaths.Count; j++)
            {
                string path = orderedPaths[j];
                if (!addedAssetPaths.Add(path))
                {
                    continue;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                source.prefabs.Add(prefab);
                source.prefabCategories.Add(!string.IsNullOrWhiteSpace(categoryName) ? categoryName : "Uncategorized");
            }
        }
    }

    private static List<string> EditorGetOrderedCustomizeSlotFolders(PartSlot slot, string sourceSlotFolderPath)
    {
        List<string> result = new List<string>();
        if (string.IsNullOrWhiteSpace(sourceSlotFolderPath) || !AssetDatabase.IsValidFolder(sourceSlotFolderPath))
        {
            return result;
        }

        string normalizedSlotPath = NormalizeAssetPath(sourceSlotFolderPath);
        string slotFolderName = GetFolderLeafName(normalizedSlotPath);
        string categoryFolderPath = GetParentFolderPath(normalizedSlotPath);
        string rootFolderPath = GetParentFolderPath(categoryFolderPath);
        List<string> candidateSlotFolderNames = BuildCustomizeSlotFolderNameCandidates(slot, slotFolderName);

        if (string.IsNullOrWhiteSpace(slotFolderName)
            || string.IsNullOrWhiteSpace(categoryFolderPath)
            || string.IsNullOrWhiteSpace(rootFolderPath)
            || !AssetDatabase.IsValidFolder(rootFolderPath))
        {
            result.Add(normalizedSlotPath);
            return result;
        }

        string[] categoryFolders = AssetDatabase.GetSubFolders(rootFolderPath);
        Array.Sort(categoryFolders, CompareCustomizeCategoryFolders);
        for (int i = 0; i < categoryFolders.Length; i++)
        {
            string categoryFolder = NormalizeAssetPath(categoryFolders[i]);
            for (int j = 0; j < candidateSlotFolderNames.Count; j++)
            {
                string candidateSlotFolderName = candidateSlotFolderNames[j];
                if (string.IsNullOrWhiteSpace(candidateSlotFolderName))
                {
                    continue;
                }

                string slotFolderPath = $"{categoryFolder}/{candidateSlotFolderName}";
                if (AssetDatabase.IsValidFolder(slotFolderPath) && !ContainsPathIgnoreCase(result, slotFolderPath))
                {
                    result.Add(slotFolderPath);
                    break;
                }
            }
        }

        if (result.Count == 0 && AssetDatabase.IsValidFolder(normalizedSlotPath))
        {
            result.Add(normalizedSlotPath);
        }

        return result;
    }

    private static List<string> BuildCustomizeSlotFolderNameCandidates(PartSlot slot, string primaryFolderName)
    {
        List<string> names = new List<string>();
        AddCustomizeSlotFolderNameCandidate(names, primaryFolderName);

        if (CustomizeFolderNamesBySlot.TryGetValue(slot, out string[] configuredNames) && configuredNames != null)
        {
            for (int i = 0; i < configuredNames.Length; i++)
            {
                AddCustomizeSlotFolderNameCandidate(names, configuredNames[i]);
            }
        }

        return names;
    }

    private static void AddCustomizeSlotFolderNameCandidate(List<string> names, string value)
    {
        if (names == null || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string trimmed = value.Trim();
        for (int i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        names.Add(trimmed);
    }

    private static bool ContainsPathIgnoreCase(List<string> values, string value)
    {
        if (values == null || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int CompareCustomizeCategoryFolders(string leftPath, string rightPath)
    {
        string leftName = GetFolderLeafName(leftPath);
        string rightName = GetFolderLeafName(rightPath);
        return CompareCustomizeCategoryNames(leftName, rightName);
    }

    private void EditorAddCustomizeRootFolderCategories(HashSet<string> categories)
    {
        if (categories == null)
        {
            return;
        }

        string root = ResolveCustomizeRootFolder(customizeRootFolder);
        if (string.IsNullOrWhiteSpace(root) || !AssetDatabase.IsValidFolder(root))
        {
            return;
        }

        string[] categoryFolders = AssetDatabase.GetSubFolders(root);
        for (int i = 0; i < categoryFolders.Length; i++)
        {
            string categoryName = GetFolderLeafName(categoryFolders[i]);
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                categories.Add(categoryName.Trim());
            }
        }
    }

    [ContextMenu("Customization/Load Variants From Casual Folder")]
    private void ContextLoadVariantsFromCasualFolder()
    {
        EditorLoadVariantsFromCasualFolder();
    }

    public void EditorLoadVariantsFromCasualFolder(bool applyAfterLoad = true)
    {
        if (IsSavedPrefabAsset())
        {
            return;
        }

        string casualFolder = ResolveEditorCasualFolder();
        if (string.IsNullOrWhiteSpace(casualFolder))
        {
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { casualFolder });
        Array.Sort(guids, StringComparer.Ordinal);

        variants.Clear();
        int setIndex = 1;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            bool supportedAsset =
                path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);

            if (!supportedAsset)
            {
                continue;
            }

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null)
            {
                continue;
            }

            variants.Add(new VariantDefinition
            {
                variantId = $"FIGURE_SET_{setIndex:00}",
                displayName = $"Figure Set {setIndex:00}",
                sourceCharacter = source
            });

            setIndex++;
        }

        defaultVariantForAllParts = Mathf.Clamp(defaultVariantForAllParts, 0, Mathf.Max(0, variants.Count - 1));
        if (applyAfterLoad)
        {
            SetAllPartsToVariant(defaultVariantForAllParts);
            ApplyCurrentSelection();
        }
        EditorUtility.SetDirty(this);
    }

    private static string ResolveEditorCasualFolder()
    {
        for (int i = 0; i < EditorCasualFolderCandidates.Length; i++)
        {
            string candidate = EditorCasualFolderCandidates[i];
            if (!AssetDatabase.IsValidFolder(candidate))
            {
                continue;
            }

            string[] fbxGuids = AssetDatabase.FindAssets("t:GameObject", new[] { candidate });
            if (fbxGuids != null && fbxGuids.Length > 0)
            {
                return candidate;
            }
        }

        return null;
    }

    private static List<string> EditorGetOrderedPrefabAssetPaths(string folderPath)
    {
        List<string> paths = new List<string>();
        if (string.IsNullOrWhiteSpace(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
        {
            return paths;
        }

        string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { folderPath });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            bool supportedAsset =
                path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
            if (supportedAsset)
            {
                paths.Add(path);
            }
        }

        paths.Sort(CompareAssetPathsNatural);
        return paths;
    }

    private static int CompareAssetPathsNatural(string leftPath, string rightPath)
    {
        string leftName = Path.GetFileNameWithoutExtension(leftPath) ?? string.Empty;
        string rightName = Path.GetFileNameWithoutExtension(rightPath) ?? string.Empty;
        int nameCompare = CompareNaturalStrings(leftName, rightName);
        if (nameCompare != 0)
        {
            return nameCompare;
        }

        return string.Compare(leftPath, rightPath, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareNaturalStrings(string left, string right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        left ??= string.Empty;
        right ??= string.Empty;

        int i = 0;
        int j = 0;
        while (i < left.Length && j < right.Length)
        {
            char a = left[i];
            char b = right[j];
            bool aDigit = char.IsDigit(a);
            bool bDigit = char.IsDigit(b);
            if (aDigit && bDigit)
            {
                long aValue = 0;
                while (i < left.Length && char.IsDigit(left[i]))
                {
                    aValue = (aValue * 10L) + (left[i] - '0');
                    i++;
                }

                long bValue = 0;
                while (j < right.Length && char.IsDigit(right[j]))
                {
                    bValue = (bValue * 10L) + (right[j] - '0');
                    j++;
                }

                int numericCompare = aValue.CompareTo(bValue);
                if (numericCompare != 0)
                {
                    return numericCompare;
                }

                continue;
            }

            int charCompare = char.ToUpperInvariant(a).CompareTo(char.ToUpperInvariant(b));
            if (charCompare != 0)
            {
                return charCompare;
            }

            i++;
            j++;
        }

        if (i < left.Length)
        {
            return 1;
        }

        if (j < right.Length)
        {
            return -1;
        }

        return 0;
    }
#endif

    private static int CompareCustomizeCategoryNames(string left, string right)
    {
        int leftPriority = GetCustomizeCategoryPriority(left);
        int rightPriority = GetCustomizeCategoryPriority(right);
        if (leftPriority != rightPriority)
        {
            return leftPriority.CompareTo(rightPriority);
        }

        return string.Compare(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetCustomizeCategoryPriority(string categoryName)
    {
        if (string.Equals(categoryName, "Default", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(categoryName, "Casual", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static string NormalizeAssetPath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Replace('\\', '/').TrimEnd('/');
    }

    private static string GetParentFolderPath(string path)
    {
        string normalized = NormalizeAssetPath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        int slashIndex = normalized.LastIndexOf('/');
        if (slashIndex <= 0)
        {
            return string.Empty;
        }

        return normalized.Substring(0, slashIndex);
    }

    private static string GetFolderLeafName(string path)
    {
        string normalized = NormalizeAssetPath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        int slashIndex = normalized.LastIndexOf('/');
        if (slashIndex < 0)
        {
            return normalized;
        }

        if (slashIndex == normalized.Length - 1)
        {
            return string.Empty;
        }

        return normalized.Substring(slashIndex + 1);
    }

    private static bool TryResolveCategoryFromSlotFolderPath(string slotFolderPath, out string categoryName)
    {
        categoryName = string.Empty;
        string normalized = NormalizeAssetPath(slotFolderPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        string parentFolder = GetParentFolderPath(normalized);
        if (string.IsNullOrWhiteSpace(parentFolder))
        {
            return false;
        }

        categoryName = GetFolderLeafName(parentFolder);
        return !string.IsNullOrWhiteSpace(categoryName);
    }

    private static bool TryResolveCategoryFromAssetPath(string assetPath, out string categoryName)
    {
        categoryName = string.Empty;
        string normalized = NormalizeAssetPath(assetPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        string assetFolder = GetParentFolderPath(normalized);
        string slotFolder = GetParentFolderPath(assetFolder);
        if (string.IsNullOrWhiteSpace(slotFolder))
        {
            return false;
        }

        categoryName = GetFolderLeafName(slotFolder);
        return !string.IsNullOrWhiteSpace(categoryName);
    }

    private void EnsureSerializationLayout()
    {
        EnsureCustomizeSourceLayout();
        partBindings ??= new List<PartBinding>();
        partSelections ??= new List<PartSelection>();
        partColorSelections ??= new List<PartColorSelection>();

        Dictionary<PartSlot, PartBinding> bindingBySlot = new Dictionary<PartSlot, PartBinding>();
        for (int i = 0; i < partBindings.Count; i++)
        {
            PartBinding binding = partBindings[i];
            if (binding == null)
            {
                continue;
            }

            bindingBySlot[binding.slot] = binding;
        }

        Dictionary<PartSlot, PartSelection> selectionBySlot = new Dictionary<PartSlot, PartSelection>();
        for (int i = 0; i < partSelections.Count; i++)
        {
            PartSelection selection = partSelections[i];
            if (selection == null)
            {
                continue;
            }

            selectionBySlot[selection.slot] = selection;
        }

        Dictionary<(PartSlot slot, int materialIndex), PartColorSelection> colorBySlot = new Dictionary<(PartSlot slot, int materialIndex), PartColorSelection>();
        for (int i = 0; i < partColorSelections.Count; i++)
        {
            PartColorSelection selection = partColorSelections[i];
            if (selection == null)
            {
                continue;
            }

            colorBySlot[(selection.slot, selection.materialIndex)] = selection;
        }

        partBindings.Clear();
        partSelections.Clear();
        partColorSelections.Clear();
        for (int i = 0; i < OrderedSlots.Length; i++)
        {
            PartSlot slot = OrderedSlots[i];
            if (!bindingBySlot.TryGetValue(slot, out PartBinding binding) || binding == null)
            {
                binding = new PartBinding { slot = slot };
            }

            binding.slot = slot;
            partBindings.Add(binding);

            if (!selectionBySlot.TryGetValue(slot, out PartSelection selection) || selection == null)
            {
                selection = new PartSelection { slot = slot, variantIndex = -1 };
            }

            selection.slot = slot;
            partSelections.Add(selection);

            if (!colorBySlot.TryGetValue((slot, -1), out PartColorSelection colorSelection) || colorSelection == null)
            {
                colorSelection = new PartColorSelection { slot = slot, materialIndex = -1, useCustomColor = false, color = Color.white };
            }

            colorSelection.slot = slot;
            colorSelection.materialIndex = -1;
            partColorSelections.Add(colorSelection);

            foreach (KeyValuePair<(PartSlot slot, int materialIndex), PartColorSelection> pair in colorBySlot)
            {
                if (pair.Key.slot != slot || pair.Key.materialIndex < 0 || pair.Value == null)
                {
                    continue;
                }

                pair.Value.slot = slot;
                pair.Value.materialIndex = pair.Key.materialIndex;
                partColorSelections.Add(pair.Value);
            }
        }

        int variantCount = GetVariantCount();
        if (useCustomizeFolderSource)
        {
            defaultVariantForAllParts = variantCount > 0 ? 0 : -1;
        }
        else
        {
            int maxVariantIndex = Mathf.Max(0, variantCount - 1);
            defaultVariantForAllParts = variantCount > 0
                ? Mathf.Clamp(defaultVariantForAllParts, 0, maxVariantIndex)
                : -1;
        }
    }

    private void EnsureCustomizeSourceLayout()
    {
        customizeSlotSources ??= new List<SlotPrefabSource>();
        Dictionary<PartSlot, SlotPrefabSource> sourceBySlot = new Dictionary<PartSlot, SlotPrefabSource>();
        for (int i = 0; i < customizeSlotSources.Count; i++)
        {
            SlotPrefabSource source = customizeSlotSources[i];
            if (source == null)
            {
                continue;
            }

            sourceBySlot[source.slot] = source;
        }

        customizeSlotSources.Clear();
        for (int i = 0; i < OrderedSlots.Length; i++)
        {
            PartSlot slot = OrderedSlots[i];
            if (!sourceBySlot.TryGetValue(slot, out SlotPrefabSource source) || source == null)
            {
                source = new SlotPrefabSource
                {
                    slot = slot,
                    prefabs = new List<GameObject>()
                };
            }

            source.slot = slot;
            source.prefabs ??= new List<GameObject>();
            source.prefabCategories ??= new List<string>();
            string defaultFolderPath = BuildDefaultCustomizeFolderPath(slot);
            if (string.IsNullOrWhiteSpace(source.folderPath))
            {
                source.folderPath = defaultFolderPath;
            }
            else if (!IsCustomizeFolderPathValid(source.folderPath) && !string.IsNullOrWhiteSpace(defaultFolderPath))
            {
                // Migrate stale folder paths when customization folders are reorganized.
                source.folderPath = defaultFolderPath;
            }

            customizeSlotSources.Add(source);
        }
    }

    private string BuildDefaultCustomizeFolderPath(PartSlot slot)
    {
        if (!CustomizeFolderNamesBySlot.TryGetValue(slot, out string[] folderNames)
            || folderNames == null
            || folderNames.Length == 0)
        {
            return string.Empty;
        }

        string root = ResolveCustomizeRootFolder(customizeRootFolder);
        string[] candidateRoots = BuildCustomizeRootCandidates(root);

#if UNITY_EDITOR
        for (int i = 0; i < candidateRoots.Length; i++)
        {
            string candidateRoot = candidateRoots[i];
            if (string.IsNullOrWhiteSpace(candidateRoot))
            {
                continue;
            }

            for (int j = 0; j < folderNames.Length; j++)
            {
                string folderName = folderNames[j];
                if (string.IsNullOrWhiteSpace(folderName))
                {
                    continue;
                }

                string candidatePath = $"{candidateRoot}/{folderName}";
                if (AssetDatabase.IsValidFolder(candidatePath))
                {
                    return candidatePath;
                }
            }
        }
#endif

        string preferredRoot = ResolvePreferredCustomizeRoot(root);
        string preferredFolderName = ResolvePreferredSlotFolderName(preferredRoot, folderNames);
        return $"{preferredRoot}/{preferredFolderName}";
    }

    private static bool IsCustomizeFolderPathValid(string folderPath)
    {
#if UNITY_EDITOR
        return !string.IsNullOrWhiteSpace(folderPath) && AssetDatabase.IsValidFolder(folderPath);
#else
        return !string.IsNullOrWhiteSpace(folderPath);
#endif
    }

    private static string ResolveCustomizeRootFolder(string configuredRoot)
    {
        string root = string.IsNullOrWhiteSpace(configuredRoot)
            ? "Assets/Minimo/Customize"
            : configuredRoot.TrimEnd('/', '\\');
        return root;
    }

    private static string[] BuildCustomizeRootCandidates(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return Array.Empty<string>();
        }

        if (root.EndsWith("/Casual", StringComparison.OrdinalIgnoreCase)
            || root.EndsWith("/Default", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { root };
        }

        return new[]
        {
            root,
            $"{root}/Casual",
            $"{root}/Default"
        };
    }

    private static string ResolvePreferredCustomizeRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return "Assets/Minimo/Customize/Casual";
        }

        if (root.EndsWith("/Casual", StringComparison.OrdinalIgnoreCase)
            || root.EndsWith("/Default", StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        return $"{root}/Casual";
    }

    private static string ResolvePreferredSlotFolderName(string preferredRoot, string[] folderNames)
    {
        if (folderNames == null || folderNames.Length == 0)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(preferredRoot)
            && preferredRoot.EndsWith("/Default", StringComparison.OrdinalIgnoreCase)
            && folderNames.Length > 1)
        {
            return folderNames[folderNames.Length - 1];
        }

        return folderNames[0];
    }

    private int GetVariantCountForSlot(PartSlot slot)
    {
        if (useCustomizeFolderSource)
        {
            if (TryGetCustomizeSlotSource(slot, out SlotPrefabSource source))
            {
                return source.prefabs != null ? source.prefabs.Count : 0;
            }

            return 0;
        }

        return variants != null ? variants.Count : 0;
    }

    private int GetCustomizeVariantCount()
    {
        if (!useCustomizeFolderSource || customizeSlotSources == null || customizeSlotSources.Count == 0)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < customizeSlotSources.Count; i++)
        {
            SlotPrefabSource source = customizeSlotSources[i];
            if (source?.prefabs == null)
            {
                continue;
            }

            count = Mathf.Max(count, source.prefabs.Count);
        }

        return count;
    }

    private bool TryGetCustomizeSlotSource(PartSlot slot, out SlotPrefabSource source)
    {
        source = null;
        if (customizeSlotSources == null)
        {
            return false;
        }

        for (int i = 0; i < customizeSlotSources.Count; i++)
        {
            SlotPrefabSource candidate = customizeSlotSources[i];
            if (candidate != null && candidate.slot == slot)
            {
                source = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryGetCustomizeVariantSourcePrefab(PartSlot slot, int variantIndex, out GameObject prefab)
    {
        prefab = null;
        if (!TryGetCustomizeSlotSource(slot, out SlotPrefabSource source)
            || source.prefabs == null
            || variantIndex < 0
            || variantIndex >= source.prefabs.Count)
        {
            return false;
        }

        prefab = source.prefabs[variantIndex];
        return prefab != null;
    }

    private bool TryGetCustomizeVariantDisplayName(int variantIndex, out string displayName)
    {
        displayName = null;
        if (!useCustomizeFolderSource || variantIndex < 0)
        {
            return false;
        }

        PartSlot[] priority =
        {
            PartSlot.Torso,
            PartSlot.Hair,
            PartSlot.Hips,
            PartSlot.ArmLeft,
            PartSlot.ShoulderLeft,
            PartSlot.LegUpperLeft,
            PartSlot.BootsLeft
        };

        for (int i = 0; i < priority.Length; i++)
        {
            if (!TryGetCustomizeVariantSourcePrefab(priority[i], variantIndex, out GameObject prefab))
            {
                continue;
            }

            displayName = prefab != null && !string.IsNullOrWhiteSpace(prefab.name)
                ? prefab.name
                : $"Set {variantIndex + 1:00}";
            return true;
        }

        if (variantIndex < GetCustomizeVariantCount())
        {
            displayName = $"Set {variantIndex + 1:00}";
            return true;
        }

        return false;
    }

    private bool TryResolveSourceRenderer(PartSlot slot, int variantIndex, out SkinnedMeshRenderer sourceRenderer)
    {
        sourceRenderer = null;
        if (variantIndex < 0)
        {
            return false;
        }

        if (useCustomizeFolderSource)
        {
            return TryResolveSourceRendererFromCustomizeFolder(slot, variantIndex, out sourceRenderer)
                   && sourceRenderer != null
                   && sourceRenderer.sharedMesh != null;
        }

        if (TryResolveSourceRendererFromVariant(slot, variantIndex, out sourceRenderer))
        {
            return sourceRenderer != null && sourceRenderer.sharedMesh != null;
        }

        return false;
    }

    private bool TryResolveSourceRendererFromCustomizeFolder(
        PartSlot slot,
        int variantIndex,
        out SkinnedMeshRenderer sourceRenderer)
    {
        sourceRenderer = null;
        if (!TryGetCustomizeVariantSourcePrefab(slot, variantIndex, out GameObject prefab) || prefab == null)
        {
            return false;
        }

        (GameObject prefab, PartSlot slot) cacheKey = (prefab, slot);
        if (sourceRendererCache.TryGetValue(cacheKey, out SkinnedMeshRenderer cachedRenderer) && cachedRenderer != null)
        {
            sourceRenderer = cachedRenderer;
            return sourceRenderer.sharedMesh != null;
        }

        SkinnedMeshRenderer[] renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return false;
        }

        SkinnedMeshRenderer selected = null;
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer candidate = renderers[i];
            if (candidate == null)
            {
                continue;
            }

            string sourceName = candidate.name;
            if (candidate.sharedMesh != null)
            {
                sourceName += "_" + candidate.sharedMesh.name;
            }

            if (TryResolveSlot(sourceName, out PartSlot resolvedSlot) && resolvedSlot == slot)
            {
                selected = candidate;
                break;
            }

            if (selected == null)
            {
                selected = candidate;
            }
        }

        if (selected == null)
        {
            return false;
        }

        sourceRendererCache[cacheKey] = selected;
        sourceRenderer = selected;
        return sourceRenderer.sharedMesh != null;
    }

    private bool TryResolveSourceRendererFromVariant(
        PartSlot slot,
        int variantIndex,
        out SkinnedMeshRenderer sourceRenderer)
    {
        sourceRenderer = null;
        if (variants == null || variantIndex < 0 || variantIndex >= variants.Count)
        {
            return false;
        }

        VariantDefinition variant = variants[variantIndex];
        if (variant == null || variant.sourceCharacter == null)
        {
            return false;
        }

        Dictionary<PartSlot, SkinnedMeshRenderer> sourceParts = GetSourceParts(variant.sourceCharacter);
        return sourceParts.TryGetValue(slot, out sourceRenderer) && sourceRenderer != null && sourceRenderer.sharedMesh != null;
    }

    private void EnsureHierarchy()
    {
        MigrateAwayFromCustomizationSlots();
    }

    private bool MigrateAwayFromCustomizationSlots()
    {
        bool changed = false;
        Transform legacySlotsRoot = ResolveLegacySlotsRoot();
        Transform defaultPartsRoot = ResolveDefaultPartsRoot();

        for (int i = 0; i < partBindings.Count; i++)
        {
            PartBinding binding = partBindings[i];
            if (binding == null)
            {
                continue;
            }

            if (binding.slotRoot != null)
            {
                binding.slotRoot = null;
                changed = true;
            }

            SkinnedMeshRenderer preferredRenderer = ResolvePreferredSceneRendererForSlot(
                binding.slot,
                defaultPartsRoot,
                legacySlotsRoot);
            if (preferredRenderer == null || preferredRenderer == binding.targetRenderer)
            {
                continue;
            }

            AssignBindingRenderer(binding, preferredRenderer);
            changed = true;
        }

        if (RemoveLegacySlotsRootIfUnused(legacySlotsRoot))
        {
            changed = true;
        }

        if (NormalizeChildNamesIfEnabled())
        {
            changed = true;
        }

        if (changed)
        {
            InvalidateBindingCache();
        }

        return changed;
    }

    private Transform ResolveLegacySlotsRoot()
    {
        Transform direct = transform.Find(SlotsRootName);
        if (direct != null)
        {
            return direct;
        }

        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && string.Equals(candidate.name, SlotsRootName, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private Transform ResolveDefaultPartsRoot()
    {
        Transform direct = transform.Find(DefaultPartsRootName);
        if (direct != null)
        {
            return direct;
        }

        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && string.Equals(candidate.name, DefaultPartsRootName, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private SkinnedMeshRenderer ResolvePreferredSceneRendererForSlot(
        PartSlot slot,
        Transform defaultPartsRoot,
        Transform legacySlotsRoot)
    {
        string rendererName = GetRendererNodeName(slot);
        SkinnedMeshRenderer renderer = FindRendererByExactName(defaultPartsRoot, rendererName);
        if (renderer != null)
        {
            return renderer;
        }

        renderer = FindRendererByResolvedSlot(defaultPartsRoot, slot, legacySlotsRoot);
        if (renderer != null)
        {
            return renderer;
        }

        renderer = FindRendererByExactName(transform, rendererName, legacySlotsRoot);
        if (renderer != null)
        {
            return renderer;
        }

        return FindRendererByResolvedSlot(transform, slot, legacySlotsRoot);
    }

    private static SkinnedMeshRenderer FindRendererByExactName(Transform root, string rendererName, Transform excludedRoot = null)
    {
        if (root == null || string.IsNullOrWhiteSpace(rendererName))
        {
            return null;
        }

        SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = renderers[i];
            if (renderer == null
                || IsTransformChildOf(renderer.transform, excludedRoot)
                || !string.Equals(renderer.name, rendererName, StringComparison.Ordinal))
            {
                continue;
            }

            return renderer;
        }

        return null;
    }

    private static SkinnedMeshRenderer FindRendererByResolvedSlot(Transform root, PartSlot slot, Transform excludedRoot)
    {
        if (root == null)
        {
            return null;
        }

        SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = renderers[i];
            if (renderer == null || IsTransformChildOf(renderer.transform, excludedRoot))
            {
                continue;
            }

            string sourceName = renderer.name;
            if (renderer.sharedMesh != null)
            {
                sourceName += "_" + renderer.sharedMesh.name;
            }

            if (TryResolveSlot(sourceName, out PartSlot resolvedSlot) && resolvedSlot == slot)
            {
                return renderer;
            }
        }

        return null;
    }

    private void AssignBindingRenderer(PartBinding binding, SkinnedMeshRenderer renderer)
    {
        if (binding == null)
        {
            return;
        }

        binding.targetRenderer = renderer;
        binding.slotRoot = null;
        ResetBindingDefaults(binding);

        if (renderer != null && normalizeChildNames)
        {
            renderer.name = GetRendererNodeName(binding.slot);
        }

        CaptureBindingDefaults(binding);
    }

    private static void ResetBindingDefaults(PartBinding binding)
    {
        if (binding == null)
        {
            return;
        }

        binding.defaultCaptured = false;
        binding.defaultEnabled = false;
        binding.defaultMesh = null;
        binding.defaultMaterials = null;
        binding.defaultRootBone = null;
        binding.defaultBones = null;
        binding.defaultLocalBounds = default;
    }

    private bool RemoveLegacySlotsRootIfUnused(Transform legacySlotsRoot)
    {
        if (legacySlotsRoot == null || legacySlotsRoot == transform)
        {
            return false;
        }

        for (int i = 0; i < partBindings.Count; i++)
        {
            PartBinding binding = partBindings[i];
            if (binding == null)
            {
                continue;
            }

            if (IsTransformChildOf(binding.targetRenderer != null ? binding.targetRenderer.transform : null, legacySlotsRoot))
            {
                return false;
            }

            if (IsTransformChildOf(binding.slotRoot, legacySlotsRoot))
            {
                binding.slotRoot = null;
            }
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEngine.Object.DestroyImmediate(legacySlotsRoot.gameObject);
            return true;
        }
#endif
        Destroy(legacySlotsRoot.gameObject);
        return true;
    }

    private static bool IsTransformChildOf(Transform target, Transform root)
    {
        return target != null && root != null && target.IsChildOf(root);
    }

    private void AutoBindRenderers()
    {
        Transform legacySlotsRoot = ResolveLegacySlotsRoot();
        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (legacySlotsRoot != null && renderer.transform.IsChildOf(legacySlotsRoot))
            {
                continue;
            }

            string sourceName = renderer.name;
            if (renderer.sharedMesh != null)
            {
                sourceName += "_" + renderer.sharedMesh.name;
            }

            if (!TryResolveSlot(sourceName, out PartSlot slot))
            {
                continue;
            }

            PartBinding binding = GetBinding(slot);
            if (binding == null || binding.targetRenderer != null)
            {
                continue;
            }

            binding.targetRenderer = renderer;
            if (normalizeChildNames)
            {
                binding.targetRenderer.name = GetRendererNodeName(slot);
            }

            CaptureBindingDefaults(binding);
            InvalidateBindingCache();
        }
    }

    private void ApplySlot(PartSlot slot, SkinnedMeshRenderer sourceRenderer)
    {
        PartBinding binding = GetBinding(slot);
        if (binding == null)
        {
            return;
        }

        if (sourceRenderer == null || sourceRenderer.sharedMesh == null)
        {
            RestoreDefaultOrDisable(slot);
            return;
        }

        SkinnedMeshRenderer targetRenderer = binding.targetRenderer;
        if (targetRenderer == null)
        {
            targetRenderer = CreateRendererForSlot(binding, sourceRenderer);
            binding.targetRenderer = targetRenderer;
            CaptureBindingDefaults(binding);
            InvalidateBindingCache();
        }

        if (targetRenderer == null)
        {
            return;
        }

        CopyRendererFlags(sourceRenderer, targetRenderer);
        targetRenderer.sharedMesh = sourceRenderer.sharedMesh;
        bool shouldKeepAssignedMaterials = keepAssignedMaterialsOnApply && !useCustomizeFolderSource;
        if (!shouldKeepAssignedMaterials || !HasCompatibleAssignedMaterials(targetRenderer, sourceRenderer))
        {
            targetRenderer.sharedMaterials = sourceRenderer.sharedMaterials != null
                ? sourceRenderer.sharedMaterials.ToArray()
                : Array.Empty<Material>();
        }

        targetRenderer.localBounds = sourceRenderer.sharedMesh != null && sourceRenderer.sharedMesh.bounds.size.sqrMagnitude > 0.000001f
            ? sourceRenderer.sharedMesh.bounds
            : sourceRenderer.localBounds;

        Transform resolvedRootBone = ResolveBone(sourceRenderer.rootBone, skeletonMap);
        Transform[] sourceBones = sourceRenderer.bones;
        bool hasValidSourceBones = sourceBones != null && sourceBones.Any(b => b != null);
        if (hasValidSourceBones || resolvedRootBone != null)
        {
            targetRenderer.rootBone = resolvedRootBone ?? binding.defaultRootBone ?? targetRenderer.rootBone;
            Transform[] remappedBones = new Transform[sourceBones.Length];
            Transform fallback = targetRenderer.rootBone ?? binding.defaultRootBone;
            for (int i = 0; i < sourceBones.Length; i++)
            {
                remappedBones[i] = ResolveBone(sourceBones[i], skeletonMap) ?? fallback;
            }

            targetRenderer.bones = remappedBones;
        }
        else if (binding.defaultCaptured)
        {
            targetRenderer.rootBone = binding.defaultRootBone;
            targetRenderer.bones = binding.defaultBones != null ? binding.defaultBones.ToArray() : Array.Empty<Transform>();
        }

        targetRenderer.enabled = true;
    }

    private void RestoreDefaultOrDisable(PartSlot slot)
    {
        PartBinding binding = GetBinding(slot);
        if (binding == null || binding.targetRenderer == null)
        {
            return;
        }

        CaptureBindingDefaults(binding);
        SkinnedMeshRenderer renderer = binding.targetRenderer;
        if (binding.defaultCaptured && binding.defaultMesh != null)
        {
            renderer.sharedMesh = binding.defaultMesh;
            renderer.sharedMaterials = binding.defaultMaterials != null ? binding.defaultMaterials.ToArray() : Array.Empty<Material>();
            renderer.rootBone = binding.defaultRootBone;
            renderer.bones = binding.defaultBones != null ? binding.defaultBones.ToArray() : Array.Empty<Transform>();
            renderer.localBounds = binding.defaultLocalBounds;
            renderer.enabled = binding.defaultEnabled;
            return;
        }

        renderer.enabled = false;
    }

    private void CaptureAllBindingDefaults()
    {
        for (int i = 0; i < partBindings.Count; i++)
        {
            PartBinding binding = partBindings[i];
            if (binding == null)
            {
                continue;
            }

            CaptureBindingDefaults(binding);
        }
    }

    private void EnsureBindingCache()
    {
        if (!isDirty && skeletonMap != null)
        {
            return;
        }

        CaptureAllBindingDefaults();
        skeletonMap = BuildSkeletonMap();
        isDirty = false;
    }

    private void InvalidateBindingCache()
    {
        isDirty = true;
    }

    private static void CaptureBindingDefaults(PartBinding binding)
    {
        if (binding == null || binding.defaultCaptured || binding.targetRenderer == null)
        {
            return;
        }

        SkinnedMeshRenderer renderer = binding.targetRenderer;
        binding.defaultCaptured = true;
        binding.defaultEnabled = renderer.enabled;
        binding.defaultMesh = renderer.sharedMesh;
        binding.defaultMaterials = renderer.sharedMaterials != null ? renderer.sharedMaterials.ToArray() : Array.Empty<Material>();
        binding.defaultRootBone = renderer.rootBone;
        binding.defaultBones = renderer.bones != null ? renderer.bones.ToArray() : Array.Empty<Transform>();
        binding.defaultLocalBounds = renderer.localBounds;
    }

    private Dictionary<PartSlot, SkinnedMeshRenderer> GetSourceParts(GameObject sourceCharacter)
    {
        if (sourceCharacter == null)
        {
            return new Dictionary<PartSlot, SkinnedMeshRenderer>();
        }

        if (sourceCache.TryGetValue(sourceCharacter, out Dictionary<PartSlot, SkinnedMeshRenderer> cached))
        {
            return cached;
        }

        Dictionary<PartSlot, SkinnedMeshRenderer> result = new Dictionary<PartSlot, SkinnedMeshRenderer>();
        SkinnedMeshRenderer[] sourceRenderers = sourceCharacter.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < sourceRenderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = sourceRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            string sourceName = renderer.name;
            if (renderer.sharedMesh != null)
            {
                sourceName += "_" + renderer.sharedMesh.name;
            }

            if (!TryResolveSlot(sourceName, out PartSlot slot))
            {
                continue;
            }

            if (!result.ContainsKey(slot))
            {
                result.Add(slot, renderer);
            }
        }

        // Some variant FBX files provide a single renderer that visually contains both sides
        // but is named only with one side suffix. Mirror missing left/right slots to avoid
        // disabling the opposite side on apply.
        ApplySymmetricSourceFallback(result, PartSlot.ShoulderLeft, PartSlot.ShoulderRight);
        ApplySymmetricSourceFallback(result, PartSlot.ArmLeft, PartSlot.ArmRight);
        ApplySymmetricSourceFallback(result, PartSlot.HandLeft, PartSlot.HandRight);
        ApplySymmetricSourceFallback(result, PartSlot.LegUpperLeft, PartSlot.LegUpperRight);
        ApplySymmetricSourceFallback(result, PartSlot.BootsLeft, PartSlot.BootsRight);

        sourceCache[sourceCharacter] = result;
        return result;
    }

    private static void ApplySymmetricSourceFallback(
        Dictionary<PartSlot, SkinnedMeshRenderer> mapping,
        PartSlot leftSlot,
        PartSlot rightSlot)
    {
        if (mapping == null)
        {
            return;
        }

        bool hasLeft = mapping.TryGetValue(leftSlot, out SkinnedMeshRenderer leftRenderer) && leftRenderer != null;
        bool hasRight = mapping.TryGetValue(rightSlot, out SkinnedMeshRenderer rightRenderer) && rightRenderer != null;

        if (hasLeft && !hasRight)
        {
            mapping[rightSlot] = leftRenderer;
            return;
        }

        if (hasRight && !hasLeft)
        {
            mapping[leftSlot] = rightRenderer;
        }
    }

    private PartBinding GetBinding(PartSlot slot)
    {
        for (int i = 0; i < partBindings.Count; i++)
        {
            PartBinding binding = partBindings[i];
            if (binding != null && binding.slot == slot)
            {
                return binding;
            }
        }

        return null;
    }

    private PartSelection GetSelection(PartSlot slot)
    {
        for (int i = 0; i < partSelections.Count; i++)
        {
            PartSelection selection = partSelections[i];
            if (selection != null && selection.slot == slot)
            {
                return selection;
            }
        }

        return null;
    }

    private PartColorSelection GetColorSelection(PartSlot slot, int materialIndex, bool createIfMissing)
    {
        for (int i = 0; i < partColorSelections.Count; i++)
        {
            PartColorSelection selection = partColorSelections[i];
            if (selection != null && selection.slot == slot && selection.materialIndex == materialIndex)
            {
                return selection;
            }
        }

        if (!createIfMissing)
        {
            return null;
        }

        PartColorSelection created = new PartColorSelection()
        {
            slot = slot,
            materialIndex = materialIndex,
            useCustomColor = false,
            color = Color.white
        };

        partColorSelections.Add(created);
        return created;
    }

    private void ApplyAllColorOverrides()
    {
        for (int i = 0; i < OrderedSlots.Length; i++)
        {
            ApplyColorOverride(OrderedSlots[i]);
        }
    }

    private void ApplyColorOverride(PartSlot slot)
    {
        PartBinding binding = GetBinding(slot);
        if (binding?.targetRenderer == null)
        {
            return;
        }

        SkinnedMeshRenderer renderer = binding.targetRenderer;
        Material[] materials = renderer.sharedMaterials;
        int materialCount = materials != null ? materials.Length : 0;
        if (materialCount <= 0)
        {
            renderer.SetPropertyBlock(null);
            return;
        }

        for (int i = 0; i < materialCount; i++)
        {
            renderer.SetPropertyBlock(null, i);
        }

        PartColorSelection fallbackSelection = GetColorSelection(slot, -1, false);
        bool anyApplied = false;

        materialPropertyBlock ??= new MaterialPropertyBlock();
        for (int i = 0; i < materialCount; i++)
        {
            Material material = materials[i];
            if (!MaterialSupportsColor(material))
            {
                continue;
            }

            PartColorSelection slotSelection = GetColorSelection(slot, i, false);
            PartColorSelection appliedSelection = slotSelection != null && slotSelection.useCustomColor
                ? slotSelection
                : (fallbackSelection != null && fallbackSelection.useCustomColor ? fallbackSelection : null);
            if (appliedSelection == null)
            {
                continue;
            }

            Color baseColor = appliedSelection.color;
            ResolveToneRampColors(slot, material, baseColor, out Color highlightColor, out Color shadowColor);
            materialPropertyBlock.Clear();
            if (material.HasProperty(ColorShaderProperty))
            {
                materialPropertyBlock.SetColor(ColorShaderProperty, baseColor);
            }

            if (material.HasProperty(BaseColorShaderProperty))
            {
                materialPropertyBlock.SetColor(BaseColorShaderProperty, baseColor);
            }

            if (material.HasProperty(HighlightShaderProperty))
            {
                materialPropertyBlock.SetColor(HighlightShaderProperty, highlightColor);
            }

            if (material.HasProperty(ShadowShaderProperty))
            {
                materialPropertyBlock.SetColor(ShadowShaderProperty, shadowColor);
            }

            renderer.SetPropertyBlock(materialPropertyBlock, i);
            anyApplied = true;
        }

        if (!anyApplied)
        {
            renderer.SetPropertyBlock(null);
        }
    }

    private static bool RendererHasColorProperty(Renderer renderer, int propertyId)
    {
        if (renderer == null)
        {
            return false;
        }

        Material[] materials = renderer.sharedMaterials;
        if (materials == null)
        {
            return false;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material != null && material.HasProperty(propertyId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MaterialSupportsColor(Material material)
    {
        if (material == null)
        {
            return false;
        }

        return material.HasProperty(BaseColorShaderProperty) || material.HasProperty(ColorShaderProperty);
    }

    private static bool ColorsApproximatelyEqual(Color a, Color b)
    {
        const float epsilon = 0.000001f;
        return Mathf.Abs(a.r - b.r) <= epsilon
               && Mathf.Abs(a.g - b.g) <= epsilon
               && Mathf.Abs(a.b - b.b) <= epsilon
               && Mathf.Abs(a.a - b.a) <= epsilon;
    }

    private static void ResolveToneRampColors(
        PartSlot slot,
        Material material,
        Color baseColor,
        out Color highlightColor,
        out Color shadowColor)
    {
        if (TryResolveToneRampFromMaterialOriginal(material, baseColor, out highlightColor, out shadowColor))
        {
            return;
        }

        bool useSkinReference = IsSkinSlot(slot) || IsTorsoSkinMaterial(material);
        Color referenceBase = useSkinReference ? SkinReferenceBaseColor : OtherReferenceBaseColor;
        Color referenceHighlight = useSkinReference ? SkinReferenceHighlightColor : OtherReferenceHighlightColor;
        Color referenceShadow = useSkinReference ? SkinReferenceShadowColor : OtherReferenceShadowColor;

        highlightColor = DeriveToneColorFromReference(baseColor, referenceBase, referenceHighlight);
        shadowColor = DeriveToneColorFromReference(baseColor, referenceBase, referenceShadow);
    }

    private static bool TryResolveToneRampFromMaterialOriginal(
        Material material,
        Color targetBaseColor,
        out Color highlightColor,
        out Color shadowColor)
    {
        highlightColor = targetBaseColor;
        shadowColor = targetBaseColor;
        if (material == null || !TryGetMaterialBaseColor(material, out Color originalBaseColor))
        {
            return false;
        }

        bool hasOriginalHighlight = TryGetMaterialColor(material, HighlightShaderProperty, out Color originalHighlightColor);
        bool hasOriginalShadow = TryGetMaterialColor(material, ShadowShaderProperty, out Color originalShadowColor);
        if (!hasOriginalHighlight && !hasOriginalShadow)
        {
            return false;
        }

        if (hasOriginalHighlight)
        {
            highlightColor = DeriveToneColorFromReference(
                targetBaseColor,
                originalBaseColor,
                originalHighlightColor,
                preserveToneAlpha: true);
        }

        if (hasOriginalShadow)
        {
            shadowColor = DeriveToneColorFromReference(
                targetBaseColor,
                originalBaseColor,
                originalShadowColor,
                preserveToneAlpha: true);
        }

        return true;
    }

    private static bool TryGetMaterialBaseColor(Material material, out Color baseColor)
    {
        baseColor = Color.white;
        if (material == null)
        {
            return false;
        }

        if (material.HasProperty(ColorShaderProperty))
        {
            baseColor = material.GetColor(ColorShaderProperty);
            return true;
        }

        if (material.HasProperty(BaseColorShaderProperty))
        {
            baseColor = material.GetColor(BaseColorShaderProperty);
            return true;
        }

        return false;
    }

    private static bool TryGetMaterialColor(Material material, int propertyId, out Color color)
    {
        color = Color.white;
        if (material == null || !material.HasProperty(propertyId))
        {
            return false;
        }

        color = material.GetColor(propertyId);
        return true;
    }

    private static Color DeriveToneColorFromReference(Color inputBaseColor, Color referenceBase, Color referenceTone)
    {
        return DeriveToneColorFromReference(inputBaseColor, referenceBase, referenceTone, preserveToneAlpha: false);
    }

    private static Color DeriveToneColorFromReference(
        Color inputBaseColor,
        Color referenceBase,
        Color referenceTone,
        bool preserveToneAlpha)
    {
        Color.RGBToHSV(referenceBase, out float refH, out float refS, out float refV);
        Color.RGBToHSV(referenceTone, out float toneH, out float toneS, out float toneV);
        Color.RGBToHSV(inputBaseColor, out float inputH, out float inputS, out float inputV);

        float hueDelta = Mathf.DeltaAngle(refH * 360f, toneH * 360f) / 360f;
        float saturationDelta = toneS - refS;
        float valueScale = refV > 0.0001f ? toneV / refV : toneV;

        float resultH = Mathf.Repeat(inputH + hueDelta, 1f);
        float resultS = Mathf.Clamp01(inputS + saturationDelta);
        float resultV = Mathf.Clamp01(inputV * valueScale);

        Color result = Color.HSVToRGB(resultH, resultS, resultV);
        if (preserveToneAlpha)
        {
            float alphaScale = referenceBase.a > 0.0001f
                ? referenceTone.a / referenceBase.a
                : referenceTone.a;
            result.a = Mathf.Clamp01(inputBaseColor.a * alphaScale);
        }
        else
        {
            result.a = inputBaseColor.a;
        }

        return result;
    }

    private static bool IsSkinSlot(PartSlot slot)
    {
        return slot == PartSlot.Face;
    }

    private static bool IsTorsoSkinMaterial(Material material)
    {
        if (material == null || string.IsNullOrWhiteSpace(material.name))
        {
            return false;
        }

        string materialName = material.name.Replace(" (Instance)", string.Empty).Trim();
        return materialName.IndexOf("skin", StringComparison.OrdinalIgnoreCase) >= 0
               || materialName.IndexOf("body", StringComparison.OrdinalIgnoreCase) >= 0
               || materialName.IndexOf("chest", StringComparison.OrdinalIgnoreCase) >= 0
               || materialName.IndexOf("neck", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool HasCompatibleAssignedMaterials(SkinnedMeshRenderer targetRenderer, SkinnedMeshRenderer sourceRenderer)
    {
        if (targetRenderer == null || sourceRenderer == null || sourceRenderer.sharedMesh == null)
        {
            return false;
        }

        Material[] targetMaterials = targetRenderer.sharedMaterials;
        if (targetMaterials == null || targetMaterials.Length == 0)
        {
            return false;
        }

        int requiredMaterialSlots = Mathf.Max(1, sourceRenderer.sharedMesh.subMeshCount);
        if (targetMaterials.Length < requiredMaterialSlots)
        {
            return false;
        }

        for (int i = 0; i < requiredMaterialSlots; i++)
        {
            if (targetMaterials[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    private static string GetSlotNodeName(PartSlot slot)
    {
        int index = Array.IndexOf(OrderedSlots, slot) + 1;
        if (!SlotDisplayNames.TryGetValue(slot, out string shortName))
        {
            shortName = slot.ToString();
        }

        return $"Slot_{index:00}_{shortName}";
    }

    private static string GetRendererNodeName(PartSlot slot)
    {
        if (!SlotDisplayNames.TryGetValue(slot, out string shortName))
        {
            shortName = slot.ToString();
        }

        return $"Part_{shortName}";
    }

    private SkinnedMeshRenderer CreateRendererForSlot(PartBinding binding, SkinnedMeshRenderer sourceRenderer)
    {
        Transform parent = ResolveDefaultPartsRoot() ?? transform;
        binding.slotRoot = null;

        GameObject rendererNode = new GameObject(GetRendererNodeName(binding.slot));
        rendererNode.transform.SetParent(parent, false);

        SkinnedMeshRenderer renderer = rendererNode.AddComponent<SkinnedMeshRenderer>();
        CopyRendererFlags(sourceRenderer, renderer);
        return renderer;
    }

    private bool TryGetOrCreatePartRendererForTransfer(PartSlot slot, out SkinnedMeshRenderer renderer)
    {
        renderer = null;

        EnsureSerializationLayout();
        MigrateAwayFromCustomizationSlots();
        AutoBindRenderers();

        PartBinding binding = GetBinding(slot);
        if (binding == null)
        {
            return false;
        }

        renderer = binding.targetRenderer;
        if (renderer == null)
        {
            Transform parent = ResolveDefaultPartsRoot() ?? transform;
            GameObject rendererNode = new GameObject(GetRendererNodeName(slot));
            rendererNode.transform.SetParent(parent, false);
            renderer = rendererNode.AddComponent<SkinnedMeshRenderer>();
            binding.targetRenderer = renderer;
            CaptureBindingDefaults(binding);
            InvalidateBindingCache();
        }

        return renderer != null;
    }

    private static void CopyRendererFlags(SkinnedMeshRenderer from, SkinnedMeshRenderer to)
    {
        if (from == null || to == null)
        {
            return;
        }

        to.shadowCastingMode = from.shadowCastingMode;
        to.receiveShadows = from.receiveShadows;
        to.lightProbeUsage = from.lightProbeUsage;
        to.reflectionProbeUsage = from.reflectionProbeUsage;
        to.allowOcclusionWhenDynamic = from.allowOcclusionWhenDynamic;
        to.motionVectorGenerationMode = from.motionVectorGenerationMode;
        to.skinnedMotionVectors = from.skinnedMotionVectors;
        to.quality = from.quality;
        to.updateWhenOffscreen = from.updateWhenOffscreen;
        to.probeAnchor = from.probeAnchor;
        to.gameObject.layer = from.gameObject.layer;
    }

    private Dictionary<string, Transform> BuildSkeletonMap()
    {
        Dictionary<string, Transform> map = new Dictionary<string, Transform>(StringComparer.Ordinal);
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || map.ContainsKey(candidate.name))
            {
                continue;
            }

            map.Add(candidate.name, candidate);
        }

        return map;
    }

    private Transform ResolveBoneByName(string boneName)
    {
        if (string.IsNullOrWhiteSpace(boneName))
        {
            return null;
        }

        if (skeletonMap == null)
        {
            skeletonMap = BuildSkeletonMap();
        }

        return skeletonMap != null && skeletonMap.TryGetValue(boneName, out Transform bone)
            ? bone
            : null;
    }

    private static Transform ResolveBone(Transform sourceBone, Dictionary<string, Transform> map)
    {
        if (sourceBone == null || map == null)
        {
            return null;
        }

        if (map.TryGetValue(sourceBone.name, out Transform mapped))
        {
            return mapped;
        }

        return null;
    }

    private bool NormalizeChildNamesIfEnabled(bool force = false)
    {
        if (!force && !normalizeChildNames)
        {
            return false;
        }

        bool changed = false;
        for (int i = 0; i < partBindings.Count; i++)
        {
            PartBinding binding = partBindings[i];
            if (binding == null)
            {
                continue;
            }

            string slotName = GetSlotNodeName(binding.slot);
            if (binding.slotRoot != null && !string.Equals(binding.slotRoot.name, slotName, StringComparison.Ordinal))
            {
                binding.slotRoot.name = slotName;
                changed = true;
            }

            SkinnedMeshRenderer targetRenderer = binding.targetRenderer;
            if (targetRenderer != null)
            {
                string rendererName = GetRendererNodeName(binding.slot);
                if (!string.Equals(targetRenderer.name, rendererName, StringComparison.Ordinal))
                {
                    targetRenderer.name = rendererName;
                    changed = true;
                }
            }
        }

        return changed;
    }

    private bool CleanupOrphanSlotRenderers()
    {
        Transform legacySlotsRoot = ResolveLegacySlotsRoot();
        if (legacySlotsRoot == null)
        {
            return false;
        }

        bool changed = false;
        HashSet<int> boundRendererIds = new HashSet<int>();
        for (int i = 0; i < partBindings.Count; i++)
        {
            PartBinding binding = partBindings[i];
            if (binding?.targetRenderer == null)
            {
                continue;
            }

            boundRendererIds.Add(binding.targetRenderer.GetHashCode());
        }

        SkinnedMeshRenderer[] slotRenderers = legacySlotsRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < slotRenderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = slotRenderers[i];
            if (renderer == null || boundRendererIds.Contains(renderer.GetHashCode()))
            {
                continue;
            }

            if (!renderer.name.StartsWith("Part_", StringComparison.Ordinal))
            {
                continue;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(renderer.gameObject);
                changed = true;
                continue;
            }
#endif
            Destroy(renderer.gameObject);
            changed = true;
        }

        return changed;
    }

    private static bool TryResolveSlot(string sourceName, out PartSlot slot)
    {
        slot = PartSlot.Face;
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return false;
        }

        string value = sourceName.ToUpperInvariant().Replace(' ', '_').Replace('-', '_');
        while (value.Contains("__", StringComparison.Ordinal))
        {
            value = value.Replace("__", "_", StringComparison.Ordinal);
        }

        if (value.Contains("SHOULDER_L", StringComparison.Ordinal)
            || value.Contains("SHOULDERLEFT", StringComparison.Ordinal))
        {
            slot = PartSlot.ShoulderLeft;
            return true;
        }

        if (value.Contains("SHOULDER_R", StringComparison.Ordinal)
            || value.Contains("SHOULDERRIGHT", StringComparison.Ordinal))
        {
            slot = PartSlot.ShoulderRight;
            return true;
        }

        if (value.Contains("HAND_L", StringComparison.Ordinal)
            || value.Contains("HANDLEFT", StringComparison.Ordinal))
        {
            slot = PartSlot.HandLeft;
            return true;
        }

        if (value.Contains("HAND_R", StringComparison.Ordinal)
            || value.Contains("HANDRIGHT", StringComparison.Ordinal))
        {
            slot = PartSlot.HandRight;
            return true;
        }

        if (value.Contains("ARM_L", StringComparison.Ordinal)
            || value.Contains("ARMLEFT", StringComparison.Ordinal))
        {
            slot = PartSlot.ArmLeft;
            return true;
        }

        if (value.Contains("ARM_R", StringComparison.Ordinal)
            || value.Contains("ARMRIGHT", StringComparison.Ordinal))
        {
            slot = PartSlot.ArmRight;
            return true;
        }

        if (value.Contains("LEG_UPPER_L", StringComparison.Ordinal)
            || value.Contains("LEGLEFT", StringComparison.Ordinal))
        {
            slot = PartSlot.LegUpperLeft;
            return true;
        }

        if (value.Contains("LEG_UPPER_R", StringComparison.Ordinal)
            || value.Contains("LEGRIGHT", StringComparison.Ordinal))
        {
            slot = PartSlot.LegUpperRight;
            return true;
        }

        if (value.Contains("BOOTS_L", StringComparison.Ordinal)
            || value.Contains("SHOELEFT", StringComparison.Ordinal)
            || value.Contains("BOOTSLEFT", StringComparison.Ordinal))
        {
            slot = PartSlot.BootsLeft;
            return true;
        }

        if (value.Contains("BOOTS_R", StringComparison.Ordinal)
            || value.Contains("SHOERIGHT", StringComparison.Ordinal)
            || value.Contains("BOOTSRIGHT", StringComparison.Ordinal))
        {
            slot = PartSlot.BootsRight;
            return true;
        }

        if (value.Contains("HIPS", StringComparison.Ordinal))
        {
            slot = PartSlot.Hips;
            return true;
        }

        if (value.Contains("TORSO", StringComparison.Ordinal)
            || value.Contains("SPINE", StringComparison.Ordinal)
            || value.Contains("CHEST", StringComparison.Ordinal))
        {
            slot = PartSlot.Torso;
            return true;
        }

        if (value.Contains("HAIR", StringComparison.Ordinal))
        {
            slot = PartSlot.Hair;
            return true;
        }

        if (value.Contains("HEAD", StringComparison.Ordinal) || value.Contains("FACE", StringComparison.Ordinal))
        {
            slot = PartSlot.Face;
            return true;
        }

        return false;
    }
}
