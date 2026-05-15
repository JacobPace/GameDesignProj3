using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Flashlight : MonoBehaviour
{
    [Header("Settings")]
    public float maxPowerTime = 60f;
    public float currentPower;
    public Light lightSource;

    [Header("Low Battery Flicker")]
    public float flickerThreshold = 10f;
    public float minFlickerInterval = 0.05f;
    public float maxFlickerInterval = 0.25f;
    public float flickerDuration = 0.04f;

    [Header("Power Out Fade")]
    [Tooltip("How long the dying fade lasts")]
    public float deathFadeDuration = 1.5f;

    [Tooltip("Extra rapid flickers during death fade")]
    public bool deathFlicker = true;

    [Header("Detection Components")]
    [Tooltip("The capsule collider assigned to the FlashlightTrigger layer")]
    public Collider flashlightTriggerCollider;

    [Header("Battery Color Shift")]
    [Tooltip("Color at full battery")]
    public Color fullPowerColor = Color.white;

    [Tooltip("Color when battery is nearly dead")]
    public Color lowPowerColor = new Color(1f, 0.55f, 0.25f); // Warm orange/yellow

    [SerializeField] private AudioClip batteryInsert, batteryZap, flashClick;

    public static Flashlight Instance { get; private set; }

    private bool isOn;
    private bool isFlickering;
    private bool isDying;

    private float baseIntensity;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        if (flashlightTriggerCollider == null)
            flashlightTriggerCollider = GetComponent<Collider>();

        if (flashlightTriggerCollider != null && !flashlightTriggerCollider.isTrigger)
        {
            Debug.LogWarning($"<b>[Flashlight]</b> Collider on {gameObject.name} was not set to 'Is Trigger'! Fixing automatically.", this);
            flashlightTriggerCollider.isTrigger = true;
        }
    }

    void Start()
    {
        currentPower = maxPowerTime;

        lightSource.color = fullPowerColor;
        baseIntensity = lightSource.intensity;

        lightSource.enabled = false;
        isOn = false;

        if (flashlightTriggerCollider != null)
            flashlightTriggerCollider.enabled = false;
    }

    void Update()
    {
        if (isOn && currentPower > 0 && !isDying)
        {
            currentPower -= Time.deltaTime;

            if (currentPower <= 0)
            {
                currentPower = 0;
                StartCoroutine(PowerOutFade());
                return;
            }

            // Gradual dimming
            float normalizedPower = currentPower / maxPowerTime;

            // Exponential dimming for more natural battery feel
            float intensityMultiplier = Mathf.Pow(normalizedPower, 1.5f);

            // Color temperature shift as battery dies
            lightSource.color = Color.Lerp(
                lowPowerColor,
                fullPowerColor,
                normalizedPower
            );

            // Low battery flicker
            if (currentPower <= flickerThreshold && !isFlickering)
            {
                StartCoroutine(FlickerRoutine());
            }
        }
    }

    public void ToggleFlashlight()
    {
        if (currentPower > 0 && !isDying)
        {
            isOn = !isOn;

            lightSource.enabled = isOn;

            if (flashlightTriggerCollider != null)
                flashlightTriggerCollider.enabled = isOn;
        }

        SoundFXManager.instance.PlaySoundFXClip(flashClick, transform, 1f);
    }

    IEnumerator FlickerRoutine()
    {
        isFlickering = true;

        while (isOn && currentPower > 0 && currentPower <= flickerThreshold && !isDying)
        {
            float normalized = currentPower / flickerThreshold;

            float waitTime = Mathf.Lerp(
                minFlickerInterval,
                maxFlickerInterval,
                normalized
            );

            yield return new WaitForSeconds(waitTime);

            lightSource.enabled = false;

            yield return new WaitForSeconds(flickerDuration);

            if (isOn && currentPower > 0)
                lightSource.enabled = true;
        }

        isFlickering = false;
    }

    IEnumerator PowerOutFade()
    {
        isDying = true;

        SoundFXManager.instance.PlaySoundFXClip(batteryZap, transform, 1f);

        float startIntensity = lightSource.intensity;
        float timer = 0f;

        while (timer < deathFadeDuration)
        {
            timer += Time.deltaTime;

            float t = timer / deathFadeDuration;

            // Incandescent-style fade curve
            float fade = Mathf.Pow(1f - t, 2.5f);

            // Random unstable brightness near death
            float instability = Random.Range(0.75f, 1f);

            lightSource.intensity = startIntensity * fade * instability;

            // Optional rapid death flickers
            if (deathFlicker)
            {
                if (Random.value < 0.08f)
                {
                    lightSource.enabled = false;
                    yield return new WaitForSeconds(Random.Range(0.02f, 0.08f));
                    lightSource.enabled = true;
                }
            }

            yield return null;
        }

        // Final weak pulse
        lightSource.intensity = 0.05f;
        yield return new WaitForSeconds(0.06f);

        lightSource.enabled = false;

        isOn = false;
        isDying = false;

        if (flashlightTriggerCollider != null)
            flashlightTriggerCollider.enabled = false;

        Debug.Log("Flashlight is dead! Use a 'Battery' to recharge.");
    }

    public void Recharge()
    {
        if (currentPower < maxPowerTime)
        {
            if (Player.Instance.inventory.TryUseItem("Battery"))
            {
                SoundFXManager.instance.PlaySoundFXClip(batteryInsert, transform, 1f);

                StopAllCoroutines();

                currentPower = maxPowerTime;

                isDying = false;
                isFlickering = false;

                lightSource.color = fullPowerColor;
                lightSource.intensity = baseIntensity;

                if (isOn)
                    lightSource.enabled = true;


                Debug.Log("Recharged Flashlight!");
            }
            else
            {
                Debug.Log("No batteries in inventory");
            }
        }
        else
        {
            Debug.Log("Flashlight battery is full");
        }
    }
}