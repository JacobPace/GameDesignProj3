using UnityEngine;
using System.Collections;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    public AudioSource musicSource;

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

    public void PlayMusic(AudioClip newTrack, float fadeTime = 1f)
    {
        StartCoroutine(SwitchTrack(newTrack, fadeTime));
    }

    IEnumerator SwitchTrack(AudioClip newTrack, float fadeTime)
    {
        // Fade out current music
        float startVolume = musicSource.volume;

        for (float t = 0; t < fadeTime; t += Time.deltaTime)
        {
            musicSource.volume = Mathf.Lerp(startVolume, 0f, t / fadeTime);
            yield return null;
        }

        musicSource.Stop();
        musicSource.clip = newTrack;
        musicSource.Play();

        // Fade in new music
        for (float t = 0; t < fadeTime; t += Time.deltaTime)
        {
            musicSource.volume = Mathf.Lerp(0f, startVolume, t / fadeTime);
            yield return null;
        }

        musicSource.volume = startVolume;
    }
}