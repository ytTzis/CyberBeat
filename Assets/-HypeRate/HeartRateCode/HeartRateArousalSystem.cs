using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

//���Ľű�
//ʵ�ֹ�������
//��Ϸ��ʼǰ�Ȳ�����������HR_case
//�������5��ƽ��ֵ
//������һ��5��ƽ��ֵ
//�ж��Ƿ�Ϊͻ������
//�ж��Ƿ�Ϊ��������
//�ж��Ƿ�ָ��侲
//״̬�л���Calm/Alert/Tense/Panic 

public class HeartRateArousalSystem : MonoBehaviour
{
    private const string CalibrationSceneName = "1_GameScene";

    public enum ArousalState
    {
        Calm,
        Alert,
        Tense,
        Panic
    }

    [Header("Calibration")]
    public float calibrationDuration = 20f;
    public bool autoStartCalibration = true;

    [Header("Window Settings")]
    public float shortWindow = 5f;
    public float longWindow = 15f;
    public float sampleInterval = 0.2f;

    [Header("Dynamic Thresholds")]
    [Tooltip("���5�����ǰһ��5����������15%����ͻ������")]
    public float suddenIncreaseThreshold = 0.15f;

    [Tooltip("��ǰ��ֵ��������12%��������������ж�")]
    public float sustainedAboveBaselineThreshold = 0.12f;

    [Tooltip("��ǰ��ֵ���ڻ���5%����Ϊ�ָ��侲�ο�")]
    public float recoverThreshold = 0.05f;

    [Header("Timers")]
    public float sustainedTenseTimeNeeded = 8f;
    public float recoverTimeNeeded = 10f;
    public float panicHoldTime = 1.5f;

    [Header("Runtime Readonly")]
    public float currentHeartRate;
    public float baselineHeartRate;
    public float shortAverage;
    public float previousShortAverage;
    public float longAverage;
    public bool isCalibrating = false;
    public bool suddenTensionTriggered = false;
    public ArousalState currentState = ArousalState.Alert;

    public Action<ArousalState> OnStateChanged;
    public Action OnSuddenTension;

    public static HeartRateArousalSystem Instance { get; private set; }

    private float sampleTimer = 0f;
    private float calibrationTimer = 0f;
    private float sustainedHighTimer = 0f;
    private float recoverTimer = 0f;
    private float panicTimer = 0f;

    private float shortWindowTimer = 0f;
    private float previousShortWindowTimer = 0f;

    private Queue<float> shortSamples = new Queue<float>();
    private Queue<float> longSamples = new Queue<float>();
    private Queue<float> previousShortSamples = new Queue<float>();

    private ArousalState lastState;
    private bool hasCompletedInitialCalibration;

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

