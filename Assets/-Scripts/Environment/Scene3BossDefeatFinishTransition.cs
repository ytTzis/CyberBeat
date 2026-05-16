using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UGG.Combat;
using UGG.Health;

[DisallowMultipleComponent]
public class Scene3BossDefeatFinishTransition : MonoBehaviour
{
    private const string SourceSceneName = "3_GameScene";
    private const string BossObjectName = "Enemy2";
    private const string FinishSceneName = "FinishScene";

    [SerializeField] private CharacterHealthSystemBase bossHealthSystem;
    [SerializeField, Min(0f)] private float delayBeforeFade = 0.45f;
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 1f;
    [SerializeField, Min(0f)] private float holdBlackDuration = 0.1f;

    private CanvasGroup fadeCanvasGroup;
    private bool transitionStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapOnSceneLoad()
    {
        EnsurePresentInScene();
    }

    public static Scene3BossDefeatFinishTransition EnsurePresentInScene()
    {
        if (SceneManager.GetActiveScene().name != SourceSceneName)
        {
            return null;
        }

        Scene3BossDefeatFinishTransition existingController =
            FindFirstObjectByType<Scene3BossDefeatFinishTransition>(FindObjectsInactive.Include);
        if (existingController != null)
        {
            return existingController;
        }

        GameObject controllerObject = new GameObject(nameof(Scene3BossDefeatFinishTransition));
        return controllerObject.AddComponent<Scene3BossDefeatFinishTransition>();
    }

    public void SetBossHealthSystem(CharacterHealthSystemBase healthSystem)
    {
        if (healthSystem == null)
        {
            return;
        }

        bossHealthSystem = healthSystem;
    }

    private void Awake()
    {
        ResolveBossHealthSystem();
    }

    private void Update()
    {
        if (transitionStarted)
        {
            return;
        }

        if (bossHealthSystem == null)
        {
            ResolveBossHealthSystem();
            return;
        }

        if (!bossHealthSystem.IsDead())
        {
            return;
        }

        transitionStarted = true;
        StartCoroutine(TransitionRoutine());
    }

    private void ResolveBossHealthSystem()
    {
        if (bossHealthSystem != null)
        {
            return;
        }

        CharacterHealthSystemBase[] healthSystems =
            FindObjectsByType<CharacterHealthSystemBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (healthSystems == null || healthSystems.Length == 0)
        {
            return;
        }

        for (int i = 0; i < healthSystems.Length; i++)
        {
            CharacterHealthSystemBase healthSystem = healthSystems[i];
            if (healthSystem == null)
            {
                continue;
            }

            if (healthSystem.gameObject.name == BossObjectName ||
                healthSystem.transform.root.name == BossObjectName)
            {
                bossHealthSystem = healthSystem;
                return;
            }
        }

        for (int i = 0; i < healthSystems.Length; i++)
        {
            if (healthSystems[i] != null && healthSystems[i].enabled)
            {
                bossHealthSystem = healthSystems[i];
                return;
            }
        }

        bossHealthSystem = healthSystems[0];
    }

    private IEnumerator TransitionRoutine()
    {
        DisablePlayerControls();
        StopBossMusic();
        EnsureFadeCanvas();

        if (delayBeforeFade > 0f)
        {
            yield return WaitForSecondsRealtimeSafe(delayBeforeFade);
        }

        yield return FadeScreen(1f, fadeOutDuration);

        if (holdBlackDuration > 0f)
        {
            yield return WaitForSecondsRealtimeSafe(holdBlackDuration);
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(FinishSceneName);
    }

    private void StopBossMusic()
    {
        Scene3BossBgmController backgroundMusicController =
            Scene3BossBgmController.FindInScene();

        if (backgroundMusicController != null)
        {
            backgroundMusicController.StopMusic();
        }
    }

    private void DisablePlayerControls()
    {
        CharacterInputSystem inputSystem = FindFirstObjectByType<CharacterInputSystem>(FindObjectsInactive.Include);
        if (inputSystem != null)
        {
            inputSystem.enabled = false;
        }

        PlayerCombatSystem playerCombatSystem = FindFirstObjectByType<PlayerCombatSystem>(FindObjectsInactive.Include);
        if (playerCombatSystem != null)
        {
            playerCombatSystem.enabled = false;
        }

        TP_CameraController tpCameraController = FindFirstObjectByType<TP_CameraController>(FindObjectsInactive.Include);
        if (tpCameraController != null)
        {
            tpCameraController.enabled = false;
        }

        UnityTemplateProjects.SimpleCameraController simpleCameraController =
            FindFirstObjectByType<UnityTemplateProjects.SimpleCameraController>(FindObjectsInactive.Include);
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

        GameObject canvasObject = new GameObject("Boss Defeat Fade Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
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
}
