using UnityEngine;
using UnityEngine.SceneManagement;
using UGG.Health;

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
    private Vector3 subwayExitClosedLocalPosition;

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
            return;
        }

        if (!enemyHealthSystem.IsDead())
        {
            return;
        }

        exitOpeningTriggered = true;
        StartCoroutine(OpenSubwayExitRoutine());
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
