using UGG.Health;
using UGG.Move;
using UnityEngine;

public abstract class AI2ConditionSO : ScriptableObject
{
    protected AI2CombatSystem _combatSystem;
    protected AI2Movement _movement;
    protected AI2HealthSystem _healthSystem;
    protected Animator animator;

    [SerializeField] protected int priority;

    public void InitCondition(AI2StateMachineSystem stateSystem)
    {
        _combatSystem = stateSystem.GetComponentInChildren<AI2CombatSystem>();
        _movement = stateSystem.GetComponent<AI2Movement>();
        _healthSystem = stateSystem.GetComponent<AI2HealthSystem>();
        animator = stateSystem.GetComponentInChildren<Animator>();
    }

    public abstract bool ConditionSetUp();

    public int GetConditionPriority() => priority;
}
