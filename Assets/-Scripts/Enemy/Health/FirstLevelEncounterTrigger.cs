using UnityEngine;

public class FirstLevelEncounterTrigger : MonoBehaviour
{
    public enum TriggerStage
    {
        Area1,
        Area2
    }

    [SerializeField] private TriggerStage triggerStage;
    [SerializeField] private FirstLevelEncounterController encounterController;

    public void Initialize(FirstLevelEncounterController controller, TriggerStage stage)
    {
        encounterController = controller;
        triggerStage = stage;
    }

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
        if (encounterController == null)
        {
            Debug.LogWarning($"[FirstLevelEncounterTrigger] {name} has no encounter controller reference.", this);
            return;
        }

        if (!encounterController.IsPlayerCollider(other))
        {
            return;
        }

        Debug.Log($"[FirstLevelEncounterTrigger] {name} triggered by player.", this);
        encounterController.NotifyAreaTriggered(triggerStage);
    }
}
