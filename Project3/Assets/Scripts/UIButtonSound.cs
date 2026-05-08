using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Audio Clips")]
    public AudioClip clickSound;
    public AudioClip hoverSound;

    [Header("Settings")]
    public float volume = 1f;
    public bool playHoverOnce = true;

    private Button button;
    private bool hasHovered = false;

    void Start()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(PlayClickSound);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (playHoverOnce && hasHovered) return;

        PlayHoverSound();
        hasHovered = true;
    }

    void PlayClickSound()
    {
        SoundFXManager.instance.PlaySoundFXClip(clickSound, transform, volume);
    }

    void PlayHoverSound()
    {
        SoundFXManager.instance.PlaySoundFXClip(hoverSound, transform, volume);
    }

    void OnDisable()
    {
        hasHovered = false; // Reset when button is disabled (optional)
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hasHovered = false;
    }
}