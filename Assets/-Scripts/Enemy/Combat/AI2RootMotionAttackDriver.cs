using System;
using UnityEngine;

[DisallowMultipleComponent]
public class AI2RootMotionAttackDriver : MonoBehaviour
{
    [Serializable]
    private struct RootMotionAttackState
    {
        public string stateName;
        [Range(0f, 1f)] public float attackEventNormalizedTime;
        public string attackEventHitName;
        public bool applyPosition;
        public bool applyRotation;
    }

    [SerializeField] private RootMotionAttackState[] rootMotionAttackStates =
    {
        new RootMotionAttackState
        {
            stateName = "GSAttack08",
            attackEventNormalizedTime = 0.28f,
            attackEventHitName = "Hit_H_Left",
            applyPosition = true,
            applyRotation = true
        }
    };

    private Animator animator;
    private CharacterController controller;
    private AI2CombatSystem combatSystem;
    private Quaternion pendingRotation = Quaternion.identity;
    private int activeStateIndex = -1;
    private bool attackEventTriggered;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponentInParent<CharacterController>();
        combatSystem = GetComponent<AI2CombatSystem>();

        if (animator != null)
        {
            animator.applyRootMotion = true;
        }
    }

    private void Update()
    {
        if (animator == null)
        {
            return;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        int matchedStateIndex = GetMatchingStateIndex(stateInfo);

        if (matchedStateIndex != activeStateIndex)
        {
            activeStateIndex = matchedStateIndex;
            attackEventTriggered = false;
        }

        if (activeStateIndex < 0 || combatSystem == null)
        {
            return;
        }

        RootMotionAttackState stateConfig = rootMotionAttackStates[activeStateIndex];
        if (!attackEventTriggered && stateInfo.normalizedTime >= stateConfig.attackEventNormalizedTime)
        {
            combatSystem.TriggerAnimationAttackEvent(stateConfig.attackEventHitName);
            attackEventTriggered = true;
        }
    }

    private void OnAnimatorMove()
    {
        if (animator == null || controller == null)
        {
            return;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        int matchedStateIndex = GetMatchingStateIndex(stateInfo);
        if (matchedStateIndex < 0)
        {
            pendingRotation = Quaternion.identity;
            return;
        }

        RootMotionAttackState stateConfig = rootMotionAttackStates[matchedStateIndex];

        if (stateConfig.applyPosition)
        {
            Vector3 deltaPosition = animator.deltaPosition;
            deltaPosition.y = 0f;
            if (deltaPosition.sqrMagnitude > 0f)
            {
                controller.Move(deltaPosition);
            }
        }

        pendingRotation = stateConfig.applyRotation
            ? animator.deltaRotation
            : Quaternion.identity;
    }

    private void LateUpdate()
    {
        if (activeStateIndex < 0)
        {
            pendingRotation = Quaternion.identity;
            return;
        }

        if (rootMotionAttackStates[activeStateIndex].applyRotation)
        {
            transform.root.rotation = transform.root.rotation * pendingRotation;
        }

        pendingRotation = Quaternion.identity;
    }

    public bool IsHandlingRootMotionAttack()
    {
        return activeStateIndex >= 0;
    }

    private int GetMatchingStateIndex(AnimatorStateInfo stateInfo)
    {
        for (int i = 0; i < rootMotionAttackStates.Length; i++)
        {
            if (stateInfo.IsName(rootMotionAttackStates[i].stateName))
            {
                return i;
            }
        }

        return -1;
    }
}
