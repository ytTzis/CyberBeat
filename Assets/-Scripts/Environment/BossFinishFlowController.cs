using System.Collections;
using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UGG.Combat;
using UGG.Health;

[DisallowMultipleComponent]
public class BossFinishFlowController : MonoBehaviour
{
    private const string SceneName = "3_GameScene";
    private const string TitleSceneName = "FirstScene";
    private const string BossObjectName = "Enemy2";
    private const string FinishObjectName = "Finish";

    [SerializeField] private CharacterHealthSystemBase bossHealthSystem;
    [SerializeField] private GameObject finishRoot;
    [SerializeField, Min(0f)] private float delayBeforeFade = 0.45f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 1f;
    [SerializeField, Min(0f)] private float clickToContinueDelay = 0.2f;

    private CanvasGroup finishCanvasGroup;
    private bool finishSequenceStarted;
    private bool waitingForContinueClick;
    private float continueClickAvailableAt;
    private CharacterInputSystem characterInputSystem;
    private PlayerCombatSystem playerCombatSystem;
    private TP_CameraController tpCameraController;
    private UnityTemplateProjects.SimpleCameraController simpleCameraController;
    private StarterAssetsInputs starterAssetsInputs;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapOnSceneLoad()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != SceneName)
        {
            return;
        }

        if (FindFirstObjectByType<BossFinishFlowController>() != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject(nameof(BossFinishFlowController));
        controllerObject.AddComponent<BossFinishFlowController>();
    }

    private void Awake()
    {
        ResolveReferences();
        PrepareFinishUi();
    }

    private void Update()
    {
        if (!finishSequenceStarted)
        {
            if (bossHealthSystem == null)
            {
                ResolveBossHealthSystem();
                return;
            }

            if (!bossHealthSystem.IsDead())
            {
                return;
            }

            finishSequenceStarted = true;
            StartCoroutine(ShowFinishSequenceRoutine());
            return;
        }

        if (!waitingForContinueClick || Time.unscaledTime < continueClickAvailableAt)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(TitleSceneName);
        }
    }

    private void ResolveReferences()
    {
        ResolveBossHealthSystem();
        ResolveFinishRoot();

        characterInputSystem = FindFirstObjectByType<CharacterInputSystem>(FindObjectsInactive.Include);
        playerCombatSystem = FindFirstObjectByType<PlayerCombatSystem>(FindObjectsInactive.Include);
        tpCameraController = FindFirstObjectByType<TP_CameraController>(FindObjectsInactive.Include);
        simpleCameraController = FindFirstObjectByType<UnityTemplateProjects.SimpleCameraController>(FindObjectsInactive.Include);
        starterAssetsInputs = FindFirstObjectByType<StarterAssetsInputs>(FindObjectsInactive.Include);
    }

    private void ResolveBossHealthSystem()
    {
        if (bossHealthSystem != null)
        {
            return;
        }

        GameObject bossObject = GameObject.Find(BossObjectName);
        if (bossObject == null)
        {
            return;
        }

        bossHealthSystem = bossObject.GetComponent<CharacterHealthSystemBase>();
        if (bossHealthSystem == null)
        {
            bossHealthSystem = bossObject.GetComponentInChildren<CharacterHealthSystemBase>(true);
        }
    }

    private void ResolveFinishRoot()
    {
        if (finishRoot != null)
        {
            return;
        }

        Transform[] sceneTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sceneTransforms.Length; i++)
        {
            Transform candidate = sceneTransforms[i];
            if (candidate.name != FinishObjectName)
            {
                continue;
            }

            if (candidate.GetComponent<RectTransform>() != null)
            {
                finishRoot = candidate.gameObject;
                return;
            }
        }
    }

    private void PrepareFinishUi()
    {
        if (finishRoot == null)
        {
            return;
        }

        if (!finishRoot.activeSelf)
        {
            finishRoot.SetActive(true);
        }

        finishCanvasGroup = finishRoot.GetComponent<CanvasGroup>();
        if (finishCanvasGroup == null)
        {
            finishCanvasGroup = finishRoot.AddComponent<CanvasGroup>();
        }

        finishCanvasGroup.alpha = 0f;
        finishCanvasGroup.interactable = false;
        finishCanvasGroup.blocksRaycasts = false;
    }

    private IEnumerator ShowFinishSequenceRoutine()
    {
        DisableGameplayControl();

        if (delayBeforeFade > 0f)
        {
            yield return WaitForSecondsRealtimeSafe(delayBeforeFade);
        }

        yield return FadeFinishUi(0f, 1f, fadeDuration);

        waitingForContinueClick = true;
        continueClickAvailableAt = Time.unscaledTime + clickToContinueDelay;
        Time.timeScale = 0f;
    }

    private void DisableGameplayControl()
    {
        if (playerCombatSystem != null)
        {
            playerCombatSystem.enabled = false;
        }

        if (characterInputSystem != null)
        {
            characterInputSystem.enabled = false;
        }

        if (tpCameraController != null)
        {
            tpCameraController.enabled = false;
        }

        if (simpleCameraController != null)
        {
            simpleCameraController.enabled = false;
        }

        if (starterAssetsInputs != null)
        {
            starterAssetsInputs.cursorLocked = false;
            starterAssetsInputs.cursorInputForLook = false;
        }

        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
    }

    private IEnumerator FadeFinishUi(float from, float to, float duration)
    {
        if (finishCanvasGroup == null)
        {
            yield break;
        }

        finishCanvasGroup.blocksRaycasts = true;
        finishCanvasGroup.interactable = false;

        if (duration <= 0f)
        {
            finishCanvasGroup.alpha = to;
            finishCanvasGroup.interactable = true;
            yield break;
        }

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            finishCanvasGroup.alpha = Mathf.Lerp(from, to, progress);
            yield return null;
        }

        finishCanvasGroup.alpha = to;
        finishCanvasGroup.interactable = true;
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
