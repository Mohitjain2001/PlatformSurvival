using UnityEngine;

/// <summary>
/// 3D Character Model Builder using Minimo 3D Character Rig (Character-01).
/// Spawns the animated 3D humanoid character, sets up physics capsule, custom color themes,
/// and binds the Character3DAnimator to drive realistic animations.
/// </summary>
public static class CharacterModelBuilder
{
    private static GameObject characterPrefabCache;

    public static GameObject GetCharacterPrefab()
    {
        if (characterPrefabCache != null) return characterPrefabCache;

        // 1. Try Resources folder
        characterPrefabCache = Resources.Load<GameObject>("Character-01");

#if UNITY_EDITOR
        // 2. Fallback in Editor
        if (characterPrefabCache == null)
        {
            characterPrefabCache = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Minimo/Character/SavedPrefabs/Characters/Default/Character-01.prefab");
        }
#endif
        return characterPrefabCache;
    }

    public static GameObject BuildRunnerCharacter(string name, Color bodyColor, bool isPlayer = false)
    {
        // Root Container
        GameObject root = new GameObject(name);
        root.layer = LayerMask.NameToLayer("Default");

        // Rigidbody
        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.mass = 1.2f;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 1.0f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Capsule Collider (matching Minimo 3D character proportions ~1.45m height)
        CapsuleCollider col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0, 0.72f, 0);
        col.radius = 0.35f;
        col.height = 1.44f;
        col.direction = 1; // Y-axis

        GameObject prefab = GetCharacterPrefab();
        GameObject visualModel = null;
        Animator animator = null;

        if (prefab != null)
        {
            visualModel = Object.Instantiate(prefab, root.transform);
            visualModel.name = "Character3DVisual";
            visualModel.transform.localPosition = Vector3.zero;
            visualModel.transform.localRotation = Quaternion.identity;
            visualModel.transform.localScale = Vector3.one;

            // Remove ragdoll/customizer child colliders so root capsule handles clean platform physics
            Collider[] childColliders = visualModel.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < childColliders.Length; i++)
            {
                Object.DestroyImmediate(childColliders[i]);
            }

            animator = visualModel.GetComponent<Animator>();

            // Distinctive clothing colors for Player vs Bots
            ApplyCharacterTheme(visualModel, bodyColor, isPlayer);
        }

        // Add animator bridge
        Character3DAnimator animBridge = root.AddComponent<Character3DAnimator>();
        animBridge.SetupAnimator(animator, rb);

        return root;
    }

    private static void ApplyCharacterTheme(GameObject visual, Color bodyColor, bool isPlayer)
    {
        if (visual == null) return;

        SkinnedMeshRenderer[] renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer smr = renderers[i];
            string partName = smr.gameObject.name;

            // Tint the Torso (hoodie/shirt), Shoulders, and Arms to the character's designated color
            if (partName.Contains("Torso") || partName.Contains("Shoulder") || partName.Contains("Arm"))
            {
                Material[] mats = smr.materials;
                for (int m = 0; m < mats.Length; m++)
                {
                    Material newMat = new Material(mats[m]);
                    if (newMat.HasProperty("_Color")) newMat.SetColor("_Color", bodyColor);
                    if (newMat.HasProperty("_BaseColor")) newMat.SetColor("_BaseColor", bodyColor);
                    newMat.color = bodyColor;
                    mats[m] = newMat;
                }
                smr.materials = mats;
            }
        }
    }
}
