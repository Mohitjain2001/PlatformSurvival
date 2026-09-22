using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SplashManager : MonoBehaviour
{
    [Header("UI Buttons & Panels")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button closeSettingsButton;

    private void Start()
    {
        // Auto-find references if not assigned in Inspector
        if (playButton == null)
        {
            Transform tPlay = transform.Find("PlayButton");
            if (tPlay != null) playButton = tPlay.GetComponent<Button>();
        }

        if (settingsButton == null)
        {
            Transform tSet = transform.Find("Settings Button");
            if (tSet != null) settingsButton = tSet.GetComponent<Button>();
        }

        if (settingsPanel == null)
        {
            Transform tPan = transform.Find("Settings panel");
            if (tPan != null) settingsPanel = tPan.gameObject;
        }

        if (settingsPanel != null && closeSettingsButton == null)
        {
            // Look for "Close" button inside Settings panel
            Button[] btns = settingsPanel.GetComponentsInChildren<Button>(true);
            foreach (var btn in btns)
            {
                if (btn.name.Equals("Close", System.StringComparison.OrdinalIgnoreCase))
                {
                    closeSettingsButton = btn;
                    break;
                }
            }
        }

        // Setup Button Listeners
        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayClicked);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(OpenSettings);
        }

        if (closeSettingsButton != null)
        {
            closeSettingsButton.onClick.RemoveAllListeners();
            closeSettingsButton.onClick.AddListener(CloseSettings);
        }

        // Ensure Settings Panel is closed initially on game start
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    public void OnPlayClicked()
    {
        SceneManager.LoadScene("GameplayScene");
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }
}
