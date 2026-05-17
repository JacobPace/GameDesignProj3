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
[RequireComponent(typeof(Animator))]
public class Player : MonoBehaviour
{
    // Public Fields
    [Header("Stamina System")]
    public float maxStamina = 100f;
    public float stamina;
    public float staminaDrain = 25f;
    public float staminaRegen = 10f;

    [Header("Sanity System")]
    public float maxSanity = 100f;
    public float sanity = 100f;
    public float drainRate = 0f;
    [Header("Sanity Drain Rate per Difficulty")]
    public float easyDrainRate = 0.5f;
    public float normalDrainRate = 1f;
    public float hardDrainRate = 2f;

    [Header("Other Settings")]
    public LayerMask lightLayer;
    public GameObject winZone;

    [Header("Inventory Settings")]
    public Inventory inventory;

    [Header("UI References")]
    public Slider staminaSlider;
    public Slider sanitySlider;
    public Animator anim;
    public GameObject VideoMenu;

    [Header("Dev Cheats")]
    public GameObject devCheat;

    // Singleton Instance
    public static Player Instance { get; private set; }

    // Input Systems
    [HideInInspector]
    public PlayerInput _playerInput;
    public StarterAssetsInputs _input;
    public FirstPersonController _controller;

    [HideInInspector]
    private readonly Collider[] _lightResults = new Collider[10];
    public InputAction playerPauseAction;
    public InputAction uiPauseAction;

    private void Awake()
    {
        Instance = this;
        inventory = new();
        _playerInput = GetComponent<PlayerInput>();
        _input = GetComponent<StarterAssetsInputs>();
        _controller = GetComponent<FirstPersonController>();
        anim = GetComponent<Animator>();
        playerPauseAction = _playerInput.actions.FindAction($"Player/Pause");
        uiPauseAction = _playerInput.actions.FindAction($"UI/Pause");
    }
    
    void Start()
    {
        stamina = maxStamina;
        sanity = maxSanity;
        if (staminaSlider != null) staminaSlider.maxValue = maxStamina;
        if (sanitySlider != null) sanitySlider.maxValue = 100f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _playerInput.SwitchCurrentActionMap("Player");
        var allRenderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);

        foreach (Renderer r in allRenderers)
        {
            // Only add the script if it isn't already there
            if (!r.gameObject.GetComponent<Outline>())
            {
                var outline = r.gameObject.AddComponent<Outline>();

                // Explicitly use Unity's color to avoid conflicts
                outline.OutlineColor = UnityEngine.Color.white;
                outline.OutlineWidth = 5f;
            }
        }
    }

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
            else
            {
                _input.sprint = false;
                if (stamina < maxStamina)
                    stamina += staminaRegen * Time.deltaTime;
            }
            if (_playerInput.actions["Flashlight"].triggered) Flashlight.Instance.ToggleFlashlight();
            if (_playerInput.actions["Recharge"].triggered) Flashlight.Instance.Recharge();
            if (_playerInput.actions["Pause"].triggered) Journal.Instance.PauseGame();
            if (_playerInput.actions["DevCheats"].triggered) TeleportToExit();
        }
        //if (_playerInput.currentActionMap.name == "UI")
            //if (_playerInput.actions["Pause"].triggered) Journal.Instance.ResumeGame();

        // Misc updates
        UpdateSanity();
        UpdatePlayerUI();
        stamina = Mathf.Clamp(stamina, 0, maxStamina);
    }
    public void DisablePauseInput()
    {
        playerPauseAction?.Disable();
        uiPauseAction?.Disable();
    }
    public void EnablePauseInput()
    {
        playerPauseAction?.Enable();
        uiPauseAction?.Enable();
    }

    private void UpdatePlayerUI()
    {
        if (staminaSlider != null) staminaSlider.value = stamina;
        if (sanitySlider != null) sanitySlider.value = sanity;
    }

    private int _lightOverlapCount = 0;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == winZone) GameOver(true);
        if (((1 << other.gameObject.layer) & lightLayer) != 0)
            _lightOverlapCount++;
    }

    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & lightLayer) != 0)
            _lightOverlapCount--;
    }
    public void UpdateSanity()
    {
        if (drainRate == 0) drainRate = SetDrainRate();
        if (_lightOverlapCount > 0)
            sanity += 2.0f * drainRate * Time.deltaTime;
        else
            sanity -= 0.5f * drainRate * Time.deltaTime;

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

    public void GameOver(bool wonGame)
    {
        // logic for animations
        if (MonsterAILogic.Instance != null)
            MonsterAILogic.Instance.ShutdownAIModule();
        if (PopupManager.Instance != null)
            PopupManager.Instance.Pause();
        if (ScoreManager.Instance == null)
        {
            ScoreManager sceneManager = FindFirstObjectByType<ScoreManager>();
            if (sceneManager != null)
            {
                Debug.LogWarning("[Fix Pass] ScoreManager exists in scene, but its Instance variable was NULL. Force-healing reference now.");
                typeof(ScoreManager).GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy)?.SetValue(null, sceneManager);
            }
            else
            {
                Debug.LogError("[Fatal Error] ScoreManager script component is completely missing from the entire active scene structure!");
                UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                return;
            }
        }
        Debug.Log($"[Handshake Complete] Direct call sent to ScoreManager. Target State: {ScoreManager.Instance != null} | Won: {wonGame}");
        ScoreManager.Instance.EndGame(wonGame);
    }

    public void TeleportToExit()
    {
        this.transform.position = devCheat.transform.position;
    }
}

