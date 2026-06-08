using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UGG.Health
{
    public class AI2HealthSystem : CharacterHealthSystemBase
    {
        private const string DefaultDeathAnimation = "GhostSamurai_Bow_Die01_Inplace";
        private const string DefaultDeathAnimationPath = "Assets/GameAssets/GreatSword_Animset/Animation/katana/APose/Die/Inplace/GhostSamurai_APose_Die05_Inplace.FBX";
        private const string NonInterruptibleJumpAttackStateName = "JumpAttack04_1";
        private const string ExecuteHitStateName = "ExecuteHit";
        private const string ExecuteHitRecoveryStateName = "BaseMotion";

        [Header("AI HP")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;

        [SerializeField] private int maxParryCount;
        [SerializeField] private int counterattackParryCount;
        [SerializeField] private float counterattackDelay = 0.5f;
        [SerializeField, Header("Counter Attack"), Range(0f, 1f)] private float counterAttackInvincibleNormalizedTime = 0.2f;
        [SerializeField, Header("跳劈霸体结束时间(0-1)"), Range(0f, 1f)] private float jumpAttackArmorEndNormalizedTime = 0.36f;
        [SerializeField, Header("处决真正倒地时间(0-1)"), Range(0f, 1f)] private float executeHitDownedNormalizedTime = 0.72f;
        [SerializeField, Header("处决倒地后起身延迟"), Min(0f)] private float executeHitGetUpDelay = 1f;
        [SerializeField, Header("处决状态进入等待时间"), Min(0f)] private float executeHitStateEnterTimeout = 0.25f;

        [SerializeField] private int maxHitCount;
        [SerializeField] private int hitCount;

        private Coroutine counterattackCoroutine;
        private Coroutine executeHitRecoveryCoroutine;
        private AICombatSystem aiCombatSystem;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float HealthNormalized => maxHealth <= 0f ? 0f : currentHealth / maxHealth;

        private void Start()
        {
            hitCount = 0;
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

            if (string.IsNullOrEmpty(deathAnimationName))
            {
                deathAnimationName = DefaultDeathAnimation;
            }

            TryAssignDefaultDeathClip();
        }

        private void LateUpdate()
        {
            OnHitLockTarget();
        }

        public override void TakeDamager(float damagar, string hitAnimationName, Transform attacker)
        {
            if (IsDead())
            {
                return;
            }

            if (!HasValidAnimator())
            {
                return;
            }

            SetAttacker(attacker);

            if (IsExecuteHitActive())
            {
                return;
            }

            if (hitAnimationName == ExecuteHitStateName)
            {
                SetExecuteHitActionLock(true);
                ApplyDamage(damagar);

                if (currentHealth <= 0f)
                {
                    RestoreAnimatorSpeedForExecuteHit();
                    Die();
                    return;
                }

                _animator.Play(ExecuteHitStateName, 0, 0f);
                TryStartExecuteHitRecovery();
                GameAssets.Instance.PlaySoundEffect(_audioSource, SoundAssetsType.hit);
                hitCount++;
                return;
            }

            if (IsCounterAttackActive())
            {
                if (!OnInvincibleState())
                {
                    ApplyDamage(damagar);

                    if (currentHealth <= 0f)
                    {
                        Die();
                    }
                }

                return;
            }

            if (IsNonInterruptibleJumpAttackActive())
            {
                ApplyDamage(damagar);

                if (currentHealth <= 0f)
                {
                    Die();
                    return;
                }

                GameAssets.Instance.PlaySoundEffect(_audioSource, SoundAssetsType.hit);
                return;
            }

            if (maxParryCount > 0 && !OnInvincibleState())
            {
                if (counterattackParryCount == 2)
                {
                    if (counterattackCoroutine != null)
                    {
                        StopCoroutine(counterattackCoroutine);
                    }

                    counterattackCoroutine = StartCoroutine(DelayedCounterattack());
                    counterattackParryCount = 0;
                    GameAssets.Instance.PlaySoundEffect(_audioSource, SoundAssetsType.parry);
                }
                else
                {
                    OnParry(hitAnimationName);
                }

                maxParryCount--;
            }
            else
            {
                if (hitCount == maxHitCount && !_animator.CheckAnimationTag("Flick_0"))
                {
                    _animator.Play("Roll_B", 0, 0f);

                    hitCount = 0;
                    maxHitCount += Random.Range(1, 4);
                }
                else if (!OnInvincibleState())
                {
                    ApplyDamage(damagar);

                    if (currentHealth <= 0f)
                    {
                        Die();
                        return;
                    }

                    _animator.Play(hitAnimationName, 0, 0f);
                    if (hitAnimationName == ExecuteHitStateName)
                    {
                        SetExecuteHitActionLock(true);
                        TryStartExecuteHitRecovery();
                    }
                    GameAssets.Instance.PlaySoundEffect(_audioSource, SoundAssetsType.hit);
                    hitCount++;
                }
            }
        }

        private bool OnInvincibleState()
        {
            if (!HasValidAnimator())
            {
                return false;
            }

            if (IsWithinCounterAttackInvincibleWindow()) return true;

            return false;
        }

        private bool IsCounterAttackActive()
        {
            if (!HasValidAnimator())
            {
                return false;
            }

            AnimatorStateInfo currentStateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            return _animator.CheckAnimationTag("CounterAttack") || currentStateInfo.IsName("GS12");
        }

        private bool IsExecuteHitActive()
        {
            if (!HasValidAnimator())
            {
                return false;
            }

            AnimatorStateInfo currentStateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            if (currentStateInfo.IsName(ExecuteHitStateName))
            {
                return true;
            }

            return _animator.IsInTransition(0) &&
                   _animator.GetNextAnimatorStateInfo(0).IsName(ExecuteHitStateName);
        }

        private void TryStartExecuteHitRecovery()
        {
            if (executeHitRecoveryCoroutine != null)
            {
                StopCoroutine(executeHitRecoveryCoroutine);
            }

            executeHitRecoveryCoroutine = StartCoroutine(RecoverFromExecuteHit());
        }

        private IEnumerator RecoverFromExecuteHit()
        {
            SetExecuteHitActionLock(true);
            float originalAnimatorSpeed = HasValidAnimator() ? _animator.speed : 1f;
            float enterElapsed = 0f;

            while (!IsDead() && HasValidAnimator() && !IsExecuteHitActive() && enterElapsed < executeHitStateEnterTimeout)
            {
                enterElapsed += Time.deltaTime;
                yield return null;
            }

            while (!IsDead() && HasValidAnimator() && IsExecuteHitActive())
            {
                AnimatorStateInfo currentStateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                if (currentStateInfo.IsName(ExecuteHitStateName) &&
                    currentStateInfo.normalizedTime >= executeHitDownedNormalizedTime)
                {
                    break;
                }

                yield return null;
            }

            if (!IsDead() && HasValidAnimator() && IsExecuteHitActive())
            {
                _animator.speed = 0f;
            }

            if (executeHitGetUpDelay > 0f)
            {
                yield return new WaitForSeconds(executeHitGetUpDelay);
            }

            if (HasValidAnimator())
            {
                _animator.speed = originalAnimatorSpeed;
            }

            if (!IsDead() && HasValidAnimator())
            {
                _animator.CrossFadeInFixedTime(ExecuteHitRecoveryStateName, 0.1f, 0, 0f);
            }

            SetExecuteHitActionLock(false);
            executeHitRecoveryCoroutine = null;
        }

        private void SetExecuteHitActionLock(bool isLocked)
        {
            AICombatSystem combatSystem = GetAICombatSystem();
            if (combatSystem != null)
            {
                combatSystem.SetCombatLogicEnabled(!isLocked);
            }
        }

        private AICombatSystem GetAICombatSystem()
        {
            if (aiCombatSystem != null)
            {
                return aiCombatSystem;
            }

            aiCombatSystem = GetComponent<AICombatSystem>();
            if (aiCombatSystem == null)
            {
                aiCombatSystem = GetComponentInChildren<AICombatSystem>(true);
            }

            if (aiCombatSystem == null)
            {
                aiCombatSystem = GetComponentInParent<AICombatSystem>();
            }

            return aiCombatSystem;
        }

        private void RestoreAnimatorSpeedForExecuteHit()
        {
            if (HasValidAnimator() && Mathf.Approximately(_animator.speed, 0f))
            {
                _animator.speed = 1f;
            }
        }

        private bool IsNonInterruptibleJumpAttackActive()
        {
            if (!HasValidAnimator())
            {
                return false;
            }

            AnimatorStateInfo currentStateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            if (currentStateInfo.IsName(NonInterruptibleJumpAttackStateName))
            {
                return IsJumpAttackInArmorWindow(currentStateInfo);
            }

            return _animator.IsInTransition(0) &&
                   IsJumpAttackInArmorWindow(_animator.GetNextAnimatorStateInfo(0));
        }

        private bool IsJumpAttackInArmorWindow(AnimatorStateInfo stateInfo)
        {
            return stateInfo.IsName(NonInterruptibleJumpAttackStateName) &&
                   stateInfo.normalizedTime < jumpAttackArmorEndNormalizedTime;
        }

        private bool IsWithinCounterAttackInvincibleWindow()
        {
            AnimatorStateInfo currentStateInfo = _animator.GetCurrentAnimatorStateInfo(0);

            if (_animator.CheckAnimationTag("CounterAttack"))
            {
                return currentStateInfo.normalizedTime <= counterAttackInvincibleNormalizedTime;
            }

            if (currentStateInfo.IsName("GS12"))
            {
                return currentStateInfo.normalizedTime <= counterAttackInvincibleNormalizedTime;
            }

            return false;
        }

        private IEnumerator DelayedCounterattack()
        {
            float delay = Mathf.Max(0f, counterattackDelay);
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (IsDead() || !HasValidAnimator())
            {
                counterattackCoroutine = null;
                yield break;
            }

            FaceCurrentAttackerForCounterattack();
            _animator.Play("GS12", 0, 0f);
            counterattackCoroutine = null;
        }

        private void OnHitLockTarget()
        {
            if (!HasValidAnimator() || currentAttacker == null)
            {
                return;
            }

            AnimatorStateInfo currentStateInfo = _animator.GetCurrentAnimatorStateInfo(0);

            if (_animator.CheckAnimationTag("Hit"))
            {
                transform.rotation = transform.LockOnTarget(currentAttacker, transform, 50f);
            }

            if (currentStateInfo.IsName("GS12") && currentStateInfo.normalizedTime < 0.25f)
            {
                FaceCurrentAttackerForCounterattack();
            }
        }

        private void FaceCurrentAttackerForCounterattack()
        {
            if (currentAttacker == null)
            {
                return;
            }

            Vector3 targetDirection = currentAttacker.position - transform.root.position;
            targetDirection.y = 0f;
            if (targetDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.root.rotation = Quaternion.LookRotation(targetDirection.normalized);
        }

        private void OnParry(string hitName)
        {
            if (!HasValidAnimator())
            {
                return;
            }

            switch (hitName)
            {
                default:
                    _animator.Play(hitName, 0, 0f);
                    GameAssets.Instance.PlaySoundEffect(_audioSource, SoundAssetsType.hit);
                    break;
                case "Hit_D_Up":
                    _animator.Play("ParryF", 0, 0f);
                    GameAssets.Instance.PlaySoundEffect(_audioSource, SoundAssetsType.parry);
                    counterattackParryCount++;
                    break;
                case "Hit_H_Left":
                    _animator.Play("ParryR", 0, 0f);
                    GameAssets.Instance.PlaySoundEffect(_audioSource, SoundAssetsType.parry);
                    counterattackParryCount++;
                    break;
                case "Hit_H_Right":
                    _animator.Play("ParryL", 0, 0f);
                    GameAssets.Instance.PlaySoundEffect(_audioSource, SoundAssetsType.parry);
                    counterattackParryCount++;
                    break;
                case "Hit_Up_Left":
                    _animator.Play("ParryR", 0, 0f);
                    GameAssets.Instance.PlaySoundEffect(_audioSource, SoundAssetsType.parry);
                    counterattackParryCount++;
                    break;
                case "Hit_Up_Right":
                    _animator.Play("ParryL", 0, 0f);
                    GameAssets.Instance.PlaySoundEffect(_audioSource, SoundAssetsType.parry);
                    counterattackParryCount++;
                    break;
            }
        }

        private void ApplyDamage(float damage)
        {
            if (damage <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Clamp(currentHealth - damage, 0f, maxHealth);

            if (currentHealth <= 0f)
            {
                currentHealth = 0f;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(deathAnimationName))
            {
                deathAnimationName = DefaultDeathAnimation;
            }

            TryAssignDefaultDeathClip();
        }
#endif

        private void TryAssignDefaultDeathClip()
        {
            if (deathAnimationClip != null)
            {
                return;
            }

#if UNITY_EDITOR
            deathAnimationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(DefaultDeathAnimationPath);
#endif
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (counterattackCoroutine != null)
            {
                StopCoroutine(counterattackCoroutine);
                counterattackCoroutine = null;
            }

            if (executeHitRecoveryCoroutine != null)
            {
                StopCoroutine(executeHitRecoveryCoroutine);
                executeHitRecoveryCoroutine = null;
            }
        }
    }
}
