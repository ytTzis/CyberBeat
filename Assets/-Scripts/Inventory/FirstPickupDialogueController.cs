using System.Collections;
using UnityEngine;

public class FirstPickupDialogueController : MonoBehaviour
{
    public static bool IsBlockingPauseMenu { get; private set; }
    public static bool IsBlockingAttackInput => Time.unscaledTime < attackInputBlockedUntil;

    private static float attackInputBlockedUntil;

    [Header("Dialogue")]
    [SerializeField] private GameObject dialogueRoot;
    [SerializeField] private string dialogueObjectName = "Dialogue";

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
    private float previousTimeScale = 1f;
    private CanvasGroup dialogueCanvasGroup;
    private Coroutine transitionCoroutine;

    private void Awake()
    {
        if (dialogueRoot == null)
        {
            GameObject foundDialogue = GameObject.Find(dialogueObjectName);
            if (foundDialogue != null)
            {
                dialogueRoot = foundDialogue;
            }
        }

        CacheDialogueCanvasGroup();

        if (hideDialogueOnStart)
        {
            SetDialogueVisible(false);
        }
    }

    private void OnEnable()
    {
        InventoryManager.ItemAdded += HandleItemPickedUp;
    }

    private void OnDisable()
    {
        InventoryManager.ItemAdded -= HandleItemPickedUp;

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
            CloseDialogue();
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

    private void HandleItemPickedUp(string itemId)
    {
        if (hasShownDialogue)
        {
            return;
        }

        hasShownDialogue = true;
        OpenDialogue();
    }

    private void OpenDialogue()
    {
        if (dialogueRoot == null)
        {
            Debug.LogWarning("[FirstPickupDialogueController] Dialogue object was not assigned or found.");
            return;
        }

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
        SetDialogueVisible(true);
        SetDialogueAlpha(0f);
        SetDialogueInteraction(false);

        yield return LerpTimeScale(previousTimeScale, slowedTimeScale, slowdownDuration);
        yield return FadeDialogue(0f, 1f, dialogueFadeDuration);

        Time.timeScale = 0f;
        isDialogueOpen = true;
        isTransitioning = false;
        SetDialogueInteraction(true);
        transitionCoroutine = null;
    }

    private IEnumerator CloseDialogueRoutine()
    {
        isTransitioning = true;
        isDialogueOpen = false;
        SetDialogueInteraction(false);

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
