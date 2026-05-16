using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UGG.Environment
{
    public class ProximitySlidingDoubleDoor : MonoBehaviour
    {
        [SerializeField, Header("检测玩家"), InspectorName("检测中心")] private Transform detectionCenter;
        [SerializeField, InspectorName("检测半径")] private float detectionRadius = 4f;
        [SerializeField, InspectorName("玩家图层")] private LayerMask playerLayer = ~0;
        [SerializeField, InspectorName("玩家标签")] private string playerTag = "Player";
        [SerializeField, InspectorName("玩家对象，可不填")] private Transform playerTarget;

        [SerializeField, Header("\u95E8对象"), InspectorName("左\u95E8，不填则移动当前物体")] private Transform leftDoor;
        [SerializeField, InspectorName("右\u95E8")] private Transform rightDoor;
        [SerializeField, InspectorName("左\u95E8本地移动偏移")] private Vector3 leftDoorLocalOffset = new Vector3(-1.5f, 0f, 0f);
        [SerializeField, InspectorName("右\u95E8本地移动偏移")] private Vector3 rightDoorLocalOffset = new Vector3(1.5f, 0f, 0f);

        [SerializeField, Header("开\u95E8设置"), InspectorName("开\u95E8耗时")] private float openDuration = 1f;
        [SerializeField, InspectorName("开\u95E8曲线")] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Header("关\u95E8设置"), InspectorName("自动关\u95E8")] private bool autoClose;
        [SerializeField, InspectorName("开\u95E8后等待时间")] private float closeDelay = 2f;
        [SerializeField, InspectorName("关\u95E8耗时")] private float closeDuration = 1f;
        [SerializeField, InspectorName("关\u95E8时玩家在范围内则等待")] private bool waitUntilPlayerLeaves = true;
        [SerializeField, InspectorName("关\u95E8检测半径，0则使用检测半径")] private float closeDetectionRadius;
        [SerializeField, InspectorName("是否已经打开")] private bool hasOpened;
        [SerializeField, InspectorName("开始时直接打开")] private bool openOnStart;
        [SerializeField, InspectorName("输出调试信息")] private bool showDebugLog;

        private readonly Collider[] playerDetectionResults = new Collider[4];
        private Vector3 leftDoorClosedLocalPosition;
        private Vector3 rightDoorClosedLocalPosition;
        private Vector3 leftDoorOpenLocalPosition;
        private Vector3 rightDoorOpenLocalPosition;
        private Coroutine openCoroutine;
        private Coroutine closeCoroutine;

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                if (leftDoor == null && rightDoor == null)
                {
                    leftDoor = transform;
                }

                CacheDoorPositions();
                DisableStaticFlagsForDoors();
            }
        }

        private void Awake()
        {
            if (detectionCenter == null)
            {
                detectionCenter = transform;
            }

            if (playerTarget == null)
            {
                playerTarget = FindPlayerTarget();
            }

            if (leftDoor == null && rightDoor == null)
            {
                leftDoor = transform;
            }

            CacheDoorPositions();
            DisableStaticFlagsForDoors();
        }

        private void Start()
        {
            if (showDebugLog)
            {
                Debug.Log($"[双开平移\u95E8] 脚本已启动：{name}，开始时直接打开={openOnStart}，是否已经打开={hasOpened}", this);
            }

            if (openOnStart)
            {
                hasOpened = false;
                OpenDoor();
            }
        }

        private void Update()
        {
            if (hasOpened)
            {
                return;
            }

            if (IsPlayerNearby())
            {
                OpenDoor();
            }
        }

        public void OpenDoor()
        {
            if (hasOpened)
            {
                return;
            }

            hasOpened = true;

            if (showDebugLog)
            {
                Debug.Log($"[双开平移\u95E8] 已触发开\u95E8：{name}", this);
            }

            if (openCoroutine != null)
            {
                StopCoroutine(openCoroutine);
            }

            if (closeCoroutine != null)
            {
                StopCoroutine(closeCoroutine);
                closeCoroutine = null;
            }

            openCoroutine = StartCoroutine(OpenRoutine());
        }

        private IEnumerator OpenRoutine()
        {
            float timer = 0f;
            Vector3 leftStart = leftDoor != null ? leftDoor.localPosition : Vector3.zero;
            Vector3 rightStart = rightDoor != null ? rightDoor.localPosition : Vector3.zero;

            while (timer < openDuration)
            {
                timer += Time.deltaTime;
                float progress = Mathf.Clamp01(timer / openDuration);
                float curvedProgress = openCurve.Evaluate(progress);

                if (leftDoor != null)
                {
                    leftDoor.localPosition = Vector3.Lerp(leftStart, leftDoorOpenLocalPosition, curvedProgress);
                }

                if (rightDoor != null)
                {
                    rightDoor.localPosition = Vector3.Lerp(rightStart, rightDoorOpenLocalPosition, curvedProgress);
                }

                yield return null;
            }

            if (leftDoor != null)
            {
                leftDoor.localPosition = leftDoorOpenLocalPosition;
            }

            if (rightDoor != null)
            {
                rightDoor.localPosition = rightDoorOpenLocalPosition;
            }

            openCoroutine = null;

            if (autoClose)
            {
                if (showDebugLog)
                {
                    Debug.Log($"[双开平移\u95E8] 开始等待自动关\u95E8：{name}，等待时间={closeDelay}", this);
                }

                closeCoroutine = StartCoroutine(AutoCloseRoutine());
            }
        }

        private IEnumerator AutoCloseRoutine()
        {
            yield return new WaitForSeconds(closeDelay);

            while (waitUntilPlayerLeaves && IsPlayerInsideCloseRange())
            {
                yield return null;
            }

            if (showDebugLog)
            {
                Debug.Log($"[双开平移\u95E8] 开始关\u95E8：{name}", this);
            }

            yield return CloseRoutine();
            closeCoroutine = null;
        }

        private IEnumerator CloseRoutine()
        {
            float timer = 0f;
            Vector3 leftStart = leftDoor != null ? leftDoor.localPosition : Vector3.zero;
            Vector3 rightStart = rightDoor != null ? rightDoor.localPosition : Vector3.zero;

            while (timer < closeDuration)
            {
                timer += Time.deltaTime;
                float progress = Mathf.Clamp01(timer / closeDuration);
                float curvedProgress = openCurve.Evaluate(progress);

                if (leftDoor != null)
                {
                    leftDoor.localPosition = Vector3.Lerp(leftStart, leftDoorClosedLocalPosition, curvedProgress);
                }

                if (rightDoor != null)
                {
                    rightDoor.localPosition = Vector3.Lerp(rightStart, rightDoorClosedLocalPosition, curvedProgress);
                }

                yield return null;
            }

            if (leftDoor != null)
            {
                leftDoor.localPosition = leftDoorClosedLocalPosition;
            }

            if (rightDoor != null)
            {
                rightDoor.localPosition = rightDoorClosedLocalPosition;
            }

            hasOpened = false;

            if (showDebugLog)
            {
                Debug.Log($"[双开平移\u95E8] 关\u95E8完成：{name}", this);
            }
        }

        private void DisableStaticFlagsForDoors()
        {
            SetDoorStaticFlag(leftDoor, false);
            SetDoorStaticFlag(rightDoor, false);
        }

        private void SetDoorStaticFlag(Transform door, bool isStatic)
        {
            if (door == null)
            {
                return;
            }

            SetTargetStaticState(door.gameObject, isStatic);

            Renderer[] renderers = door.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                SetTargetStaticState(renderers[i].gameObject, isStatic);
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
            if (leftDoor == null && rightDoor == null)
            {
                leftDoor = transform;
            }

            CacheDoorPositions();
            DisableStaticFlagsForDoors();
        }

        private void CacheDoorPositions()
        {
            if (leftDoor != null)
            {
                leftDoorClosedLocalPosition = leftDoor.localPosition;
                leftDoorOpenLocalPosition = leftDoorClosedLocalPosition + leftDoorLocalOffset;
            }

            if (rightDoor != null)
            {
                rightDoorClosedLocalPosition = rightDoor.localPosition;
                rightDoorOpenLocalPosition = rightDoorClosedLocalPosition + rightDoorLocalOffset;
            }
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

        private bool IsPlayerInsideCloseRange()
        {
            if (playerTarget == null)
            {
                return false;
            }

            float radius = closeDetectionRadius > 0f ? closeDetectionRadius : detectionRadius;
            float sqrDistance = (playerTarget.position - detectionCenter.position).sqrMagnitude;
            return sqrDistance <= radius * radius;
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

        private void OnDrawGizmosSelected()
        {
            Transform center = detectionCenter != null ? detectionCenter : transform;
            Gizmos.color = hasOpened ? Color.cyan : Color.yellow;
            Gizmos.DrawWireSphere(center.position, detectionRadius);
        }
    }
}
