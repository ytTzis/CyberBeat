using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class Scene3BackgroundMusicController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private bool restrictToScene = true;
    [SerializeField] private string sceneName = "3_GameScene";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip musicClip;
    [SerializeField] [Range(0f, 1f)] private float volume = 0.55f;
    [SerializeField] private bool loop = true;
    [SerializeField] private bool playOnStart = true;

    private void Reset()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        ConfigureAudioSource();
    }

    private void Awake()
    {
        EnsureAudioSource();
        ConfigureAudioSource();
    }

    private void Start()
    {
        if (!playOnStart || !IsSceneAllowed())
        {
            return;
        }

        PlayMusic();
    }

    public void PlayMusic()
    {
        EnsureAudioSource();
        ConfigureAudioSource();

        if (musicClip != null)
        {
            audioSource.clip = musicClip;
        }

        if (audioSource.clip == null || audioSource.isPlaying)
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
