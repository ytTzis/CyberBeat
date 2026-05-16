using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AICombat", menuName = "StateMachine/State/AICombat")]
public class AICombat : StateActionSO
{
    private int randomHorizontal;

    private float maxCombatDirection = 1.5f;

    [SerializeField] private CombatSkillBase currentSkill;

    public override void OnEnter()
    {

    }

    public override void OnUpdate()
    {
        AICombatAction();
    }

    public override void OnExit()
    {

    }

    private void AICombatAction()
    {
        if(currentSkill == null)
        {
            //如果当前没技能 就执行AI移动函数
            NoCombatMove();
            GetSkill();
        }
        else
        {
            currentSkill.InvokeSkill();

            if (!currentSkill.GetSkillIsDone())
            {
                currentSkill = null;
            }
        }
    }

    private void GetSkill()
    {
        if(currentSkill == null)
        {
            currentSkill = _combatSystem.GetNextDoneSkill();
        }
    }

    private void NoCombatMove()
    {
        //如果动画处于Motion状态
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
                        _animator.Play("Attack_0", 0, 0f);

                        randomHorizontal = GetRandomHorizontal();
                    }
                }
            }
            else if (_combatSystem.GetCurrentTargetDistance() > retreatDistance && _combatSystem.GetCurrentTargetDistance() < 6.1 + 0.5f)
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
                        default:
                            break;
                    }
                }

                _movement.CharacterMoveInterface(_movement.transform.right * ((randomHorizontal == 0) ? 1 : randomHorizontal), 1.4f, true);

                _animator.SetFloat(verticalID, 0f, 0.25f, Time.deltaTime);
                _animator.SetFloat(horizontalID, ((randomHorizontal == 0) ? 1 : randomHorizontal), 0.25f, Time.deltaTime);
            }
            else if (_combatSystem.GetCurrentTargetDistance() > 6.1 + 0.5f)
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

    private void SetAnimationValue(float movement,float horizontal,float vertical,float run)
    {
        _animator.SetFloat(movementID, movement, 0.15f, Time.deltaTime);
        _animator.SetFloat(horizontalID, horizontal, 0.15f, Time.deltaTime);
        _animator.SetFloat(verticalID, vertical, 0.15f, Time.deltaTime);
        _animator.SetFloat(runID, run, 0.15f, Time.deltaTime);
    }

    private int GetRandomHorizontal() => Random.Range(-1, 2);
}
