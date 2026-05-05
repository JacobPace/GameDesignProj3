using UnityEngine;

public class Flashlight : MonoBehaviour
{
    [Header("Settings")]
    public float maxPowerTime = 60f; // Seconds the flashlight lasts
    public float currentPower;
    public Light lightSource;

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

        /* Press 'R' to recharge using an item from the inventory class, can be changed to be done in player script
        if (Input.GetKeyDown(KeyCode.R) && currentPower < maxPowerTime)
        {
            Recharge();
        }*/
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
        isOn = false;
        currentPower = 0;
        lightSource.enabled = false;
        Debug.Log("Flashlight is dead! Use a 'Battery' to recharge.");
    }

    void Recharge()
    {
        // Accessing the non-MonoBehaviour class inside the Player script
        if (Player.Instance.inventory.TryUseItem("Battery"))
        {
            currentPower = maxPowerTime;
            Debug.Log("Recharged Flashlight!");
        }
    }

}