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

    [Header("Background & Frame")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite victoryCardSprite;

    [Header("Celebration Effects")]
    [SerializeField] private UIConfettiEffect confettiEffect;

    private void Start()
    {
        SetupBackground();

        // Start celebration confetti effect in WinScene
        if (Application.isPlaying)
        {
            if (confettiEffect == null)
            {
                confettiEffect = GetComponent<UIConfettiEffect>();
                if (confettiEffect == null)
                    confettiEffect = gameObject.AddComponent<UIConfettiEffect>();
            }
            confettiEffect.PlayConfetti(transform, true);
        }

        int completedLevel = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel - 1 : 1;
        if (completedLevel < 1) completedLevel = 1;

        // win.png already has "LEVEL CLEARED" and "CONGRATULATIONS!" built into the artwork
        if (subtitleText != null)
        {
            subtitleText.gameObject.SetActive(false);
        }

        if (titleText != null)
        {
            // Only update the level number, keep exact RectTransform position from Inspector
            titleText.text = completedLevel.ToString();
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

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            SetupBackground();
        }
#endif
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

        // 1. OUTER BACKGROUND (Floating Islands & Sky: Win background Panel.png)
        if (backgroundSprite == null)
        {
#if UNITY_EDITOR
            backgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Background/Win background Panel.png");
#endif
        }

        if (backgroundSprite != null)
        {
            Transform existingBg = cam.transform.Find("CameraBackgroundPlane");
            GameObject bgObj = existingBg != null ? existingBg.gameObject : new GameObject("CameraBackgroundPlane");
            bgObj.transform.SetParent(cam.transform, false);

            float bgDistance = 18f;
            bgObj.transform.localPosition = new Vector3(0, 0, bgDistance);
            bgObj.transform.localRotation = Quaternion.identity;

            SpriteRenderer bgSr = bgObj.GetComponent<SpriteRenderer>();
            if (bgSr == null) bgSr = bgObj.AddComponent<SpriteRenderer>();
            bgSr.sprite = backgroundSprite;
            bgSr.sortingOrder = -200;

            float fovRad = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float bgHeight = 2f * bgDistance * Mathf.Tan(fovRad);
            float bgWidth = bgHeight * cam.aspect;

            Vector2 spriteSize = backgroundSprite.bounds.size;
            if (spriteSize.x > 0 && spriteSize.y > 0)
            {
                float scaleFactor = Mathf.Max(bgWidth / spriteSize.x, bgHeight / spriteSize.y);
                bgObj.transform.localScale = new Vector3(scaleFactor, scaleFactor, 1f);
            }
        }

        // 2. INNER VICTORY CARD (Celebration Frame: win.png)
        // Positioned at distance 11f — directly behind character (~6.95f) and in front of outer bg (18f)
        if (victoryCardSprite == null)
        {
#if UNITY_EDITOR
            victoryCardSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Background/win.png");
#endif
        }

        if (victoryCardSprite != null)
        {
            Transform existingCard = cam.transform.Find("VictoryCardPlane");
            GameObject cardObj = existingCard != null ? existingCard.gameObject : new GameObject("VictoryCardPlane");
            cardObj.transform.SetParent(cam.transform, false);

            float cardDistance = 11f;
            cardObj.transform.localPosition = new Vector3(0, 0, cardDistance);
            cardObj.transform.localRotation = Quaternion.identity;

            SpriteRenderer cardSr = cardObj.GetComponent<SpriteRenderer>();
            if (cardSr == null) cardSr = cardObj.AddComponent<SpriteRenderer>();
            cardSr.sprite = victoryCardSprite;
            cardSr.sortingOrder = -100;

            float cardFovRad = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float cardViewHeight = 2f * cardDistance * Mathf.Tan(cardFovRad);

            Vector2 cardSize = victoryCardSprite.bounds.size;
            if (cardSize.x > 0 && cardSize.y > 0)
            {
                // Fit card height to ~95% of screen height so outer islands frame the sides nicely
                float cardScale = (cardViewHeight * 0.95f) / cardSize.y;
                cardObj.transform.localScale = new Vector3(cardScale, cardScale, 1f);
            }
        }
    }
}
