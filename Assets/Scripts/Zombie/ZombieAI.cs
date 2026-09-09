using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ZombieAI : MonoBehaviour
{
    [Header("Targeting")]
    public Transform playerCar;
    public string playerTag = "Player";

    [Header("Tracking")]
    [Tooltip("How far ahead to predict the car's position based on its velocity.")]
    public float predictionTime = 0.3f;
    [Tooltip("Random offset range to make zombie paths less uniform.")]
    public float randomOffsetRange = 2f;
    [Tooltip("Seconds between destination recalculations.")]
    public float destinationUpdateInterval = 0.15f;

    [Header("Status")]
    public bool isDead = false;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private Collider col;
    private Animator anim;
    private float destinationUpdateTimer;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        agent.baseOffset = 1.0f;
        agent.height = 2.0f;
        agent.radius = 0.5f;
        agent.speed = 5f;
        agent.acceleration = 12f;
        agent.angularSpeed = 180f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.avoidancePriority = 50;
        agent.updatePosition = true;
        agent.updateRotation = true;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    void Start()
    {
        if (playerCar == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObj != null)
            {
                playerCar = playerObj.transform;
            }
        }

        anim = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (isDead || playerCar == null) return;

        if (anim != null)
        {
            float normalizedSpeed = Mathf.Clamp01(agent.velocity.magnitude / Mathf.Max(agent.speed, 0.01f));
            anim.SetFloat(SpeedHash, normalizedSpeed);
        }

        destinationUpdateTimer -= Time.deltaTime;
        if (destinationUpdateTimer > 0f) return;
        destinationUpdateTimer = destinationUpdateInterval;

        if (agent.enabled && agent.isOnNavMesh)
        {
            Rigidbody carRb = playerCar.GetComponent<Rigidbody>();
            Vector3 carVelocity = carRb != null ? carRb.linearVelocity : Vector3.zero;

            Vector3 predictedPosition = playerCar.position + carVelocity * predictionTime;

            Vector3 toTarget = predictedPosition - transform.position;
            float dist = toTarget.magnitude;
            if (dist > 0.1f)
            {
                Vector3 lateral = Vector3.Cross(Vector3.up, toTarget.normalized) * randomOffsetRange * Mathf.PingPong(Time.time * 0.7f, 1f) * 0.5f;
                predictedPosition += lateral;
            }

            if (NavMesh.SamplePosition(predictedPosition, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
    }

    public void KillZombie()
    {
        if (isDead) return;
        isDead = true;

        if (agent != null) agent.enabled = false;

        Destroy(gameObject);
    }
}