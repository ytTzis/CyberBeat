using UnityEngine;
using System;

public class MonstourPickup : MonoBehaviour
{
    public static event Action<string> ItemPickedUp;

    [SerializeField] private float pickupRadius = 2.2f;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool destroyOnPickup = true;
    [SerializeField] private int fallbackInventoryCapacity = 5;
    [SerializeField] private bool enablePickupDebugLogs = true;

    private Transform playerTarget;
    private bool hasBeenPickedUp;

    private void Awake()
    {
        playerTarget = FindPlayerTarget();
    }

    private void Update()
    {
        if (hasBeenPickedUp)
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

        float sqrDistance = (playerTarget.position - transform.position).sqrMagnitude;
        if (sqrDistance > pickupRadius * pickupRadius)
        {
            return;
        }

        string itemId = ResolveItemId();
        InventoryManager inventoryManager = InventoryManager.EnsureInstance();
        if (inventoryManager == null)
        {
            DebugPickup("Pickup failed because InventoryManager could not be created.");
            return;
        }

        int maxItemCount = BagInventoryUI.Instance != null
            ? BagInventoryUI.Instance.SlotCapacity
            : fallbackInventoryCapacity;

        if (!inventoryManager.TryAddItem(itemId, maxItemCount))
        {
            DebugPickup($"Pickup failed for '{itemId}'. Inventory may be full.");
            return;
        }

        if (BagInventoryUI.Instance != null)
        {
            BagInventoryUI.Instance.RefreshFromInventory();
        }

        DebugPickup($"Picked up '{itemId}'.");
        ItemPickedUp?.Invoke(itemId);
        hasBeenPickedUp = true;

        if (destroyOnPickup)
        {
            Destroy(gameObject);
            return;
        }

        gameObject.SetActive(false);
    }

    private Transform FindPlayerTarget()
    {
        CharacterInputSystem inputSystem = FindFirstObjectByType<CharacterInputSystem>();
        if (inputSystem != null)
        {
            return inputSystem.transform;
        }

        if (!string.IsNullOrEmpty(playerTag))
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag(playerTag);
            if (taggedPlayer != null)
            {
                return taggedPlayer.transform;
            }
        }

        GameObject namedPlayer = GameObject.Find("Player (1)");
        if (namedPlayer != null)
        {
            return namedPlayer.transform;
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }

    private string ResolveItemId()
    {
        if (name.StartsWith("MonstourRed"))
        {
            return "MonstourRed";
        }

        if (name.StartsWith("MonstourBlue"))
        {
            return "MonstourBlue";
        }

        return "Monstour";
    }

    private void DebugPickup(string message)
    {
        if (!enablePickupDebugLogs)
        {
            return;
        }

        Debug.LogWarning($"[MonstourPickup] {message}", this);
    }
}