[System.Serializable]
public class Inventory
{
    private Dictionary<string, int> _stackableItems;
    private HashSet<string> _uniqueTapes;

    // Helper method to ensure the dictionary exists
    private void EnsureDictionary()
    {
        _stackableItems ??= new Dictionary<string, int>
            {
                { "Battery", 0 },
                { "Collectible", 0 }
            };
        _uniqueTapes ??= new();
    }

    /// <summary>
    /// Overload for unique items/tapes
    /// </summary>
    /// <param name="tapeID"></param>
    /// <param name="type"></param>
    public void AddItem(string tapeID, ItemType type)
    {
        EnsureDictionary();
        if (type == ItemType.VideoTape && !_uniqueTapes.Contains(tapeID))
        {
            _uniqueTapes.Add(tapeID);
            Debug.Log($"Inventory: Unique tape added: {tapeID}");

            Player.Instance.anim.SetTrigger("ToggleVideo");
            // Delay Pause by 2f
            // Activate UI
            // Find the number of tapes currently in possesion
            // Call video from list to play on UI
            // Once video is over (Probably a next button) close the menu
            // Play the animation to put video camera down
            // Resume game
        }
    }

    /// <summary>
    /// Overload for stackable items
    /// </summary>
    /// <param name="itemName"></param>
    /// <param name="amount"></param>
    public void AddItem(string itemName, int amount)
    {
        EnsureDictionary();

        if (_stackableItems.ContainsKey(itemName))
            _stackableItems[itemName] += amount;
        else
            _stackableItems[itemName] = amount;

        Debug.Log($"Inventory: Added {amount} {itemName}. Total: {_stackableItems[itemName]}");
    }

    public bool TryUseItem(string itemName)
    {
        EnsureDictionary();
        if (_stackableItems.ContainsKey(itemName) && _stackableItems[itemName] > 0)
        {
            _stackableItems[itemName]--;
            return true;
        }
        return false;
    }

    public int GetCount(string itemName)
    {
        EnsureDictionary();
        return _stackableItems.ContainsKey(itemName) ? _stackableItems[itemName] : 0;
    }

    public int TotalTapesCollected() => _uniqueTapes?.Count ?? 0;

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