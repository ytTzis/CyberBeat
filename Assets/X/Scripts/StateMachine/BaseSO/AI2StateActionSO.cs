using UGG.Health;
using UGG.Move;
using UnityEngine;

public abstract class AI2StateActionSO : ScriptableObject
{
    protected AI2CombatSystem _combatSystem;
    protected AI2Movement _movement;
    protected AI2HealthSystem _healthSystem;
    protected Animator _animator;
    protected Transform self;

    [SerializeField, Header("State Priority")] protected int statePriority;

    protected int animationMoveID = Animator.StringToHash("AnimationMove");
    protected int movementID = Animator.StringToHash("Movement");
    protected int horizontalID = Animator.StringToHash("Horizontal");
    protected int verticalID = Animator.StringToHash("Vertical");
    protected int lAtkID = Animator.StringToHash("LAtk");
    protected int runID = Animator.StringToHash("Run");

    protected float walkSpeed = 1.5f;
    protected float runSpeed = 5f;
    [SerializeField] protected float currentMoveSpeed;

    public void InitState(AI2StateMachineSystem stateMachineSystem)
    {
        _combatSystem = stateMachineSystem.GetComponentInChildren<AI2CombatSystem>();
        _movement = stateMachineSystem.GetComponent<AI2Movement>();
        _healthSystem = stateMachineSystem.GetComponent<AI2HealthSystem>();
        _animator = stateMachineSystem.GetComponentInChildren<Animator>();
        self = stateMachineSystem.transform;
    }

    protected void SetHorizontalAnimation(float value)
    {
        _animator.SetFloat(horizontalID, value);
        currentMoveSpeed = 0.85f;
    }

    protected void SetVerticalAnimation(float value)
    {
        _animator.SetFloat(verticalID, value);
        currentMoveSpeed = 1.5f;
    }

    protected void ResetAnimation()
    {
        currentMoveSpeed = 0f;
        _animator.SetFloat(verticalID, 0f);
        _animator.SetFloat(horizontalID, 0f);
    }

    public virtual void OnEnter() { }

    public abstract void OnUpdate();

    public virtual void OnExit() { }

    public int GetStatePriority() => statePriority;
}
