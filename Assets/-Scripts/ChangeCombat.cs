using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UGG.Combat;

public class ChangeCombat : StateMachineBehaviour
{
    private AICombatSystem _aiCombatSystem;

    [SerializeField] private float detectionTime;
    [SerializeField] private bool canChangeCombat;
    [SerializeField] private bool allowReleaseChangeCombat;
    [SerializeField] private string changeCombatName;
    [SerializeField, Min(0f)] private float changeCombatDistance = 2.9f;
    [SerializeField, Range(0f, 1f)] private float pressureChainChance = 0.8f;
    [SerializeField, Range(0f, 1f)] private float minimumNormalizedTimeToChain = 0.08f;

    private bool hasTriedChangeCombat;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (_aiCombatSystem == null)
        {
            _aiCombatSystem = animator.GetComponent<AICombatSystem>();
        }

        canChangeCombat = true;
        allowReleaseChangeCombat = false;
        hasTriedChangeCombat = false;
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        canChangeCombat = false;
        allowReleaseChangeCombat = false;
        hasTriedChangeCombat = false;
    }

    override public void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        CheckChangeCombatTime(animator);
        ChangeCombatAction(animator);
    }

    private void CheckChangeCombatTime(Animator animator)
    {
        if (_aiCombatSystem == null) return;
        if (_aiCombatSystem.GetCurrentTarget() == null) return;

        if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < detectionTime)
        {
            canChangeCombat = true;
        }
        else if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime > detectionTime)
        {
            canChangeCombat = false;
        }
    }

    private void ChangeCombatAction(Animator animator)
    {
        if (_aiCombatSystem == null) return;
        if (_aiCombatSystem.GetCurrentTarget() == null) return;
        if (hasTriedChangeCombat) return;

        if (canChangeCombat)
        {
            float normalizedTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            if (normalizedTime < minimumNormalizedTimeToChain)
            {
                return;
            }

            if (_aiCombatSystem.GetCurrentTargetDistance() < changeCombatDistance && Random.value <= pressureChainChance)
            {
                hasTriedChangeCombat = true;
                animator.CrossFade(changeCombatName, 0f, 0, 0f);
            }
        }

        if (!canChangeCombat && allowReleaseChangeCombat)
        {
            // Reserved for delayed chain logic if needed later.
        }
    }
}