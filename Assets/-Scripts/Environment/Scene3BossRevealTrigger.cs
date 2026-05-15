using System.Collections;
using UGG.Combat;
using UGG.Health;
using UGG.Move;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class Scene3BossRevealTrigger : MonoBehaviour
{
    [Header("Scene Filter")]
    [SerializeField] private bool restrictToScene = true;
    [SerializeField] private string sceneName = "3_GameScene";
    [SerializeField] private string playerTag = "Player";

    [Header("Boss")]
    [SerializeField] private GameObject enemy2Root;
    [SerializeField] private bool disableEnemyCombatUntilTriggered = true;
    [SerializeField] private bool triggerOnlyOnce = true;

    [Header("Reveal Focus Shot")]
    [SerializeField] private SceneIntroCameraTransition cameraTransition;
    [SerializeField] private bool playRevealFocusShot = true;
    [SerializeField] private float focusLookHeight = 1.5f;
    [SerializeField] private Vector3 focusStartOffset = new Vector3(0.5f, 1.9f, -5.8f);
    [SerializeField] private Vector3 focusEndOffset = new Vector3(0.15f, 1.25f, -3.9f);
    [SerializeField] private float focusOrbitAngle = 8f;
    [SerializeField] private float focusDuration = 1.2f;
    [SerializeField] private float focusHoldDuration = 0.35f;
    [SerializeField] private bool focusUseTargetRotation = true;
    [SerializeField] private bool focusInvertTargetRotation;
    [SerializeField] private AnimationCurve focusTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Jump Down")]
    [SerializeField] private Transform landingPoint;
    [SerializeField] private Vector3 landingOffset = new Vector3(0f, 0f, -1.4f);
    [SerializeField, Min(0f)] private float jumpStartDelay = 0.2f;
    [SerializeField, Min(0.05f)] private float jumpDuration = 1.1f;
    [SerializeField, Min(0f)] private float jumpArcHeight = 2.6f;
    [SerializeField] private string jumpAnimatorStateName = "GS12";
    [SerializeField] private bool rotateTowardLandingPoint = true;
    [SerializeField] private bool enableCombatAfterLanding = true;

    private CharacterHealthSystemBase enemyHealthSystem;
    private AICombatSystem enemyCombatSystem;
    private AI2Movement enemyMovement;
    private CharacterController enemyCharacterController;
    private Animator enemyAnimator;
    private Transform enemyMover;
    private Transform playerTransform;
    private Transform revealFocusAnchor;
    private bool hasTriggered;
    private bool originalCharacterControllerEnabled;
    private bool originalMovementEnabled;

    private void Awake()
    {
        Collider triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;

        CacheReferences();

        if (disableEnemyCombatUntilTriggered)
        {
            SetEnemyCombatEnabled(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsSceneAllowed() || !IsPlayerCollider(other))
        {
            return;
        }

        if (hasTriggered && triggerOnlyOnce)
        {
            return;
        }

        CacheReferences();
        if (enemyMover == null)
        {
            Debug.LogWarning("[Scene3BossRevealTrigger] Missing Enemy2 reference.", this);
            return;
        }

        if (enemyHealthSystem != null && enemyHealthSystem.IsDead())
        {
            return;
        }

        hasTriggered = true;
        StartCoroutine(RevealBossRoutine());
    }

    private IEnumerator RevealBossRoutine()
    {
        SetEnemyCombatEnabled(false);

        if (playRevealFocusShot)
        {
            if (cameraTransition == null)
            {
                cameraTransition = FindFirstObjectByType<SceneIntroCameraTransition>();
            }

            if (cameraTransition != null)
            {
                Transform focusTarget = GetOrCreateRevealFocusAnchor();
                if (focusTarget != null)
                {
                    SceneIntroCameraTransition.FocusShotSettings focusSettings = new SceneIntroCameraTransition.FocusShotSettings
                    {
                        LookHeight = focusLookHeight,
                        StartOffset = focusStartOffset,
                        EndOffset = focusEndOffset,
                        OrbitAngle = focusOrbitAngle,
                        Duration = focusDuration,
                        HoldDuration = focusHoldDuration,
                        // Keep the reveal camera on the player-to-enemy axis.
                        UseTargetRotation = true,
                        // Stay on the player's side of the axis instead of flipping behind Enemy2.
                        InvertTargetRotation = false,
                        LockTargetTransform = true,
                        TransitionCurve = focusTransitionCurve
                    };

                    cameraTransition.PlayTemporaryFocusShot(focusTarget, focusSettings);
                }
            }
        }

        if (jumpStartDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(jumpStartDelay);
        }

        yield return JumpEnemyDownRoutine();

        if (cameraTransition != null)
        {
            while (cameraTransition.IsTransitioning)
            {
                yield return null;
            }
        }

        if (enableCombatAfterLanding)
        {
            SetEnemyCombatEnabled(true);
        }
    }

    private IEnumerator JumpEnemyDownRoutine()
    {
        if (enemyMover == null)
        {
            yield break;
        }

        Vector3 startPosition = enemyMover.position;
        Vector3 endPosition = GetLandingPosition();
        Vector3 flatDirection = endPosition - startPosition;
        flatDirection.y = 0f;

        if (rotateTowardLandingPoint && flatDirection.sqrMagnitude > 0.0001f)
        {
            enemyMover.rotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
        }

        CacheEnemyMotionState();
        SetEnemyMotionEnabled(false);
        PlayJumpAnimation();

        float timer = 0f;
        while (timer < jumpDuration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = jumpDuration <= 0f ? 1f : Mathf.Clamp01(timer / jumpDuration);

            Vector3 horizontalPosition = Vector3.Lerp(startPosition, endPosition, progress);
            float verticalOffset = 4f * jumpArcHeight * progress * (1f - progress);
            enemyMover.position = horizontalPosition + Vector3.up * verticalOffset;

            yield return null;
        }

        enemyMover.position = endPosition;
        SetEnemyMotionEnabled(true);
    }

    private void CacheReferences()
    {
        if (enemy2Root == null)
        {
            enemy2Root = GameObject.Find("Enemy2");
        }

        if (playerTransform == null)
        {
            CharacterInputSystem inputSystem = FindFirstObjectByType<CharacterInputSystem>();
            if (inputSystem != null)
            {
                playerTransform = inputSystem.transform;
            }
        }

        if (enemy2Root == null)
        {
            return;
        }

        if (enemyHealthSystem == null)
        {
            enemyHealthSystem = enemy2Root.GetComponent<CharacterHealthSystemBase>();
            if (enemyHealthSystem == null)
            {
                enemyHealthSystem = enemy2Root.GetComponentInChildren<CharacterHealthSystemBase>(true);
            }
        }

        if (enemyCombatSystem == null)
        {
            enemyCombatSystem = enemy2Root.GetComponent<AICombatSystem>();
            if (enemyCombatSystem == null)
            {
                enemyCombatSystem = enemy2Root.GetComponentInChildren<AICombatSystem>(true);
            }
        }

        if (enemyMovement == null)
        {
            enemyMovement = enemy2Root.GetComponent<AI2Movement>();
            if (enemyMovement == null)
            {
                enemyMovement = enemy2Root.GetComponentInChildren<AI2Movement>(true);
            }
        }

        if (enemyCharacterController == null)
        {
            enemyCharacterController = enemy2Root.GetComponent<CharacterController>();
            if (enemyCharacterController == null)
            {
                enemyCharacterController = enemy2Root.GetComponentInChildren<CharacterController>(true);
            }
        }

        if (enemyAnimator == null)
        {
            enemyAnimator = enemy2Root.GetComponent<Animator>();
            if (enemyAnimator == null)
            {
                enemyAnimator = enemy2Root.GetComponentInChildren<Animator>(true);
            }
        }

        if (enemyMover == null)
        {
            if (enemyCharacterController != null)
            {
                enemyMover = enemyCharacterController.transform;
            }
            else if (enemyHealthSystem != null)
            {
                enemyMover = enemyHealthSystem.transform;
            }
            else
            {
                enemyMover = enemy2Root.transform;
            }
        }
    }

    private void CacheEnemyMotionState()
    {
        originalCharacterControllerEnabled = enemyCharacterController != null && enemyCharacterController.enabled;
        originalMovementEnabled = enemyMovement != null && enemyMovement.enabled;
    }

    private void SetEnemyMotionEnabled(bool isEnabled)
    {
        if (enemyMovement != null)
        {
            enemyMovement.enabled = isEnabled && originalMovementEnabled;
        }

        if (enemyCharacterController != null)
        {
            enemyCharacterController.enabled = isEnabled && originalCharacterControllerEnabled;
        }
    }

    private void SetEnemyCombatEnabled(bool isEnabled)
    {
        if (enemyCombatSystem != null)
        {
            enemyCombatSystem.SetCombatLogicEnabled(isEnabled);
        }

        if (!isEnabled && enemyAnimator != null)
        {
            enemyAnimator.SetFloat("LockOn", 0f);
        }
    }

    private void PlayJumpAnimation()
    {
        if (enemyAnimator == null || string.IsNullOrWhiteSpace(jumpAnimatorStateName))
        {
            return;
        }

        enemyAnimator.Play(jumpAnimatorStateName, 0, 0f);
    }

    private Vector3 GetLandingPosition()
    {
        if (landingPoint != null)
        {
            return landingPoint.position;
        }

        if (enemyMover == null)
        {
            return landingOffset;
        }

        return enemyMover.position + enemyMover.rotation * landingOffset;
    }

    private Transform GetOrCreateRevealFocusAnchor()
    {
        if (enemyMover == null)
        {
            return null;
        }

        if (revealFocusAnchor == null)
        {
            GameObject anchorObject = new GameObject("Scene3BossRevealFocusAnchor");
            revealFocusAnchor = anchorObject.transform;
        }

        revealFocusAnchor.position = enemyMover.position;

        Vector3 forward = Vector3.forward;
        if (playerTransform != null)
        {
            Vector3 playerToEnemy = enemyMover.position - playerTransform.position;
            playerToEnemy.y = 0f;
            if (playerToEnemy.sqrMagnitude > 0.001f)
            {
                forward = playerToEnemy.normalized;
            }
        }
        else
        {
            Vector3 enemyForward = enemyMover.forward;
            enemyForward.y = 0f;
            if (enemyForward.sqrMagnitude > 0.001f)
            {
                forward = enemyForward.normalized;
            }
        }

        revealFocusAnchor.rotation = Quaternion.LookRotation(forward, Vector3.up);
        return revealFocusAnchor;
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if (other.CompareTag(playerTag))
        {
            return true;
        }

        Transform otherRoot = other.transform.root;
        return otherRoot != null && otherRoot.CompareTag(playerTag);
    }

    private bool IsSceneAllowed()
    {
        if (!restrictToScene)
        {
            return true;
        }

        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == sceneName;
    }
}
