using UnityEngine;
using UnityEngine.InputSystem;

public interface IInteractable { public void Interact(); }

public class Interactor : MonoBehaviour
{
    public Transform InteractorSource;
    public float InteractRange;
    public LayerMask interactableLayer;

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

                // Hide outline if it's a station that has already been used
                bool isDisabled = (station != null && station._hasBeenUsed);

                // 2. Manage Outline Visibility
                if (currentOutline != _lastOutline)
                {
                    ClearOutline(); // Turn off the old one

                    if (currentOutline != null && !isDisabled)
                    {
                        currentOutline.enabled = true;
                        _lastOutline = currentOutline;
                    }
                }

                if (Player.Instance._playerInput.actions["Interact"].WasPressedThisFrame())
                {
                    IInteractable interactObject = hitInfo.collider.GetComponentInParent<IInteractable>();

                    if (interactObject != null && !isDisabled)
                    {
                        if (Player.Instance != null && Player.Instance.inventory != null)
                        {
                            interactObject.Interact();
                            ClearOutline();
                        }
                        else
                        {
                            Debug.LogError("Interactor: Player or Inventory is missing in the scene!");
                        }
                    }
                }
            }
            else
            {
                ClearOutline();
            }
        }
    }

    private void ClearOutline()
    {
        if (_lastOutline != null)
        {
            _lastOutline.enabled = false;
            _lastOutline = null;
        }
    }
}
