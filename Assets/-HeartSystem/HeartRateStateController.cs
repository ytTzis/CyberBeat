using UnityEngine;
using TMPro;
using System;

public class HeartRateStateController : MonoBehaviour
{
    public enum HeartRateState
    {
        Normal,
        RisingStress,   // 心率正在逐渐上升
        HighStress,     // 心率达到高位并维持，或突然冲到高位
        Recovering      // 心率正在逐渐回落到基线
    }

    [Header("Reference")]
    public HeartRateSimulator heartRate;

    [Header("Rising Stress")]
    public float risingShortMultiplier = 1.05f;     // HR_short > HR_case * 1.05
    public float risingTrendThreshold = 2f;         // Trend >= 2
    public float risingRequiredSeconds = 3f;

    [Header("High Stress")]
    public float highShortMultiplier = 1.15f;       // HR_short > HR_case * 1.15
    public float highLongMultiplier = 1.10f;        // HR_long  > HR_case * 1.10
    public float highStableTrendAbs = 2f;           // |Trend| < 2
    public float highRequiredSeconds = 4f;

    [Header("Direct Jump To High Stress")]
    public float directHighShortMultiplier = 1.22f;   // HR_short > HR_case * 1.22
    public float directHighCurrentMultiplier = 1.20f; // HR_current > HR_case * 1.20
    public float directHighTrendThreshold = 4f;       // Trend >= 4

    [Header("Recovering")]
    public float recoverShortAboveBaseline = 1.02f; // HR_short > HR_case * 1.02
    public float recoverTrendThreshold = -2f;       // Trend <= -2
    public float recoverRequiredSeconds = 3f;

    [Header("Return To Normal")]
    public float normalShortMultiplier = 1.03f;     // HR_short <= HR_case * 1.03
    public float normalLongMultiplier = 1.04f;      // HR_long  <= HR_case * 1.04
    public float normalTrendAbs = 2f;               // |Trend| < 2
    public float normalRequiredSeconds = 2f;

    [Header("Transition Protection")]
    public float stateTransitionCooldown = 0.35f;

    [Header("Optional UI")]
    public TMP_Text stateText;

    public HeartRateState CurrentState { get; private set; } = HeartRateState.Normal;

    // 趋势：短窗口 - 长窗口
    public float Trend { get; private set; }

    private float risingTimer = 0f;
    private float highTimer = 0f;
    private float recoveringTimer = 0f;
    private float normalTimer = 0f;
    private float stateTransitionCooldownTimer = 0f;

    // 只有真的进入过紧张状态后，才允许进入恢复冷静
    private bool hasBeenStressed = false;

    public Action OnRisingStressEnter;
    public Action OnHighStressEnter;
    public Action OnRecoveringEnter;
    public Action OnReturnToNormal;

    private void Awake()
    {
        TryAutoBindHeartRate();
    }

    private void Update()
    {
        TryAutoBindHeartRate();
        if (heartRate == null) return;
        if (heartRate.isCalibrating) return;

        if (stateTransitionCooldownTimer > 0f)
            stateTransitionCooldownTimer -= Time.deltaTime;

        Trend = heartRate.HR_short - heartRate.HR_long;

        bool risingStress = CheckRisingStress();
        bool highStress = CheckHighStress();
        bool directHighStress = CheckDirectHighStress();
        bool recovering = CheckRecovering();
        bool returnToNormal = CheckReturnToNormal();

        bool canTransition = stateTransitionCooldownTimer <= 0f;

        // 优先级：
        // HighStress(含直达) > Recovering > RisingStress > Normal
        if (canTransition && (directHighStress || highStress))
        {
            hasBeenStressed = true;

            if (CurrentState != HeartRateState.HighStress)
            {
                ForceSetState(HeartRateState.HighStress);
                OnHighStressEnter?.Invoke();
            }
        }
        else if (canTransition && recovering)
        {
            if (CurrentState != HeartRateState.Recovering)
            {
                ForceSetState(HeartRateState.Recovering);
                OnRecoveringEnter?.Invoke();
            }
        }
        else if (canTransition && risingStress)
        {
            hasBeenStressed = true;

            if (CurrentState != HeartRateState.RisingStress)
            {
                ForceSetState(HeartRateState.RisingStress);
                OnRisingStressEnter?.Invoke();
            }
        }
        else if (canTransition && returnToNormal)
        {
            if (CurrentState != HeartRateState.Normal)
            {
                hasBeenStressed = false;
                ForceSetState(HeartRateState.Normal);
                OnReturnToNormal?.Invoke();
            }
        }

        UpdateStateUI();
    }

