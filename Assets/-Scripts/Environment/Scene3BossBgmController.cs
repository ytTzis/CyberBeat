using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class Scene3BossBgmController : MonoBehaviour
{
    private const string DefaultObjectName = "BossBgm";

    [Header("Scene")]
    [SerializeField] private bool restrictToScene = true;
    [SerializeField] private string sceneName = "3_GameScene";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip musicClip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField] private bool loop = true;

    private void Reset()
    {
        EnsureAudioSource();
        ConfigureAudioSource();
    }

    private void Awake()
    {
        EnsureAudioSource();
        ConfigureAudioSource();
    }

    public void PlayMusic()
    {
        if (!IsSceneAllowed())
        {
            return;
        }

        EnsureAudioSource();
        ConfigureAudioSource();

        if (musicClip != null)
        {
            audioSource.clip = musicClip;
        }

        if (audioSource == null)
        {
            Debug.LogWarning("[Scene3BossBgmController] Missing AudioSource on BossBgm.", this);
            return;
        }

        if (audioSource.clip == null)
        {
            Debug.LogWarning("[Scene3BossBgmController] No music clip assigned on BossBgm AudioSource or controller.", this);
            return;
        }

        if (audioSource.isPlaying)
        {
            return;
        }

        audioSource.Play();
    }

    public void StopMusic()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    public static Scene3BossBgmController FindInScene()
    {
        Scene3BossBgmController controller =
            FindFirstObjectByType<Scene3BossBgmController>(FindObjectsInactive.Include);
        if (controller != null)
        {
            return controller;
        }

        Transform[] sceneTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        GameObject bossBgmObject = null;
        for (int i = 0; i < sceneTransforms.Length; i++)
        {
            Transform candidate = sceneTransforms[i];
            if (candidate == null || candidate.name != DefaultObjectName)
            {
                continue;
            }

            bossBgmObject = candidate.gameObject;
            break;
        }

        if (bossBgmObject == null)
        {
            return null;
        }

        controller = bossBgmObject.GetComponent<Scene3BossBgmController>();
        if (controller == null)
        {
            controller = bossBgmObject.AddComponent<Scene3BossBgmController>();
        }

        return controller;
    }

    private bool IsSceneAllowed()
    {
        return !restrictToScene || SceneManager.GetActiveScene().name == sceneName;
    }

    private void EnsureAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void ConfigureAudioSource()
    {
        if (audioSource == null)
        {
            return;
        }

        audioSource.playOnAwake = false;
        audioSource.loop = loop;
        audioSource.volume = volume;
        audioSource.spatialBlend = 0f;
        audioSource.dopplerLevel = 0f;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 500f;

        if (musicClip != null)
        {
            audioSource.clip = musicClip;
        }
    }
}
