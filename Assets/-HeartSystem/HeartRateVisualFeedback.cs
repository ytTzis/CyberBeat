using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public class HeartRateVisualFeedback : MonoBehaviour
{
    [Header("References")]
    public HeartRateSimulator heartRate;
    public HeartRateStateController stateController;

    [Header("UI")]
    public TMP_Text heartRateText;
    public RectTransform heartRateTextRect;
    public Image stressVignette;

    [Header("Text Colors")]
    public Color normalTextColor = Color.white;
    public Color risingTextColor = Color.yellow;
    public Color highTextColor = Color.red;
    public Color recoveringTextColor = Color.green;

    [Header("UI Bounce")]
    public float risingBounceScale = 1.08f;
    public float highBounceScale = 1.18f;
    public float bounceDuration = 0.35f;

    [Header("Volume Effects")]
    public Volume globalVolume;

    [Header("Chromatic Aberration")]
    public float normalChromatic = 0f;
    public float risingChromaticMin = 0.3f;
    public float risingChromaticMax = 0.3f;
    public float highChromaticMin = 0.3f;
    public float highChromaticMax = 1.0f;
    public float recoveringChromatic = 0.02f;
    public float highChromaticPulseSpeed = 5f;
    public float risingChromaticPulseSpeed = 2.5f;

    [Header("Vignette Intensity")]
    public float normalVignetteIntensity = 0.18f;
    public float risingVignetteIntensity = 0.25f;
    public float highVignetteIntensity = 0.42f;
    public float recoveringVignetteIntensity = 0.12f;

    [Header("Effect Smoothness")]
    public float effectLerpSpeed = 3f;

    private Vector3 originalTextScale;
    private float bounceTimer = 0f;
    private bool bouncing = false;
    private float targetBounceScale = 1f;

    private ChromaticAberration chromaticAberration;
    private Vignette urpVignette;
    private VolumeProfile runtimeVolumeProfile;
    private float defaultChromaticIntensity;
    private float defaultVignetteIntensity;

    private float currentOverlayAlpha = 0f;
    private HeartRateStateController.HeartRateState lastState;

    private void Awake()
    {
        TryAutoBindHeartRate();
        TryAutoBindGlobalVolume();
    }

    private void Start()
    {
        if (heartRateTextRect != null)
            originalTextScale = heartRateTextRect.localScale;

        InitializeVolumeProfile();
        ResetVisualStateImmediate();

        if (stateController != null)
            lastState = stateController.CurrentState;
    }

    private void OnDestroy()
    {
        if (globalVolume != null && runtimeVolumeProfile != null && globalVolume.profile == runtimeVolumeProfile)
        {
            globalVolume.profile = null;
        }

        if (runtimeVolumeProfile != null)
        {
            Object.Destroy(runtimeVolumeProfile);
            runtimeVolumeProfile = null;
        }
    }

    private void Update()
    {
        TryAutoBindHeartRate();
        TryAutoBindGlobalVolume();
        if (heartRate == null || stateController == null)
            return;

        DetectStateEntry();
        UpdateHeartRateText();
        UpdateBounce();
        UpdateVolumeEffects();
    }

    private void TryAutoBindHeartRate()
    {
        if (heartRate == null)
        {
            heartRate = HeartRateSimulator.Instance;
        }
    }

    private void TryAutoBindGlobalVolume()
    {
        if (globalVolume != null)
        {
            return;
        }

        globalVolume = FindFirstObjectByType<Volume>();
    }

    private void InitializeVolumeProfile()
    {
        chromaticAberration = null;
        urpVignette = null;

        if (globalVolume == null || globalVolume.sharedProfile == null)
        {
            return;
        }

        if (runtimeVolumeProfile != null)
        {
            Object.Destroy(runtimeVolumeProfile);
        }

        runtimeVolumeProfile = Object.Instantiate(globalVolume.sharedProfile);
        runtimeVolumeProfile.name = globalVolume.sharedProfile.name + " (Runtime)";
        globalVolume.profile = runtimeVolumeProfile;

        runtimeVolumeProfile.TryGet(out chromaticAberration);
        runtimeVolumeProfile.TryGet(out urpVignette);

        if (chromaticAberration != null)
        {
            defaultChromaticIntensity = chromaticAberration.intensity.value;
        }

        if (urpVignette != null)
        {
            defaultVignetteIntensity = urpVignette.intensity.value;
        }
    }

    private void ResetVisualStateImmediate()
    {
        currentOverlayAlpha = 0f;
        bounceTimer = 0f;
        bouncing = false;
        targetBounceScale = 1f;

        if (heartRateTextRect != null)
        {
            heartRateTextRect.localScale = originalTextScale == Vector3.zero ? Vector3.one : originalTextScale;
        }

        if (stressVignette != null)
        {
            Color c = stressVignette.color;
            c.a = 0f;
            stressVignette.color = c;
        }

        if (chromaticAberration != null)
        {
            chromaticAberration.intensity.value = defaultChromaticIntensity;
        }

        if (urpVignette != null)
        {
            urpVignette.intensity.value = defaultVignetteIntensity;
        }
    }

    private void DetectStateEntry()
    {
        if (stateController.CurrentState == lastState) return;

        switch (stateController.CurrentState)
        {
            case HeartRateStateController.HeartRateState.RisingStress:
                TriggerBounce(risingBounceScale);
                break;

            case HeartRateStateController.HeartRateState.HighStress:
                TriggerBounce(highBounceScale);
                break;
        }

        lastState = stateController.CurrentState;
    }

    private void TriggerBounce(float scale)
    {
        bouncing = true;
        bounceTimer = 0f;
        targetBounceScale = scale;
    }

    private void UpdateHeartRateText()
    {
        if (heartRateText == null) return;

        string baselineText = heartRate.isCalibrating
            ? "Calibrating..."
            : $"{heartRate.HR_case:F0} BPM";

        heartRateText.text =
            $"HR: {heartRate.currentHeartRate:F0} BPM\n" +
            $"Baseline: {baselineText}\n" +
            $"State: {stateController.CurrentState}";

        switch (stateController.CurrentState)
        {
            case HeartRateStateController.HeartRateState.RisingStress:
                heartRateText.color = risingTextColor;
                break;

            case HeartRateStateController.HeartRateState.HighStress:
                heartRateText.color = highTextColor;
                break;

            case HeartRateStateController.HeartRateState.Recovering:
                heartRateText.color = recoveringTextColor;
                break;

            default:
                heartRateText.color = normalTextColor;
                break;
        }
    }

    private void UpdateBounce()
    {
        if (!bouncing || heartRateTextRect == null) return;

        bounceTimer += Time.deltaTime;
        float t = bounceTimer / bounceDuration;

        if (t >= 1f)
        {
            bouncing = false;
            heartRateTextRect.localScale = originalTextScale;
            return;
        }

        float curve = Mathf.Sin(t * Mathf.PI);
        float scale = Mathf.Lerp(1f, targetBounceScale, curve);
        heartRateTextRect.localScale = originalTextScale * scale;
    }

    private void UpdateVolumeEffects()
    {
        float targetCA = normalChromatic;
        float targetVignette = normalVignetteIntensity;

        switch (stateController.CurrentState)
        {
            case HeartRateStateController.HeartRateState.RisingStress:
                {
                    targetCA = risingChromaticMax;
                    targetVignette = risingVignetteIntensity;
                    break;
                }

            case HeartRateStateController.HeartRateState.HighStress:
                {
                    targetCA = highChromaticMax;
                    targetVignette = highVignetteIntensity;
                    break;
                }

            case HeartRateStateController.HeartRateState.Recovering:
                {
                    targetCA = recoveringChromatic;
                    targetVignette = recoveringVignetteIntensity;
                    break;
                }

            default:
                {
                    targetCA = normalChromatic;
                    targetVignette = normalVignetteIntensity;
                    break;
                }
        }

        if (chromaticAberration != null)
        {
            chromaticAberration.intensity.value = Mathf.Lerp(
                chromaticAberration.intensity.value,
                targetCA,
                Time.deltaTime * effectLerpSpeed
            );
        }

        if (urpVignette != null)
        {
            urpVignette.intensity.value = Mathf.Lerp(
                urpVignette.intensity.value,
                targetVignette,
                Time.deltaTime * effectLerpSpeed
            );
        }
    }
}
