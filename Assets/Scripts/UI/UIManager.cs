using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    [SerializeField] private Button menuGameOverButton;

    [Header("Victory Panel")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private TextMeshProUGUI victoryTitleText;
    [SerializeField] private Button retryVictoryButton;
    [SerializeField] private Button menuVictoryButton;

    private void Awake()
    {
        HideGameOver();
        HideVictory();
    }

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
        if (menuGameOverButton != null)
        {
            menuGameOverButton.onClick.AddListener(OnMenuClicked);
        }
        if (menuVictoryButton != null)
        {
            menuVictoryButton.onClick.AddListener(OnMenuClicked);
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
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    private void OnMenuClicked()
    {
        SceneManager.LoadScene("SplashScene");
    }
}
