using UnityEngine;
using UnityEngine.InputSystem;

public interface IInteractable
{
    public void Interact();
}
public class Interactor : MonoBehaviour
{
    public Transform InteractorSource;
    public float InteractRange;
    public LayerMask interactableLayer;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Player.Instance._playerInput.currentActionMap.name == "Player")
        {
            Ray ray = new(InteractorSource.position, InteractorSource.forward);

            if (Physics.Raycast(ray, out RaycastHit hitInfo, InteractRange, interactableLayer))
            {
                if (Player.Instance._playerInput.actions["Interact"].WasPressedThisFrame())
                {
                    IInteractable interactObject = hitInfo.collider.GetComponentInParent<IInteractable>();
                    if (interactObject != null)
                    {
                        interactObject.Interact();
                    }
                    else
                    {
                        Debug.Log("No IInteractable found on: " + hitInfo.collider.gameObject.name);
                    }
                }
            }
        }
    }
}
