using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("HUD Elements")]
    [SerializeField] private TextMeshProUGUI aliveCountText;
    [SerializeField] private TextMeshProUGUI levelTitleText;

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

    [Header("Celebration & Toast")]
    [SerializeField] private UIConfettiEffect confettiEffect;
    [SerializeField] private TextMeshProUGUI toastText;

    private Coroutine toastCoroutine;

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

    public void SetLevelTitle(int levelNumber)
    {
        if (levelTitleText != null)
        {
            levelTitleText.text = $"LEVEL {levelNumber}";
        }
    }

    public void ShowEliminationToast(string runnerName)
    {
        if (toastText == null) return;
        if (toastCoroutine != null) StopCoroutine(toastCoroutine);
        toastCoroutine = StartCoroutine(ToastRoutine(runnerName));
    }

    private System.Collections.IEnumerator ToastRoutine(string runnerName)
    {
        toastText.text = $"{runnerName} ELIMINATED!";
        toastText.gameObject.SetActive(true);
        Color startCol = new Color(1.0f, 0.45f, 0.20f, 1.0f);
        toastText.color = startCol;

        float timer = 0f;
        while (timer < 1.8f)
        {
            timer += Time.deltaTime;
            if (timer > 1.0f)
            {
                float alpha = Mathf.Lerp(1.0f, 0.0f, (timer - 1.0f) / 0.8f);
                toastText.color = new Color(startCol.r, startCol.g, startCol.b, alpha);
            }
            yield return null;
        }

        toastText.gameObject.SetActive(false);
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

        if (confettiEffect == null)
        {
            confettiEffect = gameObject.GetComponent<UIConfettiEffect>();
            if (confettiEffect == null) confettiEffect = gameObject.AddComponent<UIConfettiEffect>();
        }

        Transform parentTransform = (victoryPanel != null) ? victoryPanel.transform : transform;
        confettiEffect.PlayConfetti(parentTransform);
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
