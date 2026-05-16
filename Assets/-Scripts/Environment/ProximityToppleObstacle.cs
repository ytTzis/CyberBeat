using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UGG.Environment
{
    public class ProximityToppleObstacle : MonoBehaviour
    {
        [SerializeField, Header("检测玩家"), InspectorName("检测中心")] private Transform detectionCenter;
        [SerializeField, InspectorName("检测半径")] private float detectionRadius = 3f;
        [SerializeField, InspectorName("玩家图层")] private LayerMask playerLayer = ~0;
        [SerializeField, InspectorName("玩家标签")] private string playerTag = "Player";
        [SerializeField, InspectorName("玩家对象，可不填")] private Transform playerTarget;

        [SerializeField, Header("倾倒对象"), InspectorName("不填则当前物体倒下")] private Transform toppleTarget;
        [SerializeField, InspectorName("额外一起倾倒的独立对象")] private List<Transform> toppleParts = new List<Transform>();
        [SerializeField, InspectorName("倾倒角度偏移")] private Vector3 toppleLocalEulerOffset = new Vector3(0f, 0f, 90f);
        [SerializeField, Min(0.01f), InspectorName("倾倒耗时")] private float toppleDuration = 0.85f;
        [SerializeField, InspectorName("倾倒曲线")] private AnimationCurve toppleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Header("防穿模"), InspectorName("使用物理倾倒")] private bool usePhysicsTopple;
        [SerializeField, InspectorName("自动添加刚体")] private bool addRigidbodyIfMissing = true;
        [SerializeField, InspectorName("物理倾倒力度")] private float physicsToppleTorque = 8f;
        [SerializeField, InspectorName("倒下后冻结时间")] private float freezePhysicsAfterSeconds = 2f;
        [SerializeField, InspectorName("旋转倾倒时防止穿过地面")] private bool preventGroundPenetration = true;

        [SerializeField, Header("触发状态"), InspectorName("是否已经触发")] private bool hasTriggered;
        [SerializeField, InspectorName("开始时直接触发")] private bool triggerOnStart;
        [SerializeField, InspectorName("输出调试信息")] private bool showDebugLog;

        private readonly Collider[] playerDetectionResults = new Collider[4];
        private readonly List<Transform> activeToppleTargets = new List<Transform>();
        private readonly List<Quaternion> targetLocalRotations = new List<Quaternion>();
        private readonly List<float> minimumWorldHeights = new List<float>();
        private Coroutine toppleCoroutine;

        private void Reset()
        {
            toppleLocalEulerOffset = new Vector3(90f, 0f, 0f);
            detectionRadius = 3f;
            playerLayer = ~0;
            playerTag = "Player";
        }

        private void OnValidate()
        {
            if (toppleLocalEulerOffset == Vector3.zero)
            {
                toppleLocalEulerOffset = new Vector3(90f, 0f, 0f);
            }

            if (!Application.isPlaying)
            {
                CacheToppleTargets();
                DisableStaticFlagsForToppleTargets();
            }
        }

        private void Awake()
        {
            if (detectionCenter == null)
            {
                detectionCenter = transform;
            }

            if (toppleTarget == null)
            {
                toppleTarget = transform;
            }

            if (playerTarget == null)
            {
                playerTarget = FindPlayerTarget();
            }

            CacheToppleTargets();
            DisableStaticFlagsForToppleTargets();
        }

        private void Start()
        {
            if (showDebugLog)
            {
                Debug.Log($"[倾倒障碍物] 脚本已启动：{name}，开始时直接触发={triggerOnStart}，是否已经触发={hasTriggered}", this);
            }

            if (triggerOnStart)
            {
                hasTriggered = false;
                TriggerTopple();
            }
        }

        private void Update()
        {
            if (hasTriggered)
            {
                return;
            }

            if (IsPlayerNearby())
            {
                TriggerTopple();
            }
        }

        public void TriggerTopple()
        {
            if (hasTriggered)
            {
                return;
            }

            hasTriggered = true;

            if (showDebugLog)
            {
                Debug.Log($"[倾倒障碍物] 已触发：{name}", this);
            }

            if (toppleCoroutine != null)
            {
                StopCoroutine(toppleCoroutine);
            }

            if (usePhysicsTopple)
            {
                StartPhysicsTopple();
                return;
            }

            toppleCoroutine = StartCoroutine(ToppleRoutine());
        }

        private bool IsPlayerNearby()
        {
            if (playerTarget != null)
            {
                float sqrDistance = (playerTarget.position - detectionCenter.position).sqrMagnitude;
                if (sqrDistance <= detectionRadius * detectionRadius)
                {
                    return true;
                }
            }

            int hitCount = Physics.OverlapSphereNonAlloc(
                detectionCenter.position,
                detectionRadius,
                playerDetectionResults,
                playerLayer,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = playerDetectionResults[i];
                if (hit == null)
                {
                    continue;
                }

                if (IsPlayerCollider(hit))
                {
                    return true;
                }
            }

            return false;
        }

        private Transform FindPlayerTarget()
        {
            CharacterInputSystem inputSystem = FindFirstObjectByType<CharacterInputSystem>();
            if (inputSystem != null)
            {
                return inputSystem.transform;
            }

            if (!string.IsNullOrEmpty(playerTag))
            {
                GameObject taggedPlayer = GameObject.FindGameObjectWithTag(playerTag);
                if (taggedPlayer != null)
                {
                    return taggedPlayer.transform;
                }
            }

            GameObject namedPlayer = GameObject.Find("Player (1)");
            if (namedPlayer != null)
            {
                return namedPlayer.transform;
            }

            return null;
        }

        private bool IsPlayerCollider(Collider hit)
        {
            if (string.IsNullOrEmpty(playerTag) || hit.CompareTag(playerTag) || hit.transform.root.CompareTag(playerTag))
            {
                return true;
            }

            return hit.GetComponentInParent<CharacterInputSystem>() != null;
        }

        private IEnumerator ToppleRoutine()
        {
            float timer = 0f;
            Quaternion[] startRotations = new Quaternion[activeToppleTargets.Count];
            for (int i = 0; i < activeToppleTargets.Count; i++)
            {
                startRotations[i] = activeToppleTargets[i].localRotation;
            }

            while (timer < toppleDuration)
            {
                timer += Time.deltaTime;
                float progress = Mathf.Clamp01(timer / toppleDuration);
                float curvedProgress = toppleCurve.Evaluate(progress);

                for (int i = 0; i < activeToppleTargets.Count; i++)
                {
                    activeToppleTargets[i].localRotation = Quaternion.Slerp(startRotations[i], targetLocalRotations[i], curvedProgress);
                    LiftTargetAboveMinimumHeight(i);
                }

                yield return null;
            }

            for (int i = 0; i < activeToppleTargets.Count; i++)
            {
                activeToppleTargets[i].localRotation = targetLocalRotations[i];
                LiftTargetAboveMinimumHeight(i);
            }

            if (showDebugLog)
            {
                Debug.Log($"[倾倒障碍物] 倾倒完成：{name}，倾倒对象数量={activeToppleTargets.Count}", this);
            }

            toppleCoroutine = null;
        }

        private void StartPhysicsTopple()
        {
            Vector3 torqueAxis = toppleLocalEulerOffset.normalized;
            if (torqueAxis == Vector3.zero)
            {
                torqueAxis = Vector3.right;
            }

            for (int i = 0; i < activeToppleTargets.Count; i++)
            {
                Transform target = activeToppleTargets[i];
                Rigidbody body = target.GetComponent<Rigidbody>();
                if (body == null && addRigidbodyIfMissing)
                {
                    body = target.gameObject.AddComponent<Rigidbody>();
                }

                if (body == null)
                {
                    if (showDebugLog)
                    {
                        Debug.LogWarning($"[倾倒障碍物] 物理倾倒需要 Rigidbody：{target.name}", target);
                    }

                    continue;
                }

                if (target.GetComponentInChildren<Collider>() == null && showDebugLog)
                {
                    Debug.LogWarning($"[倾倒障碍物] 物理倾倒建议给对象添加 Collider：{target.name}", target);
                }

                body.isKinematic = false;
                body.useGravity = true;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.AddTorque(target.TransformDirection(torqueAxis) * physicsToppleTorque, ForceMode.Impulse);

                if (freezePhysicsAfterSeconds > 0f)
                {
                    StartCoroutine(FreezePhysicsAfterDelay(body, freezePhysicsAfterSeconds));
                }
            }
        }

        private IEnumerator FreezePhysicsAfterDelay(Rigidbody body, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (body == null)
            {
                yield break;
            }

            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        private void CacheToppleTargets()
        {
            activeToppleTargets.Clear();
            targetLocalRotations.Clear();
            minimumWorldHeights.Clear();

            if (toppleParts.Count > 0)
            {
                for (int i = 0; i < toppleParts.Count; i++)
                {
                    AddToppleTarget(toppleParts[i]);
                }
            }
            else
            {
                AddToppleTarget(toppleTarget);
            }
        }

        private void AddToppleTarget(Transform target)
        {
            if (target == null || activeToppleTargets.Contains(target))
            {
                return;
            }

            activeToppleTargets.Add(target);
            targetLocalRotations.Add(target.localRotation * Quaternion.Euler(toppleLocalEulerOffset));
            minimumWorldHeights.Add(GetTargetMinimumWorldHeight(target));
        }

        private void LiftTargetAboveMinimumHeight(int index)
        {
            if (!preventGroundPenetration || index < 0 || index >= activeToppleTargets.Count)
            {
                return;
            }

            Transform target = activeToppleTargets[index];
            float currentMinimumHeight = GetTargetMinimumWorldHeight(target);
            float targetMinimumHeight = minimumWorldHeights[index];

            if (currentMinimumHeight < targetMinimumHeight)
            {
                target.position += Vector3.up * (targetMinimumHeight - currentMinimumHeight);
            }
        }

        private float GetTargetMinimumWorldHeight(Transform target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return target.position.y;
            }

            float minimumHeight = float.MaxValue;
            for (int i = 0; i < renderers.Length; i++)
            {
                minimumHeight = Mathf.Min(minimumHeight, renderers[i].bounds.min.y);
            }

            return minimumHeight;
        }

        private void DisableStaticFlagsForToppleTargets()
        {
            for (int i = 0; i < activeToppleTargets.Count; i++)
            {
                Transform target = activeToppleTargets[i];
                SetHierarchyStaticState(target, false);
            }
        }

        private static void SetHierarchyStaticState(Transform targetRoot, bool isStatic)
        {
            if (targetRoot == null)
            {
                return;
            }

            Transform[] allTransforms = targetRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                SetTargetStaticState(allTransforms[i].gameObject, isStatic);
            }
        }

        private static void SetTargetStaticState(GameObject targetObject, bool isStatic)
        {
            if (targetObject == null)
            {
                return;
            }

            if (targetObject.isStatic == isStatic)
            {
                return;
            }

            targetObject.isStatic = isStatic;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(targetObject);
            }
#endif
        }

        public void ClearStaticFlagsInEditor()
        {
            CacheToppleTargets();
            DisableStaticFlagsForToppleTargets();
        }

        private void OnDrawGizmosSelected()
        {
            Transform center = detectionCenter != null ? detectionCenter : transform;
            Gizmos.color = hasTriggered ? Color.gray : Color.yellow;
            Gizmos.DrawWireSphere(center.position, detectionRadius);
        }
    }
}
