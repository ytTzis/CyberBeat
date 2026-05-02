using System;
using System.Collections;
using System.Collections.Generic;
using UGG.Health;
using UnityEngine;

namespace UGG.Combat
{
    public class PlayerCombatSystem : CharacterCombatSystemBase
    {
        [System.Serializable]
        private class AttackVfxMapping
        {
            public string stateName;
            public GameObject vfxPrefab;
            public Vector3 positionOffset = new Vector3(0f, 0f, 1f);
            public Vector3 rotationOffset;
            public float lifetime = 2f;
        }

        private static readonly string[] FinalComboAttackStateNames =
        {
            "GhostSamurai_APose_Attack02_6_Inplace"
        };

        private PlayerHealthSystem healthSystem;

        //引用
        [SerializeField] private Transform currentTarget;

        //Speed
        [SerializeField, Header("攻击移动速度倍率"), Range(0.1f, 10f)]
        private float attackMoveMult;
        [SerializeField, Header("攻击提前恢复输入时间(0-1)")] [Range(0f, 1f)]
        private float attackRecoverNormalizedTime = 0.6f;
        [SerializeField, Header("攻击锁敌结束时间(0-1)")] [Range(0f, 1f)]
        private float attackLockReleaseNormalizedTime = 0.55f;
        [SerializeField, Header("最后一刀提前恢复移动时间(0-1)")] [Range(0f, 1f)]
        private float finalAttackMoveRecoverNormalizedTime = 0.85f;
        
        //检测
        [SerializeField, Header("检测敌人")] private Transform detectionCenter;
        [SerializeField] private float detectionRang;

        //缓存
        private Collider[] detectionedTarget = new Collider[1];

        //允许攻击输入
        [SerializeField] private bool allowAttackInput;
        [SerializeField] private AttackVfxMapping[] attackVfxMappings;
        [SerializeField, Header("Attack VFX")] private GameObject attackVfxPrefab;
        [SerializeField] private Transform attackVfxSpawnPoint;
        [SerializeField] private Vector3 attackVfxOffset = new Vector3(0f, 0f, 1f);
        [SerializeField] private Vector3 attackVfxRotationOffset;
        [SerializeField] private float attackVfxLifetime = 2f;
        [SerializeField] private Color attackVfxTintColor = new Color(1f, 0.82f, 0.12f, 1f);

        protected override void Awake()
        {
            base.Awake();

            healthSystem = GetComponentInParent<PlayerHealthSystem>();
        }

        private void Update()
        {
            PlayerAttackAction();
            DetectionTarget();
            ActionMotion();
            UpdateCurrentTarget();
            RecoverFromExecuteIfNeeded();
            PlayerParryInput();
        }

        private void LateUpdate()
        {
            OnAnimatorActionAutoLockON();
        }

        private void PlayerAttackAction()
        {
            //当玩家处于Motion状态(idle)也允许玩家输入攻击信号
            if (!allowAttackInput)
            {
                bool canInputFromMotion = _animator.CheckCurrentTagAnimationTimeIsExceed("Motion", 0.01f) &&
                                          !_animator.IsInTransition(0);
                bool canRecoverFromAttack = CanRecoverFromAttack();

                if (canInputFromMotion || canRecoverFromAttack)
                {
                    SetAllowAttackInput(true);
                }
            }

            //如果玩按下鼠标左键
            if (_characterInputSystem.playerLAtk && allowAttackInput)
            {
                if (healthSystem.GetCanExecute())
                {
                    //播放处决动画
                    _animator.Play("Execute_0", 0, 0f);

                    Time.timeScale = 1f;
                }
                else
                {
                    //触发默认攻击动画
                    _animator.SetTrigger(lAtkID);

                    SetAllowAttackInput(false);
                }
            }

            //关闭大剑分支，始终保持普通武器战斗状态
            _animator.SetBool(secondaryWeaponID, false);
        }

        private void PlayerParryInput()
        {
            if (CanInputParry())
            {
                _animator.SetBool(defenID, _characterInputSystem.playerDefen);
            }
            else
            {
                _animator.SetBool(defenID, false);
            }
        }

        private bool CanInputParry()
        {
            if (_animator.CheckAnimationTag("Motion")) return true;
            if (_animator.CheckAnimationTag("Parry")) return true;
            if (_animator.CheckCurrentTagAnimationTimeIsExceed("Hit", 0.07f)) return true;

            return false;
        } 

