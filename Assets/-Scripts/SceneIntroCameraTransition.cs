using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneIntroCameraTransition : MonoBehaviour
{
    private static bool forcePlayNextIntro;

    [SerializeField, InspectorName("Play On Start")] private bool playOnStart = true;
    [SerializeField, InspectorName("Target")] private Transform target;
    [SerializeField, InspectorName("Camera Transform")] private Transform cameraTransform;
    [SerializeField, InspectorName("Look Height")] private float lookHeight = 1.4f;

    [SerializeField, Header("Camera Movement"), InspectorName("Start Offset")] private Vector3 startOffset = new Vector3(0f, 4f, -9f);
    [SerializeField, InspectorName("End Offset")] private Vector3 endOffset = new Vector3(0f, 2.2f, -4.5f);
    [SerializeField, InspectorName("Use Target Relative Offset")] private bool useTargetRelativeOffset = true;
    [SerializeField, InspectorName("Invert Target Relative Offset")] private bool invertTargetRelativeOffset;
    [SerializeField, InspectorName("Orbit Angle")] private float orbitAngle = 35f;
    [SerializeField, InspectorName("Duration")] private float duration = 2.5f;
    [SerializeField, InspectorName("Transition Curve")] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Header("Focus Shot"), InspectorName("Enable Focus Shot")] private bool enableFocusShot;
    [SerializeField, InspectorName("Focus Target")] private Transform focusTarget;
    [SerializeField, InspectorName("Focus Look Height")] private float focusLookHeight = 0.2f;
    [SerializeField, InspectorName("Focus Start Offset")] private Vector3 focusStartOffset = new Vector3(0.6f, 0.4f, -2.2f);
    [SerializeField, InspectorName("Focus End Offset")] private Vector3 focusEndOffset = new Vector3(0f, 0.25f, -1.15f);
    [SerializeField, InspectorName("Focus Orbit Angle")] private float focusOrbitAngle = 12f;
    [SerializeField, InspectorName("Focus Duration")] private float focusDuration = 1.2f;
    [SerializeField, InspectorName("Focus Hold Duration")] private float focusHoldDuration = 0.45f;
    [SerializeField, InspectorName("Focus Use Target Rotation")] private bool focusUseTargetRotation = true;
    [SerializeField, InspectorName("Restrict Focus Shot To Scene")] private bool restrictFocusShotToScene = true;
    [SerializeField, InspectorName("Focus Shot Scene Name")] private string focusShotSceneName = "2_Game Scene";
    [SerializeField, InspectorName("Focus Transition Curve")] private AnimationCurve focusTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [SerializeField, Header("Control"), InspectorName("Disable Player Input")] private bool disablePlayerInput = true;
    [SerializeField, InspectorName("Disable Camera Controllers")] private bool disableCameraControllers = true;
    [SerializeField, InspectorName("Restore Camera When Finished")] private bool restoreCameraWhenFinished = true;
    [SerializeField, Header("Finish"), InspectorName("Fade When Finished")] private bool fadeWhenFinished = true;
    [SerializeField, InspectorName("Fade Out Duration")] private float fadeOutDuration = 0.35f;
    [SerializeField, InspectorName("Fade Hold Duration")] private float fadeHoldDuration = 0.08f;
    [SerializeField, InspectorName("Fade In Duration")] private float fadeInDuration = 0.45f;

    private CharacterInputSystem characterInputSystem;
    private TP_CameraController tpCameraController;
    private UnityTemplateProjects.SimpleCameraController simpleCameraController;
    private CanvasGroup fadeCanvasGroup;
    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;
    private bool originalInputEnabled;
    private bool originalTpCameraEnabled;
    private bool originalSimpleCameraEnabled;
    private Coroutine transitionCoroutine;
    private Coroutine delayedPlayCoroutine;

    public bool IsTransitioning => transitionCoroutine != null;
    public bool ExpectedInputEnabled => IsTransitioning ? originalInputEnabled : characterInputSystem != null && characterInputSystem.enabled;
    public bool ExpectedTpCameraEnabled => IsTransitioning ? originalTpCameraEnabled : tpCameraController != null && tpCameraController.enabled;
    public bool ExpectedSimpleCameraEnabled => IsTransitioning ? originalSimpleCameraEnabled : simpleCameraController != null && simpleCameraController.enabled;

    public static void RequestPlayOnNextSceneLoad()
    {
        forcePlayNextIntro = true;
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        bool shouldPlayOnStart = playOnStart || forcePlayNextIntro;
        forcePlayNextIntro = false;

        if (shouldPlayOnStart)
        {
            delayedPlayCoroutine = StartCoroutine(PlayIntroWhenReady());
        }
    }

    public void PlayIntro()
    {
        ResolveReferences(forceRefresh: true);

        if (target == null || cameraTransform == null)
        {
            Debug.LogWarning("[SceneIntroCameraTransition] Missing target or camera transform, intro cannot play.", this);
            return;
        }

        if (delayedPlayCoroutine != null)
        {
            StopCoroutine(delayedPlayCoroutine);
            delayedPlayCoroutine = null;
        }

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }

        transitionCoroutine = StartCoroutine(IntroRoutine());
    }

    private IEnumerator PlayIntroWhenReady()
    {
        // Give the scene one frame to finish object initialization after loading.
        yield return null;
        yield return new WaitForEndOfFrame();

        const float maxWaitTime = 1.5f;
        float timer = 0f;

        while (timer < maxWaitTime)
        {
            ResolveReferences(forceRefresh: true);

            if (target != null && cameraTransform != null)
            {
                PlayIntro();
                yield break;
            }

            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        PlayIntro();
    }

    private IEnumerator IntroRoutine()
    {
        CacheAndDisableControls();

        originalCameraPosition = cameraTransform.position;
        originalCameraRotation = cameraTransform.rotation;

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            float curvedProgress = transitionCurve.Evaluate(progress);

            float currentOrbitAngle = Mathf.Lerp(orbitAngle, 0f, curvedProgress);
            Vector3 currentOffset = Vector3.Lerp(startOffset, endOffset, curvedProgress);
            Quaternion orbitRotation = Quaternion.Euler(0f, currentOrbitAngle, 0f);

            Vector3 lookPoint = GetLookPoint();
            cameraTransform.position = lookPoint + GetCameraOffset(currentOffset, orbitRotation);
            cameraTransform.rotation = Quaternion.LookRotation(lookPoint - cameraTransform.position, Vector3.up);

            yield return null;
        }

        Vector3 finalLookPoint = GetLookPoint();
        cameraTransform.position = finalLookPoint + GetCameraOffset(endOffset, Quaternion.identity);
        cameraTransform.rotation = Quaternion.LookRotation(finalLookPoint - cameraTransform.position, Vector3.up);

        if (ShouldPlayFocusShot())
        {
            yield return PlayFocusShot();
        }

        if (ShouldRestoreCameraWhenFinished())
        {
            if (fadeWhenFinished)
            {
                yield return FadeScreen(1f, fadeOutDuration);
                yield return new WaitForSecondsRealtime(fadeHoldDuration);
            }

            cameraTransform.position = originalCameraPosition;
            cameraTransform.rotation = originalCameraRotation;
        }
        else if (fadeWhenFinished)
        {
            yield return FadeScreen(1f, fadeOutDuration);
            yield return new WaitForSecondsRealtime(fadeHoldDuration);
        }

        RestoreControls();

        if (fadeWhenFinished)
        {
            yield return FadeScreen(0f, fadeInDuration);
        }

        transitionCoroutine = null;
    }

    private bool ShouldRestoreCameraWhenFinished()
    {
        return restoreCameraWhenFinished || disableCameraControllers;
    }

    private bool ShouldPlayFocusShot()
    {
        if (!enableFocusShot || focusTarget == null || cameraTransform == null)
        {
            return false;
        }

        if (!restrictFocusShotToScene)
        {
            return true;
        }

        return SceneManager.GetActiveScene().name == focusShotSceneName;
    }

    private void ResolveReferences(bool forceRefresh = false)
    {
        if ((forceRefresh || cameraTransform == null) && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (forceRefresh || target == null)
        {
            CharacterInputSystem inputSystem = FindFirstObjectByType<CharacterInputSystem>();
            if (inputSystem != null)
            {
                target = inputSystem.transform;
            }
            else
            {
                GameObject player = GameObject.Find("Player (1)");
                if (player != null)
                {
                    target = player.transform;
                }
            }
        }

        if (forceRefresh || characterInputSystem == null)
        {
            characterInputSystem = FindFirstObjectByType<CharacterInputSystem>();
        }

        if (forceRefresh || tpCameraController == null)
        {
            tpCameraController = FindFirstObjectByType<TP_CameraController>();
        }

        if (forceRefresh || simpleCameraController == null)
        {
            simpleCameraController = FindFirstObjectByType<UnityTemplateProjects.SimpleCameraController>();
        }
    }

    private Vector3 GetLookPoint()
    {
        return target.position + Vector3.up * lookHeight;
    }

    private Vector3 GetFocusLookPoint()
    {
        return focusTarget.position + Vector3.up * focusLookHeight;
    }

    private Vector3 GetCameraOffset(Vector3 offset, Quaternion extraRotation)
    {
        float targetYaw = target.eulerAngles.y;
        if (invertTargetRelativeOffset)
        {
            targetYaw += 180f;
        }

        Quaternion baseRotation = useTargetRelativeOffset
            ? Quaternion.Euler(0f, targetYaw, 0f)
            : Quaternion.identity;

        return baseRotation * extraRotation * offset;
    }

    private Vector3 GetFocusOffset(Vector3 offset, Quaternion extraRotation)
    {
        Quaternion baseRotation = focusUseTargetRotation
            ? Quaternion.Euler(0f, focusTarget.eulerAngles.y, 0f)
            : Quaternion.identity;

        return baseRotation * extraRotation * offset;
    }

    private IEnumerator PlayFocusShot()
    {
        float timer = 0f;

        while (timer < focusDuration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / Mathf.Max(focusDuration, Mathf.Epsilon));
            float curvedProgress = focusTransitionCurve.Evaluate(progress);

            float currentOrbitAngle = Mathf.Lerp(focusOrbitAngle, 0f, curvedProgress);
            Vector3 currentOffset = Vector3.Lerp(focusStartOffset, focusEndOffset, curvedProgress);
            Quaternion orbitRotation = Quaternion.Euler(0f, currentOrbitAngle, 0f);

            Vector3 lookPoint = GetFocusLookPoint();
            cameraTransform.position = lookPoint + GetFocusOffset(currentOffset, orbitRotation);
            cameraTransform.rotation = Quaternion.LookRotation(lookPoint - cameraTransform.position, Vector3.up);

            yield return null;
        }

        Vector3 finalLookPoint = GetFocusLookPoint();
        cameraTransform.position = finalLookPoint + GetFocusOffset(focusEndOffset, Quaternion.identity);
        cameraTransform.rotation = Quaternion.LookRotation(finalLookPoint - cameraTransform.position, Vector3.up);

        if (focusHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(focusHoldDuration);
        }
    }

    private void CacheAndDisableControls()
    {
        if (characterInputSystem != null)
        {
            originalInputEnabled = characterInputSystem.enabled;
            if (disablePlayerInput)
            {
                characterInputSystem.enabled = false;
            }
        }

        if (tpCameraController != null)
        {
            originalTpCameraEnabled = tpCameraController.enabled;
            if (disableCameraControllers)
            {
                tpCameraController.enabled = false;
            }
        }

        if (simpleCameraController != null)
        {
            originalSimpleCameraEnabled = simpleCameraController.enabled;
            if (disableCameraControllers)
            {
                simpleCameraController.enabled = false;
            }
        }
    }

    private void RestoreControls()
    {
        if (characterInputSystem != null)
        {
            characterInputSystem.enabled = originalInputEnabled;
        }

        if (tpCameraController != null)
        {
            tpCameraController.enabled = originalTpCameraEnabled;
        }

        if (simpleCameraController != null)
        {
            simpleCameraController.enabled = originalSimpleCameraEnabled;
        }
    }

    private IEnumerator FadeScreen(float targetAlpha, float fadeDuration)
    {
        EnsureFadeCanvas();

        if (fadeCanvasGroup == null)
        {
            yield break;
        }

        float startAlpha = fadeCanvasGroup.alpha;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = fadeDuration <= 0f ? 1f : Mathf.Clamp01(timer / fadeDuration);
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

        GameObject canvasObject = new GameObject("Intro Camera Fade Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        fadeCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
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
