using UnityEngine;
using TMPro;

public class Journal : MonoBehaviour
{
    [SerializeField] private GameObject[] menus;

    [Header("Inventory Text UI")]
    public TextMeshProUGUI batteryCountText;
    public TextMeshProUGUI collectibleCountText;
    public TextMeshProUGUI tapeCountText;
    public static Journal Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject); 
    }


    public void ToggleMenu(int menuIndex) => menus[menuIndex].SetActive(!menus[menuIndex].activeSelf);

    public void PauseGame()
    {
        Player.Instance.anim.SetTrigger("ToggleMenu");
        //Time.timeScale = 0;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        UpdateInventoryUI();
        Player.Instance._playerInput.SwitchCurrentActionMap("UI");
        Invoke(nameof(ShowMenu), 2f);
    }
    public void ResumeGame()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Player.Instance._playerInput.SwitchCurrentActionMap("Player");
        Player.Instance.anim.SetTrigger("ToggleMenu");
        menus[0].SetActive(false);
    }

    public void UpdateInventoryUI()
    {
        if (Player.Instance != null && Player.Instance.inventory != null)
        {
            batteryCountText.text = $"Batteries: {Player.Instance.inventory.GetCount("Battery")}";
            collectibleCountText.text = $"Collectibles: {Player.Instance.inventory.GetCount("Collectible")}";
            tapeCountText.text = $"Found Tapes: {Player.Instance.inventory.TotalTapesCollected()}";
        }
    }

    public void ShowMenu()
    {
        Time.timeScale = 0;
        menus[0].SetActive(true);
    }

    public void QuitGame() => Application.Quit();
}