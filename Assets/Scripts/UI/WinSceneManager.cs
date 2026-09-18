using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controller for the dedicated WinScene.
/// Celebrates player victory with animations, statistics, and navigation options.
/// </summary>
public class WinSceneManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private Button playAgainButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Celebration Podium")]
    [SerializeField] private Transform winnerBeanTransform;

    private void Start()
    {
        if (titleText != null)
        {
            titleText.text = "VICTORY!";
        }

        if (subtitleText != null)
        {
            subtitleText.text = $"CONGRATULATIONS!\nYou survived against {GameDataManager.TotalParticipants - 1} opponents!";
        }

        if (playAgainButton != null)
        {
            playAgainButton.onClick.AddListener(OnPlayAgainClicked);
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
}
