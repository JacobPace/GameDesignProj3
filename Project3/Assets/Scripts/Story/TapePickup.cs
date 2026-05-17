using UnityEngine;

public class TapePickup : MonoBehaviour
{
    [Header("Tape in index on StoryManager")]
    public int index;
    private bool isQuitting = false;
    private void OnApplicationQuit() => isQuitting = true;

    private void OnDestroy()
    {
        if (!isQuitting && StoryManager.Instance != null)
            StoryManager.Instance.TapeCheck(index);
    }
}
