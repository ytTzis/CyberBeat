using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UGG.Health
{
    public class PlayerHealthSystem : CharacterHealthSystemBase
    {
        private const string DefaultDeathAnimation = "GhostSamurai_APose_Die01_Inplace";
        private const string DefaultDeathAnimationPath = "Assets/GameAssets/GreatSword_Animset/Animation/katana/APose/Die/Inplace/GhostSamurai_APose_Die01_Inplace.FBX";
        private static readonly string[] UnparryableEnemySkillStateNames =
        {
            "Attack03_4",
            "SPAttack02",
            "SPAttack03"
        };

        [Header("Player HP")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;
        [SerializeField, Header("受击锁定攻击者结束时间(0-1)")] [Range(0f, 1f)] private float hitLockReleaseNormalizedTime = 0.35f;
        [SerializeField, Header("Recovering 每秒回血"), Min(0f)] private float recoveringHealPerSecond = 2.5f;
        [SerializeField, Header("Recovering 受伤倍率"), Range(0f, 1f)] private float recoveringDamageMultiplier = 0.85f;
        [SerializeField, Header("Recovering 脚下脉冲圈")] private bool showRecoveringPulseRing = true;
        [SerializeField] private Color recoveringPulseRingColor = new Color(0.2f, 0.95f, 1f, 0.8f);
        [SerializeField, Min(0.1f)] private float recoveringPulseRingRadius = 0.9f;
        [SerializeField, Min(0.01f)] private float recoveringPulseRingWidth = 0.08f;
        [SerializeField] private float recoveringPulseRingYOffset = 0.06f;
        [SerializeField, Min(0.1f)] private float recoveringPulseRingPulseSpeed = 1.8f;
        [SerializeField, Range(0f, 1f)] private float recoveringPulseRingScaleAmplitude = 0.16f;
        [SerializeField, Range(0f, 1f)] private float recoveringPulseRingPulseAlpha = 0.35f;
        [SerializeField, Min(0.1f)] private float recoveringPulseRingFadeSpeed = 5f;
        [SerializeField, Header("HighStress 脚下脉冲圈")] private bool showHighStressPulseRing = true;
        [SerializeField] private Color highStressPulseRingColor = new Color(1f, 0.2f, 0.18f, 0.85f);
        [SerializeField, Min(0.1f)] private float highStressPulseRingRadius = 1f;
        [SerializeField, Min(0.01f)] private float highStressPulseRingWidth = 0.1f;
        [SerializeField] private float highStressPulseRingYOffset = 0.07f;
        [SerializeField, Min(0.1f)] private float highStressPulseRingPulseSpeed = 2.2f;
        [SerializeField, Range(0f, 1f)] private float highStressPulseRingScaleAmplitude = 0.22f;
        [SerializeField, Range(0f, 1f)] private float highStressPulseRingPulseAlpha = 0.45f;
        [SerializeField, Min(0.1f)] private float highStressPulseRingFadeSpeed = 6f;

        private bool canExecute = false;
        private UGG.Move.PlayerMovementController playerMovementController;
        private HeartRateStateController heartRateStateController;
        private GameObject recoveringPulseRingObject;
        private LineRenderer recoveringPulseRingRenderer;
        private Material recoveringPulseRingMaterial;
        private float recoveringPulseRingVisibility;
        private GameObject highStressPulseRingObject;
        private LineRenderer highStressPulseRingRenderer;
        private Material highStressPulseRingMaterial;
        private float highStressPulseRingVisibility;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float HealthNormalized => maxHealth <= 0f ? 0f : currentHealth / maxHealth;

        protected override void Awake()
        {
            base.Awake();
            playerMovementController = GetComponent<UGG.Move.PlayerMovementController>();
            heartRateStateController = HeartRateStateController.Instance;
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
            CreateRecoveringPulseRing();
            CreateHighStressPulseRing();

            if (string.IsNullOrEmpty(deathAnimationName))
            {
                deathAnimationName = DefaultDeathAnimation;
            }

            TryAssignDefaultDeathClip();
        }

        protected override void Update()
        {
            base.Update();

            TryAutoBindHeartRateStateController();
            ApplyRecoveringEffects();
            UpdateRecoveringPulseRing();
            UpdateHighStressPulseRing();
            OnHitLockTarget();
        }

        public override void TakeDamager(float damagar, string hitAnimationName, Transform attacker)
        {
            if (IsDead())
            {
                return;
            }

            if (playerMovementController != null && playerMovementController.IsDodgeInvulnerable())
            {
                return;
            }

            SetAttacker(attacker);

            if (CanParry() && !IsUnparryableEnemyAttack(attacker))
            {
                Parry(hitAnimationName);
            }
            else
            {
                ApplyDamage(damagar);

                if (currentHealth <= 0f)
                {
                    Die();
                    return;
                }

                _animator.Play(hitAnimationName, 0, 0f);
                GameAssets.Instance.PlaySoundEffect(_audioSource, SoundAssetsType.hit);
            }
        }

        private bool IsUnparryableEnemyAttack(Transform attacker)
        {
            if (attacker == null)
            {
                return false;
            }

            Animator attackerAnimator = attacker.GetComponentInChildren<Animator>();
            if (attackerAnimator == null)
            {
                return false;
            }

            AnimatorStateInfo currentStateInfo = attackerAnimator.GetCurrentAnimatorStateInfo(0);
            return MatchesUnparryableSkillState(currentStateInfo) ||
                   (attackerAnimator.IsInTransition(0) &&
                    MatchesUnparryableSkillState(attackerAnimator.GetNextAnimatorStateInfo(0)));
        }

        private static bool MatchesUnparryableSkillState(AnimatorStateInfo stateInfo)
        {
            for (int i = 0; i < UnparryableEnemySkillStateNames.Length; i++)
            {
                if (stateInfo.IsName(UnparryableEnemySkillStateNames[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public void RestoreFullHealth()
        {
            currentHealth = maxHealth;
        }

        private void TryAutoBindHeartRateStateController()
        {
            if (heartRateStateController == null)
            {
                heartRateStateController = HeartRateStateController.Instance;
            }
        }

        private void ApplyRecoveringEffects()
        {
            if (IsDead())
            {
                return;
            }

            if (!IsInRecoveringState())
            {
                return;
            }

            Heal(recoveringHealPerSecond * Time.deltaTime);
        }

        private void CreateRecoveringPulseRing()
        {
            if (!showRecoveringPulseRing || recoveringPulseRingObject != null)
            {
                return;
            }

            recoveringPulseRingObject = new GameObject("RecoveringPulseRing");
            recoveringPulseRingObject.transform.SetParent(transform, false);
            recoveringPulseRingObject.transform.localPosition = new Vector3(0f, recoveringPulseRingYOffset, 0f);

            recoveringPulseRingRenderer = recoveringPulseRingObject.AddComponent<LineRenderer>();
            recoveringPulseRingRenderer.useWorldSpace = false;
            recoveringPulseRingRenderer.loop = true;
            recoveringPulseRingRenderer.positionCount = 64;
            recoveringPulseRingRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            recoveringPulseRingRenderer.receiveShadows = false;
            recoveringPulseRingRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            recoveringPulseRingRenderer.textureMode = LineTextureMode.Stretch;
            recoveringPulseRingRenderer.alignment = LineAlignment.View;
            recoveringPulseRingRenderer.numCapVertices = 8;
            recoveringPulseRingRenderer.numCornerVertices = 8;
            recoveringPulseRingRenderer.startWidth = recoveringPulseRingWidth;
            recoveringPulseRingRenderer.endWidth = recoveringPulseRingWidth;

            Shader ringShader = Shader.Find("Sprites/Default");
            if (ringShader != null)
            {
                recoveringPulseRingMaterial = new Material(ringShader);
                recoveringPulseRingRenderer.material = recoveringPulseRingMaterial;
            }

            UpdateRecoveringPulseRingShape();
            recoveringPulseRingObject.SetActive(false);
        }

        private void UpdateRecoveringPulseRingShape()
        {
            if (recoveringPulseRingRenderer == null)
            {
                return;
            }

            int segmentCount = recoveringPulseRingRenderer.positionCount;
            float angleStep = Mathf.PI * 2f / segmentCount;

            for (int i = 0; i < segmentCount; i++)
            {
                float angle = angleStep * i;
                recoveringPulseRingRenderer.SetPosition(
                    i,
                    new Vector3(Mathf.Cos(angle) * recoveringPulseRingRadius, 0f, Mathf.Sin(angle) * recoveringPulseRingRadius));
            }
        }

        private void UpdateRecoveringPulseRing()
        {
            if (recoveringPulseRingObject == null || recoveringPulseRingRenderer == null)
            {
                return;
            }

            float targetVisibility = !IsDead() && IsInRecoveringState() ? 1f : 0f;
            recoveringPulseRingVisibility = Mathf.MoveTowards(
                recoveringPulseRingVisibility,
                targetVisibility,
                Time.deltaTime * recoveringPulseRingFadeSpeed);

            if (recoveringPulseRingVisibility <= 0.001f)
            {
                if (recoveringPulseRingObject.activeSelf)
                {
                    recoveringPulseRingObject.SetActive(false);
                }

                return;
            }

            if (!recoveringPulseRingObject.activeSelf)
            {
                recoveringPulseRingObject.SetActive(true);
            }

            recoveringPulseRingObject.transform.localPosition = new Vector3(0f, recoveringPulseRingYOffset, 0f);

            float pulse = (Mathf.Sin(Time.time * recoveringPulseRingPulseSpeed) + 1f) * 0.5f;
            float scale = 1f + pulse * recoveringPulseRingScaleAmplitude;
            recoveringPulseRingObject.transform.localScale = new Vector3(scale, 1f, scale);

            Color ringColor = recoveringPulseRingColor;
            ringColor.a *= recoveringPulseRingVisibility * Mathf.Lerp(1f - recoveringPulseRingPulseAlpha, 1f, pulse);
            recoveringPulseRingRenderer.startColor = ringColor;
            recoveringPulseRingRenderer.endColor = ringColor;

            if (recoveringPulseRingMaterial != null)
            {
                recoveringPulseRingMaterial.color = ringColor;
            }
        }

        private void CreateHighStressPulseRing()
        {
            if (!showHighStressPulseRing || highStressPulseRingObject != null)
            {
                return;
            }

            highStressPulseRingObject = new GameObject("HighStressPulseRing");
            highStressPulseRingObject.transform.SetParent(transform, false);
            highStressPulseRingObject.transform.localPosition = new Vector3(0f, highStressPulseRingYOffset, 0f);

            highStressPulseRingRenderer = highStressPulseRingObject.AddComponent<LineRenderer>();
            highStressPulseRingRenderer.useWorldSpace = false;
            highStressPulseRingRenderer.loop = true;
            highStressPulseRingRenderer.positionCount = 64;
            highStressPulseRingRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            highStressPulseRingRenderer.receiveShadows = false;
            highStressPulseRingRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            highStressPulseRingRenderer.textureMode = LineTextureMode.Stretch;
            highStressPulseRingRenderer.alignment = LineAlignment.View;
            highStressPulseRingRenderer.numCapVertices = 8;
            highStressPulseRingRenderer.numCornerVertices = 8;
            highStressPulseRingRenderer.startWidth = highStressPulseRingWidth;
            highStressPulseRingRenderer.endWidth = highStressPulseRingWidth;

            Shader ringShader = Shader.Find("Sprites/Default");
            if (ringShader != null)
            {
                highStressPulseRingMaterial = new Material(ringShader);
                highStressPulseRingRenderer.material = highStressPulseRingMaterial;
            }

            UpdateHighStressPulseRingShape();
            highStressPulseRingObject.SetActive(false);
        }

        private void UpdateHighStressPulseRingShape()
        {
            if (highStressPulseRingRenderer == null)
            {
                return;
            }

            int segmentCount = highStressPulseRingRenderer.positionCount;
            float angleStep = Mathf.PI * 2f / segmentCount;

            for (int i = 0; i < segmentCount; i++)
            {
                float angle = angleStep * i;
                highStressPulseRingRenderer.SetPosition(
                    i,
                    new Vector3(Mathf.Cos(angle) * highStressPulseRingRadius, 0f, Mathf.Sin(angle) * highStressPulseRingRadius));
            }
        }

        private void UpdateHighStressPulseRing()
        {
            if (highStressPulseRingObject == null || highStressPulseRingRenderer == null)
            {
                return;
            }

            float targetVisibility = !IsDead() && IsInHighStressState() ? 1f : 0f;
            highStressPulseRingVisibility = Mathf.MoveTowards(
                highStressPulseRingVisibility,
                targetVisibility,
                Time.deltaTime * highStressPulseRingFadeSpeed);

            if (highStressPulseRingVisibility <= 0.001f)
            {
                if (highStressPulseRingObject.activeSelf)
                {
                    highStressPulseRingObject.SetActive(false);
                }

                return;
            }

            if (!highStressPulseRingObject.activeSelf)
            {
                highStressPulseRingObject.SetActive(true);
            }

            highStressPulseRingObject.transform.localPosition = new Vector3(0f, highStressPulseRingYOffset, 0f);

            float pulse = (Mathf.Sin(Time.time * highStressPulseRingPulseSpeed) + 1f) * 0.5f;
            float scale = 1f + pulse * highStressPulseRingScaleAmplitude;
            highStressPulseRingObject.transform.localScale = new Vector3(scale, 1f, scale);

            Color ringColor = highStressPulseRingColor;
            ringColor.a *= highStressPulseRingVisibility * Mathf.Lerp(1f - highStressPulseRingPulseAlpha, 1f, pulse);
            highStressPulseRingRenderer.startColor = ringColor;
            highStressPulseRingRenderer.endColor = ringColor;

            if (highStressPulseRingMaterial != null)
            {
                highStressPulseRingMaterial.color = ringColor;
            }
        }

        #region Parry

        private bool CanParry()
        {
            if (_animator.CheckAnimationTag("Parry")) return true;
            if (_animator.CheckAnimationTag("ParryHit")) return true;

            return false;
        }

        private void Parry(string hitName)
        {
            if (!CanParry()) return;

            switch (hitName)
            {
                default:
                    _animator.Play(hitName, 0, 0f);
                    GameAssets.Instance.PlaySoundEffect(_audioSource, SoundAssetsType.hit);
                    break;
                case "Hit_D_Up":
                    //_animator.Play("ParryF", 0, 0f);
                    //GameAssets.Instance.PlaySoundEffect(_audioSource, SoundAssetsType.parry);

                    if(currentAttacker.TryGetComponent(out CharacterHealthSystemBase health))
                    {
                        health.FlickWeapon("Flick_0");
                        GameAssets.Instance.PlaySoundEffect(_audioSource, SoundAssetsType.parry);
                    }

                    canExecute = true;

                    //游戏时间缓慢 给玩家处决反应时间
                    Time.timeScale = 0.25f;
                    GameObjectPoolSystem.Instance.TakeGameObject("Timer").GetComponent<Timer>().CreateTime(0.25f, () =>
                    {
                        canExecute = false;

                        if (Time.timeScale < 1f)
                        {
                            Time.timeScale = 1f;
                        }
                    }, false);
                    break;
                case "Hit_H_Right":
                    _animator.Play("ParryL", 0, 0f);
                    GameAssets.Instance.PlaySoundEffect(_audioSource, SoundAssetsType.parry);
                    break;
            }
        }

        #endregion

        #region Hit

        private bool CanHitLockAttacker()
        {
            return true;
        }

        private void OnHitLockTarget()
        {
            //检测当前动画是否处于受伤状态
            bool isHitLocked = _animator.CheckAnimationTag("Hit") &&
                               !_animator.CheckCurrentTagAnimationTimeIsExceed("Hit", hitLockReleaseNormalizedTime);
            bool isParryHitLocked = _animator.CheckAnimationTag("ParryHit");

            if (isHitLocked || isParryHitLocked)
            {
                transform.rotation = transform.LockOnTarget(currentAttacker, transform, 50f);
            }
        }

        private void ApplyDamage(float damage)
        {
            if (damage <= 0f)
            {
                return;
            }

            if (IsInRecoveringState())
            {
                damage *= recoveringDamageMultiplier;
            }

            currentHealth = Mathf.Clamp(currentHealth - damage, 0f, maxHealth);

            if (currentHealth <= 0f)
            {
                currentHealth = 0f;
            }
        }

        private void Heal(float amount)
        {
            if (amount <= 0f || currentHealth >= maxHealth)
            {
                return;
            }

            currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
        }

        private bool IsInRecoveringState()
        {
            return heartRateStateController != null &&
                   heartRateStateController.CurrentState == HeartRateStateController.HeartRateState.Recovering;
        }

        private bool IsInHighStressState()
        {
            return heartRateStateController != null &&
                   heartRateStateController.CurrentState == HeartRateStateController.HeartRateState.HighStress;
        }

        #endregion

        public bool GetCanExecute() => canExecute;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(deathAnimationName))
            {
                deathAnimationName = DefaultDeathAnimation;
            }

            TryAssignDefaultDeathClip();

            if (recoveringPulseRingRenderer != null)
            {
                recoveringPulseRingRenderer.startWidth = recoveringPulseRingWidth;
                recoveringPulseRingRenderer.endWidth = recoveringPulseRingWidth;
                UpdateRecoveringPulseRingShape();
            }

            if (highStressPulseRingRenderer != null)
            {
                highStressPulseRingRenderer.startWidth = highStressPulseRingWidth;
                highStressPulseRingRenderer.endWidth = highStressPulseRingWidth;
                UpdateHighStressPulseRingShape();
            }
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

        private void OnDestroy()
        {
            if (recoveringPulseRingMaterial != null)
            {
                Destroy(recoveringPulseRingMaterial);
                recoveringPulseRingMaterial = null;
            }

            if (highStressPulseRingMaterial != null)
            {
                Destroy(highStressPulseRingMaterial);
                highStressPulseRingMaterial = null;
            }
        }
    }
}

