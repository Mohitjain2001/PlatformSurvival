using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controller for the dedicated WinScene.
/// Celebrates player victory with animations, statistics, and navigation options.
/// </summary>
[ExecuteAlways]
public class WinSceneManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private Button playAgainButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Celebration Podium")]
    [SerializeField] private Transform winnerBeanTransform;

    [Header("Background")]
    [SerializeField] private Sprite backgroundSprite;

    private void Start()
    {
        SetupBackground();

        int completedLevel = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel - 1 : 1;
        if (completedLevel < 1) completedLevel = 1;

        if (titleText != null)
        {
            titleText.text = $"LEVEL {completedLevel} CLEARED!";
        }

        if (subtitleText != null)
        {
            subtitleText.text = $"CONGRATULATIONS!\nYou survived against {GameDataManager.TotalParticipants - 1} opponents!";
        }

        if (playAgainButton != null)
        {
            playAgainButton.onClick.AddListener(OnPlayAgainClicked);
            TextMeshProUGUI btnText = playAgainButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = "NEXT LEVEL";
            }
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }
    }

    private void Update()
    {
        // Gentle rotation for the winning character on podium
        if (winnerBeanTransform != null)
        {
            winnerBeanTransform.Rotate(Vector3.up, 45f * Time.deltaTime, Space.World);
        }
    }

    public void OnPlayAgainClicked()
    {
        SceneManager.LoadScene("GameplayScene");
    }

    public void OnMainMenuClicked()
    {
        SceneManager.LoadScene("SplashScene");
    }

    private void SetupBackground()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        // Auto-load Win background Panel sprite if unassigned
        if (backgroundSprite == null)
        {
#if UNITY_EDITOR
            backgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Background/Win background Panel.png");
#endif
        }

        if (backgroundSprite == null) return;

        // Attach background plane to camera behind the character
        Transform existing = cam.transform.Find("CameraBackgroundPlane");
        GameObject bgObj = existing != null ? existing.gameObject : new GameObject("CameraBackgroundPlane");
        bgObj.transform.SetParent(cam.transform, false);

        // Position 16 units in front of camera (behind podium & character at distance ~6.7)
        float distance = 16f;
        bgObj.transform.localPosition = new Vector3(0, 0, distance);
        bgObj.transform.localRotation = Quaternion.identity;

        SpriteRenderer sr = bgObj.GetComponent<SpriteRenderer>();
        if (sr == null) sr = bgObj.AddComponent<SpriteRenderer>();
        sr.sprite = backgroundSprite;
        sr.sortingOrder = -100;

        // Scale to cleanly fill camera viewport
        float fovRad = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float height = 2f * distance * Mathf.Tan(fovRad);
        float width = height * cam.aspect;

        Vector2 spriteSize = backgroundSprite.bounds.size;
        if (spriteSize.x > 0 && spriteSize.y > 0)
        {
            float scaleFactor = Mathf.Max(width / spriteSize.x, height / spriteSize.y);
            bgObj.transform.localScale = new Vector3(scaleFactor, scaleFactor, 1f);
        }
    }
}
