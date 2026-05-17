using UnityEngine;

public class CameraPickup : MonoBehaviour
{
    private bool isQuitting = false;
    private void OnApplicationQuit() => isQuitting = true;
    
    private void OnDestroy()
    {
        if (!isQuitting && StoryManager.Instance != null)
            StoryManager.Instance.CameraCheck();
    }
}
