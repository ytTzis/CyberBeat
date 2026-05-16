using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class HeartBeatHighStressAudioController : MonoBehaviour
{
    [SerializeField] private HeartRateStateController stateController;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private bool stopWhenLeavingHighStress = true;

    private bool wasHighStressLastFrame;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        TryAutoBindStateController();

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = true;
        }
    }

    private void OnEnable()
    {
        wasHighStressLastFrame = IsHighStressActive();
        SyncPlaybackImmediate();
    }

    private void Update()
    {
        TryAutoBindStateController();

        bool isHighStress = IsHighStressActive();
        if (isHighStress == wasHighStressLastFrame)
        {
            return;
        }

        wasHighStressLastFrame = isHighStress;
        SyncPlaybackImmediate();
    }

    private void TryAutoBindStateController()
    {
        if (stateController == null)
        {
            stateController = HeartRateStateController.Instance;
        }
    }

    private bool IsHighStressActive()
    {
        return stateController != null &&
               stateController.CurrentState == HeartRateStateController.HeartRateState.HighStress;
    }

    private void SyncPlaybackImmediate()
    {
        if (audioSource == null)
        {
            return;
        }

        if (IsHighStressActive())
        {
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }

            return;
        }

        if (stopWhenLeavingHighStress && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}
