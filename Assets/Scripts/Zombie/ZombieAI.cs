using UnityEngine;

public enum ZombieState { Patrol, Pursue, Attack }

[RequireComponent(typeof(Rigidbody))]
public class ZombiePhysicsAI : MonoBehaviour
{
    [Header("Target & Detection")]
    public Transform playerCar;
    public float detectionRadius = 15f;
    public float attackRadius = 3f;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 8f;

    [Header("Patrol Settings")]
    public Transform[] patrolPoints;
    private int currentPatrolIndex = 0;
    public float waypointThreshold = 1f;

    [Header("Attack & Crush Stats")]
    public int attackDamage = 10;
    public float attackRate = 1.5f;
    private float nextAttackTime = 0f;
    public float minKillSpeed = 5f; // Min car speed to crush zombie

    private Rigidbody rb;
    public ZombieState currentState = ZombieState.Patrol;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // Freeze rotations so physics forces don't knock the cube onto its side while walking
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Auto-find player car if not assigned
        if (playerCar == null)
        {
            GameObject carObj = GameObject.FindWithTag("Player");
            if (carObj != null) playerCar = carObj.transform;
        }
    }

    void Update()
    {
        if (playerCar == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerCar.position);

        // State Machine logic based on raw distance
        if (distanceToPlayer <= attackRadius)
        {
            currentState = ZombieState.Attack;
        }
        else if (distanceToPlayer <= detectionRadius)
        {
            currentState = ZombieState.Pursue;
        }
        else
        {
            currentState = ZombieState.Patrol;
        }

        if (currentState == ZombieState.Attack)
        {
            AttackBehavior();
        }
    }

    void FixedUpdate()
    {
        // Physics and Movement work best inside FixedUpdate
        switch (currentState)
        {
            case ZombieState.Patrol:
                PatrolBehavior();
                break;
            case ZombieState.Pursue:
                PursueBehavior();
                break;
        }
    }

    void PatrolBehavior()
    {
        if (patrolPoints.Length == 0) return;

        Transform targetWaypoint = patrolPoints[currentPatrolIndex];
        float distanceToWaypoint = Vector3.Distance(transform.position, targetWaypoint.position);

        if (distanceToWaypoint <= waypointThreshold)
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        }
        else
        {
            MoveTowardsTarget(targetWaypoint.position);
        }
    }

    void PursueBehavior()
    {
        MoveTowardsTarget(playerCar.position);
    }

    void MoveTowardsTarget(Vector3 targetPosition)
    {
        Vector3 targetDirection = (targetPosition - transform.position);
        targetDirection.y = 0;

        if (targetDirection.magnitude > 0.1f)
        {
            // Smooth Rotation
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed);

            // Preserve current vertical velocity (so gravity still works) while setting horizontal movement velocity
            Vector3 moveVelocity = transform.forward * moveSpeed;
            rb.linearVelocity = new Vector3(moveVelocity.x, rb.linearVelocity.y, moveVelocity.z); // Note: Use rb.velocity if using older Unity version
        }
    }

    void AttackBehavior()
    {
        // Rotate to face the car while attacking
        Vector3 direction = (playerCar.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }

        if (Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackRate;
            PerformAttack();
        }
    }

    void PerformAttack()
    {
        Debug.Log("Zombie attacked the vehicle!");
    }

    // Run-over logic: checks collision speed with player vehicle
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Rigidbody carRb = collision.gameObject.GetComponent<Rigidbody>();

            if (carRb != null && carRb.linearVelocity.magnitude >= minKillSpeed)
            {
                Die();
            }
        }
    }

    void Die()
    {
        Debug.Log("Zombie crushed!");
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRadius);
    }
}