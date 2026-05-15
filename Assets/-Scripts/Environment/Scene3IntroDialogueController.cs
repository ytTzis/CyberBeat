using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Scene3IntroDialogueController : MonoBehaviour, ISceneIntroTransitionReceiver
{
    public static bool IsBlockingPauseMenu { get; private set; }
    public static bool IsBlockingAttackInput => Time.unscaledTime < attackInputBlockedUntil;

    private static float attackInputBlockedUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticStateOnPlay()
    {
        IsBlockingPauseMenu = false;
        attackInputBlockedUntil = 0f;
    }

    [Header("Dialogue")]
    [SerializeField] private GameObject dialogueRoot;
    [SerializeField] private string dialogueObjectName = "Dialogue (4)";
    [SerializeField] private TMP_Text dialogueTmpText;
    [SerializeField] private Text dialogueLegacyText;
    [SerializeField, TextArea(2, 6)] private string dialogueMessage;
    [SerializeField, Min(1f)] private float charactersPerSecond = 24f;
    [SerializeField, Min(0f)] private float typingStartDelay = 0.05f;
    [SerializeField] private bool showOnlyOnce = true;
    [SerializeField, Min(0f)] private float triggerDelay = 0.05f;

    [Header("Transition")]
    [SerializeField, Min(0f)] private float slowdownDuration = 0.25f;
    [SerializeField, Range(0.01f, 1f)] private float slowedTimeScale = 0.2f;
    [SerializeField, Min(0f)] private float dialogueFadeDuration = 0.2f;
    [SerializeField, Min(0f)] private float resumeDuration = 0.2f;

    [Header("Close")]
    [SerializeField] private KeyCode closeKey = KeyCode.Space;
    [SerializeField] private bool allowMouseClickToClose = true;
    [SerializeField] private bool hideDialogueOnStart = true;
    [SerializeField, Min(0f)] private float postCloseAttackBlockDuration = 0.15f;

    private bool hasShownDialogue;
    private bool isDialogueOpen;
    private bool isTransitioning;
    private bool isTyping;
    private float previousTimeScale = 1f;
    private CanvasGroup dialogueCanvasGroup;
    private Coroutine transitionCoroutine;
    private Coroutine typingCoroutine;
    private Coroutine delayedOpenCoroutine;
    private string resolvedDialogueMessage;

    private void Awake()
    {
        ResetStaticStateOnPlay();
        ResolveDialogueRootIfNeeded();
        CacheDialogueCanvasGroup();
        CacheDialogueTextReferences();
        ResolveDialogueMessage();

        if (hideDialogueOnStart)
        {
            SetDialogueVisible(false);
        }
    }

    private void OnDisable()
    {
        if (delayedOpenCoroutine != null)
        {
            StopCoroutine(delayedOpenCoroutine);
            delayedOpenCoroutine = null;
        }

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        isTransitioning = false;

        if (isDialogueOpen || Time.timeScale <= 0f)
        {
            ResumeGameImmediate();
        }

        StopTyping();
        IsBlockingPauseMenu = false;
        SetDialogueVisible(false);
    }

    private void Update()
    {
        if (!isDialogueOpen || isTransitioning)
        {
            return;
        }

        if (Input.GetKeyDown(closeKey) || (allowMouseClickToClose && Input.GetMouseButtonDown(0)))
        {
            if (isTyping)
            {
                CompleteTypingImmediately();
                return;
            }

            CloseDialogue();
        }
    }

    public void OnSceneIntroTransitionFinished()
    {
        if (showOnlyOnce && hasShownDialogue)
        {
            return;
        }

        if (delayedOpenCoroutine != null)
        {
            StopCoroutine(delayedOpenCoroutine);
        }

        delayedOpenCoroutine = StartCoroutine(ShowAfterDelayRoutine());
    }

    private IEnumerator ShowAfterDelayRoutine()
    {
        if (triggerDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(triggerDelay);
        }

        ShowDialogueAfterSceneIntro();
        delayedOpenCoroutine = null;
    }

    public bool ShowDialogueAfterSceneIntro()
    {
        if (showOnlyOnce && hasShownDialogue)
        {
            return false;
        }

        hasShownDialogue = true;
        OpenDialogue();
        return true;
    }

    public void ConfigureDialogue(GameObject newDialogueRoot, string newDialogueObjectName)
    {
        dialogueRoot = newDialogueRoot;
        dialogueObjectName = newDialogueObjectName;
        ResolveDialogueRootIfNeeded();
        CacheDialogueCanvasGroup();
        CacheDialogueTextReferences();
        ResolveDialogueMessage();

        if (hideDialogueOnStart && !isDialogueOpen)
        {
            SetDialogueVisible(false);
        }
    }

    public void CloseDialogue()
    {
        if (!isDialogueOpen || isTransitioning)
        {
            return;
        }

        float totalAttackBlockDuration = dialogueFadeDuration + resumeDuration + postCloseAttackBlockDuration;
        attackInputBlockedUntil = Time.unscaledTime + Mathf.Max(0f, totalAttackBlockDuration);

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }

        transitionCoroutine = StartCoroutine(CloseDialogueRoutine());
    }

    private void ResolveDialogueRootIfNeeded()
    {
        if (dialogueRoot != null || string.IsNullOrWhiteSpace(dialogueObjectName))
        {
            return;
        }

        Transform[] sceneTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sceneTransforms.Length; i++)
        {
            if (sceneTransforms[i].name == dialogueObjectName)
            {
                dialogueRoot = sceneTransforms[i].gameObject;
                return;
            }
        }

        GameObject foundDialogue = GameObject.Find(dialogueObjectName);
        if (foundDialogue != null)
        {
            dialogueRoot = foundDialogue;
        }
    }

    private void OpenDialogue()
    {
        if (dialogueRoot == null)
        {
            Debug.LogWarning("[Scene3IntroDialogueController] Dialogue object was not assigned or found.");
            return;
        }

        CacheDialogueTextReferences();
        ResolveDialogueMessage();
        previousTimeScale = Time.timeScale;

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }

        transitionCoroutine = StartCoroutine(OpenDialogueRoutine());
    }

    private IEnumerator OpenDialogueRoutine()
    {
        isTransitioning = true;
        IsBlockingPauseMenu = true;
        PrepareTypingDisplay();
        SetDialogueVisible(true);
        SetDialogueAlpha(0f);
        SetDialogueInteraction(false);

        yield return LerpTimeScale(previousTimeScale, slowedTimeScale, slowdownDuration);
        yield return FadeDialogue(0f, 1f, dialogueFadeDuration);

        Time.timeScale = 0f;
        isDialogueOpen = true;
        isTransitioning = false;
        SetDialogueInteraction(true);
        StartTyping();
        transitionCoroutine = null;
    }

    private IEnumerator CloseDialogueRoutine()
    {
        isTransitioning = true;
        isDialogueOpen = false;
        SetDialogueInteraction(false);
        StopTyping();

        yield return FadeDialogue(1f, 0f, dialogueFadeDuration);
        SetDialogueVisible(false);
        yield return LerpTimeScale(0f, GetResumeTimeScale(), resumeDuration);
        TriggerBackgroundMusicAfterDialogue();

        isTransitioning = false;
        IsBlockingPauseMenu = false;
        transitionCoroutine = null;
    }

    private void TriggerBackgroundMusicAfterDialogue()
    {
        Scene3BackgroundMusicController backgroundMusicController =
            FindFirstObjectByType<Scene3BackgroundMusicController>(FindObjectsInactive.Exclude);

        if (backgroundMusicController != null)
        {
            backgroundMusicController.PlayMusic();
        }
    }

    private void SetDialogueVisible(bool visible)
    {
        if (dialogueRoot != null)
        {
            dialogueRoot.SetActive(visible);
        }
    }

    private void CacheDialogueCanvasGroup()
    {
        if (dialogueRoot == null)
        {
            dialogueCanvasGroup = null;
            return;
        }

        dialogueCanvasGroup = dialogueRoot.GetComponent<CanvasGroup>();
        if (dialogueCanvasGroup == null)
        {
            dialogueCanvasGroup = dialogueRoot.AddComponent<CanvasGroup>();
        }
    }

    private void CacheDialogueTextReferences()
    {
        if (dialogueRoot == null)
        {
            dialogueTmpText = null;
            dialogueLegacyText = null;
            return;
        }

        if (dialogueTmpText == null)
        {
            dialogueTmpText = dialogueRoot.GetComponentInChildren<TMP_Text>(true);
        }

        if (dialogueLegacyText == null)
        {
            dialogueLegacyText = dialogueRoot.GetComponentInChildren<Text>(true);
        }
    }

    private void ResolveDialogueMessage()
    {
        if (!string.IsNullOrEmpty(dialogueMessage))
        {
            resolvedDialogueMessage = dialogueMessage;
            return;
        }

        if (dialogueTmpText != null)
        {
            resolvedDialogueMessage = dialogueTmpText.text;
            return;
        }

        if (dialogueLegacyText != null)
        {
            resolvedDialogueMessage = dialogueLegacyText.text;
            return;
        }

        resolvedDialogueMessage = string.Empty;
    }

    private void StartTyping()
    {
        StopTyping();

        if (string.IsNullOrEmpty(resolvedDialogueMessage))
        {
            return;
        }

        typingCoroutine = StartCoroutine(TypeDialogueRoutine());
    }

    private void PrepareTypingDisplay()
    {
        if (string.IsNullOrEmpty(resolvedDialogueMessage))
        {
            return;
        }

        ApplyVisibleCharacterCount(0);
    }

    private void StopTyping()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        isTyping = false;
    }

    private IEnumerator TypeDialogueRoutine()
    {
        isTyping = true;
        ApplyVisibleCharacterCount(0);

        if (typingStartDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(typingStartDelay);
        }

        int totalCharacters = resolvedDialogueMessage.Length;
        float elapsed = 0f;

        while (true)
        {
            elapsed += Time.unscaledDeltaTime;
            int visibleCharacters = Mathf.Clamp(Mathf.FloorToInt(elapsed * charactersPerSecond), 0, totalCharacters);
            ApplyVisibleCharacterCount(visibleCharacters);

            if (visibleCharacters >= totalCharacters)
            {
                break;
            }

            yield return null;
        }

        isTyping = false;
        typingCoroutine = null;
    }

    private void CompleteTypingImmediately()
    {
        StopTyping();
        ApplyVisibleCharacterCount(resolvedDialogueMessage.Length);
    }

    private void ApplyVisibleCharacterCount(int visibleCharacters)
    {
        if (dialogueTmpText != null)
        {
            dialogueTmpText.text = resolvedDialogueMessage;
            dialogueTmpText.ForceMeshUpdate();
            dialogueTmpText.maxVisibleCharacters = visibleCharacters;
        }

        if (dialogueLegacyText != null)
        {
            int safeLength = Mathf.Clamp(visibleCharacters, 0, resolvedDialogueMessage.Length);
            dialogueLegacyText.text = resolvedDialogueMessage.Substring(0, safeLength);
        }
    }

    private void SetDialogueAlpha(float alpha)
    {
        CacheDialogueCanvasGroup();
        if (dialogueCanvasGroup != null)
        {
            dialogueCanvasGroup.alpha = alpha;
        }
    }

    private void SetDialogueInteraction(bool interactive)
    {
        CacheDialogueCanvasGroup();
        if (dialogueCanvasGroup != null)
        {
            dialogueCanvasGroup.interactable = interactive;
            dialogueCanvasGroup.blocksRaycasts = interactive;
        }
    }

    private IEnumerator FadeDialogue(float from, float to, float duration)
    {
        CacheDialogueCanvasGroup();
        if (dialogueCanvasGroup == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            dialogueCanvasGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            dialogueCanvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        dialogueCanvasGroup.alpha = to;
    }

    private IEnumerator LerpTimeScale(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            Time.timeScale = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Time.timeScale = Mathf.Lerp(from, to, t);
            yield return null;
        }

        Time.timeScale = to;
    }

    private float GetResumeTimeScale()
    {
        return previousTimeScale <= 0f ? 1f : previousTimeScale;
    }

    private void ResumeGameImmediate()
    {
        Time.timeScale = GetResumeTimeScale();
        isDialogueOpen = false;
        IsBlockingPauseMenu = false;
    }
}
