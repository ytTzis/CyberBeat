using System.Collections;
using System.Collections.Generic;
using UGG.Move;
using UnityEngine;

[CreateAssetMenu(fileName = "NormalSkill", menuName = "Skill/NormalSkill")]
public class NormalSkill : CombatSkillBase
{
    [SerializeField] private float aggressiveApproachSpeed = 2.15f;
    [SerializeField, Min(0f)] private float attackCommitRangeBuffer = 0.2f;

    public override void InvokeSkill()
    {
        if (animator.CheckAnimationTag("Motion") && skillIsDone)
        {
            if (combat.GetCurrentTargetDistance() > skillUseDistance + attackCommitRangeBuffer)
            {
                float approachSpeed = Mathf.Max(aggressiveApproachSpeed, combat.GetPressureApproachSpeed());
                movement.CharacterMoveInterface(combat.GetDirectionForTarget(), approachSpeed, true);

                animator.SetFloat(verticalID, 1f, 0.25f, Time.deltaTime);
                animator.SetFloat(horizontalID, 0f, 0.25f, Time.deltaTime);
            }
            else
            {
                UseSkill();
            }
        }
    }
}