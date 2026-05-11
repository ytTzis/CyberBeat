using UGG.Health;
using UnityEngine;

[DisallowMultipleComponent]
public class SmokeChaserKillTrigger : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Transform attackerSource;
    [SerializeField] private bool triggerOnce = true;

    private bool hasTriggered;

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void Awake()
    {
        if (attackerSource == null)
        {
            attackerSource = transform.root;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered && triggerOnce)
        {
            return;
        }

        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag) && !other.transform.root.CompareTag(playerTag))
        {
            return;
        }

        PlayerHealthSystem playerHealth = other.GetComponent<PlayerHealthSystem>();
        if (playerHealth == null)
        {
            playerHealth = other.GetComponentInParent<PlayerHealthSystem>();
        }

        if (playerHealth == null || playerHealth.IsDead())
        {
            return;
        }

        hasTriggered = true;
        playerHealth.ForceDie(attackerSource);
    }
}
