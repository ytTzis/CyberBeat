using UnityEngine;
using UnityEngine.Events;

public class Oscillogram : MonoBehaviour
{
    [SerializeField] new ParticleSystem particleSystem;
    [SerializeField] new Rigidbody rigidbody;

    // ** 心率频率由 hyperateSocket 实时提供，无需此字段 **
    // [SerializeField] [Min(0)] [Tooltip("每分钟跳动次数")] int frequency; 

    [SerializeField][Tooltip("基础跳动时施加的力")] float baseAmplitude = 30f;
    [SerializeField][Tooltip("心率变化对力的影响系数")] float amplitudeScale = 2.0f; // 增大了敏感度
    [SerializeField] UnityEvent beatEvent;

    float lastTime;

    void FixedUpdate()
    {
        // 确保粒子系统每帧模拟，绘制曲线
        particleSystem.Emit(1);
        particleSystem.Simulate(Time.fixedDeltaTime, true, false);

        // 1. 获取当前心率 (BPM)
        int currentHeartRate = hyperateSocket.CurrentHeartRate;

        if (currentHeartRate <= 0)
        {
            return; // 心率无效时，不执行跳动
        }

        // 2. 根据心率计算跳动间隔时间 (秒)
        // 实现了“根据心率快慢，增大或减小每次产生波动的间隔”
        float beatInterval = 60.0f / currentHeartRate;

        if (Time.fixedTime - lastTime > beatInterval)
        {
            // 3. 根据心率动态计算跳动施加的力 (振幅)
            float dynamicAmplitude = baseAmplitude + (currentHeartRate - 60) * amplitudeScale;
            // 确保力不会小于基础力
            dynamicAmplitude = Mathf.Max(baseAmplitude, dynamicAmplitude);

            beatEvent.Invoke();
            rigidbody.AddForce(Vector3.up * dynamicAmplitude);
            lastTime = Time.fixedTime;
        }
    }
}
