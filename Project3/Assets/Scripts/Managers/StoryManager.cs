using TMPro;
using UnityEngine;

public class StoryManager : MonoBehaviour
{
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
        popupText.gameObject.SetActive(false);
    }

    public void CameraCheck() => cameraCheck.SetActive(false);

    public void TapeCheck(int idx) => tapeChecks[idx].SetActive(false);
    
    /// <summary>
    /// You can use this function to set a custom popup message for the player witha custom popup display duration or leave it at the default 3 seconds
    /// </summary>
    /// <param name="message"></param>
    /// <param name="customDuration"></param>
    public void ShowPopup(string message, float? customDuration = null)
    {
        if (popupText != null && popupText != null)
        {
            CancelInvoke(nameof(HidePopup)); // Reset timer if a new popup appears
            popupText.text = message;
            popupText.gameObject.SetActive(true);
            float finalDuration = customDuration ?? popupDuration;
            Invoke(nameof(HidePopup), finalDuration); // Hide after X seconds
        }
    }

    private void HidePopup()
    {
        if (popupText != null) popupText.gameObject.SetActive(false);
    }

}
