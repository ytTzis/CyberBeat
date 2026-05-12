using System;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UGG.Combat;
using UGG.Move;
using UnityEngine;

namespace UGG.Health
{
    public abstract class CharacterHealthSystemBase : MonoBehaviour, IDamagar
    {
        //引用
        protected Animator _animator;
        protected CharacterMovementBase _movement;
        protected CharacterCombatSystemBase _combatSystem;
        protected AudioSource _audioSource;
        protected CharacterInputSystem _inputSystem;
        private PlayableGraph deathPlayableGraph;
        
        //攻击者
        protected Transform currentAttacker;

        [Header("Death")]
        [SerializeField] protected string deathAnimationName;
        [SerializeField] protected AnimationClip deathAnimationClip;
        [SerializeField] protected bool disableMovementOnDeath = true;
        [SerializeField] protected bool disableCombatOnDeath = true;
        [SerializeField] protected bool disableInputOnDeath = true;
        [SerializeField] protected bool disableCollidersOnDeath = true;
        [SerializeField] protected bool disableCharacterControllerOnDeath;
        [SerializeField] protected bool restoreTimeScaleOnDeath = true;
        protected bool isDead;
        
        //AnimationID
        protected int animationMove = Animator.StringToHash("AnimationMove");
        
        //HitAnimationMoveSpeedMult
        public float hitAnimationMoveMult;


        protected virtual void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            _movement = GetComponent<CharacterMovementBase>();
            _combatSystem = GetComponentInChildren<CharacterCombatSystemBase>();
            _audioSource = _movement.GetComponentInChildren<AudioSource>();
            _inputSystem = GetComponent<CharacterInputSystem>();
        }


        protected virtual void Update()
        {
            HitAnimaitonMove();
        }
        
        /// <summary>
        /// 设置攻击者
        /// </summary>
        /// <param name="attacker">攻击者</param>
        public virtual void SetAttacker(Transform attacker)
        {
            if (currentAttacker != attacker || currentAttacker == null)
            {
                currentAttacker = attacker;
            }
        }

        protected virtual void HitAnimaitonMove()
        {
            if (!HasValidAnimator() || _movement == null) return;
            if(!_animator.CheckAnimationTag("Hit")) return;
            _movement.CharacterMoveInterface(transform.forward,_animator.GetFloat(animationMove) * hitAnimationMoveMult,true);
        }

        #region 接口

        public virtual void TakeDamager(float damager)
        {
            throw new NotImplementedException();
        }

        public virtual void TakeDamager(string hitAnimationName)
        {
            
        }

        public virtual void TakeDamager(float damager, string hitAnimationName)
        {
            throw new NotImplementedException();
        }

        public virtual void TakeDamager(float damagar, string hitAnimationName, Transform attacker)
        {
            
        }

        #endregion

        #region 外部接口

        public bool IsDead() => isDead;

        public void ForceDie(Transform attacker = null)
        {
            if (attacker != null)
            {
                SetAttacker(attacker);
            }

            Die();
        }

        /// <summary>
        /// 弹刀动画
        /// </summary>
        /// <param name="animationName"></param>
        public void FlickWeapon(string animationName)
        {
            if (!HasValidAnimator())
            {
                return;
            }

            _animator.Play(animationName, 0, 0f);
        }

        protected virtual void Die()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;

            if (restoreTimeScaleOnDeath && Time.timeScale < 1f)
            {
                Time.timeScale = 1f;
            }

            PlayDeathAnimation();

            if (disableCombatOnDeath && _combatSystem != null)
            {
                _combatSystem.enabled = false;
            }

            if (disableMovementOnDeath && _movement != null)
            {
                _movement.enabled = false;
            }

            if (disableInputOnDeath && _inputSystem != null)
            {
                _inputSystem.enabled = false;
            }

            if (disableCollidersOnDeath)
            {
                DisableCollidersOnDeath();
            }

            if (disableCharacterControllerOnDeath && TryGetComponent(out CharacterController controller))
            {
                controller.enabled = false;
            }
        }

        private void DisableCollidersOnDeath()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        private void PlayDeathAnimation()
        {
            if (!HasValidAnimator())
            {
                return;
            }

            if (deathAnimationClip != null)
            {
                if (deathPlayableGraph.IsValid())
                {
                    deathPlayableGraph.Destroy();
                }

                deathPlayableGraph = PlayableGraph.Create($"{name}_DeathAnimation");
                var output = AnimationPlayableOutput.Create(deathPlayableGraph, "Death", _animator);
                var clipPlayable = AnimationClipPlayable.Create(deathPlayableGraph, deathAnimationClip);
                output.SetSourcePlayable(clipPlayable);
                deathPlayableGraph.Play();
                return;
            }

            if (!string.IsNullOrEmpty(deathAnimationName))
            {
                _animator.Play(deathAnimationName, 0, 0f);
            }
        }

        protected bool HasValidAnimator()
        {
            if (_animator != null)
            {
                return true;
            }

            _animator = GetComponentInChildren<Animator>(true);
            return _animator != null;
        }

        protected virtual void OnDisable()
        {
            if (deathPlayableGraph.IsValid())
            {
                deathPlayableGraph.Destroy();
            }
        }

        #endregion
    }
}

