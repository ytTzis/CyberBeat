using UnityEngine;

[CreateAssetMenu(fileName = "AI2ToCombatCondition", menuName = "StateMachine/Condition/AI2ToCombatCondition")]
public class AI2ToCombatCondition : AI2ConditionSO
{
    public override bool ConditionSetUp()
    {
        return _combatSystem != null && _combatSystem.GetCurrentTarget() != null;
    }
}
