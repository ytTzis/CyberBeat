using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UGG.Combat;
using UGG.Health;

public class AICombatSystem : CharacterCombatSystemBase
{
    [SerializeField, Header("Detection Center")] private Transform detectionCenter;
    [SerializeField, Header("Detection Range")] private float detectionRang;
    [SerializeField, Range(-1f, 1f), Header("Vision Forward Threshold")] private float visionForwardThreshold = 0.15f;
    [SerializeField, Min(0f), Header("Target Memory Duration")] private float targetMemoryDuration = 1.35f;

    [SerializeField, Header("Target Layer")] private LayerMask whatisEnemy;
    [SerializeField, Header("Obstacle Layer")] private LayerMask whatisObs;

    private readonly Collider[] colliderTargets = new Collider[8];
    private readonly Collider[] detectionedTarget = new Collider[8];

    [SerializeField, Header("Current Target")] private Transform currentTarget;
    private Transform playerTarget;

    private int lockOnID = Animator.StringToHash("LockOn");

    [SerializeField] private float animationMoveMult;
    [SerializeField, Header("Jump Attack Assist")] private string assistedGapCloseSkillName = "JumpAttack04_1";
    [SerializeField, Min(0f)] private float assistedGapCloseMoveSpeed = 2.6f;
    [SerializeField, Range(0f, 1f)] private float assistedGapCloseEndNormalizedTime = 0.55f;
    [SerializeField, Min(0f)] private float assistedGapCloseRangeBuffer = 0.15f;
    [SerializeField, Header("Pressure Move"), Min(0f)] private float pressureApproachRange = 4.75f;
    [SerializeField, Min(0f)] private float pressureApproachSpeed = 1.85f;
    [SerializeField, Min(0f)] private float pressureStopDistanceBuffer = 0.2f;

