using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UGG.Combat;
using UGG.Health;
using UnityEngine.Rendering.PostProcessing;

public class AICombatSystem : CharacterCombatSystemBase
{
    [SerializeField, Header("Detection Center")] private Transform detectionCenter;
    [SerializeField, Header("Detection Range")] private float detectionRang;

    [SerializeField, Header("Target Layer")] private LayerMask whatisEnemy;
    [SerializeField, Header("Obstacle Layer")] private LayerMask whatisObs;

    private readonly Collider[] colliderTargets = new Collider[8];
    private readonly Collider[] detectionedTarget = new Collider[8];

    [SerializeField, Header("Current Target")] private Transform currentTarget;
    private Transform playerTarget;

    private int lockOnID = Animator.StringToHash("LockOn");

    [SerializeField] private float animationMoveMult;

    [SerializeField, Header("Skills")] private List<CombatSkillBase> skills = new List<CombatSkillBase>();
    private int nextSkillIndex;

    private void Start()
    {
        CachePlayerTarget();
        InitAllSkill();
    }

    private void Update()
    {
        if (IsOwnerDead())
        {
            ClearCurrentTarget();
            return;
        }

        AIView();
        LockOnTarget();
        UpdateAnimationMove();
        DetectionTarget();
    }

    private void LateUpdate()
    {
        if (IsOwnerDead())
        {
            return;
        }

        OnAnimatorActionAutoLockON();
    }

    /// <summary>
    /// AI vision
    /// </summary>
    private void AIView()
    {
        if (IsOwnerDead())
        {
            ClearCurrentTarget();
            return;
        }

        int targetCount = Physics.OverlapSphereNonAlloc(detectionCenter.position, detectionRang, colliderTargets, whatisEnemy);
        Transform target = FindPlayerTarget(colliderTargets, targetCount);

        if (target == null)
        {
            return;
        }

        if (!Physics.Raycast((transform.root.position + transform.root.up * 0.5f), (target.position - transform.root.position).normalized, out var hit, detectionRang, whatisObs))
        {
            if (Vector3.Dot((target.position - transform.root.position).normalized, transform.root.forward) > 0.35f)
            {
                currentTarget = target;
            }
        }
    }

    private void LockOnTarget()
    {
        if (IsOwnerDead())
        {
            _animator.SetFloat(lockOnID, 0f);
            ClearCurrentTarget();
            return;
        }

        if (_animator.CheckAnimationTag("Motion") && currentTarget != null)
        {
            _animator.SetFloat(lockOnID, 1f);
            transform.root.rotation = transform.LockOnTarget(currentTarget, transform, 50f);
        }
        else
        {
            _animator.SetFloat(lockOnID, 0f);
        }
    }

    public Transform GetCurrentTarget()
    {
        if (currentTarget == null)
        {
            return null;
        }

        return currentTarget;
    }

    private void UpdateAnimationMove()
    {
        if (_animator.CheckAnimationTag("Roll"))
        {
            _characterMovementBase.CharacterMoveInterface(transform.root.forward, _animator.GetFloat(animationMoveID) * animationMoveMult, true);
        }

        if (_animator.CheckAnimationTag("Attack"))
        {
            _characterMovementBase.CharacterMoveInterface(transform.root.forward, _animator.GetFloat(animationMoveID) * animationMoveMult, true);
        }
    }

    private void OnAnimatorActionAutoLockON()
    {
        if (CanAttackLockOn())
        {
            if (_animator.CheckAnimationTag("Attack") || _animator.CheckAnimationTag("GSAttack"))
            {
                transform.root.rotation = transform.LockOnTarget(currentTarget, transform.root.transform, 50f);
            }
        }
    }

    #region Target Detection

    private bool CanAttackLockOn()
    {
        if (_animator.CheckAnimationTag("Attack") || _animator.CheckAnimationTag("GSAttack"))
        {
            if (_animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.75f)
            {
                return true;
            }
        }

        return false;
    }

    private void DetectionTarget()
    {
        if (IsOwnerDead())
        {
            ClearCurrentTarget();
            return;
        }

        int targetCount = Physics.OverlapSphereNonAlloc(detectionCenter.position, detectionRang, detectionedTarget, enemyLayer);
        Transform target = FindPlayerTarget(detectionedTarget, targetCount);

        if (target != null)
        {
            SetCurrentTarget(target);
        }
    }

    private void SetCurrentTarget(Transform target)
    {
        if (currentTarget == null || currentTarget != target)
        {
            currentTarget = target;
        }
    }

    private void ClearCurrentTarget()
    {
        currentTarget = null;
    }

    private bool IsOwnerDead()
    {
        return _healthSystem != null && _healthSystem.IsDead();
    }

    private void CachePlayerTarget()
    {
        if (playerTarget != null)
        {
            return;
        }

        PlayerHealthSystem playerHealth = FindFirstObjectByType<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            playerTarget = playerHealth.transform;
        }
    }

    private Transform FindPlayerTarget(Collider[] targets, int targetCount)
    {
        CachePlayerTarget();

        for (int i = 0; i < targetCount; i++)
        {
            Collider targetCollider = targets[i];
            if (targetCollider == null)
            {
                continue;
            }

            if (playerTarget != null && targetCollider.transform.root == playerTarget)
            {
                return playerTarget;
            }

            if (targetCollider.TryGetComponent(out PlayerHealthSystem playerHealth))
            {
                playerTarget = playerHealth.transform;
                return playerTarget;
            }

            PlayerHealthSystem parentPlayerHealth = targetCollider.GetComponentInParent<PlayerHealthSystem>();
            if (parentPlayerHealth != null)
            {
                playerTarget = parentPlayerHealth.transform;
                return playerTarget;
            }
        }

        return null;
    }

    #endregion

    #region Skills

    private void InitAllSkill()
    {
        if (skills.Count == 0) return;

        for (int i = 0; i < skills.Count; i++)
        {
            skills[i].InitSkill(_animator, this, _characterMovementBase);

            if (!skills[i].GetSkillIsDone())
            {
                skills[i].ResetSkill();
            }
        }
    }

    public CombatSkillBase GetAnDoneSkill()
    {
        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i].GetSkillIsDone()) return skills[i];
            else continue;
        }

        return null;
    }

    public CombatSkillBase GetNextDoneSkill()
    {
        if (skills.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < skills.Count; i++)
        {
            int skillIndex = (nextSkillIndex + i) % skills.Count;
            if (!skills[skillIndex].GetSkillIsDone())
            {
                continue;
            }

            nextSkillIndex = (skillIndex + 1) % skills.Count;
            return skills[skillIndex];
        }

        return null;
    }

    public CombatSkillBase GetSkillUseName(string name)
    {
        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i].GetSkillName().Equals(name)) return skills[i];
            else continue;
        }

        return null;
    }

    public CombatSkillBase GetSkillUseID(int id)
    {
        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i].GetSkillID() == id) return skills[i];
            else continue;
        }

        return null;
    }

    #endregion

    public float GetCurrentTargetDistance() => Vector3.Distance(currentTarget.position, transform.root.position);

    public Vector3 GetDirectionForTarget() => (currentTarget.position - transform.root.position).normalized;
}
