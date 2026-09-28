using System;
using System.Collections.Generic;
using UnityEngine;

public static class CharacterDatabase
{
    private const string SELECTED_CHAR_KEY = "SelectedCharacterModelId";
    public const string DEFAULT_CHAR_ID = "bean";

    private const string COINS_KEY = "PlayerCoins";
    private const int DEFAULT_COINS = 2130; // Starts at 2130 matching reference mockup!

    public static event Action<string> OnCharacterChanged;
    public static event Action<string> OnCharacterUnlocked;
    public static event Action OnCurrencyChanged;

    private static List<CharacterData> characters;
    private static RuntimeAnimatorController cachedController;

    public static List<CharacterData> GetAllCharacters()
    {
        if (characters == null)
        {
            Initialize();
        }
        return characters;
    }

    public static CharacterData GetSelectedCharacter()
    {
        string id = GetSelectedCharacterId();
        return GetCharacterById(id);
    }

    public static string GetSelectedCharacterId()
    {
        return PlayerPrefs.GetString(SELECTED_CHAR_KEY, DEFAULT_CHAR_ID);
    }

    public static void SelectCharacter(string id)
    {
        PlayerPrefs.SetString(SELECTED_CHAR_KEY, id);
        PlayerPrefs.Save();
        OnCharacterChanged?.Invoke(id);
    }

