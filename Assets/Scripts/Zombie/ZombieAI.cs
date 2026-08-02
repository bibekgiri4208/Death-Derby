using UnityEngine;
using UnityEngine.AI;

public enum ZombieState { Patrol, Pursue, Attack }

[RequireComponent(typeof(NavMeshAgent))]
public class ZombieAI : MonoBehaviour
{
    [Header("Target & Detection")]
    public Transform playerCar;
    public float detectionRadius = 15f;
    public float attackRadius = 3f;

    [Header("Patrol Settings")]
    public Transform[] patrolPoints;
    private int currentPatrolIndex;

    [Header("Zombie Stats")]
    public int attackDamage = 10;
    public float attackRate = 1.5f;
    private float nextAttackTime = 0f;
    public float minKillSpeed = 5f; // Minimum car speed required to crush the zombie

    private NavMeshAgent agent;
    public ZombieState currentState = ZombieState.Patrol;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        // Auto-find player car if not assigned
        if (playerCar == null)
        {
            GameObject carObj = GameObject.FindWithTag("Player");
            if (carObj != null) playerCar = carObj.transform;
        }

        GoToNextPatrolPoint();
    }

    void Update()
    {
        if (playerCar == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerCar.position);

        // State Transitions
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
            if (currentState == ZombieState.Attack || currentState == ZombieState.Pursue)
            {
                currentState = ZombieState.Patrol;
                GoToNextPatrolPoint();
            }
        }

        // State Behaviors
        switch (currentState)
        {
            case ZombieState.Patrol:
                PatrolBehavior();
                break;
            case ZombieState.Pursue:
                PursueBehavior();
                break;
            case ZombieState.Attack:
                AttackBehavior();
                break;
        }
    }

    void PatrolBehavior()
    {
        if (patrolPoints.Length == 0) return;

        // Move to next point when reaching current destination
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            GoToNextPatrolPoint();
        }
    }

    void GoToNextPatrolPoint()
    {
        if (patrolPoints.Length == 0) return;
        agent.destination = patrolPoints[currentPatrolIndex].position;
        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
    }

    void PursueBehavior()
    {
        agent.SetDestination(playerCar.position);
    }

    void AttackBehavior()
    {
        // Stop moving while attacking
        agent.SetDestination(transform.position);

        // Face towards the car
        Vector3 direction = (playerCar.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 5f);
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
        // Add damage call here (e.g., playerCar.GetComponent<VehicleHealth>()?.TakeDamage(attackDamage));
    }

    // Handle getting driven over / killed by the car
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Rigidbody carRb = collision.gameObject.GetComponent<Rigidbody>();

            // Check if car is moving fast enough to crush the zombie
            if (carRb != null && carRb.linearVelocity.magnitude >= minKillSpeed)
            {
                Die();
            }
        }
    }

    void Die()
    {
        Debug.Log("Zombie squished!");
        // Spawn particle effects, blood decals, or squish sound here
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        // Visualize detection and attack ranges in the editor
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRadius);
    }
}