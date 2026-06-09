using UnityEngine;
using UGG.Health;
using StarterAssets;
using UnityEngine.SceneManagement;
using System.Collections;
using UGG.Combat;
using UnityEngine.UI;
using TMPro;

public class PauseMenuController : MonoBehaviour
{
    private const string TitleSceneName = "FirstScene";
    private const string MusicSliderObjectName = "MusicUI";
    private const string SensitivitySliderObjectName = "SensitivityUI";
    private const string NonCombatUiObjectName = "Heartrate";
    private const string RetryButtonObjectName = "Retry";
    private const string QuitButtonObjectName = "Quit";
    private const float SliderCenterValue = 0.5f;

    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private PlayerHealthSystem playerHealth;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TMP_Text musicValueText;
    [SerializeField] private TMP_Text sensitivityValueText;
    [SerializeField] private GameObject nonCombatUIRoot;
    [SerializeField] private float deathMenuDelay = 0.75f;
    [SerializeField] private float deathMenuFadeDuration = 1f;
    [SerializeField] private Button gameOverRetryButton;
    [SerializeField] private Button gameOverQuitButton;

    private bool menuOpen;
    private bool deathMenuShown;
    private CanvasGroup menuCanvasGroup;
    private CanvasGroup gameOverCanvasGroup;
    private bool hideWithCanvasGroup;
    private bool hideGameOverWithCanvasGroup;
    private CanvasGroup nonCombatUICanvasGroup;
    private TP_CameraController tpCameraController;
    private UnityTemplateProjects.SimpleCameraController simpleCameraController;
    private SceneIntroCameraTransition sceneIntroCameraTransition;
    private StarterAssetsInputs starterAssetsInputs;
    private PlayerCombatSystem playerCombatSystem;
    private CharacterInputSystem characterInputSystem;
    private bool starterCursorLockedBeforeMenu;
    private bool starterCursorInputForLookBeforeMenu;
    private bool tpCameraEnabledBeforeMenu;
    private bool simpleCameraEnabledBeforeMenu;
    private bool combatEnabledBeforeMenu;
    private bool inputEnabledBeforeMenu;
    private Coroutine restoreGameplayCoroutine;
    private Coroutine deathMenuCoroutine;
    private float baseMusicVolume;
    private float baseMouseSensitivity;

    private void Awake()
    {
        menuOpen = false;
        deathMenuShown = false;
        tpCameraController = FindObjectOfType<TP_CameraController>(true);
        simpleCameraController = FindObjectOfType<UnityTemplateProjects.SimpleCameraController>(true);
        sceneIntroCameraTransition = FindObjectOfType<SceneIntroCameraTransition>(true);
        starterAssetsInputs = FindObjectOfType<StarterAssetsInputs>(true);
        playerCombatSystem = FindObjectOfType<PlayerCombatSystem>(true);
        characterInputSystem = FindObjectOfType<CharacterInputSystem>(true);
        TryAutoBindPlayerHealth();
        baseMusicVolume = AudioListener.volume;
        baseMouseSensitivity = tpCameraController != null ? tpCameraController.mouseInputSpeed : 0.1f;
        ResolveNonCombatUIRoot();
        ResolveGameOverPanel();
        ResolveGameOverButtons();

        if (menuPanel != null)
        {
            hideWithCanvasGroup = menuPanel == gameObject;

            if (hideWithCanvasGroup)
            {
                menuCanvasGroup = menuPanel.GetComponent<CanvasGroup>();

                if (menuCanvasGroup == null)
                {
                    menuCanvasGroup = menuPanel.AddComponent<CanvasGroup>();
                }
            }

            ConfigureMenuCanvasGroup(0f, false);
            SetMenuVisible(false);
        }

        if (gameOverPanel != null)
        {
            hideGameOverWithCanvasGroup = true;

            gameOverCanvasGroup = gameOverPanel.GetComponent<CanvasGroup>();

            if (gameOverCanvasGroup == null)
            {
                gameOverCanvasGroup = gameOverPanel.AddComponent<CanvasGroup>();
            }

            gameOverPanel.SetActive(true);
            ConfigureGameOverCanvasGroup(0f, false);
            gameOverPanel.SetActive(false);
        }

        BindSettingsSliders();
        BindGameOverButtons();
        EnsureGameOverButtonFeedback();
    }

