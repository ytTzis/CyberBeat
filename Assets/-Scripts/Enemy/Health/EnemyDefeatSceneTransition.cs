using System.Collections;
using UGG.Health;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EnemyDefeatSceneTransition : MonoBehaviour
{
    [System.Serializable]
    private struct SceneTransitionEntry
    {
        public string sourceSceneName;
        public string nextSceneName;
    }

    [Header("Scene")]
    [SerializeField] private string sourceSceneName = "1_GameScene";
    [SerializeField] private string nextSceneName = "2_Game Scene";
    [SerializeField] private SceneTransitionEntry[] sceneTransitions;

    [Header("Timing")]
    [SerializeField] private float delayBeforeFade = 1f;
    [SerializeField] private float fadeOutDuration = 0.9f;
    [SerializeField] private float holdBlackDuration = 0.1f;

    [Header("References")]
    [SerializeField] private AIHealthSystem targetHealthSystem;

    private CanvasGroup fadeCanvasGroup;
    private bool transitionStarted;

    private void Awake()
    {
        if (targetHealthSystem == null)
        {
            targetHealthSystem = GetComponent<AIHealthSystem>();
        }
    }

    private void Update()
    {
        if (transitionStarted || targetHealthSystem == null)
        {
            return;
        }

        if (!CanTransitionFromCurrentScene() || !targetHealthSystem.IsDead())
        {
            return;
        }

        transitionStarted = true;
        StartCoroutine(TransitionRoutine());
    }

    private bool CanTransitionFromCurrentScene()
    {
        return !string.IsNullOrEmpty(GetResolvedNextSceneName());
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

        string resolvedNextSceneName = GetResolvedNextSceneName();

        if (string.IsNullOrEmpty(resolvedNextSceneName))
        {
            Debug.LogWarning("[EnemyDefeatSceneTransition] No scene transition is configured for the current scene.", this);
            yield break;
        }

        SceneIntroCameraTransition.RequestPlayOnNextSceneLoad();
        SceneManager.LoadScene(resolvedNextSceneName);
    }

    private string GetResolvedNextSceneName()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;

        if (sceneTransitions != null)
        {
            for (int i = 0; i < sceneTransitions.Length; i++)
            {
                SceneTransitionEntry entry = sceneTransitions[i];
                if (!string.IsNullOrEmpty(entry.sourceSceneName) && entry.sourceSceneName == activeSceneName)
                {
                    return entry.nextSceneName;
                }
            }
        }

        if (string.IsNullOrEmpty(sourceSceneName) || sourceSceneName == activeSceneName)
        {
            return nextSceneName;
        }

        return string.Empty;
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

        GameObject canvasObject = new GameObject("Enemy Defeat Fade Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
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
