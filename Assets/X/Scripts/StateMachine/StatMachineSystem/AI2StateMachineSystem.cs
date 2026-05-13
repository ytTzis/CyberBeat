using UnityEngine;

public class AI2StateMachineSystem : MonoBehaviour
{
    public AI2_NB_Transition transition;
    public AI2StateActionSO currentState;

    private void Awake()
    {
        transition?.InitTransition(this);
        currentState?.OnEnter();
    }

    private void Update()
    {
        transition?.TryGetApplyCondition();
        currentState?.OnUpdate();
    }
}
