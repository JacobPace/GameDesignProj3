using UnityEngine;
using System.Collections;

public class PopupManager : MonoBehaviour
{
    [Header("Layer Settings")]
    [SerializeField] private LayerMask targetLayer;

    [Header("Popup Messages")]
    public string[] customPopupMessages;
    private int prev = -1;
    private bool pausePopups = false;
    public static PopupManager Instance { get; private set; }

    private void Start()
    {
        if (Instance == null) Instance = this;
        pausePopups = false;
    }

    public void StartPlaying()
    {
        StartCoroutine(WaitAndCallRoutine());
    }

    private IEnumerator WaitAndCallRoutine()
    {
        float randomTime = Random.Range(60f, 90f);
        yield return new WaitForSeconds(randomTime);
        PlayPopup();
    }

    private void PlayPopup()
    {
        if (StoryManager.Instance != null && !pausePopups)
        {
            string text = ChoosePopup();
            StoryManager.Instance.ShowPopup(text, 1.5f + (text.Length * 0.1f));
        }
    }

    private string ChoosePopup()
    {
        int idx = prev;
        while (idx == prev)
            idx = Random.Range(0, customPopupMessages.Length);
        prev = idx;
        return customPopupMessages[idx];
    }
    public void Pause() => pausePopups = true;
    public void Resume() => pausePopups = false;
}