    private void TryAutoBindHeartRate()
    {
        if (heartRate == null)
        {
            heartRate = HeartRateSimulator.Instance;
        }
    }

    private bool CheckRisingStress()
    {
        bool aboveBaseline = heartRate.HR_short > heartRate.HR_case * risingShortMultiplier;
        bool trendUp = Trend >= risingTrendThreshold;

        if (aboveBaseline && trendUp)
            risingTimer += Time.deltaTime;
        else
            risingTimer = 0f;

        return risingTimer >= risingRequiredSeconds;
    }

    private bool CheckHighStress()
    {
        bool shortHigh = heartRate.HR_short > heartRate.HR_case * highShortMultiplier;
        bool longHigh = heartRate.HR_long > heartRate.HR_case * highLongMultiplier;
        bool trendStable = Mathf.Abs(Trend) < highStableTrendAbs;

        if (shortHigh && longHigh && trendStable)
            highTimer += Time.deltaTime;
        else
            highTimer = 0f;

        return highTimer >= highRequiredSeconds;
    }

    private bool CheckDirectHighStress()
    {
        bool shortVeryHigh = heartRate.HR_short > heartRate.HR_case * directHighShortMultiplier;

        bool currentVeryHighAndJumping =
            heartRate.currentHeartRate > heartRate.HR_case * directHighCurrentMultiplier &&
            Trend >= directHighTrendThreshold;

        return shortVeryHigh || currentVeryHighAndJumping;
    }

    private bool CheckRecovering()
    {
        if (!hasBeenStressed)
        {
            recoveringTimer = 0f;
            return false;
        }

        bool stillAboveBase = heartRate.HR_short > heartRate.HR_case * recoverShortAboveBaseline;
        bool trendDown = Trend <= recoverTrendThreshold;

        if (stillAboveBase && trendDown)
            recoveringTimer += Time.deltaTime;
        else
            recoveringTimer = 0f;

        return recoveringTimer >= recoverRequiredSeconds;
    }

    private bool CheckReturnToNormal()
    {
        bool shortNormal = heartRate.HR_short <= heartRate.HR_case * normalShortMultiplier;
        bool longNormal = heartRate.HR_long <= heartRate.HR_case * normalLongMultiplier;
        bool trendStable = Mathf.Abs(Trend) < normalTrendAbs;

        if (shortNormal && longNormal && trendStable)
            normalTimer += Time.deltaTime;
        else
            normalTimer = 0f;

        return normalTimer >= normalRequiredSeconds;
    }

    private void ForceSetState(HeartRateState newState)
    {
        if (CurrentState == newState) return;

        HeartRateState oldState = CurrentState;
        CurrentState = newState;
        stateTransitionCooldownTimer = stateTransitionCooldown;

        Debug.Log($"[HeartRateState] {oldState} -> {newState}");
    }

    private void UpdateStateUI()
    {
        if (stateText == null || heartRate == null) return;

        stateText.text =
            $"State: {CurrentState}\n" +
            $"HR: {heartRate.currentHeartRate:F0} BPM\n" +
            $"Baseline: {(heartRate.isCalibrating ? "Calibrating..." : $"{heartRate.HR_case:F0} BPM")}";
    }
}
