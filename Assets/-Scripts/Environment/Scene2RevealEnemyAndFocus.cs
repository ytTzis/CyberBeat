using UnityEngine;
using UnityEngine.SceneManagement;
using UGG.Health;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class Scene2RevealEnemyAndFocus : MonoBehaviour
{
    [SerializeField] private bool restrictToScene = true;
    [SerializeField] private string sceneName = "2_Game Scene";
    [SerializeField] private GameObject enemyToReveal;
    [SerializeField] private SceneIntroCameraTransition cameraTransition;
    [SerializeField] private bool hideOnStart = true;
    [SerializeField] private bool allowOnlyOnce = true;
    [SerializeField, Header("Enemy Focus Shot")] private float focusLookHeight = 1.2f;
    [SerializeField] private Vector3 focusStartOffset = new Vector3(0.8f, 1.6f, -3f);
    [SerializeField] private Vector3 focusEndOffset = new Vector3(0.3f, 1.1f, -1.8f);
    [SerializeField] private float focusOrbitAngle = 10f;
    [SerializeField] private float focusDuration = 1.2f;
    [SerializeField] private float focusHoldDuration = 0.45f;
    [SerializeField] private bool focusUseTargetRotation = true;
    [SerializeField] private bool focusInvertTargetRotation = true;
    [SerializeField] private AnimationCurve focusTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Header("Subway Exit")] private Transform subwayExit;
    [SerializeField] private float openHeight = 4f;
    [SerializeField] private float openDuration = 2.8f;
    [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private GameObject invisibleWall;
    [SerializeField, Header("Exit Transition")] private bool enableExitSceneTransition = true;
    [SerializeField] private string exitSceneName = "3_GameScene";
    [SerializeField] private bool useInternalExitTriggerVolume;
    [SerializeField] private Vector3 exitTriggerLocalCenter = new Vector3(0f, 2f, 2.5f);
    [SerializeField] private Vector3 exitTriggerSize = new Vector3(3f, 4f, 3f);
    [SerializeField] private float exitFadeOutDuration = 0.9f;
    [SerializeField] private float exitFadeHoldDuration = 0.1f;
    [SerializeField, Header("Subway Exit Focus Shot")] private bool playSubwayExitFocusShot = true;
    [SerializeField] private float subwayExitFocusLookHeight = 2.2f;
    [SerializeField] private Vector3 subwayExitFocusStartOffset = new Vector3(0f, 0f, -5.2f);
    [SerializeField] private Vector3 subwayExitFocusEndOffset = new Vector3(0f, 0f, -4.2f);
    [SerializeField] private float subwayExitFocusOrbitAngle = 0f;
    [SerializeField] private float subwayExitFocusDuration = 1.8f;
    [SerializeField] private float subwayExitFocusHoldDuration = 0.35f;
    [SerializeField] private bool subwayExitFocusUseTargetRotation = true;
    [SerializeField] private bool subwayExitFocusInvertTargetRotation;
    [SerializeField] private AnimationCurve subwayExitFocusTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private AIHealthSystem enemyHealthSystem;
    private bool hasTriggered;
    private bool exitOpeningTriggered;
    private bool exitReady;
    private bool exitSceneTransitionStarted;
    private Vector3 subwayExitClosedLocalPosition;
    private Transform playerTransform;
    private CanvasGroup exitFadeCanvasGroup;

    private void Awake()
    {
        if (!IsSceneAllowed() || !hideOnStart || enemyToReveal == null)
        {
            CacheReferences();
            return;
        }

        CacheReferences();
        enemyToReveal.SetActive(false);
    }

    private void Update()
    {
        if (!IsSceneAllowed() || exitOpeningTriggered || !hasTriggered || enemyHealthSystem == null || subwayExit == null)
        {
            if (useInternalExitTriggerVolume)
            {
                UpdateExitSceneTransition();
            }
            return;
        }

        if (!enemyHealthSystem.IsDead())
        {
            if (useInternalExitTriggerVolume)
            {
                UpdateExitSceneTransition();
            }
            return;
        }

        exitOpeningTriggered = true;
        StartCoroutine(OpenSubwayExitRoutine());
        if (useInternalExitTriggerVolume)
        {
            UpdateExitSceneTransition();
        }
    }

    public void RevealEnemyAndPlayFocus()
    {
        if (!IsSceneAllowed())
        {
            return;
        }

        if (allowOnlyOnce && hasTriggered)
        {
            return;
        }

        if (enemyToReveal == null)
        {
            Debug.LogWarning("[Scene2RevealEnemyAndFocus] Missing enemy to reveal.", this);
            return;
        }

        CacheReferences();
        enemyToReveal.SetActive(true);
        hasTriggered = true;

        if (cameraTransition == null)
        {
            cameraTransition = FindFirstObjectByType<SceneIntroCameraTransition>();
        }

        if (cameraTransition == null)
        {
            Debug.LogWarning("[Scene2RevealEnemyAndFocus] Missing SceneIntroCameraTransition reference.", this);
            return;
        }

        SceneIntroCameraTransition.FocusShotSettings focusSettings = new SceneIntroCameraTransition.FocusShotSettings
        {
            LookHeight = focusLookHeight,
            StartOffset = focusStartOffset,
            EndOffset = focusEndOffset,
            OrbitAngle = focusOrbitAngle,
            Duration = focusDuration,
            HoldDuration = focusHoldDuration,
            UseTargetRotation = focusUseTargetRotation,
            InvertTargetRotation = focusInvertTargetRotation,
            TransitionCurve = focusTransitionCurve
        };

        cameraTransition.PlayTemporaryFocusShot(enemyToReveal.transform, focusSettings);
    }

    private void CacheReferences()
    {
        if (enemyHealthSystem == null && enemyToReveal != null)
        {
            enemyHealthSystem = enemyToReveal.GetComponent<AIHealthSystem>();
            if (enemyHealthSystem == null)
            {
                enemyHealthSystem = enemyToReveal.GetComponentInChildren<AIHealthSystem>(true);
            }
        }

        if (enemyToReveal != null)
        {
            EnemyDefeatSceneTransition[] transitions = enemyToReveal.GetComponentsInChildren<EnemyDefeatSceneTransition>(true);
            for (int i = 0; i < transitions.Length; i++)
            {
                transitions[i].enabled = false;
            }
        }

        if (subwayExit != null)
        {
            subwayExitClosedLocalPosition = subwayExit.localPosition;
        }

        if (playerTransform == null)
        {
            CharacterInputSystem inputSystem = FindFirstObjectByType<CharacterInputSystem>();
            if (inputSystem != null)
            {
                playerTransform = inputSystem.transform;
            }
            else
            {
                GameObject player = GameObject.Find("Player");
                if (player != null)
                {
                    playerTransform = player.transform;
                }
            }
        }
    }

    private System.Collections.IEnumerator OpenSubwayExitRoutine()
    {
        if (playSubwayExitFocusShot && cameraTransition != null)
        {
            SceneIntroCameraTransition.FocusShotSettings focusSettings = new SceneIntroCameraTransition.FocusShotSettings
            {
                LookHeight = subwayExitFocusLookHeight,
                StartOffset = subwayExitFocusStartOffset,
                EndOffset = subwayExitFocusEndOffset,
                OrbitAngle = subwayExitFocusOrbitAngle,
                Duration = subwayExitFocusDuration,
                HoldDuration = subwayExitFocusHoldDuration,
                UseTargetRotation = subwayExitFocusUseTargetRotation,
                InvertTargetRotation = subwayExitFocusInvertTargetRotation,
                LockTargetTransform = true,
                TransitionCurve = subwayExitFocusTransitionCurve
            };

            cameraTransition.PlayTemporaryFocusShot(subwayExit, focusSettings);
        }

        Vector3 openTargetLocalPosition = subwayExitClosedLocalPosition + Vector3.up * openHeight;
        float timer = 0f;

        while (timer < openDuration)
        {
            timer += Time.deltaTime;
            float progress = openDuration <= 0f ? 1f : Mathf.Clamp01(timer / openDuration);
            AnimationCurve curve = openCurve ?? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            float curvedProgress = curve.Evaluate(progress);
            subwayExit.localPosition = Vector3.Lerp(subwayExitClosedLocalPosition, openTargetLocalPosition, curvedProgress);
            yield return null;
        }

        subwayExit.localPosition = openTargetLocalPosition;

        if (invisibleWall != null)
        {
            invisibleWall.SetActive(false);
        }

        exitReady = true;
    }

    private void UpdateExitSceneTransition()
    {
        if (!enableExitSceneTransition || !exitReady || exitSceneTransitionStarted || subwayExit == null)
        {
            return;
        }

        if (playerTransform == null)
        {
            CacheReferences();
            if (playerTransform == null)
            {
                return;
            }
        }

        Vector3 triggerWorldCenter = subwayExit.TransformPoint(exitTriggerLocalCenter);
        Vector3 halfExtents = exitTriggerSize * 0.5f;
        Vector3 localPlayerPosition = subwayExit.InverseTransformPoint(playerTransform.position) - exitTriggerLocalCenter;

        bool insideTrigger =
            Mathf.Abs(localPlayerPosition.x) <= halfExtents.x &&
            Mathf.Abs(localPlayerPosition.y) <= halfExtents.y &&
            Mathf.Abs(localPlayerPosition.z) <= halfExtents.z;

        if (!insideTrigger)
        {
            return;
        }

        exitSceneTransitionStarted = true;
        StartCoroutine(ExitSceneTransitionRoutine());
    }

    public bool CanTriggerExitSceneTransition()
    {
        return enableExitSceneTransition && exitReady && !exitSceneTransitionStarted && IsSceneAllowed();
    }

    public void TriggerExitSceneTransition()
    {
        if (!CanTriggerExitSceneTransition())
        {
            Debug.Log($"[Scene2RevealEnemyAndFocus] Exit scene transition blocked. enableExitSceneTransition={enableExitSceneTransition}, exitReady={exitReady}, exitSceneTransitionStarted={exitSceneTransitionStarted}, sceneAllowed={IsSceneAllowed()}.", this);
            return;
        }

        Debug.Log($"[Scene2RevealEnemyAndFocus] Starting fade to scene '{exitSceneName}'.", this);
        exitSceneTransitionStarted = true;
        StartCoroutine(ExitSceneTransitionRoutine());
    }

    private System.Collections.IEnumerator ExitSceneTransitionRoutine()
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

        EnsureExitFadeCanvas();
        yield return FadeExitScreen(1f, exitFadeOutDuration);

        if (exitFadeHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(exitFadeHoldDuration);
        }

        SceneIntroCameraTransition.RequestPlayOnNextSceneLoad();
        SceneManager.LoadScene(exitSceneName);
    }

    private System.Collections.IEnumerator FadeExitScreen(float targetAlpha, float duration)
    {
        if (exitFadeCanvasGroup == null)
        {
            yield break;
        }

        float startAlpha = exitFadeCanvasGroup.alpha;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = duration <= 0f ? 1f : Mathf.Clamp01(timer / duration);
            exitFadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            yield return null;
        }

        exitFadeCanvasGroup.alpha = targetAlpha;
    }

    private void EnsureExitFadeCanvas()
    {
        if (exitFadeCanvasGroup != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Scene2 Exit Fade Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10002;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        exitFadeCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
        exitFadeCanvasGroup.alpha = 0f;
        exitFadeCanvasGroup.blocksRaycasts = true;
        exitFadeCanvasGroup.interactable = false;

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

    private void OnDrawGizmosSelected()
    {
        if (!useInternalExitTriggerVolume || subwayExit == null)
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = subwayExit.localToWorldMatrix;
        Gizmos.color = new Color(0.2f, 1f, 0.6f, 0.8f);
        Gizmos.DrawWireCube(exitTriggerLocalCenter, exitTriggerSize);
        Gizmos.matrix = previousMatrix;
    }

    private bool IsSceneAllowed()
    {
        if (!restrictToScene)
        {
            return true;
        }

        return SceneManager.GetActiveScene().name == sceneName;
    }
}
