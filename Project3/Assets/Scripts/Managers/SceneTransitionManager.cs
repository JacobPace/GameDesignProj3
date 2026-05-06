using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance;

    [Header("UI")]
    public Image fadeImage;
    public float fadeDuration = 1f;

    [Header("Vignette")]
    public Volume volume;
    private Vignette vignette;

    void Awake()
    {
        // Singleton
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (volume != null)
            volume.profile.TryGet(out vignette);
    }

    void Start()
    {
        // Fade in at game start
        StartCoroutine(Fade(1, 0));
    }

    public void LoadScene(string sceneName)
    {
        StartCoroutine(Transition(sceneName));
    }

    IEnumerator Transition(string sceneName)
    {
        fadeImage.raycastTarget = true;

        // FADE OUT (to black)
        yield return StartCoroutine(Fade(0, 1));

        // Load scene BUT DO NOT switch instantly
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = true;

        // Wait until scene is fully loaded
        while (!op.isDone)
            yield return null;

        // Force screen black AFTER scene loads
        SetAlpha(1);

        // Small buffer frame (VERY IMPORTANT)
        yield return null;

        // FADE IN (black → gameplay)
        yield return StartCoroutine(Fade(1, 0));

        fadeImage.raycastTarget = false;
    }

    IEnumerator Fade(float start, float end)
    {
        float time = 0f;
        Color color = fadeImage.color;

        while (time < fadeDuration)
        {
            float t = time / fadeDuration;

            // Fade screen
            color.a = Mathf.Lerp(start, end, t);
            fadeImage.color = color;

            // Fade vignette
            if (vignette != null)
                vignette.intensity.value = Mathf.Lerp(0f, 0.5f, color.a);

            time += Time.deltaTime;
            yield return null;
        }

        color.a = end;
        fadeImage.color = color;

        if (vignette != null)
            vignette.intensity.value = end > start ? 0.5f : 0f;
    }
    void SetAlpha(float alpha)
    {
        Color c = fadeImage.color;
        c.a = alpha;
        fadeImage.color = c;
    }
}