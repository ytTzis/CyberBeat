using UnityEngine;
using UGG.Health;

[DisallowMultipleComponent]
public class Scene2ExitSceneTrigger : MonoBehaviour
{
    [SerializeField] private Scene2RevealEnemyAndFocus exitController;
    [SerializeField] private string playerTag = "Player";

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (exitController == null)
        {
            exitController = FindFirstObjectByType<Scene2RevealEnemyAndFocus>();
        }

        if (exitController == null || !exitController.CanTriggerExitSceneTransition())
        {
            return;
        }

        if (!IsPlayerCollider(other))
        {
            return;
        }

        Debug.Log($"[Scene2ExitSceneTrigger] Player entered exit trigger on '{name}'.", this);
        exitController.TriggerExitSceneTransition();
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag))
        {
            return true;
        }

        Transform root = other.transform.root;
        if (root != null && !string.IsNullOrEmpty(playerTag) && root.CompareTag(playerTag))
        {
            return true;
        }

        if (other.GetComponentInParent<CharacterInputSystem>() != null)
        {
            return true;
        }

        return other.GetComponentInParent<PlayerHealthSystem>() != null;
    }
}