    public static CharacterData GetCharacterById(string id)
    {
        if (characters == null) Initialize();

        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i].id == id)
                return characters[i];
        }
        return characters[0];
    }

    // ─────────────────────────────────────────────────────────────
    //  UNLOCKING & INVENTORY
    // ─────────────────────────────────────────────────────────────

    public static bool IsCharacterUnlocked(string id)
    {
        if (string.IsNullOrEmpty(id) || id == DEFAULT_CHAR_ID) return true;
        return PlayerPrefs.GetInt("Unlocked_" + id, 1) == 1;
    }

    public static bool BuyCharacter(string id)
    {
        CharacterData data = GetCharacterById(id);
        if (data == null) return false;

        if (IsCharacterUnlocked(id)) return true;

        if (SpendCoins(data.price))
        {
            PlayerPrefs.SetInt("Unlocked_" + id, 1);
            PlayerPrefs.Save();
            OnCharacterUnlocked?.Invoke(id);
            return true;
        }
        return false;
    }

    public static bool UnlockCharacter(string id)
    {
        PlayerPrefs.SetInt("Unlocked_" + id, 1);
        PlayerPrefs.Save();
        OnCharacterUnlocked?.Invoke(id);
        return true;
    }

    // ─────────────────────────────────────────────────────────────
    //  CURRENCY
    // ─────────────────────────────────────────────────────────────

    public static int GetCoins()
    {
        return PlayerPrefs.GetInt(COINS_KEY, DEFAULT_COINS);
    }

    public static void AddCoins(int amount)
    {
        int cur = GetCoins() + amount;
        PlayerPrefs.SetInt(COINS_KEY, cur);
        PlayerPrefs.Save();
        OnCurrencyChanged?.Invoke();
    }

    public static bool SpendCoins(int amount)
    {
        int cur = GetCoins();
        if (cur >= amount)
        {
            PlayerPrefs.SetInt(COINS_KEY, cur - amount);
            PlayerPrefs.Save();
            OnCurrencyChanged?.Invoke();
            return true;
        }
        return false;
    }

    // ─────────────────────────────────────────────────────────────
    //  CHARACTER CATALOG (Matching Reference Image)
    // ─────────────────────────────────────────────────────────────

    private static void Initialize()
    {
        characters = new List<CharacterData>
        {
            new CharacterData(
                id: "bean",
                displayName: "Default",
                prefabPath: "Assets/Minimo/Character/SavedPrefabs/Characters/Default/Character-01.prefab",
                scale: 1.0f,
                localOffset: Vector3.zero,
                price: 0,
                themeColor: new Color(0.95f, 0.25f, 0.25f) // Red/Blue Default
            ),
            new CharacterData(
                id: "police",
                displayName: "Police",
                prefabPath: "Assets/Models/character_police.fbx",
                scale: 0.82f,
                localOffset: Vector3.zero,
                price: 800,
                themeColor: new Color(0.12f, 0.25f, 0.55f) // Police Blue
            ),
            new CharacterData(
                id: "girl",
                displayName: "Girl",
                prefabPath: "Assets/Prefabs/Girl_character.fbx",
                scale: 0.82f,
                localOffset: Vector3.zero,
                price: 500,
                themeColor: new Color(0.95f, 0.50f, 0.70f) // Pink Girl
            ),
            new CharacterData(
                id: "ch4",
                displayName: "Ch4",
                prefabPath: "Assets/Models/Ch4.fbx",
                scale: 0.82f,
                localOffset: Vector3.zero,
                price: 1000,
                themeColor: new Color(0.30f, 0.60f, 0.95f) // Ch4 Blue
            ),
            new CharacterData(
                id: "ninja",
                displayName: "Ninja",
                prefabPath: "Assets/Minimo/Character/SavedPrefabs/Characters/Default/Character-01.prefab",
                scale: 1.0f,
                localOffset: Vector3.zero,
                price: 500,
                themeColor: new Color(0.12f, 0.12f, 0.15f) // Black Ninja
            ),
            new CharacterData(
                id: "noob",
                displayName: "Noob",
                prefabPath: "Assets/Minimo/Character/SavedPrefabs/Characters/Default/Character-01.prefab",
                scale: 1.0f,
                localOffset: Vector3.zero,
                price: 800,
                themeColor: new Color(1.00f, 0.85f, 0.15f) // Yellow/Green Noob
            ),
            new CharacterData(
                id: "robot",
                displayName: "Robot",
                prefabPath: "Assets/Minimo/Character/SavedPrefabs/Characters/Default/Character-01.prefab",
                scale: 1.0f,
                localOffset: Vector3.zero,
                price: 1000,
                themeColor: new Color(0.85f, 0.90f, 0.95f) // Cyber Metallic
            ),
            new CharacterData(
                id: "alien",
                displayName: "Alien",
                prefabPath: "Assets/Minimo/Character/SavedPrefabs/Characters/Default/Character-01.prefab",
                scale: 1.0f,
                localOffset: Vector3.zero,
                price: 1200,
                themeColor: new Color(0.20f, 0.85f, 0.40f) // Neon Alien Green
            ),
            new CharacterData(
                id: "santa",
                displayName: "Santa",
                prefabPath: "Assets/Minimo/Character/SavedPrefabs/Characters/Default/Character-01.prefab",
                scale: 1.0f,
                localOffset: Vector3.zero,
                price: 1500,
                themeColor: new Color(0.92f, 0.15f, 0.15f) // Santa Red
            ),
            new CharacterData(
                id: "tiger",
                displayName: "Tiger",
                prefabPath: "Assets/Minimo/Character/SavedPrefabs/Characters/Default/Character-01.prefab",
                scale: 1.0f,
                localOffset: Vector3.zero,
                price: 1500,
                themeColor: new Color(0.98f, 0.52f, 0.10f) // Orange Tiger
            )
        };
    }

    public static RuntimeAnimatorController GetAnimatorController()
    {
        if (cachedController != null) return cachedController;

        cachedController = Resources.Load<RuntimeAnimatorController>("CharacterController");

#if UNITY_EDITOR
        if (cachedController == null)
        {
            cachedController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Minimo/Character/Animations/Character.controller");
        }
#endif
        return cachedController;
    }

    /// <summary>
    /// Instantiates the selected 3D character visual under parent, configures Animator & Controller,
    /// removes non-root colliders, and applies scale.
    /// </summary>
    public static GameObject SpawnVisual(string characterId, Transform parent, out Animator animator)
    {
        CharacterData data = GetCharacterById(characterId);
        GameObject sourcePrefab = null;

        // 1. Try Resources folder first (works at runtime & in builds)
        if (data.id == "bean")
        {
            sourcePrefab = Resources.Load<GameObject>("Character-01");
        }
        else if (data.id == "police")
        {
            sourcePrefab = Resources.Load<GameObject>("Character_Police");
        }
        else if (data.id == "girl")
        {
            sourcePrefab = Resources.Load<GameObject>("Girl_character");
        }
        else if (data.id == "ch4")
        {
            sourcePrefab = Resources.Load<GameObject>("Ch4");
        }

#if UNITY_EDITOR
        // 2. Fallback in Editor to direct asset path if prefab not yet built in Resources
        if (sourcePrefab == null && !string.IsNullOrEmpty(data.prefabPath))
        {
            sourcePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(data.prefabPath);
        }
#endif

        // 3. Fallback to default runner model only if still null
        if (sourcePrefab == null)
        {
            sourcePrefab = Resources.Load<GameObject>("Character-01");
#if UNITY_EDITOR
            if (sourcePrefab == null)
            {
                sourcePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Minimo/Character/SavedPrefabs/Characters/Default/Character-01.prefab");
            }
#endif
        }

        Debug.Log($"[CharacterDatabase] SpawnVisual '{characterId}' -> loaded sourcePrefab: {(sourcePrefab != null ? sourcePrefab.name : "NULL")}");

        if (sourcePrefab == null)
        {
            Debug.LogWarning($"[CharacterDatabase] Could not load model for '{characterId}'");
            animator = null;
            return null;
        }

        GameObject visual = UnityEngine.Object.Instantiate(sourcePrefab, parent);
        visual.name = "Character3DVisual";
        visual.transform.localPosition = data.localOffset;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one * data.scale;

        // Clean up child colliders so root capsule handles clean platform physics
        Collider[] childColliders = visual.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < childColliders.Length; i++)
        {
            UnityEngine.Object.DestroyImmediate(childColliders[i]);
        }

        // Setup Animator & Humanoid Avatar
        animator = visual.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            animator = visual.AddComponent<Animator>();
        }

        if (animator.avatar == null && sourcePrefab != null)
        {
            Animator srcAnim = sourcePrefab.GetComponentInChildren<Animator>(true);
            if (srcAnim != null && srcAnim.avatar != null)
            {
                animator.avatar = srcAnim.avatar;
            }
        }

        RuntimeAnimatorController controller = GetAnimatorController();
        if (controller != null)
        {
            animator.runtimeAnimatorController = controller;
        }

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.enabled = true;

        // Verify shaders are valid (fallback to Standard if pink/broken)
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] mats = renderers[i].materials;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] != null && mats[m].shader != null && mats[m].shader.name == "Hidden/InternalErrorShader")
                {
                    mats[m].shader = Shader.Find("Standard");
                }
            }
        }

        // Apply theme color tint for characters using base model
        if (data.id != "bean" && data.id != "police" && data.themeColor != Color.white)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].gameObject.name.Contains("Torso") || renderers[i].gameObject.name.Contains("Body") || renderers[i].gameObject.name.Contains("Default"))
                {
                    renderers[i].material.color = data.themeColor;
                }
            }
        }

        return visual;
    }
}