        private void OnAnimatorActionAutoLockON()
        {
            //检测攻击状态是否允许自动锁定敌人
            if (CanAttackLockOn())
            {
                //检测动画是否在默认攻击状态或者大剑攻击状态 如果是的话执行看向目标位置方法
                if (_animator.CheckAnimationTag("Attack") || _animator.CheckAnimationTag("GSAttack"))
                {
                    transform.root.rotation = transform.LockOnTarget(currentTarget, transform.root.transform, 50f);
                }
            }
        }

        private void ActionMotion()
        {
            if (_animator.CheckAnimationTag("Attack") || _animator.CheckAnimationTag("GSAttack"))
            {
                _characterMovementBase.CharacterMoveInterface(transform.forward, _animator.GetFloat(animationMoveID) * attackMoveMult, true);
            }
        }

        #region 动作检测
        
        /// <summary>
        /// 攻击状态是否允许自动锁定敌人
        /// </summary>
        /// <returns></returns>
        private bool CanAttackLockOn()
        {
            if (_animator.CheckAnimationTag("Attack") || _animator.CheckAnimationTag("GSAttack"))
            {
                if (_animator.GetCurrentAnimatorStateInfo(0).normalizedTime < attackLockReleaseNormalizedTime)
                {
                    return true;
                }
            }
            return false;
        }

        public bool CanRecoverFromAttack()
        {
            bool canRecoverFromNormalAttack = _animator.CheckAnimationTag("Attack") &&
                                              _animator.CheckCurrentTagAnimationTimeIsExceed("Attack", attackRecoverNormalizedTime);
            bool canRecoverFromGreatSwordAttack = _animator.CheckAnimationTag("GSAttack") &&
                                                  _animator.GetCurrentAnimatorStateInfo(0).normalizedTime > attackRecoverNormalizedTime;

            return canRecoverFromNormalAttack || canRecoverFromGreatSwordAttack;
        }

