using UnityEngine;

public class MonstourPickup : MonoBehaviour
{
    [SerializeField] private float pickupRadius = 2.2f;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool destroyOnPickup = true;

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

        if (BagInventoryUI.Instance == null)
        {
            return;
        }

        if (!BagInventoryUI.Instance.TryAddItem(null, "Monstour"))
        {
            return;
        }

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
}
