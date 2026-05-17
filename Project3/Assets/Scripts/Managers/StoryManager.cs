using TMPro;
using UnityEngine;

public class StoryManager : MonoBehaviour
{
    public GameObject popupPanel;
    public TextMeshProUGUI popupText;
    public float popupDuration = 3f;

    [Header("Progression Checks")]
    public GameObject cameraCheck;
    public GameObject[] tapeChecks;

    public static StoryManager Instance { get; private set; }

    void Start()
    {
        if (Instance == null) Instance = this;
        cameraCheck.SetActive(true);
        foreach (var item in tapeChecks) item.SetActive(true);
        popupPanel.SetActive(false);
    }

    public void CameraCheck() => cameraCheck.SetActive(false);

    public void TapeCheck(int idx) => tapeChecks[idx].SetActive(false);
    

    public void ShowPopup(string message)
    {
        if (popupPanel != null && popupText != null)
        {
            CancelInvoke(nameof(HidePopup)); // Reset timer if a new popup appears
            popupText.text = message;
            popupPanel.SetActive(true);
            Invoke(nameof(HidePopup), popupDuration); // Hide after X seconds
        }
    }

    private void HidePopup()
    {
        if (popupPanel != null) popupPanel.SetActive(false);
    }

}