        public bool CanRecoverMovementFromFinalAttack()
        {
            if (!_animator.CheckAnimationTag("Attack"))
            {
                return false;
            }

            if (_animator.GetCurrentAnimatorStateInfo(0).normalizedTime <= finalAttackMoveRecoverNormalizedTime)
            {
                return false;
            }

            for (int i = 0; i < FinalComboAttackStateNames.Length; i++)
            {
                if (_animator.CheckAnimationName(FinalComboAttackStateNames[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private void RecoverFromExecuteIfNeeded()
        {
            if (!_animator.CheckAnimationName("Execute_0"))
            {
                return;
            }

            if (_animator.IsInTransition(0))
            {
                return;
            }

            if (_animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.98f)
            {
                return;
            }

            _animator.Play("BaseMotion", 0, 0f);
            SetAllowAttackInput(true);
        }

        private void DetectionTarget()
        {
            //检测球体范围内的目标
            int targetCount = Physics.OverlapSphereNonAlloc(detectionCenter.position, detectionRang, detectionedTarget, enemyLayer);

            //后续功能补充
            if (targetCount > 0)
            {
                SetCurrentTarget(detectionedTarget[0].transform);
            }
        }

        private void SetCurrentTarget(Transform target)
        {
            //如果当前目标等于空或者不等于当前传递进来的目标
            if (currentTarget == null || currentTarget != target)
            {
                //给当前目标赋值
                currentTarget = target;
            }
        }

        private void UpdateCurrentTarget()
        {
            if (_animator.CheckAnimationTag("Motion"))
            {
                if (_characterInputSystem.playerMovement.magnitude > 0)
                {
                    currentTarget = null;
                }
            }
        }

        /// <summary>
        /// 获取当前锁定目标
        /// </summary>
        /// <returns></returns>
        public Transform GetCurrentTarget() => currentTarget;

        /// <summary>
        /// 获取当前是否允许玩家攻击输入
        /// </summary>
        /// <returns></returns>
        public bool GetAllowAttackInput() => allowAttackInput;

        /// <summary>
        /// 设置是否允许玩家输入攻击信号 
        /// </summary>
        /// <param name="allow"></param>
        public void SetAllowAttackInput(bool allow) => allowAttackInput = allow;
        
        #endregion

        protected override void OnAnimationAttackEvent(string hitName)
        {
            base.OnAnimationAttackEvent(hitName);
            SpawnAttackVfx();
        }

        private void SpawnAttackVfx()
        {
            Transform spawnSource = attackVfxSpawnPoint != null ? attackVfxSpawnPoint : attackDetectionCenter;
            if (spawnSource == null)
            {
                spawnSource = transform;
            }

            if (TrySpawnMappedAttackVfx(spawnSource))
            {
                return;
            }

            SpawnAttackVfxInstance(spawnSource, attackVfxPrefab, attackVfxOffset, attackVfxRotationOffset, attackVfxLifetime);
        }

        private bool TrySpawnMappedAttackVfx(Transform spawnSource)
        {
            if (attackVfxMappings == null || attackVfxMappings.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < attackVfxMappings.Length; i++)
            {
                AttackVfxMapping mapping = attackVfxMappings[i];
                if (mapping == null || string.IsNullOrEmpty(mapping.stateName) || mapping.vfxPrefab == null)
                {
                    continue;
                }

                if (!_animator.CheckAnimationName(mapping.stateName))
                {
                    continue;
                }

                SpawnAttackVfxInstance(spawnSource, mapping.vfxPrefab, mapping.positionOffset, mapping.rotationOffset, mapping.lifetime);
                return true;
            }

            return false;
        }

        private void SpawnAttackVfxInstance(Transform spawnSource, GameObject vfxPrefab, Vector3 positionOffset, Vector3 rotationOffset, float lifetime)
        {
            if (vfxPrefab == null)
            {
                return;
            }

            Vector3 spawnPosition = spawnSource.position + transform.root.TransformDirection(positionOffset);
            Quaternion spawnRotation = Quaternion.LookRotation(transform.root.forward, Vector3.up) * Quaternion.Euler(rotationOffset);

            GameObject effectInstance = Instantiate(vfxPrefab, spawnPosition, spawnRotation);
            DisableUnsupportedUrpChildren(effectInstance.transform);
            ApplyAttackVfxTint(effectInstance);
            Destroy(effectInstance, lifetime);
        }

        private void ApplyAttackVfxTint(GameObject effectInstance)
        {
            if (effectInstance == null)
            {
                return;
            }

            ParticleSystem[] particleSystems = effectInstance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem.MainModule mainModule = particleSystems[i].main;
                ParticleSystem.MinMaxGradient startColor = mainModule.startColor;
                startColor.color = RemapColorToTint(startColor.color, attackVfxTintColor);
                mainModule.startColor = startColor;
            }

            Renderer[] renderers = effectInstance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                Renderer currentRenderer = renderers[i];
                currentRenderer.GetPropertyBlock(propertyBlock);

                Material[] sharedMaterials = currentRenderer.sharedMaterials;
                bool hasColorOverride = false;
                for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                {
                    Material sharedMaterial = sharedMaterials[materialIndex];
                    if (sharedMaterial == null)
                    {
                        continue;
                    }

                    if (sharedMaterial.HasProperty("_Color"))
                    {
                        propertyBlock.SetColor("_Color", RemapColorToTint(sharedMaterial.GetColor("_Color"), attackVfxTintColor));
                        hasColorOverride = true;
                    }

                    if (sharedMaterial.HasProperty("_TintColor"))
                    {
                        propertyBlock.SetColor("_TintColor", RemapColorToTint(sharedMaterial.GetColor("_TintColor"), attackVfxTintColor));
                        hasColorOverride = true;
                    }

                    if (sharedMaterial.HasProperty("_BaseColor"))
                    {
                        propertyBlock.SetColor("_BaseColor", RemapColorToTint(sharedMaterial.GetColor("_BaseColor"), attackVfxTintColor));
                        hasColorOverride = true;
                    }
                }

                if (hasColorOverride)
                {
                    currentRenderer.SetPropertyBlock(propertyBlock);
                }
            }
        }

        private static Color RemapColorToTint(Color source, Color tint)
        {
            float luminance = source.grayscale;
            float brightness = Mathf.Lerp(0.45f, 1.35f, luminance);

            return new Color(
                Mathf.Clamp01(tint.r * brightness),
                Mathf.Clamp01(tint.g * brightness),
                Mathf.Clamp01(tint.b * brightness),
                source.a * tint.a);
        }

        private static void DisableUnsupportedUrpChildren(Transform root)
        {
            if (root.name.Contains("Distortion"))
            {
                root.gameObject.SetActive(false);
                return;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                DisableUnsupportedUrpChildren(root.GetChild(i));
            }
        }
    }
}

