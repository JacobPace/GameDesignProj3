using HighScore;
using StarterAssets;
using System;
using System.Collections.Generic;
using Tripolygon.UModeler.UI.Data;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static StationManager;

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
    public float sanity = 100f;
    public LayerMask lightLayer;

    [Header("Inventory Settings")]
    public Inventory inventory;

    [Header("Sanity Drain Rate per Difficulty")]
    public float easyDrainRate = 0.5f;
    public float normalDrainRate = 1f;
    public float hardDrainRate = 2f;

    public float drainRate = 0f;

    // Singleton Instance
    public static Player Instance { get; private set; }


    // Private Fields
    private Collider[] _lightResults = new Collider[10];

    // Input Systems
    public PlayerInput _playerInput;
    public StarterAssetsInputs _input;
    public FirstPersonController _controller;

    //ScoreStuff
    public String PlayerName = null;
    public String inputScore = null;



    private void Awake()
    {
        Instance = this;
        inventory = new();
        _playerInput = GetComponent<PlayerInput>();
        _input = GetComponent<StarterAssetsInputs>();
        _controller = GetComponent<FirstPersonController>();

        HS.Init(this, "Catacombs");

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

            if (_playerInput.actions["Flashlight"].triggered)
            {
                Flashlight.Instance.ToggleFlashlight();
            }

            stamina = Mathf.Clamp(stamina, 0, maxStamina);
        }
        if (drainRate == 0) drainRate = SetDrainRate();
        UpdateSanity();

    }

    public void SubmitScore()
    {
        HS.SubmitHighScore(this, PlayerName, int.Parse(inputScore));
    }

    public void ClearScores()
    {
        HS.Clear(this);
    }

    public void UpdateSanity()
    {
        int numFound = Physics.OverlapSphereNonAlloc(transform.position, 20f, _lightResults, lightLayer, QueryTriggerInteraction.Collide);

        bool isInLight = false;

        for (int i = 0; i < numFound; i++)
        {
            Collider lightCollider = _lightResults[i];

            if (!Physics.Linecast(lightCollider.transform.position, transform.position + Vector3.up))
            {
                isInLight = true;
                break;
            }
        }

        if (isInLight)
        {
            sanity += 2f * drainRate * Time.deltaTime;
        }
        else
        {
            sanity -= 5f * drainRate * Time.deltaTime;
        }
        sanity = Mathf.Clamp(sanity, 0, 100);
    }

    public float SetDrainRate()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("GameManager Instance not found! Defaulting to Normal drain rate.");
            return normalDrainRate;
        }
        return drainRate = GameManager.Instance.currentDifficulty switch
        {
            GameManager.Difficulty.Easy => easyDrainRate,
            GameManager.Difficulty.Hard => hardDrainRate,
            GameManager.Difficulty.Normal => normalDrainRate,
            _ => normalDrainRate,
        };
    }

}

[System.Serializable]
public class Inventory
{
    private Dictionary<string, int> _items;

    // Helper method to ensure the dictionary exists
    private void EnsureDictionary()
    {
        if (_items == null)
        {
            _items = new Dictionary<string, int>
            {
                { "Battery", 0 },
                { "Collectible", 0 }
            };
        }
    }

    public void AddItem(string itemName, int amount)
    {
        EnsureDictionary();

        if (_items.ContainsKey(itemName))
            _items[itemName] += amount;
        else
            _items[itemName] = amount;

        Debug.Log($"Inventory: Added {amount} {itemName}. Total: {_items[itemName]}");
    }

    public bool TryUseItem(string itemName)
    {
        EnsureDictionary();
        if (_items.ContainsKey(itemName) && _items[itemName] > 0)
        {
            _items[itemName]--;
            return true;
        }
        return false;
    }

    public int GetCount(string itemName)
    {
        EnsureDictionary();
        return _items.ContainsKey(itemName) ? _items[itemName] : 0;
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