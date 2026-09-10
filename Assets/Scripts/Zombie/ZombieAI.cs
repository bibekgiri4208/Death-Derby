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

    [Header("Attack")]
    [Tooltip("Distance at which the zombie stops chasing and starts attacking.")]
    public float attackDistance = 3f;
    [Tooltip("Minimum seconds between consecutive attacks.")]
    public float attackCooldown = 1f;

    [Header("Audio")]
    public AudioClip killSound;

    [Header("Status")]
    public bool isDead = false;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private Collider col;
    private Animator anim;
    private float destinationUpdateTimer;
    private bool isAttacking;
    private float attackCooldownTimer;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int Attack1Hash = Animator.StringToHash("Attack1");
    private static readonly int Attack2Hash = Animator.StringToHash("Attack2");

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        agent.baseOffset = 0.9f;
        agent.height = 1.4f;
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

        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.deltaTime;

        if (isAttacking)
        {
            FaceTarget(playerCar.position);

            if (anim != null)
            {
                AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
                if ((stateInfo.IsName("Attack1") || stateInfo.IsName("Attack2")) && stateInfo.normalizedTime >= 1f)
                {
                    EndAttack();
                }
            }
            return;
        }

        if (anim != null)
        {
            float normalizedSpeed = Mathf.Clamp01(agent.velocity.magnitude / Mathf.Max(agent.speed, 0.01f));
            anim.SetFloat(SpeedHash, normalizedSpeed);
        }

        float distToCar = Vector3.Distance(transform.position, playerCar.position);
        if (distToCar <= attackDistance && attackCooldownTimer <= 0f && agent.enabled && agent.isOnNavMesh)
        {
            StartAttack();
            return;
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

    void StartAttack()
    {
        isAttacking = true;
        agent.ResetPath();

        if (Random.value > 0.5f)
        {
            anim.SetBool(Attack1Hash, true);
            anim.SetBool(Attack2Hash, false);
        }
        else
        {
            anim.SetBool(Attack1Hash, false);
            anim.SetBool(Attack2Hash, true);
        }
    }

    void EndAttack()
    {
        isAttacking = false;
        attackCooldownTimer = attackCooldown;
        anim.SetBool(Attack1Hash, false);
        anim.SetBool(Attack2Hash, false);
    }

    void FaceTarget(Vector3 target)
    {
        Vector3 dir = (target - transform.position).normalized;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
        }
    }

    public void KillZombie()
    {
        if (isDead) return;
        isDead = true;

        BloodSplash.Spawn(transform.position + Vector3.up * 0.15f, Quaternion.identity);

        if (killSound != null)
        {
            AudioSource.PlayClipAtPoint(killSound, transform.position);
        }

        if (agent != null) agent.enabled = false;

        Destroy(gameObject);
    }
}
