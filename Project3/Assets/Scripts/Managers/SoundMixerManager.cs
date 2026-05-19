using UnityEngine;
using UnityEngine.Audio;

public class SoundMixerManager : MonoBehaviour
{
    public static SoundMixerManager Instance;

    [SerializeField] private AudioMixer audioMixer;

    // Keys used to save and look up data in the player's system registry
    private const string MasterKey = "MasterVolume";
    private const string SFXKey = "SFXVolume";
    private const string MusicKey = "MusicVolume";

    [Range(0.0001f, 1f)] public float masterVolume = 1f;
    [Range(0.0001f, 1f)] public float soundFXVolume = 1f;
    [Range(0.0001f, 1f)] public float musicVolume = 1f;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Load saved data immediately before the first frame runs
        LoadVolumeSettings();
    }

    void Start()
    {
        ApplyAll();
    }

    public void SetMasterVolume(float level)
    {
        masterVolume = level;
        ApplyMaster();
        PlayerPrefs.SetFloat(MasterKey, masterVolume);
    }

    public void SetSoundFXVolume(float level)
    {
        soundFXVolume = level;
        ApplySFX();
        PlayerPrefs.SetFloat(SFXKey, soundFXVolume);
    }

    public void SetMusicVolume(float level)
    {
        musicVolume = level;
        ApplyMusic();
        PlayerPrefs.SetFloat(MusicKey, musicVolume);
    }

    void ApplyMaster() => audioMixer.SetFloat("masterVolume", ToDB(masterVolume));
    void ApplySFX() => audioMixer.SetFloat("soundFXVolume", ToDB(soundFXVolume));
    void ApplyMusic() => audioMixer.SetFloat("musicVolume", ToDB(musicVolume));

    void ApplyAll()
    {
        ApplyMaster();
        ApplySFX();
        ApplyMusic();
    }

    float ToDB(float value) => Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20f;

    void LoadVolumeSettings()
    {
        // GetFloat reads the key. If it doesn't exist yet, it falls back to the default value (1f)
        masterVolume = PlayerPrefs.GetFloat(MasterKey, 1f);
        soundFXVolume = PlayerPrefs.GetFloat(SFXKey, 1f);
        musicVolume = PlayerPrefs.GetFloat(MusicKey, 1f);
    }

    // Force an explicit save data write when the game closes or loses focus
    void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }
}