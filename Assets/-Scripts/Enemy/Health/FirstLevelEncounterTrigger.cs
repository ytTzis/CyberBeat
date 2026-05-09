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
            return;
        }

        Debug.Log($"[FirstLevelEncounterTrigger] {name} entered by {other.name}", this);

        if (!encounterController.IsPlayerCollider(other))
        {
            Debug.Log($"[FirstLevelEncounterTrigger] {name} ignored collider {other.name} because it is not recognized as Player.", this);
            return;
        }

        encounterController.NotifyAreaTriggered(triggerStage);
    }
}
