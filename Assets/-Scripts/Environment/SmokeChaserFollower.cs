using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class SmokeChaserFollower : MonoBehaviour, ISceneIntroTransitionReceiver
{
    [SerializeField] private Transform target;
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private bool startChasingOnSceneIntroFinished = true;
    [SerializeField] private float startDelayAfterSceneIntro = 1f;
    [SerializeField] private string stopObjectName = "Stop";
    [SerializeField] private bool hideWhenStopped = true;

    private bool isChasing;
    private float chaseStartTime = -1f;

    private void Awake()
    {
        EnsureTriggerRigidbody();
        ResolveTarget();
    }

    private void Update()
    {
        if (!isChasing)
        {
            return;
        }

        if (Time.time < chaseStartTime)
        {
            return;
        }

        if (target == null)
        {
            ResolveTarget();
            if (target == null)
            {
                return;
            }
        }

        float currentX = transform.position.x;
        float targetX = target.position.x;
        float deltaX = targetX - currentX;
        float distance = Mathf.Abs(deltaX);

        if (distance > Mathf.Epsilon)
        {
            float moveStep = Mathf.Sign(deltaX) * moveSpeed * Time.deltaTime;
            float nextX = Mathf.Abs(moveStep) >= distance ? targetX : currentX + moveStep;
            Vector3 nextPosition = transform.position;
            nextPosition.x = nextX;
            transform.position = nextPosition;
        }
    }

    public void OnSceneIntroTransitionFinished()
    {
        if (startChasingOnSceneIntroFinished)
        {
            StartChasing();
        }
    }

    public void StartChasing()
    {
        ResolveTarget();
        isChasing = true;
        chaseStartTime = Time.time + Mathf.Max(0f, startDelayAfterSceneIntro);
    }

    public void StopChasing()
    {
        isChasing = false;
        chaseStartTime = -1f;

        if (hideWhenStopped)
        {
            gameObject.SetActive(false);
        }
    }

    private void ResolveTarget()
    {
        if (target != null)
        {
            return;
        }

        CharacterInputSystem inputSystem = FindFirstObjectByType<CharacterInputSystem>();
        if (inputSystem != null)
        {
            target = inputSystem.transform;
            return;
        }

        GameObject player = GameObject.Find("Player (1)");
        if (player != null)
        {
            target = player.transform;
        }
    }

    private void EnsureTriggerRigidbody()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryStopFromCollider(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryStopFromCollider(collision.collider);
    }

    private void TryStopFromCollider(Collider other)
    {
        if (other == null || string.IsNullOrEmpty(stopObjectName))
        {
            return;
        }

        if (other.name == stopObjectName || other.transform.root.name == stopObjectName)
        {
            StopChasing();
        }
    }
}
