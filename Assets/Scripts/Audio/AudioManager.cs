using UnityEngine;

public class AudioManager : MonoBehaviour
{
    private const string SOUND_PREF_KEY = "SoundEnabled";
    private const string VIBE_PREF_KEY = "VibrationEnabled";

    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Audio Clips (Optional - Procedural synth fallbacks built-in)")]
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField] private AudioClip jumpClip;
    [SerializeField] private AudioClip tileStepClip;
    [SerializeField] private AudioClip tileDropClip;
    [SerializeField] private AudioClip fallClip; // Player/Runner Falling SFX (girne wala sound)
    [SerializeField] private AudioClip eliminationClip;
    [SerializeField] private AudioClip victoryClip;
    [SerializeField] private AudioClip gameOverClip;
    [SerializeField] private AudioClip bgmClip;

    private bool soundEnabled = true;
    private bool vibrationEnabled = true;

    public bool IsSoundEnabled => soundEnabled;
    public bool IsVibrationEnabled => vibrationEnabled;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Copy any audio clips assigned in this scene's Inspector to the persistent Instance
            Instance.CopyClipsFrom(this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Load saved preferences (default ON = 1)
        soundEnabled = PlayerPrefs.GetInt(SOUND_PREF_KEY, 1) == 1;
        vibrationEnabled = PlayerPrefs.GetInt(VIBE_PREF_KEY, 1) == 1;

        SetupAudioSources();
    }

    public void CopyClipsFrom(AudioManager other)
    {
        if (other == null) return;
        if (other.buttonClickClip != null) buttonClickClip = other.buttonClickClip;
        if (other.jumpClip != null) jumpClip = other.jumpClip;
        if (other.tileStepClip != null) tileStepClip = other.tileStepClip;
        if (other.tileDropClip != null) tileDropClip = other.tileDropClip;
        if (other.fallClip != null) fallClip = other.fallClip;
        if (other.eliminationClip != null) eliminationClip = other.eliminationClip;
        if (other.victoryClip != null) victoryClip = other.victoryClip;
        if (other.gameOverClip != null) gameOverClip = other.gameOverClip;
        if (other.bgmClip != null) bgmClip = other.bgmClip;
        UpdateVolumes();
    }

    private void SetupAudioSources()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.volume = 0.60f;
            musicSource.spatialBlend = 0.0f; // 2D audio
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
            sfxSource.volume = 1.0f;
            sfxSource.spatialBlend = 0.0f; // 2D audio
        }

        UpdateVolumes();
    }

    private void UpdateVolumes()
    {
        if (musicSource != null) musicSource.mute = !soundEnabled;
        if (sfxSource != null) sfxSource.mute = !soundEnabled;
    }

    public void SetSoundEnabled(bool enabled)
    {
        soundEnabled = enabled;
        PlayerPrefs.SetInt(SOUND_PREF_KEY, soundEnabled ? 1 : 0);
        PlayerPrefs.Save();
        UpdateVolumes();
        Debug.Log($"[AudioManager] Sound Enabled: {soundEnabled}");
    }

    public void ToggleSound()
    {
        SetSoundEnabled(!soundEnabled);
    }

    public void SetVibrationEnabled(bool enabled)
    {
        vibrationEnabled = enabled;
        PlayerPrefs.SetInt(VIBE_PREF_KEY, vibrationEnabled ? 1 : 0);
        PlayerPrefs.Save();
        if (vibrationEnabled) TriggerVibration();
        Debug.Log($"[AudioManager] Vibration Enabled: {vibrationEnabled}");
    }

    public void ToggleVibration()
    {
        SetVibrationEnabled(!vibrationEnabled);
    }

    // --- Sound Playing Helper Methods ---

    public void PlayButtonClick()
    {
        PlaySFX(buttonClickClip, 1.0f, 1.2f);
    }

    public void PlayJump()
    {
        PlaySFX(jumpClip, 0.8f, 1.1f);
    }

    public void PlayTileStep()
    {
        PlaySFX(tileStepClip, 0.7f, 1.4f);
    }

    public void PlayTileDrop()
    {
        PlaySFX(tileDropClip, 0.9f, 0.85f);
    }

    public void PlayFall()
    {
        AudioClip clipToPlay = fallClip != null ? fallClip : (eliminationClip != null ? eliminationClip : tileDropClip);
        PlaySFX(clipToPlay, 1.0f, 1.0f);
        TriggerVibration();
    }

    public void PlayElimination()
    {
        PlaySFX(eliminationClip, 1.0f, 0.9f);
        TriggerVibration();
    }

    public void PlayVictory()
    {
        StopSFX();
        PlaySFX(victoryClip, 1.0f, 1.0f);
        TriggerVibration();
    }

    public void PlayGameOver()
    {
        StopSFX();
        PlaySFX(gameOverClip, 1.0f, 0.8f);
        TriggerVibration();
    }

    public void StopSFX()
    {
        if (sfxSource != null && sfxSource.isPlaying)
        {
            sfxSource.Stop();
        }
    }

    public void PlayBGM(AudioClip clip = null)
    {
        if (clip != null) bgmClip = clip;
        if (musicSource != null && bgmClip != null)
        {
            if (musicSource.clip != bgmClip || !musicSource.isPlaying)
            {
                musicSource.clip = bgmClip;
                if (soundEnabled) musicSource.Play();
            }
        }
    }

    private void PlaySFX(AudioClip clip, float volume = 1.0f, float pitch = 1.0f)
    {
        if (!soundEnabled || clip == null) return;

        Debug.Log($"[AudioManager] Playing SFX: {clip.name}");
        if (sfxSource != null)
        {
            sfxSource.pitch = pitch;
            sfxSource.PlayOneShot(clip, volume);
        }
    }

    public void TriggerVibration()
    {
        if (!vibrationEnabled) return;

#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }
}
