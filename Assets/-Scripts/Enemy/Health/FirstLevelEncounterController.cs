using System.Collections;
using UGG.Health;
using UGG.Move;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FirstLevelEncounterController : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField, Min(0f)] private float triggerFallbackPadding = 0.4f;

    [Header("Encounter Enemies")]
    [SerializeField] private AIHealthSystem enemy1;
    [SerializeField] private AIHealthSystem enemy2;
    [SerializeField] private AIHealthSystem enemy3;
    [SerializeField] private AIHealthSystem enemy4;

    [Header("Area Triggers")]
    [SerializeField] private FirstLevelEncounterTrigger area1Trigger;
    [SerializeField] private FirstLevelEncounterTrigger area2Trigger;

    [Header("Enemy3/4 Reveal Focus")]
    [SerializeField] private SceneIntroCameraTransition cameraTransition;
    [SerializeField] private bool playEnemy34RevealFocusShot = true;
    [SerializeField] private float enemy34FocusLookHeight = 1.6f;
    [SerializeField] private Vector3 enemy34FocusStartOffset = new Vector3(0f, 2.2f, -8f);
    [SerializeField] private Vector3 enemy34FocusEndOffset = new Vector3(0f, 1.6f, -6.2f);
    [SerializeField] private float enemy34FocusOrbitAngle = 0f;
    [SerializeField] private float enemy34FocusDuration = 1.25f;
    [SerializeField] private float enemy34FocusHoldDuration = 0.4f;
    [SerializeField] private bool enemy34FocusUseTargetRotation;
    [SerializeField] private bool enemy34FocusInvertTargetRotation;
    [SerializeField] private AnimationCurve enemy34FocusTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Enemy2 Patrol Before Activation")]
    [SerializeField, Min(0f)] private float enemy2PatrolDistanceX = 2.5f;
    [SerializeField, Min(0f)] private float enemy2PatrolSpeed = 1.5f;
    [SerializeField, Min(0f)] private float enemy2PatrolTurnSpeed = 12f;

    [Header("Scene Transition")]
    [SerializeField] private string nextSceneName = "2_Game Scene";
    [SerializeField] private float delayBeforeFade = 1f;
    [SerializeField] private float fadeOutDuration = 0.9f;
    [SerializeField] private float holdBlackDuration = 0.1f;

    private Vector3 enemy2StartPosition;
    private AICombatSystem enemy2CombatSystem;
    private AIMovement enemy2Movement;
    private Animator enemy2Animator;
    private Transform playerTransform;
    private int movementParameterId;
    private bool enemy2Activated;
    private bool enemy34Activated;
    private bool transitionStarted;
    private CanvasGroup fadeCanvasGroup;
    private Transform enemy34FocusAnchor;

    private void Awake()
    {
        movementParameterId = Animator.StringToHash("Movement");

        if (enemy2 != null)
        {
            enemy2StartPosition = enemy2.transform.position;
            enemy2CombatSystem = enemy2.GetComponentInChildren<AICombatSystem>(true);
            enemy2Movement = enemy2.GetComponent<AIMovement>();
            enemy2Animator = enemy2.GetComponentInChildren<Animator>(true);
        }

        ConfigureTrigger(area1Trigger, FirstLevelEncounterTrigger.TriggerStage.Area1);
        ConfigureTrigger(area2Trigger, FirstLevelEncounterTrigger.TriggerStage.Area2);
        CachePlayerTransform();

        EnterEnemy2PatrolState();
        SetEnemyGroupActive(false, enemy3, enemy4);
        DisableManagedEnemyTransitions();
    }

    private void Update()
    {
        CachePlayerTransform();
        UpdateAreaTriggerFallbacks();
        UpdateEnemy2Patrol();

        if (transitionStarted || !enemy34Activated)
        {
            return;
        }

        if (!AreAllEnemiesDead(enemy3, enemy4))
        {
            return;
        }

        transitionStarted = true;
        StartCoroutine(TransitionRoutine());
    }

    public bool IsPlayerCollider(Collider other)
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

    private void CachePlayerTransform()
    {
        if (playerTransform != null)
        {
            return;
        }

        PlayerHealthSystem playerHealthSystem = FindFirstObjectByType<PlayerHealthSystem>();
        if (playerHealthSystem != null)
        {
            playerTransform = playerHealthSystem.transform;
        }
    }

    private void UpdateAreaTriggerFallbacks()
    {
        if (playerTransform == null)
        {
            return;
        }

        if (!enemy2Activated && enemy1 != null && enemy1.IsDead() && IsPlayerInsideTrigger(area1Trigger))
        {
            TryActivateEnemy2();
        }

        if (!enemy34Activated && enemy2 != null && enemy2.IsDead() && IsPlayerInsideTrigger(area2Trigger))
        {
            TryActivateEnemy34();
        }
    }

    private bool IsPlayerInsideTrigger(FirstLevelEncounterTrigger trigger)
    {
        if (trigger == null || !trigger.isActiveAndEnabled)
        {
            return false;
        }

        Collider triggerCollider = trigger.GetComponent<Collider>();
        if (triggerCollider == null || !triggerCollider.enabled)
        {
            return false;
        }

        Bounds bounds = triggerCollider.bounds;
        bounds.Expand(triggerFallbackPadding);
        return bounds.Contains(playerTransform.position);
    }

    public void NotifyAreaTriggered(FirstLevelEncounterTrigger.TriggerStage triggerStage)
    {
        switch (triggerStage)
        {
            case FirstLevelEncounterTrigger.TriggerStage.Area1:
                TryActivateEnemy2();
                break;
            case FirstLevelEncounterTrigger.TriggerStage.Area2:
                TryActivateEnemy34();
                break;
        }
    }

    private void TryActivateEnemy2()
    {
        if (enemy2Activated || enemy1 == null || !enemy1.IsDead())
        {
            Debug.Log($"[FirstLevelEncounter] Area1 ignored. enemy2Activated={enemy2Activated}, enemy1Assigned={enemy1 != null}, enemy1Dead={(enemy1 != null && enemy1.IsDead())}", this);
            return;
        }

        EnterEnemy2CombatState();
        Debug.Log("[FirstLevelEncounter] Enemy2 switched from patrol to combat.", this);

        if (area1Trigger != null)
        {
            area1Trigger.gameObject.SetActive(false);
        }
    }

    private void TryActivateEnemy34()
    {
        if (enemy34Activated || enemy2 == null || !enemy2.IsDead())
        {
            return;
        }

        enemy34Activated = true;
        SetEnemyGroupActive(true, enemy3, enemy4);
        PlayEnemy34RevealFocusShot();

        if (area2Trigger != null)
        {
            area2Trigger.gameObject.SetActive(false);
        }
    }

    private void UpdateEnemy2Patrol()
    {
        if (enemy2 == null)
        {
            return;
        }

        if (enemy2Activated)
        {
            return;
        }

        if (enemy2.IsDead() || !enemy2.gameObject.activeInHierarchy)
        {
            return;
        }

        float patrolOffsetX = Mathf.PingPong(Time.time * enemy2PatrolSpeed, enemy2PatrolDistanceX * 2f) - enemy2PatrolDistanceX;
        Vector3 patrolPosition = enemy2StartPosition + new Vector3(patrolOffsetX, 0f, 0f);
        Vector3 moveDirection = patrolPosition - enemy2.transform.position;
        moveDirection.y = 0f;

        if (moveDirection.sqrMagnitude <= 0.0025f)
        {
            if (enemy2Animator != null && enemy2Animator.isActiveAndEnabled)
            {
                enemy2Animator.SetFloat(movementParameterId, 0f);
            }

            return;
        }

        Vector3 normalizedDirection = moveDirection.normalized;

        if (enemy2Movement != null)
        {
            CharacterController controller = enemy2Movement.GetComponent<CharacterController>();
            if (controller != null && controller.enabled && enemy2Movement.isActiveAndEnabled)
            {
                enemy2Movement.CharacterMoveInterface(normalizedDirection, enemy2PatrolSpeed, true);
            }
        }
        else
        {
            enemy2.transform.position += normalizedDirection * (enemy2PatrolSpeed * Time.deltaTime);
        }

        Transform enemyRoot = enemy2.transform.root;
        if (enemyRoot != null)
        {
            Quaternion targetRotation = Quaternion.LookRotation(normalizedDirection, Vector3.up);
            enemyRoot.rotation = Quaternion.Slerp(enemyRoot.rotation, targetRotation, enemy2PatrolTurnSpeed * Time.deltaTime);
        }

        if (enemy2Animator != null && enemy2Animator.isActiveAndEnabled)
        {
            enemy2Animator.SetFloat(movementParameterId, 1f, 0.1f, Time.deltaTime);
        }
    }

    private void SetEnemyGroupActive(bool isActive, params AIHealthSystem[] enemies)
    {
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null)
            {
                continue;
            }

            enemies[i].gameObject.SetActive(isActive);
        }
    }

    private void EnterEnemy2PatrolState()
    {
        enemy2Activated = false;
        SetEnemyCombatEnabled(enemy2, false);
    }

    private void EnterEnemy2CombatState()
    {
        enemy2Activated = true;
        SetEnemyCombatEnabled(enemy2, true);

        if (TryGetEnemy2Animator(out Animator enemyAnimator))
        {
            enemyAnimator.SetFloat(movementParameterId, 0f);
            enemyAnimator.SetFloat("LockOn", 0f);
        }

        if (enemy2CombatSystem != null)
        {
            Debug.Log($"[FirstLevelEncounter] Enemy2 combat enabled={enemy2CombatSystem.enabled}.", enemy2CombatSystem);
        }
    }

    private void SetEnemyCombatEnabled(AIHealthSystem healthSystem, bool isEnabled)
    {
        if (healthSystem == null)
        {
            return;
        }

        AICombatSystem combatSystem = healthSystem == enemy2 && enemy2CombatSystem != null
            ? enemy2CombatSystem
            : healthSystem.GetComponentInChildren<AICombatSystem>(true);

        if (combatSystem != null)
        {
            combatSystem.SetCombatLogicEnabled(isEnabled);
        }

        Animator animator = null;

        if (healthSystem == enemy2)
        {
            TryGetEnemy2Animator(out animator);
        }

        if (animator == null)
        {
            animator = healthSystem.GetComponentInChildren<Animator>(true);
        }

        if (animator != null)
        {
            if (!isEnabled)
            {
                animator.SetFloat("LockOn", 0f);
            }

            if (!isEnabled && healthSystem == enemy2)
            {
                animator.SetFloat(movementParameterId, 0f);
            }
        }
    }

    private bool TryGetEnemy2Animator(out Animator animator)
    {
        if (enemy2Animator != null)
        {
            animator = enemy2Animator;
            return true;
        }

        animator = null;

        if (enemy2 == null)
        {
            return false;
        }

        enemy2Animator = enemy2.GetComponentInChildren<Animator>(true);
        animator = enemy2Animator;
        return animator != null;
    }

    private void ConfigureTrigger(FirstLevelEncounterTrigger trigger, FirstLevelEncounterTrigger.TriggerStage triggerStage)
    {
        if (trigger == null)
        {
            return;
        }

        trigger.Initialize(this, triggerStage);
    }

    private bool AreAllEnemiesDead(params AIHealthSystem[] enemies)
    {
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null || !enemies[i].IsDead())
            {
                return false;
            }
        }

        return true;
    }

    private void DisableManagedEnemyTransitions()
    {
        DisableTransitions(enemy1);
        DisableTransitions(enemy2);
        DisableTransitions(enemy3);
        DisableTransitions(enemy4);
    }

    private static void DisableTransitions(AIHealthSystem healthSystem)
    {
        if (healthSystem == null)
        {
            return;
        }

        EnemyDefeatSceneTransition[] transitions = healthSystem.GetComponentsInChildren<EnemyDefeatSceneTransition>(true);
        for (int i = 0; i < transitions.Length; i++)
        {
            transitions[i].enabled = false;
        }
    }

    private void PlayEnemy34RevealFocusShot()
    {
        if (!playEnemy34RevealFocusShot)
        {
            return;
        }

        if (cameraTransition == null)
        {
            cameraTransition = FindFirstObjectByType<SceneIntroCameraTransition>();
        }

        if (cameraTransition == null)
        {
            return;
        }

        Transform focusTarget = GetOrCreateEnemy34FocusAnchor();
        if (focusTarget == null)
        {
            return;
        }

        SceneIntroCameraTransition.FocusShotSettings focusSettings = new SceneIntroCameraTransition.FocusShotSettings
        {
            LookHeight = enemy34FocusLookHeight,
            StartOffset = enemy34FocusStartOffset,
            EndOffset = enemy34FocusEndOffset,
            OrbitAngle = enemy34FocusOrbitAngle,
            Duration = enemy34FocusDuration,
            HoldDuration = enemy34FocusHoldDuration,
            // Keep the reveal camera on the player-to-enemy axis.
            UseTargetRotation = true,
            InvertTargetRotation = enemy34FocusInvertTargetRotation,
            LockTargetTransform = true,
            TransitionCurve = enemy34FocusTransitionCurve
        };

        cameraTransition.PlayTemporaryFocusShot(focusTarget, focusSettings);
    }

    private Transform GetOrCreateEnemy34FocusAnchor()
    {
        Vector3 focusPosition = Vector3.zero;
        int activeEnemyCount = 0;

        if (enemy3 != null)
        {
            focusPosition += enemy3.transform.position;
            activeEnemyCount++;
        }

        if (enemy4 != null)
        {
            focusPosition += enemy4.transform.position;
            activeEnemyCount++;
        }

        if (activeEnemyCount == 0)
        {
            return null;
        }

        focusPosition /= activeEnemyCount;

        if (enemy34FocusAnchor == null)
        {
            GameObject anchorObject = new GameObject("Enemy34RevealFocusAnchor");
            enemy34FocusAnchor = anchorObject.transform;
        }

        enemy34FocusAnchor.position = focusPosition;

        Vector3 forward = Vector3.forward;
        if (playerTransform != null)
        {
            Vector3 playerToEnemies = focusPosition - playerTransform.position;
            playerToEnemies.y = 0f;
            if (playerToEnemies.sqrMagnitude > 0.001f)
            {
                forward = playerToEnemies.normalized;
            }
        }
        else if (enemy3 != null)
        {
            Vector3 enemyForward = enemy3.transform.forward;
            enemyForward.y = 0f;
            if (enemyForward.sqrMagnitude > 0.001f)
            {
                forward = enemyForward.normalized;
            }
        }

        enemy34FocusAnchor.rotation = Quaternion.LookRotation(forward, Vector3.up);
        return enemy34FocusAnchor;
    }

    private IEnumerator TransitionRoutine()
    {
        DisablePlayerControls();
        EnsureFadeCanvas();

        if (delayBeforeFade > 0f)
        {
            yield return new WaitForSecondsRealtime(delayBeforeFade);
        }

        yield return FadeScreen(1f, fadeOutDuration);

        if (holdBlackDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(holdBlackDuration);
        }

        SceneIntroCameraTransition.RequestPlayOnNextSceneLoad();
        SceneManager.LoadScene(nextSceneName);
    }

    private void DisablePlayerControls()
    {
        CharacterInputSystem inputSystem = FindFirstObjectByType<CharacterInputSystem>();
        if (inputSystem != null)
        {
            inputSystem.enabled = false;
        }

        TP_CameraController tpCameraController = FindFirstObjectByType<TP_CameraController>();
        if (tpCameraController != null)
        {
            tpCameraController.enabled = false;
        }

        UnityTemplateProjects.SimpleCameraController simpleCameraController = FindFirstObjectByType<UnityTemplateProjects.SimpleCameraController>();
        if (simpleCameraController != null)
        {
            simpleCameraController.enabled = false;
        }
    }

    private IEnumerator FadeScreen(float targetAlpha, float duration)
    {
        if (fadeCanvasGroup == null)
        {
            yield break;
        }

        float startAlpha = fadeCanvasGroup.alpha;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = duration <= 0f ? 1f : Mathf.Clamp01(timer / duration);
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            yield return null;
        }

        fadeCanvasGroup.alpha = targetAlpha;
    }

    private void EnsureFadeCanvas()
    {
        if (fadeCanvasGroup != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("First Level Fade Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10001;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        fadeCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = true;
        fadeCanvasGroup.interactable = false;

        GameObject imageObject = new GameObject("Fade Image", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.color = Color.black;
    }
}
