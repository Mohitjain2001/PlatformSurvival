using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    private const string LEVEL_PREF_KEY = "CurrentLevelIndex";

    private static LevelManager instance;
    public static LevelManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<LevelManager>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("LevelManager");
                    instance = obj.AddComponent<LevelManager>();
                }
            }
            return instance;
        }
    }

    public int CurrentLevel
    {
        get => PlayerPrefs.GetInt(LEVEL_PREF_KEY, 1);
        private set
        {
            PlayerPrefs.SetInt(LEVEL_PREF_KEY, Mathf.Max(1, value));
            PlayerPrefs.Save();
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public LevelConfig GetCurrentLevelConfig()
    {
        return LevelGenerator.GetConfigForLevel(CurrentLevel);
    }

    public void AdvanceToNextLevel()
    {
        CurrentLevel++;
        Debug.Log($"[LevelManager] Advanced to Level {CurrentLevel}!");
    }

    public void ResetProgress()
    {
        CurrentLevel = 1;
        Debug.Log("[LevelManager] Progress reset to Level 1.");
    }

    public void LoadGameplayScene()
    {
        SceneManager.LoadScene("GameplayScene");
    }
}
