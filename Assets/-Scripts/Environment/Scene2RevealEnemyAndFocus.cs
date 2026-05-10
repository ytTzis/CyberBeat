using UnityEngine;
using UnityEngine.SceneManagement;

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

    private bool hasTriggered;

    private void Awake()
    {
        if (!IsSceneAllowed() || !hideOnStart || enemyToReveal == null)
        {
            return;
        }

        enemyToReveal.SetActive(false);
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

    private bool IsSceneAllowed()
    {
        if (!restrictToScene)
        {
            return true;
        }

        return SceneManager.GetActiveScene().name == sceneName;
    }
}
