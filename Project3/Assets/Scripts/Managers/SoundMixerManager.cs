using UnityEngine;
using UnityEngine.Audio;

public class SoundMixerManager : MonoBehaviour
{
    public static SoundMixerManager Instance;

    [SerializeField] private AudioMixer audioMixer;

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
    }

    void Start()
    {
        ApplyAll();
    }

    public void SetMasterVolume(float level)
    {
        masterVolume = level;
        ApplyMaster();
    }

    public void SetSoundFXVolume(float level)
    {
        soundFXVolume = level;
        ApplySFX();
    }

    public void SetMusicVolume(float level)
    {
        musicVolume = level;
        ApplyMusic();
    }

    void ApplyMaster()
    {
        audioMixer.SetFloat("masterVolume", ToDB(masterVolume));
    }

    void ApplySFX()
    {
        audioMixer.SetFloat("soundFXVolume", ToDB(soundFXVolume));
    }

    void ApplyMusic()
    {
        audioMixer.SetFloat("musicVolume", ToDB(musicVolume));
    }

    void ApplyAll()
    {
        ApplyMaster();
        ApplySFX();
        ApplyMusic();
    }

    float ToDB(float value)
    {
        return Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20f;
    }
}
