using UnityEngine;

[CreateAssetMenu(fileName = "AI2Combat", menuName = "StateMachine/State/AI2Combat")]
public class AI2Combat : AI2StateActionSO
{
    private int randomHorizontal;
    private bool currentSkillEnteredAttackState;

    [SerializeField] private CombatSkillBase currentSkill;

    public override void OnUpdate()
    {
        AICombatAction();
    }

    private void AICombatAction()
    {
        if (currentSkill == null)
        {
            NoCombatMove();
            GetSkill();
        }
        else
        {
            currentSkill.InvokeSkill();

            if (_animator == null)
            {
                return;
            }

            if (_animator.CheckAnimationTag("Attack") || _animator.CheckAnimationTag("GSAttack"))
            {
                currentSkillEnteredAttackState = true;
                return;
            }

            // Animator.Play() often doesn't report the new tag until the next frame.
            // Only release the skill after we've definitely entered an attack state
            // and then returned to motion.
            if (currentSkillEnteredAttackState && _animator.CheckAnimationTag("Motion"))
            {
                currentSkill = null;
                currentSkillEnteredAttackState = false;
            }
        }
    }

    private void GetSkill()
    {
        if (currentSkill == null)
        {
            currentSkill = _combatSystem.GetNextDoneSkill();
            currentSkillEnteredAttackState = false;
        }
    }

    private void NoCombatMove()
    {
        if (_animator == null || _combatSystem == null || _movement == null)
        {
            return;
        }

        if (_animator.CheckAnimationTag("Motion"))
        {
            float retreatDistance = _combatSystem.GetCloseRetreatDistance();
            float fallbackAttackDistance = _combatSystem.GetFallbackCloseRangeAttackDistance();

            if (_combatSystem.GetCurrentTargetDistance() < retreatDistance)
            {
                _movement.CharacterMoveInterface(-_combatSystem.GetDirectionForTarget(), 1.4f, true);
                _animator.SetFloat(verticalID, -1f, 0.25f, Time.deltaTime);
                _animator.SetFloat(horizontalID, 0f, 0.25f, Time.deltaTime);

                randomHorizontal = GetRandomHorizontal();

                if (_combatSystem.GetCurrentTargetDistance() < fallbackAttackDistance)
                {
                    if (!_animator.CheckAnimationTag("Hit") && !_animator.CheckAnimationTag("Defen"))
                    {
                        string attackName = _combatSystem.GetFallbackCloseRangeAttackAnimation();
                        if (!string.IsNullOrEmpty(attackName))
                        {
                            _animator.Play(attackName, 0, 0f);
                        }

                        randomHorizontal = GetRandomHorizontal();
                    }
                }
            }
            else if (_combatSystem.GetCurrentTargetDistance() > retreatDistance && _combatSystem.GetCurrentTargetDistance() < 6.6f)
            {
                if (HorizontalDirectionHasObject(randomHorizontal))
                {
                    switch (randomHorizontal)
                    {
                        case 1:
                            randomHorizontal = -1;
                            break;
                        case -1:
                            randomHorizontal = 1;
                            break;
                    }
                }

                _movement.CharacterMoveInterface(_movement.transform.right * (randomHorizontal == 0 ? 1 : randomHorizontal), 1.4f, true);
                _animator.SetFloat(verticalID, 0f, 0.25f, Time.deltaTime);
                _animator.SetFloat(horizontalID, randomHorizontal == 0 ? 1 : randomHorizontal, 0.25f, Time.deltaTime);
            }
            else if (_combatSystem.GetCurrentTargetDistance() > 6.6f)
            {
                _movement.CharacterMoveInterface(_movement.transform.forward, 1.4f, true);
                _animator.SetFloat(verticalID, 1f, 0.25f, Time.deltaTime);
                _animator.SetFloat(horizontalID, 0f, 0.25f, Time.deltaTime);
            }
        }
        else
        {
            _animator.SetFloat(verticalID, 0f);
            _animator.SetFloat(horizontalID, 0f);
            _animator.SetFloat(runID, 0f);
        }
    }

    private bool HorizontalDirectionHasObject(int direction)
    {
        return Physics.Raycast(_movement.transform.position, _movement.transform.right * direction, 1.5f, 1 << 8);
    }

    private int GetRandomHorizontal() => Random.Range(-1, 2);
}