    private void Update()
    {
        if (!deathMenuShown && playerHealth != null && playerHealth.IsDead())
        {
            deathMenuShown = true;
            BeginDeathMenuSequence();
        }

        if (menuOpen)
        {
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
        }

        if (!deathMenuShown &&
            !IsIntroTransitionActive() &&
            !FirstPickupDialogueController.IsBlockingPauseMenu &&
            !Area2DialogueController.IsBlockingPauseMenu &&
            !Scene2IntroDialogueController.IsBlockingPauseMenu &&
            !Scene3IntroDialogueController.IsBlockingPauseMenu &&
            !BossTurnDialogueController.IsBlockingPauseMenu &&
            Input.GetKeyDown(KeyCode.Tab))
        {
            if (menuOpen) CloseMenu();
            else OpenMenu();
        }
    }

    public void OpenMenu()
    {
        if (deathMenuShown)
        {
            return;
        }

        if (restoreGameplayCoroutine != null)
        {
            StopCoroutine(restoreGameplayCoroutine);
            restoreGameplayCoroutine = null;
        }

        menuOpen = true;
        SetMenuVisible(true);
        ConfigureMenuCanvasGroup(1f, true);
        Time.timeScale = 0f;
        ReleaseCursorControl();
    }

    public void CloseMenu()
    {
        if (deathMenuShown)
        {
            return;
        }

        menuOpen = false;
        SetMenuVisible(false);
        Time.timeScale = 1f;

        if (restoreGameplayCoroutine != null)
        {
            StopCoroutine(restoreGameplayCoroutine);
        }

        restoreGameplayCoroutine = StartCoroutine(RestoreGameplayNextFrame());
    }

    public void ContinueGame()
    {
        if (deathMenuShown)
        {
            return;
        }

        CloseMenu();
    }

    public void RetryGame()
    {
        Time.timeScale = 1f;
        PlayerHealthSystem.ResetPersistentHealth();
        InventoryManager.ResetPersistentInventory();
        ResetHeartRateItemEffects();
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        PlayerHealthSystem.ResetPersistentHealth();
        InventoryManager.ResetPersistentInventory();
        ResetHeartRateItemEffects();
        SceneManager.LoadScene(TitleSceneName);
    }

    private static void ResetHeartRateItemEffects()
    {
        if (HeartRateStateController.Instance != null)
        {
            HeartRateStateController.Instance.ResetItemStateEffects();
        }

        if (HeartRateSimulator.Instance != null)
        {
            HeartRateSimulator.Instance.ResetItemHeartRateEffects();
        }
    }

    private void ReleaseCursorControl()
    {
        SetNonCombatUIInteractionEnabled(false);

        if (playerCombatSystem != null)
        {
            combatEnabledBeforeMenu = playerCombatSystem.enabled;
            playerCombatSystem.enabled = false;
        }

        if (characterInputSystem != null)
        {
            inputEnabledBeforeMenu = GetExpectedInputEnabledForMenu();
            characterInputSystem.enabled = false;
        }

        if (tpCameraController != null)
        {
            tpCameraEnabledBeforeMenu = GetExpectedTpCameraEnabledForMenu();
            tpCameraController.enabled = false;
        }

        if (simpleCameraController != null)
        {
            simpleCameraEnabledBeforeMenu = GetExpectedSimpleCameraEnabledForMenu();
            simpleCameraController.enabled = false;
        }

        if (starterAssetsInputs != null)
        {
            starterCursorLockedBeforeMenu = starterAssetsInputs.cursorLocked;
            starterCursorInputForLookBeforeMenu = starterAssetsInputs.cursorInputForLook;
            starterAssetsInputs.cursorLocked = false;
            starterAssetsInputs.cursorInputForLook = false;
        }

        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
    }

