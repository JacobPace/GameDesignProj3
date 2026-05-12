using Unity.Cinemachine;
using UnityEngine;

public enum ItemType { Collectible, VideoTape }
/// <summary>
/// This script can be attached to a collectible or a video tape
/// Set the type in the inspector accordingly
/// For video tapes make sure to set an 'optionalTag' identifier
/// escpecially if you want to make it have logic to play the video later
/// </summary>
[RequireComponent(typeof(Outline))]
public class Item : MonoBehaviour, IInteractable
{
    [SerializeField] private ItemType type;
    [SerializeField] private string optionalTag;
    [SerializeField] private string promptText = "Press 'E' to Pickup";
    public string InteractionPrompt => promptText;

    public string ItemTag => optionalTag;
    public ItemType Type => type;

    public void Interact()
    {
        Debug.Log($"Interacted with {gameObject.name}. Removing from scene.");
        if (type == ItemType.VideoTape) Player.Instance.inventory.AddItem(optionalTag, type);
        else if (type == ItemType.Collectible) Player.Instance.inventory.AddItem("Collectible", 1);
        Destroy(gameObject);
    }

    public bool HasTag(string tag) => !string.IsNullOrEmpty(optionalTag) && optionalTag == tag;
}