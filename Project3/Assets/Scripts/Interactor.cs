using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;


public interface IInteractable { public string InteractionPrompt { get; } public void Interact(); }

public class Interactor : MonoBehaviour
{
    public Transform InteractorSource;
    public float InteractRange;
    public LayerMask interactableLayer;

    [Header("UI Reference")]
    public GameObject interactionPrompt;

    private Outline _lastOutline;

    void Update()
    {
        if (Player.Instance._playerInput.currentActionMap.name != "Player") return;

        Ray ray = new(InteractorSource.position, InteractorSource.forward);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, InteractRange, interactableLayer))
        {
            IInteractable interactObject = hitInfo.collider.GetComponentInParent<IInteractable>();
            if (!hitInfo.collider.TryGetComponent(out Outline currentOutline)) currentOutline = hitInfo.collider.GetComponentInParent<Outline>();
            hitInfo.collider.TryGetComponent(out StationManager station);

            bool isDisabled = (station != null && station._hasBeenUsed);

            if (currentOutline != null && !isDisabled)
            {
                if (_lastOutline != currentOutline)
                {
                    ClearInteraction();
                    currentOutline.enabled = true;
                    _lastOutline = currentOutline;
                    if (interactionPrompt != null)
                    {
                        var promptTMP = interactionPrompt.GetComponentInChildren<TextMeshProUGUI>();
                        if (promptTMP != null) promptTMP.text = interactObject.InteractionPrompt;
                        interactionPrompt.SetActive(true);
                    }
                }
            }
            else ClearInteraction();
            if (Player.Instance._playerInput.actions["Interact"].WasPressedThisFrame()) if (interactObject != null && !isDisabled) interactObject.Interact();
        }
        else ClearInteraction();
    }

    private void ClearInteraction()
    {
        if (_lastOutline != null) _lastOutline.enabled = false;
        _lastOutline = null;
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
    }
}