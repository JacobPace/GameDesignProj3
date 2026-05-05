using UnityEngine;

public class StationManager : MonoBehaviour, IInteractable
{
    [System.Serializable]
    public struct LootRange
    {
        public int min;
        public int max;
    }

    [Header("Loot per Difficulty")]
    public LootRange easyRange = new() { min = 3, max = 6 };
    public LootRange normalRange = new() { min = 1, max = 3 };
    public LootRange hardRange = new() { min = 0, max = 2 };

    private bool _hasBeenUsed = false;

    public void Interact()
    {
        if (_hasBeenUsed)
        {
            Debug.Log("Station is empty!");
            return;
        }

        int finalAmount = CalculateRandomLoot();
        Player.Instance.inventory.AddItem("Battery", finalAmount);
        _hasBeenUsed = true;
        Debug.Log($"Station gave {finalAmount} items on {GameManager.Instance.currentDifficulty}");
        // Logic to change model or color can be done here
    }

    private int CalculateRandomLoot()
    {
        var chosenRange = GameManager.Instance.currentDifficulty switch
        {
            GameManager.Difficulty.Easy => easyRange,
            GameManager.Difficulty.Hard => hardRange,
            GameManager.Difficulty.Normal => normalRange,
            _ => normalRange,
        };
        return Random.Range(chosenRange.min, chosenRange.max + 1);
    }

}