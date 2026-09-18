using UnityEngine;

public static class MinimoRuntimeAutoBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureBootstrapExists()
    {
        EnsureCharacterBootstrapExists();
        EnsureCustomizationBootstrapExists();
    }

    private static void EnsureCharacterBootstrapExists()
    {
        if (MinimoUnityCompatibility.FindFirstObjectByType<MinimoCharacterBootstrap>() != null)
        {
            return;
        }

        // Keep gameplay bootstrap conservative: only when controller already exists.
        MinimoTpsController existingController =
            MinimoUnityCompatibility.FindFirstObjectByType<MinimoTpsController>();
        if (existingController == null)
        {
            return;
        }

        GameObject host = existingController.gameObject;
        if (!host.TryGetComponent(out MinimoCharacterBootstrap _))
        {
            host.AddComponent<MinimoCharacterBootstrap>();
        }
    }

    private static void EnsureCustomizationBootstrapExists()
    {
        GameObject customizeCharacter = GameObject.Find("CustomizeCharacterPrefab");
        if (customizeCharacter != null)
        {
            if (!customizeCharacter.TryGetComponent(out MinimoCharacterCustomizer _))
            {
                customizeCharacter.AddComponent<MinimoCharacterCustomizer>();
            }

            if (!customizeCharacter.TryGetComponent(out MinimoCharacterDragRotate _))
            {
                customizeCharacter.AddComponent<MinimoCharacterDragRotate>();
            }
        }

        GameObject customizationCanvas = GameObject.Find("Customization");
        if (customizationCanvas != null && !customizationCanvas.TryGetComponent(out MinimoCustomizationUIController _))
        {
            customizationCanvas.AddComponent<MinimoCustomizationUIController>();
        }
    }
}
