using UnityEngine;
using UnityEngine.InputSystem;

public interface IInteractable { public void Interact(); }

public class Interactor : MonoBehaviour
{
    public Transform InteractorSource;
    public float InteractRange;
    public LayerMask interactableLayer;

    [Header("UI Reference")]
    public GameObject interactionPrompt;

    private Outline _lastOutline; // Store reference to current outline

    void Update()
    {
        // Only run if the action map is "Player"
        if (Player.Instance._playerInput.currentActionMap.name == "Player")
        {
            Ray ray = new(InteractorSource.position, InteractorSource.forward);

            if (Physics.Raycast(ray, out RaycastHit hitInfo, InteractRange, interactableLayer))
            {
                StationManager station = hitInfo.collider.GetComponentInParent<StationManager>();
                Outline currentOutline = hitInfo.collider.GetComponentInParent<Outline>();

                // Hide outline if the interactable has already been used
                bool isDisabled = (station != null && station._hasBeenUsed);

                // Manage Outline Visibility
                if (currentOutline != _lastOutline || (isDisabled && _lastOutline != null))
                {
                    ClearInteraction();

                    if (currentOutline != null && !isDisabled)
                    {
                        currentOutline.enabled = true;
                        _lastOutline = currentOutline;
                        if (interactionPrompt != null) interactionPrompt.SetActive(true);
                    }
                    
                }

                if (Player.Instance._playerInput.actions["Interact"].WasPressedThisFrame())
                {
                    IInteractable interactObject = hitInfo.collider.GetComponentInParent<IInteractable>();

                    if (interactObject != null && !isDisabled) interactObject.Interact();
                }
            }
            else
            {
                ClearInteraction();
            }
        }
    }

    private void ClearInteraction()
    {
        if (_lastOutline != null)
        {
            _lastOutline.enabled = false;
            _lastOutline = null;
        }
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
    }
}
