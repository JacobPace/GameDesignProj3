using System;
using UnityEngine;

public class Flashlight : MonoBehaviour
{
    [Header("Settings")]
    public float maxPowerTime = 60f; // Seconds the flashlight lasts
    public float currentPower;
    public Light lightSource;
    [SerializeField] private AudioClip batteryInsert;
    [SerializeField] private AudioClip batteryZap;

    public static Flashlight Instance { get; private set; }

    private bool isOn;

    private void Awake()
    {
        Instance = this;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentPower = maxPowerTime;
        lightSource.enabled = false;
        isOn = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (isOn && currentPower > 0)
        {
            currentPower -= Time.deltaTime;
            if (currentPower <= 0) PowerOut();
        }
    }

    public void ToggleFlashlight()
    {
        if (currentPower > 0)
        {
            isOn = !isOn;
            lightSource.enabled = isOn;
        }
    }

    void PowerOut()
    {
        SoundFXManager.instance.PlaySoundFXClip(batteryZap, transform, 1f);
        isOn = false;
        currentPower = 0;
        lightSource.enabled = false;
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
        else Debug.Log("Flashlight battery is full");
    }

}