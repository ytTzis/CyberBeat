using UGG.Combat;
using UGG.Health;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("心率可视化/战斗心率可视化控制器")]
public class CombatHeartRateVisualizationController : MonoBehaviour
{
    private enum LightStressState
    {
        Normal,
        RisingStress,
        HighStress
    }

    [Header("场景引用")]
    [SerializeField, InspectorName("玩家战斗系统")] private PlayerCombatSystem playerCombatSystem;
    [SerializeField, InspectorName("玩家生命系统")] private PlayerHealthSystem playerHealthSystem;
    [SerializeField, InspectorName("玩家根节点")] private Transform playerRoot;
    [SerializeField, InspectorName("主方向光")] private Light mainDirectionalLight;
    [SerializeField, InspectorName("非战斗UI根节点")] private GameObject nonCombatUIRoot;
    [SerializeField, InspectorName("战斗UI根节点")] private GameObject combatUIRoot;
    [SerializeField, InspectorName("心率压力状态控制器")] private HeartRateStateController heartRateStateController;

    [Header("战斗检测")]
    [SerializeField, InspectorName("敌人感知器")] private AICombatSystem[] enemySensors;
    [SerializeField, InspectorName("敌人判定距离")] private float enemyCombatDistance = 8f;

    [Header("灯光响应设置")]
    [SerializeField, InspectorName("启用主方向光响应")] private bool affectMainDirectionalLight = true;
    [SerializeField, InspectorName("启用灯组响应")] private bool affectResponsiveLights = true;
    [SerializeField, InspectorName("响应灯组")] private Light[] responsiveLights;

    [Header("心率压力灯光颜色")]
    [SerializeField, InspectorName("非战斗也应用压力颜色")] private bool applyStressColorOutsideCombat = true;

    [Header("生命值颜色阶段")]
    [SerializeField, InspectorName("高生命颜色")] private Color highHealthColor = new Color(0.15f, 0.95f, 0.85f);
    [SerializeField, InspectorName("中生命颜色")] private Color midHealthColor = new Color(1.0f, 0.65f, 0.2f);
    [SerializeField, InspectorName("低生命颜色")] private Color lowHealthColor = new Color(0.95f, 0.2f, 0.65f);
    [SerializeField, Range(0f, 1f), InspectorName("中生命阈值")] private float midHealthThreshold = 0.66f;
    [SerializeField, Range(0f, 1f), InspectorName("低生命阈值")] private float lowHealthThreshold = 0.33f;

    [Header("心跳脉动")]
    [SerializeField, InspectorName("战斗基础亮度")] private float combatBaseIntensity = 1.2f;
    [SerializeField, InspectorName("主光脉动强度")] private float pulseIntensityAmount = 0.8f;
    [SerializeField, InspectorName("过渡速度")] private float transitionSpeed = 4f;
    [SerializeField, InspectorName("回退心率")] private int fallbackHeartRate = 72;
    [SerializeField, InspectorName("最小心率")] private int minimumHeartRate = 45;
    [SerializeField, InspectorName("最大心率")] private int maximumHeartRate = 180;

    [Header("调试心率")]
    [SerializeField, InspectorName("使用调试心率")] private bool useDebugHeartRate;
    [SerializeField, InspectorName("调试心率值")] private int debugHeartRate = 72;

    [Header("调试战斗状态")]
    [SerializeField, InspectorName("使用调试战斗状态")] private bool useDebugCombatState;
    [SerializeField, InspectorName("调试为战斗中")] private bool debugIsInCombat;

    [Header("手动压力状态调试")]
    [SerializeField, InspectorName("启用手动压力状态")] private bool useDebugHeartRateState;
    [SerializeField, InspectorName("手动压力状态")] private LightStressState debugHeartRateState = LightStressState.Normal;
    [SerializeField, InspectorName("当前灯光压力状态")] private LightStressState currentHeartRateState = LightStressState.Normal;
    [SerializeField, InspectorName("当前灯光颜色")] private Color currentAppliedColor = Color.green;

    private Color defaultLightColor;
    private float defaultLightIntensity;
    private Color[] responsiveLightDefaultColors;
    private float[] responsiveLightDefaultIntensities;

    private void Reset()
    {
        TryAutoBind();
    }

    private void Awake()
    {
        TryAutoBind();
        CacheDefaultLightState();
    }

    private void OnValidate()
    {
        ClampHeartRateRange();
    }

    private void Update()
    {
        bool isInCombat = GetActiveCombatState();
        UpdateUIState(isInCombat);
        UpdateLights(isInCombat);
    }

