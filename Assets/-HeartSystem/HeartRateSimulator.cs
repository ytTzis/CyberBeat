using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class HeartRateSimulator : MonoBehaviour
{
    public static HeartRateSimulator Instance { get; private set; }

    [Header("Simulation")]
    public bool useSimulation = true;
    public float currentHeartRate = 75f;
    public float minHeartRate = 45f;
    public float maxHeartRate = 180f;

    [Header("Player Manual Control")]
    public KeyCode increaseKey = KeyCode.UpArrow;
    public KeyCode decreaseKey = KeyCode.DownArrow;
    public float adjustPerSecond = 12f;

    [Header("Calibration")]
    public float calibrationDuration = 20f;
    public bool isCalibrating = true;
    public float HR_case { get; private set; }

    [Header("Realtime Window")]
    public float updateInterval = 1f;

    public float HR_short { get; private set; }
    public float HR_long { get; private set; }
    public float PreviousHRShort { get; private set; }

    [Header("Optional UI")]
    public TMP_Text debugText;

    private float updateTimer = 0f;
    private float calibrationTimer = 0f;
    private float runtimeHeartRateOffset = 0f;
    private bool hasTemporaryHeartRateOverride = false;
    private float temporaryHeartRateOverride = 0f;
    private float temporaryHeartRateOverrideTimer = 0f;
    private float temporaryHeartRateOverrideMinimum = 0f;
    private bool resetRuntimeOffsetWhenOverrideEnds = false;
    private float lastKnownRealtimeHeartRate = 0f;
    private List<float> secondSamples = new List<float>();
    private Queue<float> shortWindow = new Queue<float>();
    private Queue<float> longWindow = new Queue<float>();
    private float shortSum = 0f;
    private float longSum = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        currentHeartRate = Mathf.Clamp(currentHeartRate, minHeartRate, maxHeartRate);

        if (HR_case <= 0f)
        {
            HR_case = currentHeartRate;
        }

        TryAutoBindDebugText();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void Update()
    {
        UpdateTemporaryHeartRateOverride();
        UpdateHeartRateFromRealtimeSource();

        updateTimer += Time.deltaTime;

        if (isCalibrating)
        {
            calibrationTimer += Time.deltaTime;
        }

        if (updateTimer >= updateInterval)
        {
            updateTimer -= updateInterval;
            TickOneSecond();
        }

        UpdateDebugUI();
    }

    private void UpdateHeartRateFromRealtimeSource()
    {
        if (hasTemporaryHeartRateOverride)
        {
            currentHeartRate = Mathf.Clamp(temporaryHeartRateOverride, minHeartRate, maxHeartRate);
            return;
        }

        int realtimeHeartRate = hyperateSocket.CurrentHeartRate;
        if (realtimeHeartRate > 0)
        {
            lastKnownRealtimeHeartRate = realtimeHeartRate;
            currentHeartRate = Mathf.Clamp(realtimeHeartRate + runtimeHeartRateOffset, minHeartRate, maxHeartRate);
        }
    }

    public void ForceHeartRateForDuration(float targetHeartRate, float durationSeconds, bool restoreToRawRealtimeOnEnd = false, float minimumHeartRateDuringOverride = 0f)
    {
        hasTemporaryHeartRateOverride = true;
        temporaryHeartRateOverride = Mathf.Clamp(targetHeartRate, minHeartRate, maxHeartRate);
        temporaryHeartRateOverrideTimer = Mathf.Max(0f, durationSeconds);
        temporaryHeartRateOverrideMinimum = Mathf.Clamp(minimumHeartRateDuringOverride, minHeartRate, temporaryHeartRateOverride);
        resetRuntimeOffsetWhenOverrideEnds = restoreToRawRealtimeOnEnd;
        currentHeartRate = temporaryHeartRateOverride;
    }

    public void ClearForcedHeartRateOverride()
    {
        hasTemporaryHeartRateOverride = false;
        temporaryHeartRateOverrideTimer = 0f;
        temporaryHeartRateOverrideMinimum = 0f;
        if (resetRuntimeOffsetWhenOverrideEnds)
        {
            runtimeHeartRateOffset = 0f;
        }
        resetRuntimeOffsetWhenOverrideEnds = false;
        RestoreRealtimeHeartRateImmediately();
    }

    public void ResetItemHeartRateEffects()
    {
        ClearForcedHeartRateOverride();
    }

    public bool HasTemporaryHeartRateOverride()
    {
        return hasTemporaryHeartRateOverride;
    }

    public bool ConsumeHeartRate(float amount)
    {
        if (amount <= 0f)
        {
            return true;
        }

        if (hasTemporaryHeartRateOverride)
        {
            float availableOverrideHeartRate = temporaryHeartRateOverride - temporaryHeartRateOverrideMinimum;
            if (availableOverrideHeartRate <= 0f)
            {
                return false;
            }

            float consumedOverrideAmount = Mathf.Min(amount, availableOverrideHeartRate);
            if (consumedOverrideAmount < amount)
            {
                return false;
            }

            temporaryHeartRateOverride = Mathf.Clamp(temporaryHeartRateOverride - consumedOverrideAmount, temporaryHeartRateOverrideMinimum, maxHeartRate);
            currentHeartRate = temporaryHeartRateOverride;
            return true;
        }

        float availableHeartRate = currentHeartRate - minHeartRate;
        if (availableHeartRate <= 0f)
        {
            return false;
        }

        float consumedAmount = Mathf.Min(amount, availableHeartRate);
        if (hyperateSocket.CurrentHeartRate > 0)
        {
            runtimeHeartRateOffset -= consumedAmount;
            float minimumOffset = minHeartRate - hyperateSocket.CurrentHeartRate;
            runtimeHeartRateOffset = Mathf.Max(runtimeHeartRateOffset, minimumOffset);
            currentHeartRate = Mathf.Clamp(hyperateSocket.CurrentHeartRate + runtimeHeartRateOffset, minHeartRate, maxHeartRate);
        }
        else
        {
            currentHeartRate = Mathf.Clamp(currentHeartRate - consumedAmount, minHeartRate, maxHeartRate);
        }

        return true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryAutoBindDebugText();
    }

    private void TryAutoBindDebugText()
    {
        if (debugText != null)
        {
            return;
        }

        TMP_Text[] textCandidates = FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
        for (int i = 0; i < textCandidates.Length; i++)
        {
            TMP_Text candidate = textCandidates[i];
            if (candidate != null && candidate.name == "LeftText")
            {
                debugText = candidate;
                return;
            }
        }
    }

    private void TickOneSecond()
    {
        float hr = currentHeartRate;

        if (isCalibrating)
        {
            secondSamples.Add(hr);

            if (calibrationTimer >= calibrationDuration)
            {
                HR_case = Average(secondSamples);
                isCalibrating = false;
                Debug.Log($"[HeartRate] Calibration finished. HR_case = {HR_case:F1}");
            }
        }

        PreviousHRShort = HR_short;

        PushShort(hr);
        PushLong(hr);

        HR_short = shortWindow.Count > 0 ? shortSum / shortWindow.Count : hr;
        HR_long = longWindow.Count > 0 ? longSum / longWindow.Count : hr;
    }

    private void UpdateTemporaryHeartRateOverride()
    {
        if (!hasTemporaryHeartRateOverride)
        {
            return;
        }

        temporaryHeartRateOverrideTimer -= Time.deltaTime;
        if (temporaryHeartRateOverrideTimer <= 0f)
        {
            ClearForcedHeartRateOverride();
        }
    }

    private void RestoreRealtimeHeartRateImmediately()
    {
        int realtimeHeartRate = hyperateSocket.CurrentHeartRate;
        if (realtimeHeartRate > 0)
        {
            lastKnownRealtimeHeartRate = realtimeHeartRate;
            currentHeartRate = Mathf.Clamp(realtimeHeartRate + runtimeHeartRateOffset, minHeartRate, maxHeartRate);
            return;
        }

        if (lastKnownRealtimeHeartRate > 0f)
        {
            currentHeartRate = Mathf.Clamp(lastKnownRealtimeHeartRate + runtimeHeartRateOffset, minHeartRate, maxHeartRate);
        }
    }

    private void PushShort(float value)
    {
        shortWindow.Enqueue(value);
        shortSum += value;

        while (shortWindow.Count > 3)
        {
            shortSum -= shortWindow.Dequeue();
        }
    }

    private void PushLong(float value)
    {
        longWindow.Enqueue(value);
        longSum += value;

        while (longWindow.Count > 10)
        {
            longSum -= longWindow.Dequeue();
        }
    }

    private float Average(List<float> values)
    {
        if (values == null || values.Count == 0)
            return currentHeartRate;

        float sum = 0f;
        for (int i = 0; i < values.Count; i++)
            sum += values[i];

        return sum / values.Count;
    }

    private void UpdateDebugUI()
    {
        if (debugText == null)
        {
            TryAutoBindDebugText();
        }

        if (debugText == null) return;

        if (isCalibrating)
        {
            debugText.text =
                $"HR: {currentHeartRate:F0}\n" +
                $"Baseline: Calibrating...";
        }
        else
        {
            debugText.text =
    $"HR: {currentHeartRate:F0} BPM\n" +
    $"Baseline: {(isCalibrating ? "..." : HR_case.ToString("F0"))}\n" +
    $"State: {FindObjectOfType<HeartRateStateController>().CurrentState}";
        }
    }
}
