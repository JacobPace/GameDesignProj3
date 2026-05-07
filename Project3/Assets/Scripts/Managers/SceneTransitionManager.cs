using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;


public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance;
    public Camera mainCamera;
    private float defaultFOV;

    [Header("UI")]
    public Image fadeImage;
    public float fadeDuration = 1f;

    [Header("Vignette")]
    public Volume volume;
    private Vignette vignette;

    [Header("Zoom")]
    public float zoomAmount = 8f;

    void Awake()
    {
        defaultFOV = mainCamera.fieldOfView;

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

        // fade to black
        yield return StartCoroutine(Fade(0, 1));

        // Load scene, do NOT switch yet
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = true;

        // Wait until scene is fully loaded
        while (!op.isDone)
            yield return null;

        // Force screen black AFTER scene loads
        SetAlpha(1);

        // Small buffer frame 
        yield return null;

        // Fade in
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

            // Camera easing
            float eased = Mathf.SmoothStep(0f, 1f, t);

            // Screen fade
            color.a = Mathf.Lerp(start, end, eased);
            fadeImage.color = color;

            // -- Camera zoom --
            if (mainCamera != null)
            {
                float targetFOV = defaultFOV - zoomAmount;

                // Zoom in during fade-out
                if (start < end)
                {
                    mainCamera.fieldOfView = Mathf.Lerp(defaultFOV, targetFOV, eased);
                }
                // Zoom out during fade-in
                else
                {
                    mainCamera.fieldOfView = Mathf.Lerp(targetFOV, defaultFOV, eased);
                }
            }

            // -- Vignette pulse --
            if (vignette != null)
            {
                // intensity ramp
                float baseIntensity = Mathf.Lerp(0.2f, 0.6f, color.a);

                // pulse peak
                float pulse = Mathf.Sin(eased * Mathf.PI);
                float pulseStrength = 0.25f;
                vignette.intensity.value = baseIntensity + pulse * pulseStrength;
            }

            time += Time.deltaTime;
            yield return null;
        }

        // -- Final state --
        color.a = end;
        fadeImage.color = color;

        if (mainCamera != null) 
            mainCamera.fieldOfView = defaultFOV;
        if (vignette != null)
            vignette.intensity.value = (end == 1f) ? 0.6f : 0f; 

    }
    void SetAlpha(float alpha)
    {
        Color c = fadeImage.color;
        c.a = alpha;
        fadeImage.color = c;
    }
}