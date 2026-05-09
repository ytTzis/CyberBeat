using System.Collections;
using System.Collections.Generic;
using UGG.Health;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UGG.Combat
{
    public class HighStressSkillController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform skillOrigin;
        [SerializeField] private LayerMask enemyLayer;
        [SerializeField] private string skill1HitAnimationName = "Hit_D_Up";
        [SerializeField] private string dashSlashHitAnimationName = "Hit_H_Right";

        [Header("Skill 1 - Air Combo")]
        [SerializeField] private KeyCode skill1Key = KeyCode.Alpha1;
        [SerializeField, Min(0f)] private float skill1Cooldown = 3f;
        [SerializeField, Min(0f)] private float skill1HeartRateCost = 6f;
        [SerializeField] private string skill1AnimatorTriggerName = "HighStressSkill1";
        [SerializeField] private string skill1AnimatorStateName = "HighStressSkill1";
        [SerializeField] private string skill1AnimationStateName = "GhostSamurai_APose_Air_Attack02_Inplace";
        [SerializeField] private SoundAssetsType skill1FirstHitSound = SoundAssetsType.swordWave;
        [SerializeField, Range(0f, 1.5f)] private float skill1FirstHitSoundDelay = 0.24f;
        [SerializeField, Range(0f, 1.5f)] private float skill1SecondHitSoundDelay = 0.7f;
        [SerializeField, Range(0f, 2f)] private float skill1FirstHitSoundVolume = 0.95f;
        [SerializeField, Min(0f)] private float skill1FirstHitDamage = 12f;
        [SerializeField, Min(0f)] private float skill1SecondHitDamage = 16f;
        [SerializeField, Min(0.1f)] private float skill1Radius = 2.4f;
        [SerializeField] private Vector3 skill1CenterOffset = new Vector3(0f, 1.1f, 1.05f);
        [SerializeField, Min(0f)] private float skill1LiftHeight = 0.1f;
        [SerializeField, Min(0f)] private float skill1ForwardStep = 0.75f;
        [SerializeField, Min(0.01f)] private float skill1LiftDuration = 0.12f;
        [SerializeField, Min(0f)] private float skill1AnimatorTransitionDuration = 0.05f;
        [SerializeField, Min(0f)] private float skill1StateEnterTimeout = 0.12f;
        [SerializeField, Min(0f)] private float skill1FirstHitDelay = 0.16f;
        [SerializeField, Min(0f)] private float skill1SecondHitDelay = 0.42f;

        [Header("Skill 2 - Dash Slash")]
        [SerializeField] private KeyCode skill2Key = KeyCode.Alpha2;
        [SerializeField, Min(0f)] private float skill2Cooldown = 4.5f;
        [SerializeField, Min(0f)] private float skill2HeartRateCost = 8f;
        [SerializeField, Min(0f)] private float skill2Damage = 26f;
        [SerializeField, Min(0f)] private float skill2DashDistance = 2.5f;
        [SerializeField, Min(0.1f)] private float skill2DamageRadius = 1.75f;
        [SerializeField] private Vector3 skill2CenterOffset = new Vector3(0f, 0.8f, 0f);

        private readonly Collider[] overlapResults = new Collider[12];
        private readonly HashSet<IDamagar> hitTargets = new HashSet<IDamagar>();

        private CharacterController characterController;
        private Animator animator;
        private AudioSource audioSource;
        private PlayerHealthSystem playerHealthSystem;
        private PlayerCombatSystem playerCombatSystem;
        private int skill1TriggerId;
        private int skill1AnimatorStateHash;
        private float nextSkill1ReadyTime;
        private float nextSkill2ReadyTime;
        private Coroutine skill1Routine;

        public bool IsSkill1Active { get; private set; }
        public bool BlocksStandardActions => IsSkill1Active;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            animator = GetComponentInChildren<Animator>();
            audioSource = GetComponentInChildren<AudioSource>();
            playerHealthSystem = GetComponent<PlayerHealthSystem>();
            playerCombatSystem = GetComponentInChildren<PlayerCombatSystem>();
            skill1TriggerId = string.IsNullOrEmpty(skill1AnimatorTriggerName) ? 0 : Animator.StringToHash(skill1AnimatorTriggerName);
            skill1AnimatorStateHash = string.IsNullOrEmpty(skill1AnimatorStateName) ? 0 : Animator.StringToHash(skill1AnimatorStateName);

            if (skillOrigin == null)
            {
                skillOrigin = transform;
            }

            AutoAssignEnemyLayerIfNeeded();
        }

        private void Update()
        {
            if (!CanUseHighStressSkills())
            {
                return;
            }

            if (WasSkillKeyPressedThisFrame(skill1Key))
            {
                TryCastAirCombo();
            }

            if (WasSkillKeyPressedThisFrame(skill2Key))
            {
                TryCastDashSlash();
            }
        }

        private bool CanUseHighStressSkills()
        {
            if (playerHealthSystem != null && playerHealthSystem.IsDead())
            {
                return false;
            }

            HeartRateStateController stateController = HeartRateStateController.Instance;
            if (stateController == null)
            {
                return false;
            }

            if (stateController.CurrentState != HeartRateStateController.HeartRateState.HighStress)
            {
                return false;
            }

            HeartRateSimulator simulator = HeartRateSimulator.Instance;
            if (simulator == null)
            {
                return false;
            }

            if (simulator.HasTemporaryHeartRateOverride())
            {
                return true;
            }

            float normalBoundaryHeartRate = stateController.GetNormalStateBoundaryHeartRate();
            if (normalBoundaryHeartRate <= 0f)
            {
                return true;
            }

            return simulator.currentHeartRate > normalBoundaryHeartRate;
        }

        private void TryCastAirCombo()
        {
            if (Time.time < nextSkill1ReadyTime)
            {
                return;
            }

            if (!TryConsumeHeartRate(skill1HeartRateCost))
            {
                return;
            }

            nextSkill1ReadyTime = Time.time + skill1Cooldown;

            if (skill1Routine != null)
            {
                StopCoroutine(skill1Routine);
            }

            IsSkill1Active = true;
            skill1Routine = StartCoroutine(PlaySkill1AirCombo());
        }

        private void TryCastDashSlash()
        {
            if (Time.time < nextSkill2ReadyTime)
            {
                return;
            }

            if (!TryConsumeHeartRate(skill2HeartRateCost))
            {
                return;
            }

            DashForward(skill2DashDistance);

            Vector3 center = skillOrigin.position + transform.forward * skill2DamageRadius + skill2CenterOffset;
            DealDamageInSphere(center, skill2DamageRadius, skill2Damage, dashSlashHitAnimationName);
            nextSkill2ReadyTime = Time.time + skill2Cooldown;
        }

        private bool TryConsumeHeartRate(float amount)
        {
            HeartRateSimulator simulator = HeartRateSimulator.Instance;
            if (simulator == null)
            {
                return false;
            }

            return simulator.ConsumeHeartRate(amount);
        }

        private IEnumerator PlaySkill1AirCombo()
        {
            bool enteredSkillState = false;

            if (animator != null && skill1TriggerId != 0)
            {
                animator.ResetTrigger(skill1TriggerId);
                animator.SetTrigger(skill1TriggerId);
                yield return null;

                enteredSkillState = IsPlayingSkill1State();
                if (!enteredSkillState && !string.IsNullOrEmpty(skill1AnimatorStateName))
                {
                    animator.CrossFadeInFixedTime(skill1AnimatorStateName, skill1AnimatorTransitionDuration, 0, 0f);
                }
            }
            else if (animator != null && !string.IsNullOrEmpty(skill1AnimationStateName))
            {
                animator.Play(skill1AnimationStateName, 0, 0f);
                enteredSkillState = true;
            }

            if (animator != null && !enteredSkillState)
            {
                float elapsed = 0f;
                while (elapsed < skill1StateEnterTimeout)
                {
                    if (IsPlayingSkill1State())
                    {
                        enteredSkillState = true;
                        break;
                    }

                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            if (!enteredSkillState && animator != null)
            {
                EndSkill1Lock();
                yield break;
            }

            yield return SmoothLiftAndStep(skill1LiftHeight, skill1ForwardStep, skill1LiftDuration);

            yield return ResolveSkill1HitPhase(
                skill1FirstHitDelay,
                skill1FirstHitSoundDelay,
                skill1FirstHitSound,
                skill1FirstHitSoundVolume,
                skill1FirstHitDamage,
                transform.forward * 0.75f + skill1CenterOffset,
                skill1HitAnimationName);

            float secondHitDelayFromNow = Mathf.Max(0f, skill1SecondHitDelay - skill1FirstHitDelay);
            float secondSoundDelayFromNow = Mathf.Max(0f, skill1SecondHitSoundDelay - skill1FirstHitDelay);
            yield return ResolveSkill1HitPhase(
                secondHitDelayFromNow,
                secondSoundDelayFromNow,
                skill1FirstHitSound,
                skill1FirstHitSoundVolume,
                skill1SecondHitDamage,
                transform.forward * 1.1f + skill1CenterOffset,
                skill1HitAnimationName);

            if (animator != null)
            {
                while (IsPlayingSkill1State())
                {
                    yield return null;
                }
            }

            EndSkill1Lock();
        }

        private bool IsPlayingSkill1State()
        {
            if (animator == null)
            {
                return false;
            }

            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            if (MatchesSkill1State(currentState))
            {
                return true;
            }

            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
                return MatchesSkill1State(nextState);
            }

            return false;
        }

        private bool MatchesSkill1State(AnimatorStateInfo stateInfo)
        {
            if (skill1AnimatorStateHash != 0 && stateInfo.shortNameHash == skill1AnimatorStateHash)
            {
                return true;
            }

            return !string.IsNullOrEmpty(skill1AnimatorStateName) && stateInfo.IsName(skill1AnimatorStateName);
        }

        private void DashForward(float distance)
        {
            if (distance <= 0f)
            {
                return;
            }

            Vector3 displacement = transform.forward * distance;
            if (characterController != null && characterController.enabled)
            {
                characterController.Move(displacement);
                return;
            }

            transform.position += displacement;
        }

        private IEnumerator SmoothLiftAndStep(float liftHeight, float forwardDistance, float duration)
        {
            if (duration <= 0f)
            {
                ApplyDisplacement(Vector3.up * liftHeight + transform.forward * forwardDistance);
                yield break;
            }

            Vector3 totalDisplacement = Vector3.up * liftHeight + transform.forward * forwardDistance;
            Vector3 appliedDisplacement = Vector3.zero;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float easedProgress = Mathf.SmoothStep(0f, 1f, normalizedTime);
                Vector3 targetDisplacement = totalDisplacement * easedProgress;
                Vector3 frameDisplacement = targetDisplacement - appliedDisplacement;

                ApplyDisplacement(frameDisplacement);
                appliedDisplacement = targetDisplacement;

                yield return null;
            }
        }

        private void ApplyDisplacement(Vector3 displacement)
        {
            if (characterController != null && characterController.enabled)
            {
                characterController.Move(displacement);
                return;
            }

            transform.position += displacement;
        }

        private void DealDamageInSphere(Vector3 center, float radius, float damage, string hitAnimationName)
        {
            if (damage <= 0f || radius <= 0f)
            {
                return;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(center, radius, overlapResults, enemyLayer);
            if (hitCount <= 0)
            {
                return;
            }

            hitTargets.Clear();

            for (int i = 0; i < hitCount; i++)
            {
                Collider currentCollider = overlapResults[i];
                if (currentCollider == null)
                {
                    continue;
                }

                IDamagar damagar = currentCollider.GetComponentInParent<IDamagar>();
                if (damagar == null || !hitTargets.Add(damagar))
                {
                    continue;
                }

                damagar.TakeDamager(damage, hitAnimationName, transform);
            }
        }

        private IEnumerator ResolveSkill1HitPhase(
            float hitDelay,
            float soundDelay,
            SoundAssetsType soundType,
            float soundVolume,
            float damage,
            Vector3 centerOffset,
            string hitAnimationName)
        {
            float elapsed = 0f;
            bool soundPlayed = false;
            bool hitResolved = false;
            float targetDuration = hitDelay;

            while (!soundPlayed || !hitResolved)
            {
                if (!soundPlayed && elapsed >= hitDelay)
                {
                    PlaySkillSound(soundType, soundVolume);
                    soundPlayed = true;
                }

                if (!hitResolved && elapsed >= hitDelay)
                {
                    Vector3 hitCenter = skillOrigin.position + centerOffset;
                    DealDamageInSphere(hitCenter, skill1Radius, damage, hitAnimationName);
                    hitResolved = true;
                }

                if (soundPlayed && hitResolved)
                {
                    yield break;
                }

                yield return null;
                elapsed += Time.deltaTime;

                if (elapsed > targetDuration + 0.25f)
                {
                    break;
                }
            }
        }

        private void PlaySkillSound(SoundAssetsType soundType, float volume)
        {
            if (GameAssets.Instance == null || audioSource == null)
            {
                return;
            }

            GameAssets.Instance.PlaySoundEffectOneShot(audioSource, soundType, volume);
        }

        private static bool WasSkillKeyPressedThisFrame(KeyCode key)
        {
            if (Keyboard.current == null)
            {
                return false;
            }

            return key switch
            {
                KeyCode.Alpha1 => Keyboard.current.digit1Key.wasPressedThisFrame,
                KeyCode.Alpha2 => Keyboard.current.digit2Key.wasPressedThisFrame,
                _ => false
            };
        }

        private void OnDrawGizmosSelected()
        {
            Transform origin = skillOrigin != null ? skillOrigin : transform;

            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.45f);
            Gizmos.DrawWireSphere(origin.position + origin.forward * 0.75f + skill1CenterOffset, skill1Radius);
            Gizmos.DrawWireSphere(origin.position + origin.forward * 1.1f + skill1CenterOffset, skill1Radius);

            Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.45f);
            Gizmos.DrawWireSphere(origin.position + transform.forward * skill2DamageRadius + skill2CenterOffset, skill2DamageRadius);
        }

        private void OnDisable()
        {
            EndSkill1Lock();
        }

        private void EndSkill1Lock()
        {
            IsSkill1Active = false;
            skill1Routine = null;
        }

        private void OnValidate()
        {
            AutoAssignEnemyLayerIfNeeded();
        }

        private void AutoAssignEnemyLayerIfNeeded()
        {
            if (enemyLayer.value != 0)
            {
                return;
            }

            if (playerCombatSystem == null)
            {
                playerCombatSystem = GetComponentInChildren<PlayerCombatSystem>();
            }

            if (playerCombatSystem != null)
            {
                enemyLayer = playerCombatSystem.GetEnemyLayerMask();
            }

            if (enemyLayer.value == 0)
            {
                enemyLayer = 1 << 7;
            }
        }
    }
}
