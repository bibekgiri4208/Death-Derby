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
    [Tooltip("Seconds between destination recalculations.")]
    public float destinationUpdateInterval = 0.15f;

    [Header("Surround")]
    [Tooltip("Angular deviation (degrees) from a direct approach. Higher = more spread around the car.")]
    public float surroundAngle = 30f;

    [Header("Attack")]
    [Tooltip("Distance at which the zombie stops chasing and starts attacking.")]
    public float attackDistance = 3f;
    [Tooltip("Minimum seconds between consecutive attacks.")]
    public float attackCooldown = 1f;

    [Header("Audio")]
    public AudioClip killSound;
    [Tooltip("Volume of the kill sound (0-1).")]
    public float killSoundVolume = 1f;

    [Header("Status")]
    public bool isDead = false;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private Collider col;
    private Animator anim;
    private float destinationUpdateTimer;
    private bool isAttacking;
    private float attackCooldownTimer;
    private int zombieId;
    private float surroundOffset;
    private static int nextZombieId = 0;

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

        zombieId = nextZombieId++;
        surroundOffset = Random.Range(0f, 360f);
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
            float distToPredicted = Vector3.Distance(transform.position, predictedPosition);

            Vector3 destination;

            if (distToPredicted <= attackDistance * 2f)
            {
                destination = predictedPosition;
            }
            else
            {
                Vector3 toZombie = transform.position - predictedPosition;
                toZombie.y = 0f;

                float targetAngle;
                if (toZombie.sqrMagnitude > 0.25f)
                {
                    float currentAngle = Mathf.Atan2(toZombie.x, toZombie.z) * Mathf.Rad2Deg;
                    targetAngle = currentAngle + surroundOffset;
                }
                else
                {
                    targetAngle = surroundOffset;
                }

                Vector3 approachDir = new Vector3(Mathf.Sin(targetAngle * Mathf.Deg2Rad), 0f, Mathf.Cos(targetAngle * Mathf.Deg2Rad));
                destination = predictedPosition + approachDir * distToPredicted * Mathf.Tan(surroundAngle * Mathf.Deg2Rad);
            }

            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 3f, NavMesh.AllAreas))
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
            GameObject soundObj = new GameObject("ZombieKillSound");
            AudioSource src = soundObj.AddComponent<AudioSource>();
            src.clip = killSound;
            src.volume = killSoundVolume;
            src.spatialBlend = 0f;
            src.Play();
            Destroy(soundObj, killSound.length + 0.1f);
        }

        if (agent != null) agent.enabled = false;

        Destroy(gameObject);
    }
}
