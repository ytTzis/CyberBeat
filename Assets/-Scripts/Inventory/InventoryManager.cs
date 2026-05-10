using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    private readonly List<string> itemIds = new List<string>();
    private int selectedSlotIndex;

    public IReadOnlyList<string> ItemIds => itemIds;
    public int ItemCount => itemIds.Count;
    public int SelectedSlotIndex => selectedSlotIndex;

    public static InventoryManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        InventoryManager existingManager = FindFirstObjectByType<InventoryManager>();
        if (existingManager != null)
        {
            Instance = existingManager;
            DontDestroyOnLoad(existingManager.gameObject);
            return existingManager;
        }

        GameObject managerObject = new GameObject("InventoryManager");
        Instance = managerObject.AddComponent<InventoryManager>();
        DontDestroyOnLoad(managerObject);
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool TryAddItem(string itemId, int maxItemCount)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        if (maxItemCount > 0 && itemIds.Count >= maxItemCount)
        {
            return false;
        }

        itemIds.Add(itemId);
        return true;
    }

    public bool RemoveItemAt(int index)
    {
        if (index < 0 || index >= itemIds.Count)
        {
            return false;
        }

        itemIds.RemoveAt(index);
        selectedSlotIndex = Mathf.Clamp(selectedSlotIndex, 0, Mathf.Max(0, itemIds.Count - 1));
        return true;
    }

    public void SetSelectedSlotIndex(int index, int slotCount)
    {
        if (slotCount <= 0)
        {
            selectedSlotIndex = 0;
            return;
        }

        selectedSlotIndex = ((index % slotCount) + slotCount) % slotCount;
    }
}
