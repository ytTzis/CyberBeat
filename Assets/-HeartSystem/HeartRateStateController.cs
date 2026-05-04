using UnityEngine;
using TMPro;
using System;

public class HeartRateStateController : MonoBehaviour
{
    public static HeartRateStateController Instance { get; private set; }

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
    public float risingShortMultiplier = 1.035f;     // HR_short > HR_case * 1.035
    public float risingTrendThreshold = 1f;          // Trend >= 1
    public float risingRequiredSeconds = 2f;

    [Header("High Stress")]
    public float highShortMultiplier = 1.10f;       // HR_short > HR_case * 1.10
    public float highLongMultiplier = 1.06f;        // HR_long  > HR_case * 1.06
    public float highStableTrendAbs = 2.5f;         // |Trend| < 2.5
    public float highRequiredSeconds = 2f;

    [Header("Direct Jump To High Stress")]
    public float directHighShortMultiplier = 1.18f;   // HR_short > HR_case * 1.18
    public float directHighCurrentMultiplier = 1.16f; // HR_current > HR_case * 1.16
    public float directHighTrendThreshold = 3.5f;     // Trend >= 3.5

    [Header("Recovering")]
    public float recoverShortAboveBaseline = 1.015f; // HR_short > HR_case * 1.015
    public float recoverTrendThreshold = -1.5f;      // Trend <= -1.5
    public float recoverRequiredSeconds = 3f;

    [Header("Return To Normal")]
    public float normalShortMultiplier = 1.03f;     // HR_short <= HR_case * 1.03
    public float normalLongMultiplier = 1.04f;      // HR_long  <= HR_case * 1.04
    public float normalTrendAbs = 1.5f;             // |Trend| < 1.5
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
    private bool hasForcedStateOverride = false;
    private HeartRateState forcedState;
    private float forcedStateTimer = 0f;

    // 只有真的进入过紧张状态后，才允许进入恢复冷静
    private bool hasBeenStressed = false;

    public Action OnRisingStressEnter;
    public Action OnHighStressEnter;
    public Action OnRecoveringEnter;
    public Action OnReturnToNormal;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        if (heartRate == null) return;
        if (heartRate.isCalibrating) return;

        if (stateTransitionCooldownTimer > 0f)
            stateTransitionCooldownTimer -= Time.deltaTime;

        Trend = heartRate.HR_short - heartRate.HR_long;

        if (hasForcedStateOverride)
        {
            forcedStateTimer -= Time.deltaTime;

            if (CurrentState != forcedState)
            {
                ForceSetState(forcedState);
                InvokeEnterAction(forcedState);
            }

            if (forcedStateTimer <= 0f)
            {
                ClearForcedStateOverride();
            }

            UpdateStateUI();
            return;
        }

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

    public void ForceStateForDuration(HeartRateState newState, float durationSeconds)
    {
        hasForcedStateOverride = true;
        forcedState = newState;
        forcedStateTimer = Mathf.Max(0f, durationSeconds);

        if (newState == HeartRateState.Normal)
        {
            hasBeenStressed = false;
        }
        else if (newState == HeartRateState.HighStress || newState == HeartRateState.RisingStress)
        {
            hasBeenStressed = true;
        }

        ResetTransitionTimers();
        ForceSetState(newState);
        InvokeEnterAction(newState);
    }

    public void ForceReturnToNormal()
    {
        ClearForcedStateOverride();
        hasBeenStressed = false;
        ResetTransitionTimers();
        ForceSetState(HeartRateState.Normal);
        OnReturnToNormal?.Invoke();
    }

    private void ClearForcedStateOverride()
    {
        hasForcedStateOverride = false;
        forcedStateTimer = 0f;
    }

    private void ResetTransitionTimers()
    {
        risingTimer = 0f;
        highTimer = 0f;
        recoveringTimer = 0f;
        normalTimer = 0f;
    }

    private void InvokeEnterAction(HeartRateState state)
    {
        switch (state)
        {
            case HeartRateState.RisingStress:
                OnRisingStressEnter?.Invoke();
                break;
            case HeartRateState.HighStress:
                OnHighStressEnter?.Invoke();
                break;
            case HeartRateState.Recovering:
                OnRecoveringEnter?.Invoke();
                break;
            case HeartRateState.Normal:
                OnReturnToNormal?.Invoke();
                break;
        }
    }

    private void UpdateStateUI()
    {
        if (stateText == null || heartRate == null) return;

        stateText.text =
            $"State: {CurrentState}\n" +
            $"HR_case: {heartRate.HR_case:F1}\n" +
            $"HR_current: {heartRate.currentHeartRate:F1}\n" +
            $"HR_short: {heartRate.HR_short:F1}\n" +
            $"HR_long: {heartRate.HR_long:F1}\n" +
            $"Trend: {Trend:F1}\n" +
            $"Rising Need: short > {(heartRate.HR_case * risingShortMultiplier):F1}, trend >= {risingTrendThreshold:F1}\n" +
            $"High Need: short > {(heartRate.HR_case * highShortMultiplier):F1}, long > {(heartRate.HR_case * highLongMultiplier):F1}, |trend| < {highStableTrendAbs:F1}\n" +
            $"Direct High Need A: short > {(heartRate.HR_case * directHighShortMultiplier):F1}\n" +
            $"Direct High Need B: current > {(heartRate.HR_case * directHighCurrentMultiplier):F1}, trend >= {directHighTrendThreshold:F1}\n" +
            $"Recover Need: short > {(heartRate.HR_case * recoverShortAboveBaseline):F1}, trend <= {recoverTrendThreshold:F1}\n" +
            $"Normal Need: short <= {(heartRate.HR_case * normalShortMultiplier):F1}, long <= {(heartRate.HR_case * normalLongMultiplier):F1}, |trend| < {normalTrendAbs:F1}\n" +
            $"RisingTimer: {risingTimer:F1}\n" +
            $"HighTimer: {highTimer:F1}\n" +
            $"RecoverTimer: {recoveringTimer:F1}\n" +
            $"NormalTimer: {normalTimer:F1}\n" +
            $"StateCD: {Mathf.Max(0f, stateTransitionCooldownTimer):F1}\n" +
            $"HasBeenStressed: {hasBeenStressed}";
    }
}
