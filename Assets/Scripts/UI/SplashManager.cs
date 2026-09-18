using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SplashManager : MonoBehaviour
{
    [Header("UI Buttons")]
    [SerializeField] private Button playButton;

    private void Start()
    {
        if (playButton != null)
        {
            playButton.onClick.AddListener(OnPlayClicked);
        }
    }

    public void OnPlayClicked()
    {
        // Load Gameplay Scene (index 1 or by name)
        SceneManager.LoadScene("GameplayScene");
    }
}
