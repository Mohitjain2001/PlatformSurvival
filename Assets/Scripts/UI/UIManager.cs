using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("HUD Elements")]
    [SerializeField] private TextMeshProUGUI aliveCountText;

    [Header("Game Over Panel")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI gameOverTitleText;
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private Button retryGameOverButton;

    [Header("Victory Panel")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private TextMeshProUGUI victoryTitleText;
    [SerializeField] private Button retryVictoryButton;

    private void Start()
    {
        if (retryGameOverButton != null)
        {
            retryGameOverButton.onClick.AddListener(OnRetryClicked);
        }
        if (retryVictoryButton != null)
        {
            retryVictoryButton.onClick.AddListener(OnRetryClicked);
        }

        HideGameOver();
        HideVictory();
    }

    public void UpdateAliveCount(int currentAlive, int total)
    {
        if (aliveCountText != null)
        {
            aliveCountText.text = $"ALIVE: {currentAlive} / {total}";
        }
    }

    public void ShowGameOver(int rank, int total)
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        if (rankText != null)
        {
            rankText.text = $"ELIMINATED!\nPlaced #{rank} out of {total}";
        }
    }

    public void HideGameOver()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    public void ShowVictory()
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }

        if (victoryTitleText != null)
        {
            victoryTitleText.text = "VICTORY!\nLAST PLAYER STANDING!";
        }
    }

    public void HideVictory()
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }
    }

    private void OnRetryClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartMatch();
        }
    }
}