    private void TryAutoBind()
    {
        if (playerCombatSystem == null)
        {
            playerCombatSystem = FindFirstObjectByType<PlayerCombatSystem>();
        }

        if (playerHealthSystem == null)
        {
            playerHealthSystem = FindFirstObjectByType<PlayerHealthSystem>();
        }

        if (heartRateStateController == null)
        {
            heartRateStateController = HeartRateStateController.Instance;
        }

        if (heartRateStateController == null)
        {
            heartRateStateController = FindFirstObjectByType<HeartRateStateController>(FindObjectsInactive.Include);
        }

        if (playerRoot == null && playerCombatSystem != null)
        {
            playerRoot = playerCombatSystem.transform.root;
        }

        if (mainDirectionalLight == null)
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Light sceneLight in lights)
            {
                if (sceneLight.type == LightType.Directional)
                {
                    mainDirectionalLight = sceneLight;
                    break;
                }
            }
        }

        if (ShouldAutoBindResponsiveLights())
        {
            responsiveLights = FindAllResponsiveLights();
        }

        if (enemySensors == null || enemySensors.Length == 0)
        {
            enemySensors = FindObjectsByType<AICombatSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
    }

    private bool ShouldAutoBindResponsiveLights()
    {
        if (responsiveLights == null || responsiveLights.Length == 0)
        {
            return true;
        }

        if (responsiveLights.Length == 1 && responsiveLights[0] == mainDirectionalLight)
        {
            return true;
        }

        return false;
    }

    private Light[] FindAllResponsiveLights()
    {
        Light[] sceneLights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<Light> collectedLights = new List<Light>();

        foreach (Light sceneLight in sceneLights)
        {
            if (sceneLight == null || !sceneLight.enabled)
            {
                continue;
            }

            collectedLights.Add(sceneLight);
        }

        if (collectedLights.Count == 0 && mainDirectionalLight != null)
        {
            collectedLights.Add(mainDirectionalLight);
        }

        return collectedLights.ToArray();
    }

    private void CacheDefaultLightState()
    {
        if (mainDirectionalLight != null)
        {
            defaultLightColor = mainDirectionalLight.color;
            defaultLightIntensity = mainDirectionalLight.intensity;
        }

        if (responsiveLights == null)
        {
            responsiveLightDefaultColors = null;
            responsiveLightDefaultIntensities = null;
            return;
        }

        responsiveLightDefaultColors = new Color[responsiveLights.Length];
        responsiveLightDefaultIntensities = new float[responsiveLights.Length];

        for (int i = 0; i < responsiveLights.Length; i++)
        {
            Light responsiveLight = responsiveLights[i];
            if (responsiveLight == null)
            {
                continue;
            }

            responsiveLightDefaultColors[i] = responsiveLight.color;
            responsiveLightDefaultIntensities[i] = responsiveLight.intensity;
        }
    }

    private void ClampHeartRateRange()
    {
        if (maximumHeartRate < minimumHeartRate)
        {
            maximumHeartRate = minimumHeartRate;
        }
    }

    private bool IsInCombat()
    {
        if (playerCombatSystem != null && playerCombatSystem.GetCurrentTarget() != null)
        {
            return true;
        }

        if (enemySensors == null)
        {
            return false;
        }

        foreach (AICombatSystem sensor in enemySensors)
        {
            if (sensor == null)
            {
                continue;
            }

            Transform currentTarget = sensor.GetCurrentTarget();
            if (currentTarget == null)
            {
                continue;
            }

            if (playerRoot == null || currentTarget.root == playerRoot)
            {
                if (sensor.GetCurrentTargetDistance() > enemyCombatDistance)
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    private bool GetActiveCombatState()
    {
        if (useDebugCombatState)
        {
            return debugIsInCombat;
        }

        return IsInCombat();
    }

    private void UpdateUIState(bool isInCombat)
    {
        if (nonCombatUIRoot != null)
        {
            nonCombatUIRoot.SetActive(!isInCombat);
        }

        if (combatUIRoot != null)
        {
            combatUIRoot.SetActive(isInCombat);
        }
    }

    private void UpdateLights(bool isInCombat)
    {
        Color targetColor = GetCombatColorFromHeartRateState();
        float pulseValue = GetPulseValueFromHeartRate();
        float mainLightTargetIntensity = combatBaseIntensity + pulseValue * pulseIntensityAmount;
        bool shouldApplyStressColor = isInCombat || applyStressColorOutsideCombat;

        UpdateMainDirectionalLight(shouldApplyStressColor, targetColor, mainLightTargetIntensity);
        UpdateResponsiveLights(shouldApplyStressColor, targetColor, pulseValue);
    }

    private void UpdateMainDirectionalLight(bool isInCombat, Color targetColor, float targetIntensity)
    {
        if (!affectMainDirectionalLight || mainDirectionalLight == null)
        {
            return;
        }

        if (!isInCombat)
        {
            mainDirectionalLight.color = Color.Lerp(
                mainDirectionalLight.color,
                defaultLightColor,
                Time.deltaTime * transitionSpeed);

            mainDirectionalLight.intensity = Mathf.Lerp(
                mainDirectionalLight.intensity,
                defaultLightIntensity,
                Time.deltaTime * transitionSpeed);
            return;
        }

        mainDirectionalLight.color = Color.Lerp(
            mainDirectionalLight.color,
            targetColor,
            Time.deltaTime * transitionSpeed);

        mainDirectionalLight.intensity = Mathf.Lerp(
            mainDirectionalLight.intensity,
            targetIntensity,
            Time.deltaTime * transitionSpeed);
    }

    private void UpdateResponsiveLights(bool isInCombat, Color targetColor, float pulseValue)
    {
        if (!affectResponsiveLights || responsiveLights == null || responsiveLights.Length == 0)
        {
            return;
        }

        if (responsiveLightDefaultColors == null || responsiveLightDefaultColors.Length != responsiveLights.Length)
        {
            CacheDefaultLightState();
        }

        for (int i = 0; i < responsiveLights.Length; i++)
        {
            Light responsiveLight = responsiveLights[i];
            if (responsiveLight == null)
            {
                continue;
            }

            Color defaultColor = responsiveLightDefaultColors[i];
            float defaultIntensity = responsiveLightDefaultIntensities[i];

            if (!isInCombat)
            {
                responsiveLight.color = Color.Lerp(
                    responsiveLight.color,
                    defaultColor,
                    Time.deltaTime * transitionSpeed);

                responsiveLight.intensity = Mathf.Lerp(
                    responsiveLight.intensity,
                    defaultIntensity,
                    Time.deltaTime * transitionSpeed);
                continue;
            }

            float targetIntensity = defaultIntensity + pulseValue * pulseIntensityAmount;

            responsiveLight.color = Color.Lerp(
                responsiveLight.color,
                targetColor,
                Time.deltaTime * transitionSpeed);

            responsiveLight.intensity = Mathf.Lerp(
                responsiveLight.intensity,
                targetIntensity,
                Time.deltaTime * transitionSpeed);
        }
    }

    private Color GetCombatColorFromHealth()
    {
        if (playerHealthSystem == null)
        {
            return highHealthColor;
        }

        float healthNormalized = playerHealthSystem.HealthNormalized;
        if (healthNormalized <= lowHealthThreshold)
        {
            return lowHealthColor;
        }

        if (healthNormalized <= midHealthThreshold)
        {
            return midHealthColor;
        }

        return highHealthColor;
    }

    private Color GetCombatColorFromHeartRateState()
    {
        currentHeartRateState = GetActiveHeartRateState();

        switch (currentHeartRateState)
        {
            case LightStressState.RisingStress:
                currentAppliedColor = midHealthColor;
                return currentAppliedColor;
            case LightStressState.HighStress:
                currentAppliedColor = lowHealthColor;
                return currentAppliedColor;
            case LightStressState.Normal:
            default:
                currentAppliedColor = highHealthColor;
                return currentAppliedColor;
        }
    }

    private LightStressState GetActiveHeartRateState()
    {
        if (useDebugHeartRateState)
        {
            return debugHeartRateState;
        }

        if (heartRateStateController == null)
        {
            heartRateStateController = HeartRateStateController.Instance;
        }

        if (heartRateStateController == null)
        {
            return LightStressState.Normal;
        }

        switch (heartRateStateController.CurrentState)
        {
            case HeartRateStateController.HeartRateState.RisingStress:
                return LightStressState.RisingStress;
            case HeartRateStateController.HeartRateState.HighStress:
                return LightStressState.HighStress;
            case HeartRateStateController.HeartRateState.Normal:
            case HeartRateStateController.HeartRateState.Recovering:
            default:
                return LightStressState.Normal;
        }
    }

    public int GetActiveHeartRate()
    {
        int heartRate = useDebugHeartRate ? debugHeartRate : hyperateSocket.CurrentHeartRate;
        if (heartRate <= 0)
        {
            heartRate = fallbackHeartRate;
        }

        return Mathf.Clamp(heartRate, minimumHeartRate, maximumHeartRate);
    }

    private float GetPulseValueFromHeartRate()
    {
        float beatsPerSecond = GetActiveHeartRate() / 60f;
        float pulse = Mathf.Sin(Time.time * beatsPerSecond * Mathf.PI * 2f);
        return (pulse + 1f) * 0.5f;
    }
}
