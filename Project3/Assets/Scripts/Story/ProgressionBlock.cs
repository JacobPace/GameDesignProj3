using UnityEngine;

public class ProgressionBlock : MonoBehaviour
{
    [Header("Layer Settings")]
    [SerializeField] private LayerMask targetLayer;

    [Header("Feedback")]
    [SerializeField]
    private string customPopupMessage = "";

    private void OnTriggerEnter(Collider collision)
    {
        if (((1 << collision.gameObject.layer) & targetLayer) != 0)
        {
            if (StoryManager.Instance != null)
            {
                StoryManager.Instance.ShowPopup(customPopupMessage);
            }
        }
    }
}
