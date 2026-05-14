using UnityEngine;

public class AI2CombatSystem : AICombatSystem
{
    [SerializeField] private string fallbackCloseRangeAttackAnimation = "Attack03_4";

    public string GetFallbackCloseRangeAttackAnimation()
    {
        return fallbackCloseRangeAttackAnimation;
    }

    public void TriggerAnimationAttackEvent(string hitName)
    {
        OnAnimationAttackEvent(hitName);
    }
}
