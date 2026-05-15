using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Flashlight : MonoBehaviour
{
    [Header("Settings")]
    public float maxPowerTime = 60f; // Seconds the flashlight lasts
    public float currentPower;
    public Light lightSource;

    [Header("Detection Components")]
    [Tooltip("The capsule collider assigned to the FlashlightTrigger layer")]
    public Collider flashlightTriggerCollider;

    [SerializeField] private AudioClip batteryInsert, batteryZap, flashClick;

    public static Flashlight Instance { get; private set; }
    private bool isOn;

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
        lightSource.enabled = false;
        isOn = false;
        if (flashlightTriggerCollider != null)
            flashlightTriggerCollider.enabled = false;
    }

    void Update()
    {
        if (isOn && currentPower > 0)
        {
            currentPower -= Time.deltaTime;
            if (currentPower <= 0)
                PowerOut();

            lightSource.intensity = currentPower;
        }
    }

    public void ToggleFlashlight()
    {
        if (currentPower > 0)
        {
            isOn = !isOn;
            lightSource.enabled = isOn;

            if (flashlightTriggerCollider != null)
                flashlightTriggerCollider.enabled = isOn;
        }
        SoundFXManager.instance.PlaySoundFXClip(flashClick, transform, 1f);
    }

    void PowerOut()
    {
        SoundFXManager.instance.PlaySoundFXClip(batteryZap, transform, 1f);
        isOn = false;
        currentPower = 0;
        lightSource.enabled = false;

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
                currentPower = maxPowerTime;
                Debug.Log("Recharged Flashlight!");
                return;
            }
            else
            {
                Debug.Log("No batteries in inventory");
                return;
            }
        }
        else
            Debug.Log("Flashlight battery is full");
    }
}