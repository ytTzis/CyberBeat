using UnityEngine;
using UnityEngine.UI;

public class BagInventoryUI : MonoBehaviour
{
    [System.Serializable]
    private class ItemIconEntry
    {
        public string itemNamePrefix;
        public Sprite icon;
    }

    public static BagInventoryUI Instance { get; private set; }

    [SerializeField] private string slotNamePrefix = "Slot";
    [SerializeField] private int slotCount = 5;
    [SerializeField] private Color emptySlotColor = new Color(0.2901961f, 0.2901961f, 0.2901961f, 0.9f);
    [SerializeField] private Color filledSlotColor = new Color(0.8509804f, 0.7607843f, 0.2901961f, 0.95f);
    [SerializeField] private Color selectedEmptySlotColor = new Color(0.5529412f, 0.5529412f, 0.5529412f, 0.98f);
    [SerializeField] private Color selectedFilledSlotColor = new Color(1f, 0.8980392f, 0.38039216f, 1f);
    [SerializeField] private float pickupRadius = 2.2f;
    [SerializeField] private float scrollThreshold = 0.05f;
    [SerializeField] private Vector3 selectedSlotScale = new Vector3(1.12f, 1.12f, 1f);
    [SerializeField] private Vector3 normalSlotScale = Vector3.one;
    [SerializeField] private ItemIconEntry[] itemIcons;

