using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controller for the dedicated GameOverScene.
/// Displays placement rank, total players, and retry/menu navigation.
/// </summary>
public class GameOverSceneManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private TextMeshProUGUI tipText;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button mainMenuButton;

    private void Start()
    {
        if (titleText != null)
        {
            titleText.text = "ELIMINATED!";
        }

        if (rankText != null)
        {
            int rank = GameDataManager.LastPlacementRank;
            int total = GameDataManager.TotalParticipants;
            rankText.text = $"You Placed #{rank}\n(out of {total} players)";
        }

        if (tipText != null)
        {
            tipText.text = "PRO TIP:\nKeep moving! Hexagons flash before they drop!";
        }

        if (retryButton != null)
        {
            retryButton.onClick.AddListener(OnRetryClicked);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }
    }

    public void OnRetryClicked()
    {
        SceneManager.LoadScene("GameplayScene");
    }

    public void OnMainMenuClicked()
    {
        SceneManager.LoadScene("SplashScene");
    }
}
