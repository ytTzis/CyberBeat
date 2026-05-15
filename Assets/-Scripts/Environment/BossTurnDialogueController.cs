using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BossTurnDialogueController : MonoBehaviour
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
    [SerializeField] private string dialogueObjectName = "Dialogue(5)";
    [SerializeField] private TMP_Text dialogueTmpText;
    [SerializeField] private Text dialogueLegacyText;
    [SerializeField, TextArea(2, 6)] private string dialogueMessage;
    [SerializeField, Min(1f)] private float charactersPerSecond = 24f;
    [SerializeField, Min(0f)] private float typingStartDelay = 0.05f;

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
    private string resolvedDialogueMessage;

    public bool IsDialogueActiveOrTransitioning => isDialogueOpen || isTransitioning;

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

    public bool ShowDialogueAfterBossTurn(bool onlyOnce = true)
    {
        if (onlyOnce && hasShownDialogue)
        {
            return false;
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        hasShownDialogue = true;
        OpenDialogue();
        return true;
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
            Debug.LogWarning("[BossTurnDialogueController] Dialogue object was not assigned or found.");
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

        isTransitioning = false;
        IsBlockingPauseMenu = false;
        transitionCoroutine = null;
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
        SetDialogueText(resolvedDialogueMessage);
        ApplyVisibleCharacterCount(0);

        if (typingStartDelay > 0f)
        {
            yield return WaitForSecondsRealtimeSafe(typingStartDelay);
        }

        int totalCharacters = resolvedDialogueMessage.Length;
        float visibleCharacters = 0f;

        while (visibleCharacters < totalCharacters)
        {
            visibleCharacters += charactersPerSecond * Time.unscaledDeltaTime;
            ApplyVisibleCharacterCount(Mathf.Clamp(Mathf.FloorToInt(visibleCharacters), 0, totalCharacters));
            yield return null;
        }

        ApplyVisibleCharacterCount(totalCharacters);
        isTyping = false;
        typingCoroutine = null;
    }

    private void CompleteTypingImmediately()
    {
        StopTyping();
        SetDialogueText(resolvedDialogueMessage);
        ApplyVisibleCharacterCount(resolvedDialogueMessage.Length);
    }

    private void SetDialogueText(string value)
    {
        if (dialogueTmpText != null)
        {
            dialogueTmpText.text = value;
        }

        if (dialogueLegacyText != null)
        {
            dialogueLegacyText.text = value;
        }
    }

    private void ApplyVisibleCharacterCount(int count)
    {
        if (dialogueTmpText != null)
        {
            dialogueTmpText.maxVisibleCharacters = count;
        }

        if (dialogueLegacyText != null)
        {
            dialogueLegacyText.text = resolvedDialogueMessage.Substring(0, Mathf.Clamp(count, 0, resolvedDialogueMessage.Length));
        }
    }

    private IEnumerator FadeDialogue(float from, float to, float duration)
    {
        if (dialogueCanvasGroup == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            dialogueCanvasGroup.alpha = to;
            yield break;
        }

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            dialogueCanvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(timer / duration));
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

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(from, to, Mathf.Clamp01(timer / duration));
            yield return null;
        }

        Time.timeScale = to;
    }

    private void SetDialogueAlpha(float alpha)
    {
        if (dialogueCanvasGroup != null)
        {
            dialogueCanvasGroup.alpha = alpha;
        }
    }

    private void SetDialogueInteraction(bool enabled)
    {
        if (dialogueCanvasGroup == null)
        {
            return;
        }

        dialogueCanvasGroup.interactable = enabled;
        dialogueCanvasGroup.blocksRaycasts = enabled;
    }

    private float GetResumeTimeScale()
    {
        return previousTimeScale <= 0f ? 1f : previousTimeScale;
    }

    private void ResumeGameImmediate()
    {
        Time.timeScale = GetResumeTimeScale();
        isDialogueOpen = false;
        isTransitioning = false;
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