    private Image[] slotImages;
    private RectTransform[] slotRects;
    private int collectedCount;
    private int selectedSlotIndex;
    private Transform playerTarget;
    private Sprite[] collectedIcons;
    private string[] collectedItemIds;
    private InventoryManager inventoryManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        inventoryManager = InventoryManager.EnsureInstance();
        CacheSlots();
        collectedIcons = new Sprite[slotCount];
        collectedItemIds = new string[slotCount];
        RebuildInventoryFromManager();
        playerTarget = FindPlayerTarget();
    }

    private void Update()
    {
        HandleScrollSelection();
        HandleUseSelectedItem();

        if (collectedCount >= slotImages.Length)
        {
            return;
        }

        if (playerTarget == null)
        {
            playerTarget = FindPlayerTarget();
            if (playerTarget == null)
            {
                return;
            }
        }

        TryCollectNearbyMonstour();
    }

    public bool TryAddItem(Sprite icon, string itemId)
    {
        if (inventoryManager == null)
        {
            inventoryManager = InventoryManager.EnsureInstance();
        }

        if (inventoryManager == null || !inventoryManager.TryAddItem(itemId, slotImages.Length))
        {
            return false;
        }

        RebuildInventoryFromManager();
        return true;
    }

    private void RebuildInventoryFromManager()
    {
        if (collectedIcons == null || collectedItemIds == null)
        {
            return;
        }

        System.Array.Clear(collectedIcons, 0, collectedIcons.Length);
        System.Array.Clear(collectedItemIds, 0, collectedItemIds.Length);

        if (inventoryManager == null)
        {
            collectedCount = 0;
            selectedSlotIndex = 0;
            RefreshSlots();
            return;
        }

        collectedCount = Mathf.Min(inventoryManager.ItemCount, collectedItemIds.Length);
        selectedSlotIndex = Mathf.Clamp(inventoryManager.SelectedSlotIndex, 0, Mathf.Max(0, slotCount - 1));

        for (int i = 0; i < collectedCount; i++)
        {
            string itemId = inventoryManager.ItemIds[i];
            collectedItemIds[i] = itemId;
            ItemIconEntry entry = FindItemIconEntry(itemId);
            collectedIcons[i] = entry != null ? entry.icon : null;
        }

        RefreshSlots();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool ContainsItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        for (int i = 0; i < collectedCount; i++)
        {
            if (collectedItemIds[i] == itemId)
            {
                return true;
            }
        }

        return false;
    }

    private void CacheSlots()
    {
        slotImages = new Image[slotCount];
        slotRects = new RectTransform[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            Transform slot = transform.Find($"{slotNamePrefix}{i + 1}");
            if (slot == null)
            {
                continue;
            }

            slotImages[i] = slot.GetComponent<Image>();
            slotRects[i] = slot.GetComponent<RectTransform>();
        }
    }

    private void RefreshSlots()
    {
        if (slotImages == null)
        {
            return;
        }

        for (int i = 0; i < slotImages.Length; i++)
        {
            if (slotImages[i] == null)
            {
                continue;
            }

            bool isFilled = i < collectedCount;
            Sprite icon = isFilled ? collectedIcons[i] : null;
            bool isSelected = i == selectedSlotIndex;

            slotImages[i].sprite = icon;
            slotImages[i].preserveAspect = true;

            if (icon != null)
            {
                slotImages[i].color = isSelected ? selectedFilledSlotColor : Color.white;
            }
            else
            {
                slotImages[i].color = isSelected
                    ? selectedEmptySlotColor
                    : (isFilled ? filledSlotColor : emptySlotColor);
            }

            if (slotRects != null && i < slotRects.Length && slotRects[i] != null)
            {
                slotRects[i].localScale = isSelected ? selectedSlotScale : normalSlotScale;
            }
        }
    }

    private void HandleScrollSelection()
    {
        if (slotCount <= 0)
        {
            return;
        }

        float scrollDelta = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scrollDelta) < scrollThreshold)
        {
            return;
        }

        int direction = scrollDelta > 0f ? -1 : 1;
        selectedSlotIndex = (selectedSlotIndex + direction + slotCount) % slotCount;
        if (inventoryManager != null)
        {
            inventoryManager.SetSelectedSlotIndex(selectedSlotIndex, slotCount);
        }
        RefreshSlots();
    }

    private void TryCollectNearbyMonstour()
    {
        Transform[] sceneTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        float maxSqrDistance = pickupRadius * pickupRadius;

        for (int i = 0; i < sceneTransforms.Length; i++)
        {
            Transform candidate = sceneTransforms[i];
            if (candidate == null)
            {
                continue;
            }

            ItemIconEntry matchedEntry = FindItemIconEntry(candidate.name);
            if (matchedEntry == null)
            {
                continue;
            }

            if (candidate.parent != null && IsTrackedItem(candidate.parent.name))
            {
                continue;
            }

            float sqrDistance = (playerTarget.position - candidate.position).sqrMagnitude;
            if (sqrDistance > maxSqrDistance)
            {
                continue;
            }

            if (!TryAddItem(matchedEntry.icon, matchedEntry.itemNamePrefix))
            {
                return;
            }

            Destroy(candidate.gameObject);
            return;
        }
    }

    private void HandleUseSelectedItem()
    {
        if (!Input.GetKeyDown(KeyCode.E))
        {
            return;
        }

        if (selectedSlotIndex < 0 || selectedSlotIndex >= collectedCount)
        {
            return;
        }

        string itemId = collectedItemIds[selectedSlotIndex];
        UseItemEffect(itemId);
        RemoveItemAt(selectedSlotIndex);
    }

    private void RemoveItemAt(int index)
    {
        if (inventoryManager == null)
        {
            return;
        }

        if (!inventoryManager.RemoveItemAt(index))
        {
            return;
        }

        RebuildInventoryFromManager();
    }

    // Placeholder for per-item behavior. Add your actual effects here later.
    private void UseItemEffect(string itemId)
    {
        switch (itemId)
        {
            case "MonstourRed":
                if (HeartRateStateController.Instance != null)
                {
                    HeartRateSimulator heartRateSimulator = HeartRateSimulator.Instance;
                    if (heartRateSimulator != null)
                    {
                        float targetHeartRate = HeartRateStateController.Instance.GetRecommendedHighStressHeartRate(2f);
                        float normalBoundaryHeartRate = HeartRateStateController.Instance.GetNormalStateBoundaryHeartRate();
                        heartRateSimulator.ForceHeartRateForDuration(targetHeartRate, 10f, true, normalBoundaryHeartRate);
                    }

                    HeartRateStateController.Instance.ForceStateForDuration(
                        HeartRateStateController.HeartRateState.HighStress,
                        10f);
                }
                break;
            case "MonstourBlue":
                if (HeartRateStateController.Instance != null)
                {
                    HeartRateStateController.Instance.ForceReturnToNormal();
                }
                break;
            case "Monstour":
                break;
            default:
                break;
        }
    }

    private ItemIconEntry FindItemIconEntry(string candidateName)
    {
        if (itemIcons == null)
        {
            return null;
        }

        for (int i = 0; i < itemIcons.Length; i++)
        {
            ItemIconEntry entry = itemIcons[i];
            if (entry == null || string.IsNullOrEmpty(entry.itemNamePrefix))
            {
                continue;
            }

            if (candidateName.StartsWith(entry.itemNamePrefix))
            {
                return entry;
            }
        }

        return null;
    }

    private bool IsTrackedItem(string candidateName)
    {
        return FindItemIconEntry(candidateName) != null;
    }

    private Transform FindPlayerTarget()
    {
        CharacterInputSystem inputSystem = FindFirstObjectByType<CharacterInputSystem>();
        if (inputSystem != null)
        {
            return inputSystem.transform;
        }

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            return taggedPlayer.transform;
        }

        GameObject namedPlayer = GameObject.Find("Player (1)");
        if (namedPlayer != null)
        {
            return namedPlayer.transform;
        }

        return null;
    }
}
