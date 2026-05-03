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

        [SerializeField, Header("攻击伤害")] protected float normalAttackDamage = 10f;
        [SerializeField] protected float heavyAttackDamage = 20f;

        protected virtual void Awake()
        {
            _animator = GetComponent<Animator>();
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

            Collider[] attackDetectionTargets = new Collider[4];

            int counts = Physics.OverlapSphereNonAlloc(attackDetectionCenter.position, attackDetectionRang, attackDetectionTargets, enemyLayer);

            if (counts > 0)
            {
                HashSet<IDamagar> hitTargets = new HashSet<IDamagar>();

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

                    damagar.TakeDamager(GetCurrentAttackDamage(), hitName, transform.root.transform);
                }
            }
        }

        private float GetCurrentAttackDamage()
        {
            if (_animator.CheckAnimationTag("GSAttack"))
            {
                return heavyAttackDamage;
            }

            return normalAttackDamage;
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

        private void OnDrawGizmos()
        {
            Gizmos.DrawWireSphere(attackDetectionCenter.position, attackDetectionRang);
        }
    }
}
