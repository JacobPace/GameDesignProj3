using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class SoundMixerManager : MonoBehaviour
{
    public static SoundMixerManager Instance;

    [Header("Audio Configurations")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Optional Title Scene Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider musicSlider;

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

        LoadVolumeSettings();
    }

    void Start()
    {
        ApplyAll();
        InitializeSliders();
    }

    private void InitializeSliders()
    {
        // Set values SILENTLY so they don't trigger a false change event back to 1.0
        if (masterSlider != null) masterSlider.SetValueWithoutNotify(masterVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(soundFXVolume);
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(musicVolume);

        // Wipe any old listeners to ensure clean dynamic execution loops
        ClearSliderListeners();

        // Assign runtime link behaviors directly via code
        if (masterSlider != null) masterSlider.onValueChanged.AddListener(SetMasterVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(SetSoundFXVolume);
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(SetMusicVolume);
    }

    private void ClearSliderListeners()
    {
        if (masterSlider != null) masterSlider.onValueChanged.RemoveAllListeners();
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveAllListeners();
        if (musicSlider != null) musicSlider.onValueChanged.RemoveAllListeners();
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

    void ApplyAll() { ApplyMaster(); ApplySFX(); ApplyMusic(); }
    float ToDB(float value) => Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20f;

    void LoadVolumeSettings()
    {
        masterVolume = PlayerPrefs.GetFloat(MasterKey, 1f);
        soundFXVolume = PlayerPrefs.GetFloat(SFXKey, 1f);
        musicVolume = PlayerPrefs.GetFloat(MusicKey, 1f);
    }

    void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }
}