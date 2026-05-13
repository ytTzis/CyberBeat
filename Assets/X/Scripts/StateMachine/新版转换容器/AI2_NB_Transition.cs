using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AI2_NB_Transition", menuName = "StateMachine/Transition/AI2_NB_Transition")]
public class AI2_NB_Transition : ScriptableObject
{
    [Serializable]
    private class StateActionConfig
    {
        public AI2StateActionSO fromState;
        public AI2StateActionSO toState;
        public List<AI2ConditionSO> conditions;

        public void Init(AI2StateMachineSystem stateMachineSystem)
        {
            fromState.InitState(stateMachineSystem);
            toState.InitState(stateMachineSystem);

            foreach (AI2ConditionSO item in conditions)
            {
                item.InitCondition(stateMachineSystem);
            }
        }
    }

    [SerializeField] private List<StateActionConfig> configStateData = new List<StateActionConfig>();

    private readonly Dictionary<AI2StateActionSO, List<StateActionConfig>> states = new Dictionary<AI2StateActionSO, List<StateActionConfig>>();
    private AI2StateMachineSystem stateMachineSystem;

    public void InitTransition(AI2StateMachineSystem stateMachineSystem)
    {
        this.stateMachineSystem = stateMachineSystem;
        states.Clear();
        SaveAllStateTransitionInfo();
    }

    private void SaveAllStateTransitionInfo()
    {
        foreach (StateActionConfig item in configStateData)
        {
            item.Init(stateMachineSystem);

            if (!states.ContainsKey(item.fromState))
            {
                states.Add(item.fromState, new List<StateActionConfig>());
            }

            states[item.fromState].Add(item);
        }
    }

    public void TryGetApplyCondition()
    {
        int conditionPriority = 0;
        int statePriority = 0;
        List<AI2StateActionSO> toStates = new List<AI2StateActionSO>();
        AI2StateActionSO toState = null;

        if (!states.ContainsKey(stateMachineSystem.currentState))
        {
            return;
        }

        foreach (StateActionConfig stateItem in states[stateMachineSystem.currentState])
        {
            foreach (AI2ConditionSO conditionItem in stateItem.conditions)
            {
                if (!conditionItem.ConditionSetUp())
                {
                    continue;
                }

                if (conditionItem.GetConditionPriority() >= conditionPriority)
                {
                    conditionPriority = conditionItem.GetConditionPriority();
                    toStates.Add(stateItem.toState);
                }
            }
        }

        foreach (AI2StateActionSO item in toStates)
        {
            if (item.GetStatePriority() >= statePriority)
            {
                statePriority = item.GetStatePriority();
                toState = item;
            }
        }

        if (toState == null)
        {
            return;
        }

        stateMachineSystem.currentState.OnExit();
        stateMachineSystem.currentState = toState;
        stateMachineSystem.currentState.OnEnter();
    }
}