    void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (ShouldStartInitialCalibrationForScene(SceneManager.GetActiveScene().name))
        {
            StartCalibration();
        }
        else if (!autoStartCalibration)
        {
            baselineHeartRate = 75f;
            currentState = ArousalState.Alert;
            lastState = currentState;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    public void StartCalibration()
    {
        isCalibrating = true;
        calibrationTimer = 0f;
        baselineHeartRate = 0f;
        sustainedHighTimer = 0f;
        recoverTimer = 0f;
        panicTimer = 0f;

        shortSamples.Clear();
        longSamples.Clear();
        previousShortSamples.Clear();

        shortWindowTimer = 0f;
        previousShortWindowTimer = 0f;
    }

    private bool ShouldStartInitialCalibrationForScene(string sceneName)
    {
        return autoStartCalibration &&
               !hasCompletedInitialCalibration &&
               string.Equals(sceneName, CalibrationSceneName, StringComparison.Ordinal);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (ShouldStartInitialCalibrationForScene(scene.name))
        {
            StartCalibration();
        }
    }

    void Update()
    {
        //ֱ�Ӷ�ȡhyperateSocket �ű����ʵʱ����
        currentHeartRate = hyperateSocket.CurrentHeartRate;

        //��ֹ������ʱ���ʻ��� 0������ƽ��ֵ�ͻ����쳣
        if (currentHeartRate <= 0f)
            return;

        sampleTimer += Time.deltaTime;
        if (sampleTimer >= sampleInterval)
        {
            sampleTimer = 0f;
            AddSample(currentHeartRate);
        }

        if (isCalibrating)
        {
            UpdateCalibration();
            return;
        }

        UpdateArousalLogic();
        UpdateState();
    }

    void UpdateCalibration()
    {
        calibrationTimer += Time.deltaTime;

        if (longSamples.Count > 0)
        {
            baselineHeartRate = AverageQueue(longSamples);
        }

        if (calibrationTimer >= calibrationDuration)
        {
            isCalibrating = false;
            baselineHeartRate = Mathf.Max(1f, AverageQueue(longSamples));
            hasCompletedInitialCalibration = true;
            currentState = ArousalState.Alert;
            lastState = currentState;

            Debug.Log("Calibration Finished. Baseline HR = " + baselineHeartRate);
        }
    }

    void AddSample(float hr)
    {
        shortSamples.Enqueue(hr);
        longSamples.Enqueue(hr);

        shortWindowTimer += sampleInterval;
        previousShortWindowTimer += sampleInterval;

        while (shortWindowTimer > shortWindow && shortSamples.Count > 0)
        {
            float moved = shortSamples.Dequeue();
            previousShortSamples.Enqueue(moved);
            shortWindowTimer -= sampleInterval;
        }

        while (previousShortWindowTimer > shortWindow && previousShortSamples.Count > 0)
        {
            previousShortSamples.Dequeue();
            previousShortWindowTimer -= sampleInterval;
        }

        int maxLongCount = Mathf.CeilToInt(longWindow / sampleInterval);
        while (longSamples.Count > maxLongCount)
        {
            longSamples.Dequeue();
        }

        shortAverage = AverageQueue(shortSamples);
        previousShortAverage = AverageQueue(previousShortSamples);
        longAverage = AverageQueue(longSamples);
    }

    void UpdateArousalLogic()
    {
        suddenTensionTriggered = false;

        float baselineDelta = 0f;
        if (baselineHeartRate > 0.01f)
            baselineDelta = (shortAverage - baselineHeartRate) / baselineHeartRate;

        float shortIncreaseRatio = 0f;
        if (previousShortAverage > 0.01f)
            shortIncreaseRatio = (shortAverage - previousShortAverage) / previousShortAverage;

        // ͻ�������ж�
        if (shortIncreaseRatio >= suddenIncreaseThreshold)
        {
            suddenTensionTriggered = true;
            panicTimer = panicHoldTime;
            OnSuddenTension?.Invoke();

            // ===== ������д��ͻ�����š�ʱҪִ�еĹ��� =====
            //
            //
            //
            //
            //
            // ===========================================
        }

        // ���������ж�
        if (baselineDelta >= sustainedAboveBaselineThreshold)
        {
            sustainedHighTimer += Time.deltaTime;
            recoverTimer = 0f;

            // =====������д�����������ڼ䡱Ҫִ�еĹ��� =====
            //
            //
            //
            //
            // =============================================
        }
        else if (baselineDelta <= recoverThreshold)
        {
            recoverTimer += Time.deltaTime;
            sustainedHighTimer = 0f;
        }
        else
        {
            sustainedHighTimer = Mathf.Max(0f, sustainedHighTimer - Time.deltaTime * 0.5f);
            recoverTimer = 0f;
        }

        if (panicTimer > 0f)
        {
            panicTimer -= Time.deltaTime;
        }
    }

    void UpdateState()
    {
        ArousalState newState = currentState;

        if (panicTimer > 0f)
        {
            newState = ArousalState.Panic;
        }
        else if (shortAverage < baselineHeartRate * 0.95f && recoverTimer >= recoverTimeNeeded)
        {
            newState = ArousalState.Calm;
        }
        else if (sustainedHighTimer >= sustainedTenseTimeNeeded)
        {
            newState = ArousalState.Tense;
        }
        else
        {
            newState = ArousalState.Alert;
        }

        currentState = newState;

        if (currentState != lastState)
        {
            lastState = currentState;
            OnStateChanged?.Invoke(currentState);
            Debug.Log("Arousal State Changed => " + currentState);
        }
    }

    float AverageQueue(Queue<float> queue)
    {
        if (queue.Count == 0) return 0f;

        float sum = 0f;
        foreach (var v in queue)
            sum += v;

        return sum / queue.Count;
    }
}
