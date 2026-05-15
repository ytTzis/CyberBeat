using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class FinishSceneFlowController : MonoBehaviour
{
    private const string SceneName = "FinishScene";
    private const string TitleSceneName = "FirstScene";

    [SerializeField] private TMP_Text mainText;
    [SerializeField] private TMP_Text nextText;
    [SerializeField, Min(1f)] private float charactersPerSecond = 24f;
    [SerializeField, Min(0f)] private float startDelay = 0.2f;
    [SerializeField, Min(0f)] private float nextFadeDuration = 0.5f;

    private float elapsed;
    private int totalCharacters;
    private float nextFadeElapsed;
    private bool isTyping = true;
    private bool isFadingNext;
    private bool canLoadTitleScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapOnSceneLoad()
    {
        if (SceneManager.GetActiveScene().name != SceneName)
        {
            return;
        }

        if (FindFirstObjectByType<FinishSceneFlowController>() != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject(nameof(FinishSceneFlowController));
        controllerObject.AddComponent<FinishSceneFlowController>();
    }

    private void Awake()
    {
        ResolveTextReferences();
        PrepareTextState();
    }

    private void OnEnable()
    {
        elapsed = 0f;
        nextFadeElapsed = 0f;
        isTyping = true;
        isFadingNext = false;
        canLoadTitleScene = false;

        PrepareTextState();
    }

    private void Update()
    {
        if (mainText == null || totalCharacters <= 0)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (isTyping)
            {
                CompleteTyping();
                return;
            }

            if (canLoadTitleScene)
            {
                SceneManager.LoadScene(TitleSceneName);
                return;
            }
        }

        if (isFadingNext)
        {
            UpdateNextFade();
            return;
        }

        if (!isTyping)
        {
            return;
        }

        elapsed += Time.unscaledDeltaTime;
        float revealTime = elapsed - startDelay;
        if (revealTime <= 0f)
        {
            return;
        }

        int visibleCharacters = Mathf.Clamp(Mathf.FloorToInt(revealTime * charactersPerSecond), 0, totalCharacters);
        mainText.maxVisibleCharacters = visibleCharacters;

        if (visibleCharacters >= totalCharacters)
        {
            CompleteTyping();
        }
    }

    private void ResolveTextReferences()
    {
        if (mainText != null && nextText != null)
        {
            return;
        }

        TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (texts == null || texts.Length == 0)
        {
            return;
        }

        TMP_Text longestText = null;
        TMP_Text shortestText = null;
        int longestLength = -1;
        int shortestLength = int.MaxValue;

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text candidate = texts[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.text))
            {
                continue;
            }

            int length = candidate.text.Length;
            if (length > longestLength)
            {
                longestLength = length;
                longestText = candidate;
            }

            if (length < shortestLength)
            {
                shortestLength = length;
                shortestText = candidate;
            }
        }

        if (mainText == null)
        {
            mainText = longestText;
        }

        if (nextText == null)
        {
            nextText = shortestText != mainText ? shortestText : null;
        }
    }

    private void PrepareTextState()
    {
        if (mainText == null)
        {
            ResolveTextReferences();
        }

        if (mainText == null)
        {
            enabled = false;
            return;
        }

        mainText.ForceMeshUpdate();
        totalCharacters = mainText.textInfo.characterCount;
        mainText.maxVisibleCharacters = 0;

        if (nextText != null)
        {
            SetTextAlpha(nextText, 0f);
        }
    }

    private void CompleteTyping()
    {
        isTyping = false;
        mainText.maxVisibleCharacters = totalCharacters;
        StartNextFade();
    }

    private void StartNextFade()
    {
        if (nextText == null)
        {
            canLoadTitleScene = true;
            return;
        }

        nextFadeElapsed = 0f;
        isFadingNext = true;
        SetTextAlpha(nextText, 0f);
    }

    private void UpdateNextFade()
    {
        if (nextText == null)
        {
            isFadingNext = false;
            canLoadTitleScene = true;
            return;
        }

        nextFadeElapsed += Time.unscaledDeltaTime;
        float alpha = nextFadeDuration <= 0f ? 1f : Mathf.Clamp01(nextFadeElapsed / nextFadeDuration);
        SetTextAlpha(nextText, alpha);

        if (alpha >= 1f)
        {
            isFadingNext = false;
            canLoadTitleScene = true;
        }
    }

    private static void SetTextAlpha(TMP_Text textComponent, float alpha)
    {
        if (textComponent == null)
        {
            return;
        }

        Color color = textComponent.color;
        color.a = alpha;
        textComponent.color = color;
    }
}
