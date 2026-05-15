using System.Collections;
using UnityEngine.Animations;
using UnityEngine.Playables;
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
    [SerializeField] private BossTurnDialogueController bossTurnDialogueController;

    [Header("Reveal Focus Shot")]
    [SerializeField] private SceneIntroCameraTransition cameraTransition;
    [SerializeField] private bool playRevealFocusShot = true;
    [SerializeField] private float focusLookHeight = 1.85f;
    [SerializeField] private Vector3 focusStartOffset = new Vector3(0.22f, 0.95f, -5.4f);
    [SerializeField] private Vector3 focusEndOffset = new Vector3(0.06f, 1.18f, -4.45f);
    [SerializeField] private float focusOrbitAngle = 1.5f;
    [SerializeField] private float focusDuration = 3.2f;
    [SerializeField] private float focusHoldDuration = 0.8f;
    [SerializeField, Min(0f)] private float postLandingFocusDuration = 2f;
    [SerializeField] private bool focusInvertTargetRotation;
    [SerializeField] private AnimationCurve focusTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Jump Down")]
    [SerializeField] private Transform landingPoint;
    [SerializeField] private Vector3 landingOffset = new Vector3(0f, 0f, -2.4f);
    [SerializeField, Min(0f)] private float jumpStartDelay = 0f;
    [SerializeField, Min(0.05f)] private float jumpDuration = 1.1f;
    [SerializeField, Min(0f)] private float jumpArcHeight = 1.6f;
    [SerializeField, Range(0.1f, 0.9f)] private float jumpApexProgress = 0.34f;
    [SerializeField] private string jumpAnimatorStateName = "GS12";
    [SerializeField] private AnimationClip jumpStartClip;
    [SerializeField] private AnimationClip jumpLoopClip;
    [SerializeField] private AnimationClip jumpEndClip;
    [SerializeField] private bool playJumpEndOnLanding = false;
    [SerializeField, Range(0f, 1f)] private float airborneStartPoseNormalizedTime = 0.72f;
    [SerializeField, Range(0f, 1f)] private float airborneLoopPoseNormalizedTime = 0.12f;
    [SerializeField] private bool freezeAirborneLoopPose = true;
    [SerializeField] private string landingIdleStateName = "BaseMotion";
    [SerializeField] private bool rotateTowardLandingPoint = true;
    [SerializeField] private bool enableCombatAfterLanding = true;

    [Header("Turn Reveal")]
    [SerializeField] private bool useTurnReveal = true;
    [SerializeField, Min(0.05f)] private float turnDuration = 0.75f;
    [SerializeField, Min(0f)] private float postTurnFocusDuration = 0f;

    private CharacterHealthSystemBase enemyHealthSystem;
    private AICombatSystem enemyCombatSystem;
    private AI2Movement enemyMovement;
    private CharacterController enemyCharacterController;
    private Animator enemyAnimator;
    private Transform enemyMover;
    private Transform playerTransform;
    private Transform revealFocusAnchor;
    private bool hasTriggered;
    private bool keepRevealFocusAnchorSynced;
    private bool originalCharacterControllerEnabled;
    private bool originalMovementEnabled;
    private PlayableGraph jumpPlayableGraph;
    private Coroutine jumpAnimationCoroutine;

    private void Awake()
    {
        Collider triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;

        CacheReferences();
        PlayLandingIdlePose();

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

    private void LateUpdate()
    {
        if (keepRevealFocusAnchorSynced)
        {
            UpdateRevealFocusAnchorTransform();
        }
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
                    keepRevealFocusAnchorSynced = true;
                    float followWindow = useTurnReveal
                        ? turnDuration + postTurnFocusDuration
                        : jumpStartDelay + jumpDuration + postLandingFocusDuration;
                    SceneIntroCameraTransition.FocusShotSettings focusSettings = new SceneIntroCameraTransition.FocusShotSettings
                    {
                        LookHeight = focusLookHeight,
                        StartOffset = focusStartOffset,
                        EndOffset = focusEndOffset,
                        OrbitAngle = focusOrbitAngle,
                        Duration = Mathf.Max(focusDuration, followWindow),
                        HoldDuration = focusHoldDuration,
                        // Keep the reveal camera on the player-to-enemy axis.
                        UseTargetRotation = true,
                        // Stay on the player's side of the axis instead of flipping behind Enemy2.
                        InvertTargetRotation = false,
                        // A close turn reveal should stay tight on Enemy2 instead of drifting.
                        LockTargetTransform = useTurnReveal,
                        TransitionCurve = focusTransitionCurve
                    };

                    cameraTransition.PlayTemporaryFocusShot(focusTarget, focusSettings);
                }
            }
        }

        if (useTurnReveal)
        {
            yield return TurnEnemyTowardPlayerRoutine();
            if (postTurnFocusDuration > 0f)
            {
                yield return WaitForSecondsRealtimeSafe(postTurnFocusDuration);
            }
        }
        else
        {
            if (jumpStartDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(jumpStartDelay);
            }

            yield return JumpEnemyDownRoutine();
        }

        if (bossTurnDialogueController != null)
        {
            bossTurnDialogueController.ShowDialogueAfterBossTurn();

            while (bossTurnDialogueController.IsDialogueActiveOrTransitioning)
            {
                yield return null;
            }
        }

        if (cameraTransition != null)
        {
            while (cameraTransition.IsTransitioning)
            {
                yield return null;
            }
        }

        keepRevealFocusAnchorSynced = false;

        if (enableCombatAfterLanding)
        {
            SetEnemyCombatEnabled(true);
        }
    }

    private IEnumerator TurnEnemyTowardPlayerRoutine()
    {
        if (enemyMover == null)
        {
            yield break;
        }

        Vector3 targetForward = enemyMover.forward;
        if (playerTransform != null)
        {
            Vector3 toPlayer = playerTransform.position - enemyMover.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.001f)
            {
                targetForward = toPlayer.normalized;
            }
        }

        Quaternion startRotation = enemyMover.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(targetForward, Vector3.up);

        CacheEnemyMotionState();
        SetEnemyMotionEnabled(false);
        PlayLandingIdlePose();

        float timer = 0f;
        while (timer < turnDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = turnDuration <= 0f ? 1f : Mathf.Clamp01(timer / turnDuration);
            enemyMover.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            UpdateRevealFocusAnchorTransform();
            yield return null;
        }

        enemyMover.rotation = targetRotation;
        UpdateRevealFocusAnchorTransform();
        SetEnemyMotionEnabled(true);
        PlayLandingIdlePose();
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

        ShowJumpStartPose();
        CacheEnemyMotionState();
        SetEnemyMotionEnabled(false);
        StartJumpAnimationSequence();

        float timer = 0f;
        while (timer < jumpDuration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = jumpDuration <= 0f ? 1f : Mathf.Clamp01(timer / jumpDuration);

            Vector3 horizontalPosition = Vector3.Lerp(startPosition, endPosition, progress);
            float verticalOffset = EvaluateJumpArc(progress);
            enemyMover.position = horizontalPosition + Vector3.up * verticalOffset;
            UpdateRevealFocusAnchorTransform();

            yield return null;
        }

        enemyMover.position = endPosition;
        UpdateRevealFocusAnchorTransform();
        StopJumpAnimationSequence();
        yield return PlayLandingEndAnimationRoutine();

        SetEnemyMotionEnabled(true);
        PlayLandingIdlePose();
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

        if (bossTurnDialogueController == null)
        {
            BossTurnDialogueController[] dialogueControllers =
                FindObjectsByType<BossTurnDialogueController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < dialogueControllers.Length; i++)
            {
                if (dialogueControllers[i] == null)
                {
                    continue;
                }

                if (dialogueControllers[i].gameObject.name == "Dialogue(5)")
                {
                    bossTurnDialogueController = dialogueControllers[i];
                    break;
                }
            }

            if (bossTurnDialogueController == null && dialogueControllers.Length > 0)
            {
                bossTurnDialogueController = dialogueControllers[0];
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
            PlayLandingIdlePose();
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

    private void StartJumpAnimationSequence()
    {
        StopJumpAnimationSequence();

        if (enemyAnimator == null)
        {
            return;
        }

        if (jumpStartClip == null && jumpLoopClip == null && jumpEndClip == null)
        {
            PlayJumpAnimation();
            return;
        }

        jumpAnimationCoroutine = StartCoroutine(PlayJumpAnimationSequenceRoutine());
    }

    private IEnumerator PlayJumpAnimationSequenceRoutine()
    {
        float loopWindow = Mathf.Max(0f, jumpDuration);

        if (jumpStartClip != null && loopWindow > 0f)
        {
            PlayJumpClip(
                jumpStartClip,
                0d,
                jumpStartClip.length * airborneStartPoseNormalizedTime);
            yield return WaitForSecondsRealtimeSafe(loopWindow);
        }
        else if (jumpLoopClip != null && loopWindow > 0f)
        {
            PlayJumpClip(
                jumpLoopClip,
                freezeAirborneLoopPose ? 0d : 1d,
                jumpLoopClip.length * airborneLoopPoseNormalizedTime);
            yield return WaitForSecondsRealtimeSafe(loopWindow);
        }
        else if (jumpStartClip != null && loopWindow > 0f)
        {
            yield return WaitForSecondsRealtimeSafe(loopWindow);
        }

        DestroyJumpPlayableGraph();
        jumpAnimationCoroutine = null;
    }

    private IEnumerator PlayLandingEndAnimationRoutine()
    {
        if (!playJumpEndOnLanding || jumpEndClip == null)
        {
            yield break;
        }

        PlayJumpClip(jumpEndClip, 1d);
        yield return WaitForSecondsRealtimeSafe(GetClipDuration(jumpEndClip));
        DestroyJumpPlayableGraph();
    }

    private void PlayJumpClip(AnimationClip clip, double speed = 1d, double startTime = 0d)
    {
        if (clip == null || enemyAnimator == null)
        {
            return;
        }

        DestroyJumpPlayableGraph();

        jumpPlayableGraph = PlayableGraph.Create("Scene3BossRevealJumpGraph");
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(jumpPlayableGraph, "Animation", enemyAnimator);
        AnimationClipPlayable playable = AnimationClipPlayable.Create(jumpPlayableGraph, clip);
        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);
        playable.SetDuration(clip.length);
        playable.SetTime(Mathf.Clamp((float)startTime, 0f, clip.length));
        playable.SetSpeed(speed);

        output.SetSourcePlayable(playable);
        jumpPlayableGraph.Play();
    }

    private void ShowJumpStartPose()
    {
        if (jumpStartClip != null)
        {
            PlayJumpClip(jumpStartClip, 0d);
            return;
        }

        PlayJumpAnimation();
    }

    private void PlayLandingIdlePose()
    {
        if (enemyAnimator == null || string.IsNullOrWhiteSpace(landingIdleStateName))
        {
            return;
        }

        enemyAnimator.SetFloat("LockOn", 0f);
        enemyAnimator.SetFloat("Movement", 0f);
        enemyAnimator.SetFloat("Run", 0f);
        enemyAnimator.SetFloat("Crouch", 0f);
        enemyAnimator.SetFloat("Horizontal", 0f);
        enemyAnimator.SetFloat("Vertical", 0f);
        enemyAnimator.CrossFadeInFixedTime(landingIdleStateName, 0.08f, 0, 0f);
        enemyAnimator.Update(0f);
    }

    private void StopJumpAnimationSequence()
    {
        if (jumpAnimationCoroutine != null)
        {
            StopCoroutine(jumpAnimationCoroutine);
            jumpAnimationCoroutine = null;
        }

        DestroyJumpPlayableGraph();
    }

    private void DestroyJumpPlayableGraph()
    {
        if (jumpPlayableGraph.IsValid())
        {
            jumpPlayableGraph.Destroy();
        }
    }

    private static IEnumerator WaitForSecondsRealtimeSafe(float duration)
    {
        if (duration <= 0f)
        {
            yield break;
        }

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static float GetClipDuration(AnimationClip clip)
    {
        return clip != null ? Mathf.Max(0f, clip.length) : 0f;
    }

    private float EvaluateJumpArc(float progress)
    {
        float apex = Mathf.Clamp(jumpApexProgress, 0.1f, 0.9f);

        if (progress <= apex)
        {
            float riseProgress = progress / apex;
            return jumpArcHeight * Mathf.Sin(riseProgress * Mathf.PI * 0.5f);
        }

        float fallProgress = (progress - apex) / (1f - apex);
        return jumpArcHeight * Mathf.Cos(fallProgress * Mathf.PI * 0.5f);
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

        Vector3 forwardCandidate = enemyMover.position + enemyMover.rotation * landingOffset;
        Vector3 backwardCandidate = enemyMover.position + enemyMover.rotation * new Vector3(-landingOffset.x, landingOffset.y, -landingOffset.z);

        if (playerTransform == null)
        {
            return forwardCandidate;
        }

        float forwardDistanceToPlayer = Vector3.SqrMagnitude(forwardCandidate - playerTransform.position);
        float backwardDistanceToPlayer = Vector3.SqrMagnitude(backwardCandidate - playerTransform.position);
        return forwardDistanceToPlayer <= backwardDistanceToPlayer ? forwardCandidate : backwardCandidate;
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

        UpdateRevealFocusAnchorTransform();
        return revealFocusAnchor;
    }

    private void UpdateRevealFocusAnchorTransform()
    {
        if (revealFocusAnchor == null || enemyMover == null)
        {
            return;
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

#if UNITY_EDITOR
    private void OnValidate()
    {
        TryAssignDefaultJumpClips();
    }

    private void Reset()
    {
        TryAssignDefaultJumpClips();
    }

    private void TryAssignDefaultJumpClips()
    {
        if (jumpStartClip == null)
        {
            jumpStartClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/GameAssets/GreatSword_Animset/Animation/GreatSword/Inplace/Movement/Inplace_GreatSword_Jump_Start.FBX");
        }

        if (jumpLoopClip == null)
        {
            jumpLoopClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/GameAssets/GreatSword_Animset/Animation/GreatSword/Inplace/Movement/Inplace_GreatSword_Jump_Loop.FBX");
        }

        if (jumpEndClip == null)
        {
            jumpEndClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/GameAssets/GreatSword_Animset/Animation/GreatSword/Inplace/Movement/Inplace_GreatSword_Jump_End.FBX");
        }
    }
#endif

    private void OnDisable()
    {
        StopJumpAnimationSequence();
    }
}