    [SerializeField, Header("Skills")] private List<CombatSkillBase> skills = new List<CombatSkillBase>();
    [SerializeField, Header("Skill Variation")] private bool useRandomSkillSelection = true;
    [SerializeField, Range(0f, 1f)] private float repeatSkillWeightMultiplier = 0.45f;
    [SerializeField, Range(0f, 1f)] private float recentSkillWeightMultiplier = 0.7f;
    [SerializeField, Min(0)] private int recentSkillMemory = 2;
    [SerializeField, Min(0.1f)] private float distancePreferenceRange = 2.5f;
    [SerializeField, Header("Context Preference")] private string longRangePreferredSkillName = "JumpAttack04_1";
    [SerializeField, Min(0f)] private float longRangeThreshold = 3.1f;
    [SerializeField, Min(1f)] private float longRangePreferredSkillWeightMultiplier = 3f;
    [SerializeField, Min(0f), Header("Close Range Threshold")] private float closeRangeThreshold = 2.2f;
    [SerializeField, Min(1f)] private float closeRangePreferredSkillWeightMultiplier = 1.35f;
    private int nextSkillIndex;
    private readonly List<int> recentSkillIds = new List<int>();
    private float lastSeenTargetTime = float.MinValue;

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
        UpdateTargetMemory();
    }

    private void LateUpdate()
    {
        if (IsOwnerDead())
        {
            return;
        }

        OnAnimatorActionAutoLockON();
    }

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
            if (Vector3.Dot((target.position - transform.root.position).normalized, transform.root.forward) > visionForwardThreshold)
            {
                SetCurrentTarget(target);
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
            TryAssistJumpAttackGapClose();
        }

        TryPressureApproach();
    }

    private void TryPressureApproach()
    {
        if (currentTarget == null || !_animator.CheckAnimationTag("Motion"))
        {
            return;
        }

        if (_animator.CheckAnimationTag("Attack") || _animator.CheckAnimationTag("Roll"))
        {
            return;
        }

        float pressureStopDistance = attackDetectionRang + pressureStopDistanceBuffer;
        float currentDistance = GetCurrentTargetDistance();
        if (currentDistance <= pressureStopDistance || currentDistance > pressureApproachRange)
        {
            return;
        }

        _characterMovementBase.CharacterMoveInterface(GetDirectionForTarget(), pressureApproachSpeed, true);
    }

    private void TryAssistJumpAttackGapClose()
    {
        if (currentTarget == null || string.IsNullOrEmpty(assistedGapCloseSkillName))
        {
            return;
        }

        if (!_animator.CheckAnimationName(assistedGapCloseSkillName))
        {
            return;
        }

        if (_animator.GetCurrentAnimatorStateInfo(0).normalizedTime > assistedGapCloseEndNormalizedTime)
        {
            return;
        }

        float requiredHitDistance = attackDetectionRang + assistedGapCloseRangeBuffer;
        if (GetCurrentTargetDistance() <= requiredHitDistance)
        {
            return;
        }

        _characterMovementBase.CharacterMoveInterface(GetDirectionForTarget(), assistedGapCloseMoveSpeed, true);
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

        lastSeenTargetTime = Time.time;
    }

    private void ClearCurrentTarget()
    {
        currentTarget = null;
        lastSeenTargetTime = float.MinValue;
    }

    private void UpdateTargetMemory()
    {
        if (currentTarget == null || targetMemoryDuration <= 0f)
        {
            return;
        }

        if (Time.time - lastSeenTargetTime > targetMemoryDuration)
        {
            ClearCurrentTarget();
        }
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

        if (useRandomSkillSelection)
        {
            return GetRandomDoneSkill();
        }

        for (int i = 0; i < skills.Count; i++)
        {
            int skillIndex = (nextSkillIndex + i) % skills.Count;
            if (!skills[skillIndex].GetSkillIsDone())
            {
                continue;
            }

            nextSkillIndex = (skillIndex + 1) % skills.Count;
            RememberSkill(skills[skillIndex]);
            return skills[skillIndex];
        }

        return null;
    }

    private CombatSkillBase GetRandomDoneSkill()
    {
        List<CombatSkillBase> availableSkills = new List<CombatSkillBase>();
        List<float> weights = new List<float>();
        float totalWeight = 0f;
        float targetDistance = currentTarget != null ? GetCurrentTargetDistance() : 0f;
        int lastSkillId = recentSkillIds.Count > 0 ? recentSkillIds[recentSkillIds.Count - 1] : -1;

        for (int i = 0; i < skills.Count; i++)
        {
            CombatSkillBase skill = skills[i];
            if (!skill.GetSkillIsDone())
            {
                continue;
            }

            float weight = 1f;

            if (skill.GetSkillID() == lastSkillId)
            {
                weight *= repeatSkillWeightMultiplier;
            }
            else if (recentSkillIds.Contains(skill.GetSkillID()))
            {
                weight *= recentSkillWeightMultiplier;
            }

            if (currentTarget != null)
            {
                float distanceDelta = Mathf.Abs(targetDistance - skill.GetSkillUseDistance());
                float distanceBonus = Mathf.Clamp01(1f - (distanceDelta / distancePreferenceRange));
                weight *= 1f + distanceBonus;

                if (targetDistance >= longRangeThreshold &&
                    !string.IsNullOrEmpty(longRangePreferredSkillName) &&
                    skill.GetSkillName() == longRangePreferredSkillName)
                {
                    weight *= longRangePreferredSkillWeightMultiplier;
                }

                if (targetDistance <= closeRangeThreshold && skill.GetSkillUseDistance() <= closeRangeThreshold + 0.35f)
                {
                    weight *= closeRangePreferredSkillWeightMultiplier;
                }
            }

            if (weight <= 0f)
            {
                continue;
            }

            availableSkills.Add(skill);
            weights.Add(weight);
            totalWeight += weight;
        }

        if (availableSkills.Count == 0)
        {
            return null;
        }

        float randomPoint = Random.Range(0f, totalWeight);
        float accumulatedWeight = 0f;

        for (int i = 0; i < availableSkills.Count; i++)
        {
            accumulatedWeight += weights[i];
            if (randomPoint > accumulatedWeight)
            {
                continue;
            }

            RememberSkill(availableSkills[i]);
            return availableSkills[i];
        }

        CombatSkillBase fallbackSkill = availableSkills[availableSkills.Count - 1];
        RememberSkill(fallbackSkill);
        return fallbackSkill;
    }

    private void RememberSkill(CombatSkillBase skill)
    {
        if (skill == null)
        {
            return;
        }

        recentSkillIds.Add(skill.GetSkillID());

        while (recentSkillIds.Count > recentSkillMemory)
        {
            recentSkillIds.RemoveAt(0);
        }
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

    public float GetCurrentTargetDistance() => currentTarget == null ? float.MaxValue : Vector3.Distance(currentTarget.position, transform.root.position);

    public Vector3 GetDirectionForTarget() => currentTarget == null ? transform.root.forward : (currentTarget.position - transform.root.position).normalized;

    public float GetPressureApproachSpeed() => pressureApproachSpeed;
}