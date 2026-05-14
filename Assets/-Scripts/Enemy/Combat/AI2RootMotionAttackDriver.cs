using System;
using UnityEngine;
using UGG.Move;

[DisallowMultipleComponent]
public class AI2RootMotionAttackDriver : MonoBehaviour
{
    [Serializable]
    private struct RootMotionAttackEvent
    {
        [Range(0f, 1f)] public float normalizedTime;
        public string hitName;
    }

    [Serializable]
    private struct RootMotionAttackState
    {
        public string stateName;

        // Legacy single-event fields kept for backward compatibility.
        [Range(0f, 1f)] public float attackEventNormalizedTime;
        public string attackEventHitName;

        public RootMotionAttackEvent[] attackEvents;
        public bool applyPosition;
        public bool applyRotation;
        [Range(0f, 1f)] public float positionMultiplier;

        public int StateNameHash => Animator.StringToHash(stateName);
    }

    [SerializeField] private RootMotionAttackState[] rootMotionAttackStates =
    {
        new RootMotionAttackState
        {
            stateName = "GSWhirlwind_Start",
            attackEventNormalizedTime = 0.72f,
            attackEventHitName = "Hit_H_Left",
            applyPosition = true,
            applyRotation = true,
            positionMultiplier = 0.7f
        },
        new RootMotionAttackState
        {
            stateName = "GSWhirlwind_Loop",
            attackEventNormalizedTime = 0.35f,
            attackEventHitName = "Hit_H_Right",
            applyPosition = true,
            applyRotation = true,
            positionMultiplier = 0.65f
        },
        new RootMotionAttackState
        {
            stateName = "GSWhirlwind_End",
            attackEventNormalizedTime = 1f,
            attackEventHitName = "",
            applyPosition = true,
            applyRotation = true,
            positionMultiplier = 0.6f
        },
        new RootMotionAttackState
        {
            stateName = "GS12",
            attackEventNormalizedTime = 0.24f,
            attackEventHitName = "Hit_H_Right",
            applyPosition = true,
            applyRotation = true,
            positionMultiplier = 0.85f
        }
    };

    private Animator animator;
    private CharacterController controller;
    private AICombatSystem combatSystem;
    private AudioSource audioSource;
    private Quaternion pendingRotation = Quaternion.identity;
    private int activeStateIndex = -1;
    private bool[] triggeredAttackEvents = Array.Empty<bool>();

    private void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponentInParent<CharacterController>();
        combatSystem = GetComponent<AICombatSystem>();
        CharacterMovementBase movementBase = GetComponentInParent<CharacterMovementBase>();
        audioSource = movementBase != null
            ? movementBase.GetComponentInChildren<AudioSource>()
            : GetComponentInParent<AudioSource>();

        if (audioSource == null && transform.root != null)
        {
            audioSource = transform.root.GetComponentInChildren<AudioSource>(true);
        }

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
        if (matchedStateIndex < 0 && animator.IsInTransition(0))
        {
            matchedStateIndex = GetMatchingStateIndex(animator.GetNextAnimatorStateInfo(0));
        }

        if (matchedStateIndex != activeStateIndex)
        {
            activeStateIndex = matchedStateIndex;
            ResetTriggeredAttackEvents();

            if (activeStateIndex >= 0 && GetAttackEventCount(rootMotionAttackStates[activeStateIndex]) > 0)
            {
                PlayGreatSwordSwingSound();
            }
        }

        if (activeStateIndex < 0 || combatSystem == null)
        {
            return;
        }

        RootMotionAttackState stateConfig = rootMotionAttackStates[activeStateIndex];
        for (int i = 0; i < GetAttackEventCount(stateConfig); i++)
        {
            if (triggeredAttackEvents[i])
            {
                continue;
            }

            string hitName = GetAttackEventHitName(stateConfig, i);
            if (string.IsNullOrEmpty(hitName))
            {
                triggeredAttackEvents[i] = true;
                continue;
            }

            if (stateInfo.normalizedTime >= GetAttackEventNormalizedTime(stateConfig, i))
            {
                PlayGreatSwordSwingSound();
                if (stateConfig.stateName.StartsWith("GSWhirlwind", StringComparison.Ordinal))
                {
                    combatSystem.TriggerWhirlwindAttackEvent(hitName);
                }
                else
                {
                    combatSystem.TriggerAnimationAttackEvent(hitName);
                }
                triggeredAttackEvents[i] = true;
            }
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
        if (matchedStateIndex < 0 && animator.IsInTransition(0))
        {
            matchedStateIndex = GetMatchingStateIndex(animator.GetNextAnimatorStateInfo(0));
        }
        if (matchedStateIndex < 0)
        {
            pendingRotation = Quaternion.identity;
            return;
        }

        RootMotionAttackState stateConfig = rootMotionAttackStates[matchedStateIndex];

        if (stateConfig.applyPosition)
        {
            float positionMultiplier = stateConfig.positionMultiplier <= 0f ? 1f : stateConfig.positionMultiplier;
            Vector3 deltaPosition = animator.deltaPosition * positionMultiplier;
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
            RootMotionAttackState stateConfig = rootMotionAttackStates[i];
            if (stateInfo.shortNameHash == stateConfig.StateNameHash ||
                stateInfo.fullPathHash == stateConfig.StateNameHash ||
                stateInfo.IsName(stateConfig.stateName))
            {
                return i;
            }
        }

        return -1;
    }

    private void ResetTriggeredAttackEvents()
    {
        if (activeStateIndex < 0)
        {
            triggeredAttackEvents = Array.Empty<bool>();
            return;
        }

        triggeredAttackEvents = new bool[GetAttackEventCount(rootMotionAttackStates[activeStateIndex])];
    }

    private static int GetAttackEventCount(RootMotionAttackState stateConfig)
    {
        if (stateConfig.attackEvents != null && stateConfig.attackEvents.Length > 0)
        {
            return stateConfig.attackEvents.Length;
        }

        return string.IsNullOrEmpty(stateConfig.attackEventHitName) ? 0 : 1;
    }

    private static float GetAttackEventNormalizedTime(RootMotionAttackState stateConfig, int eventIndex)
    {
        if (stateConfig.attackEvents != null && stateConfig.attackEvents.Length > 0)
        {
            return stateConfig.attackEvents[eventIndex].normalizedTime;
        }

        return stateConfig.attackEventNormalizedTime;
    }

    private static string GetAttackEventHitName(RootMotionAttackState stateConfig, int eventIndex)
    {
        if (stateConfig.attackEvents != null && stateConfig.attackEvents.Length > 0)
        {
            return stateConfig.attackEvents[eventIndex].hitName;
        }

        return stateConfig.attackEventHitName;
    }

    private void PlayGreatSwordSwingSound()
    {
        if (audioSource == null || GameAssets.Instance == null)
        {
            return;
        }

        GameAssets.Instance.PlaySoundEffectOneShot(audioSource, SoundAssetsType.hSwordWave);
    }
}