    private void RestoreCursorControl()
    {
        SetNonCombatUIInteractionEnabled(true);

        if (playerCombatSystem != null)
        {
            playerCombatSystem.enabled = combatEnabledBeforeMenu;
        }

        if (characterInputSystem != null)
        {
            characterInputSystem.enabled = inputEnabledBeforeMenu;
        }

        if (tpCameraController != null)
        {
            tpCameraController.enabled = tpCameraEnabledBeforeMenu;
        }

        if (simpleCameraController != null)
        {
            simpleCameraController.enabled = simpleCameraEnabledBeforeMenu;
        }

        if (starterAssetsInputs != null)
        {
            starterAssetsInputs.cursorLocked = starterCursorLockedBeforeMenu;
            starterAssetsInputs.cursorInputForLook = starterCursorInputForLookBeforeMenu;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private IEnumerator RestoreGameplayNextFrame()
    {
        yield return null;
        RestoreCursorControl();
        restoreGameplayCoroutine = null;
    }

    public void OnMusicSliderChanged(float sliderValue)
    {
        float targetVolume = GetValueAroundCenter(sliderValue, baseMusicVolume, 0f, 1f);
        AudioListener.volume = targetVolume;
        UpdateValueText(musicValueText, sliderValue);
    }

    public void OnSensitivitySliderChanged(float sliderValue)
    {
        if (tpCameraController == null)
        {
            return;
        }

        float targetSensitivity = GetValueAroundCenter(sliderValue, baseMouseSensitivity, 0.01f, 1f);
        tpCameraController.mouseInputSpeed = targetSensitivity;
        UpdateValueText(sensitivityValueText, sliderValue);
    }

    private void BindSettingsSliders()
    {
        if (musicSlider == null)
        {
            musicSlider = FindSliderByName(MusicSliderObjectName);
        }

        if (sensitivitySlider == null)
        {
            sensitivitySlider = FindSliderByName(SensitivitySliderObjectName);
        }

        ConfigureSlider(musicSlider, OnMusicSliderChanged);
        ConfigureSlider(sensitivitySlider, OnSensitivitySliderChanged);

        OnMusicSliderChanged(SliderCenterValue);
        OnSensitivitySliderChanged(SliderCenterValue);
    }

    private void ConfigureSlider(Slider slider, UnityEngine.Events.UnityAction<float> onValueChanged)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.onValueChanged.RemoveListener(onValueChanged);
        slider.SetValueWithoutNotify(SliderCenterValue);
        slider.onValueChanged.AddListener(onValueChanged);
    }

    private void BindGameOverButtons()
    {
        if (gameOverRetryButton != null)
        {
            gameOverRetryButton.onClick.RemoveListener(RetryGame);
            gameOverRetryButton.onClick.AddListener(RetryGame);
        }

        if (gameOverQuitButton != null)
        {
            gameOverQuitButton.onClick.RemoveListener(QuitGame);
            gameOverQuitButton.onClick.AddListener(QuitGame);
        }
    }

    private void EnsureGameOverButtonFeedback()
    {
        AddButtonFeedback(gameOverRetryButton);
        AddButtonFeedback(gameOverQuitButton);
    }

    private void AddButtonFeedback(Button button)
    {
        if (button == null)
        {
            return;
        }

        if (button.GetComponent<FirstSceneButtonFeedback>() == null)
        {
            button.gameObject.AddComponent<FirstSceneButtonFeedback>();
        }
    }

    private Slider FindSliderByName(string objectName)
    {
        Transform searchRoot = menuPanel != null ? menuPanel.transform : transform;
        Transform target = FindChildRecursive(searchRoot, objectName);
        return target != null ? target.GetComponentInChildren<Slider>(true) : null;
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent.name == childName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindChildRecursive(parent.GetChild(i), childName);

            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private float GetValueAroundCenter(float sliderValue, float baseValue, float minValue, float maxValue)
    {
        float clampedSliderValue = Mathf.Clamp01(sliderValue);

        if (clampedSliderValue < SliderCenterValue)
        {
            float t = clampedSliderValue / SliderCenterValue;
            return Mathf.Lerp(minValue, baseValue, t);
        }

        float normalizedUpper = (clampedSliderValue - SliderCenterValue) / SliderCenterValue;
        return Mathf.Lerp(baseValue, maxValue, normalizedUpper);
    }

    private void UpdateValueText(TMP_Text targetText, float sliderValue)
    {
        if (targetText == null)
        {
            return;
        }

        float displayValue = Mathf.Clamp01(sliderValue) * 10f;
        targetText.text = displayValue.ToString("0.0");
    }

    private void SetMenuVisible(bool visible)
    {
        if (menuPanel == null)
        {
            return;
        }

        if (hideWithCanvasGroup && menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = visible ? 1f : 0f;
            menuCanvasGroup.interactable = visible;
            menuCanvasGroup.blocksRaycasts = visible;
            return;
        }

        menuPanel.SetActive(visible);
    }

    private void BeginDeathMenuSequence()
    {
        if (deathMenuCoroutine != null)
        {
            StopCoroutine(deathMenuCoroutine);
        }

        deathMenuCoroutine = StartCoroutine(ShowDeathMenuSequence());
    }

    private IEnumerator ShowDeathMenuSequence()
    {
        if (restoreGameplayCoroutine != null)
        {
            StopCoroutine(restoreGameplayCoroutine);
            restoreGameplayCoroutine = null;
        }

        menuOpen = true;
        SetGameOverVisible(true);
        ConfigureGameOverCanvasGroup(0f, false);
        ReleaseCursorControl();

        if (deathMenuDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(deathMenuDelay);
        }

        Time.timeScale = 0f;

        float duration = Mathf.Max(0.01f, deathMenuFadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(elapsed / duration);
            ConfigureGameOverCanvasGroup(alpha, false);
            yield return null;
        }

        ConfigureGameOverCanvasGroup(1f, true);
        deathMenuCoroutine = null;
    }

    private void ConfigureMenuCanvasGroup(float alpha, bool interactive)
    {
        if (menuCanvasGroup == null)
        {
            return;
        }

        menuCanvasGroup.alpha = alpha;
        menuCanvasGroup.interactable = interactive;
        menuCanvasGroup.blocksRaycasts = interactive;
    }

    private void ConfigureGameOverCanvasGroup(float alpha, bool interactive)
    {
        if (gameOverCanvasGroup == null)
        {
            return;
        }

        gameOverCanvasGroup.alpha = alpha;
        gameOverCanvasGroup.interactable = interactive;
        gameOverCanvasGroup.blocksRaycasts = interactive;
    }

    private void ResolveGameOverButtons()
    {
        if (gameOverPanel == null)
        {
            return;
        }

        if (gameOverRetryButton == null)
        {
            Transform retryTransform = FindChildRecursive(gameOverPanel.transform, RetryButtonObjectName);
            gameOverRetryButton = retryTransform != null ? retryTransform.GetComponent<Button>() : null;
        }

        if (gameOverQuitButton == null)
        {
            Transform quitTransform = FindChildRecursive(gameOverPanel.transform, QuitButtonObjectName);
            gameOverQuitButton = quitTransform != null ? quitTransform.GetComponent<Button>() : null;
        }
    }

    private void ResolveGameOverPanel()
    {
        if (gameOverPanel != null)
        {
            return;
        }

        Transform searchRoot = menuPanel != null ? menuPanel.transform : transform;
        Transform panelTransform = FindChildRecursive(searchRoot, "GameOver");
        if (panelTransform != null)
        {
            gameOverPanel = panelTransform.gameObject;
        }
    }

    private void SetGameOverVisible(bool visible)
    {
        if (gameOverPanel == null)
        {
            return;
        }

        gameOverPanel.SetActive(visible);

        if (hideGameOverWithCanvasGroup && gameOverCanvasGroup != null)
        {
            gameOverCanvasGroup.alpha = visible ? 1f : 0f;
            gameOverCanvasGroup.interactable = visible;
            gameOverCanvasGroup.blocksRaycasts = visible;
        }
    }

    private void ResolveNonCombatUIRoot()
    {
        if (nonCombatUIRoot == null)
        {
            GameObject foundObject = GameObject.Find(NonCombatUiObjectName);

            if (foundObject != null)
            {
                nonCombatUIRoot = foundObject;
            }
        }

        if (nonCombatUIRoot == null)
        {
            return;
        }

        nonCombatUICanvasGroup = nonCombatUIRoot.GetComponent<CanvasGroup>();

        if (nonCombatUICanvasGroup == null)
        {
            nonCombatUICanvasGroup = nonCombatUIRoot.AddComponent<CanvasGroup>();
        }
    }

    private void SetNonCombatUIInteractionEnabled(bool enabled)
    {
        if (nonCombatUICanvasGroup == null)
        {
            return;
        }

        nonCombatUICanvasGroup.interactable = enabled;
        nonCombatUICanvasGroup.blocksRaycasts = enabled;
    }

    private void TryAutoBindPlayerHealth()
    {
        if (playerHealth != null)
        {
            return;
        }

        playerHealth = FindFirstObjectByType<PlayerHealthSystem>();
    }

    private bool GetExpectedInputEnabledForMenu()
    {
        if (sceneIntroCameraTransition != null)
        {
            return sceneIntroCameraTransition.ExpectedInputEnabled;
        }

        return characterInputSystem != null && characterInputSystem.enabled;
    }

    private bool GetExpectedTpCameraEnabledForMenu()
    {
        if (sceneIntroCameraTransition != null)
        {
            return sceneIntroCameraTransition.ExpectedTpCameraEnabled;
        }

        return tpCameraController != null && tpCameraController.enabled;
    }

    private bool GetExpectedSimpleCameraEnabledForMenu()
    {
        if (sceneIntroCameraTransition != null)
        {
            return sceneIntroCameraTransition.ExpectedSimpleCameraEnabled;
        }

        return simpleCameraController != null && simpleCameraController.enabled;
    }

    private bool IsIntroTransitionActive()
    {
        return sceneIntroCameraTransition != null && sceneIntroCameraTransition.IsTransitioning;
    }
}
