using UnityEngine;
using UnityEngine.InputSystem;
using StarterAssets;
using UnityEngine.Rendering;
using Tripolygon.UModeler.UI.Data;


[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(StarterAssetsInputs))]
[RequireComponent(typeof(FirstPersonController))]
public class Player : MonoBehaviour
{
    // Public Fields
    [Header("Stamina System")]
    public float maxStamina = 100f;
    public float stamina;
    public float staminaDrain = 25f;
    public float staminaRegen = 10f;


    // Singleton Instance
    public static Player Instance { get; private set; }
    
    
    // Private Fields

    // Input Systems
    public PlayerInput _playerInput;
    public StarterAssetsInputs _input;
    public FirstPersonController _controller;


    private void Awake()
    {
        Instance = this;
        _playerInput = GetComponent<PlayerInput>();
        _input = GetComponent<StarterAssetsInputs>();
        _controller = GetComponent<FirstPersonController>();

        
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        stamina = maxStamina;
    }

    // Update is called once per frame
    void Update()
    {
        // Current Action Map check
        if (_playerInput.currentActionMap.name == "Player")
        {
            bool isSprinting = _input.sprint && _input.move != Vector2.zero;
            if (isSprinting && stamina > 0)
            {
                stamina -= staminaDrain * Time.deltaTime;
                if (stamina <= 0)
                {
                    stamina = 0;
                    _input.sprint = false; // Force stop sprint
                }
            }
            else if (stamina < maxStamina) stamina += staminaRegen * Time.deltaTime;

            if (_playerInput.actions["Flashlight"].triggered){
                Debug.Log("light");
            }


            stamina = Mathf.Clamp(stamina, 0, maxStamina);
        }
        
    }
}
/*
Dev Notes:

With starter assets package, we are given an input system action to work with
It can be found here Assets/Starter Assets/Runtime/FirstPersonController/InputSystem

There are a few methods that we can use to enable/disable input action maps and toggle between them, for ex:

Swapping Current Action Map:
    Say we want to disable player movement while the inventory is open or they are in a menu, we could use this:
_playerInput.SwitchCurrentActionMap("UI");

FYI, the default action map is set to "Player"

Disabling Specific Action Maps in the System:
    If you needed to enable or disable a specific action map in the system you can use this method call:
_playerInput.actions.FindActionMap("Player").Enable(); // .Disable();
    If you really wanted to you could define this as a varible, but it must be of the InputActionMap type.

We should not have to enable the action map at the start due to the Player Input component on the "PlayerCapsule" prefab
 */