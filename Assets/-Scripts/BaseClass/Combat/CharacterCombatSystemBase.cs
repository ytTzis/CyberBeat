using System;
using System.Collections;
using System.Collections.Generic;
using UGG.Health;
using UGG.Move;
using UnityEngine;

namespace UGG.Combat
{
    public abstract class CharacterCombatSystemBase : MonoBehaviour
    {
        protected Animator _animator;
        protected CharacterInputSystem _characterInputSystem;
        protected CharacterMovementBase _characterMovementBase;
        protected AudioSource _audioSource;
        protected CharacterHealthSystemBase _healthSystem;

        //aniamtionID
        protected int lAtkID = Animator.StringToHash("LAtk");
        protected int rAtkID = Animator.StringToHash("RAtk");
        protected int defenID = Animator.StringToHash("Parry");
        protected int animationMoveID = Animator.StringToHash("AnimationMove");
        protected int secondaryWeaponID = Animator.StringToHash("SecondaryWeapon");

        //攻击检测
        [SerializeField, Header("攻击检测")] protected Transform attackDetectionCenter;
        [SerializeField] protected float attackDetectionRang;
        [SerializeField] protected LayerMask enemyLayer;
        [SerializeField, Header("持续命中检测")] protected float attackHitWindowDuration = 0.12f;
        [SerializeField, Min(1)] protected int attackHitChecksPerWindow = 3;

        [SerializeField, Header("攻击伤害")] protected float normalAttackDamage = 10f;
        [SerializeField] protected float heavyAttackDamage = 20f;

        private readonly Collider[] attackDetectionTargets = new Collider[4];
        private Coroutine attackHitWindowCoroutine;

        protected virtual void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            _characterInputSystem = GetComponentInParent<CharacterInputSystem>();
            _characterMovementBase = GetComponentInParent<CharacterMovementBase>();
            _audioSource = _characterMovementBase.GetComponentInChildren<AudioSource>();
            _healthSystem = GetComponentInParent<CharacterHealthSystemBase>();
        }

        /// <summary>
        /// 攻击动画攻击检测事件
        /// </summary>
        /// <param name="hitName">传递受伤动画名</param>
        protected virtual void OnAnimationAttackEvent(string hitName)
        {
            if (_healthSystem != null && _healthSystem.IsDead())
            {
                return;
            }

            PlayerWeaponEffect();

            if (attackHitWindowCoroutine != null)
            {
                StopCoroutine(attackHitWindowCoroutine);
            }

            attackHitWindowCoroutine = StartCoroutine(AttackHitWindowRoutine(hitName));
        }

        private IEnumerator AttackHitWindowRoutine(string hitName)
        {
            HashSet<IDamagar> hitTargets = new HashSet<IDamagar>();
            int checksRemaining = Mathf.Max(1, attackHitChecksPerWindow);
            float totalDuration = Mathf.Max(0f, attackHitWindowDuration);
            float checkInterval = checksRemaining <= 1 || totalDuration <= 0f
                ? 0f
                : totalDuration / (checksRemaining - 1);

            for (int checkIndex = 0; checkIndex < checksRemaining; checkIndex++)
            {
                if (!CanContinueAttackHitWindow())
                {
                    break;
                }

                TryApplyAttackHits(hitName, hitTargets);

                if (checkIndex < checksRemaining - 1 && checkInterval > 0f)
                {
                    yield return new WaitForSeconds(checkInterval);
                }
            }

            attackHitWindowCoroutine = null;
        }

        protected virtual bool CanContinueAttackHitWindow()
        {
            return _animator.CheckAnimationTag("Attack") ||
                   _animator.CheckAnimationTag("LAttack") ||
                   _animator.CheckAnimationTag("GSAttack");
        }

        private void TryApplyAttackHits(string hitName, HashSet<IDamagar> hitTargets)
        {
            if (attackDetectionCenter == null)
            {
                return;
            }

            int counts = Physics.OverlapSphereNonAlloc(
                attackDetectionCenter.position,
                attackDetectionRang,
                attackDetectionTargets,
                enemyLayer);

            if (counts <= 0)
            {
                return;
            }

            float damage = GetAttackDamage(hitName);

            for (int i = 0; i < counts; i++)
            {
                Collider attackTarget = attackDetectionTargets[i];
                if (attackTarget == null)
                {
                    continue;
                }

                IDamagar damagar = attackTarget.GetComponentInParent<IDamagar>();
                if (damagar == null || !hitTargets.Add(damagar))
                {
                    continue;
                }

                damagar.TakeDamager(damage, hitName, transform.root.transform);
            }
        }

        private float GetCurrentAttackDamage()
        {
            float baseDamage;

            if (_animator.CheckAnimationTag("GSAttack"))
            {
                baseDamage = heavyAttackDamage;
            }
            else
            {
                baseDamage = normalAttackDamage;
            }

            return baseDamage * GetAttackDamageMultiplier();
        }

        protected virtual float GetAttackDamage(string hitName)
        {
            return GetCurrentAttackDamage();
        }

        protected virtual float GetAttackDamageMultiplier()
        {
            return 1f;
        }

        private void PlayerWeaponEffect()
        {
            if (_animator.CheckAnimationTag("Attack") || _animator.CheckAnimationTag("LAttack"))
            {
                GameAssets.Instance.PlaySoundEffect(_audioSource, SoundAssetsType.swordWave);
            }

            if (_animator.CheckAnimationTag("GSAttack"))
            {
                GameAssets.Instance.PlaySoundEffect(_audioSource, SoundAssetsType.hSwordWave);
            }
        }

        protected virtual void OnDisable()
        {
            if (attackHitWindowCoroutine != null)
            {
                StopCoroutine(attackHitWindowCoroutine);
                attackHitWindowCoroutine = null;
            }
        }

        private void OnDrawGizmos()
        {
            if (attackDetectionCenter == null)
            {
                return;
            }

            Gizmos.DrawWireSphere(attackDetectionCenter.position, attackDetectionRang);
        }
    }
}
