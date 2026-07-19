using UnityEngine;
using UnityEngine.AI; // Required for NavMesh

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyCarNavMesh : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform playerTarget;

    [Header("Update Frequency")]
    [Tooltip("How often (in seconds) the path recalculates.")]
    public float pathUpdateRate = 0.2f;

    [Header("Proximity Stopping Settings")]
    [Tooltip("The distance (in meters) from the player where the enemy car will stop chasing.")]
    public float stoppingDistanceThreshold = 4.5f;
    [Tooltip("How fast the enemy car slows down to a halt when getting close.")]
    public float brakingDeceleration = 20f;

    private NavMeshAgent agent;
    private float nextUpdateTime;
    private float originalAcceleration;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        // Ensure the Rigidbody remains purely Kinematic since we are dodging physics crashes entirely
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        originalAcceleration = agent.acceleration;

        // Force snap to NavMesh on awake
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 2.0f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }

        // Auto-find player by tag if not assigned
        if (playerTarget == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTarget = player.transform;
            }
        }
    }

    void Update()
    {
        if (playerTarget == null) return;

        // Calculate the flat horizontal distance between the two vehicles
        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);

        if (distanceToPlayer <= stoppingDistanceThreshold)
        {
            // PROXIMITY STOP TRIGGERED:
            // Crank up acceleration/braking so it halts cleanly instead of sliding into the player
            agent.acceleration = brakingDeceleration;
            agent.isStopped = true;
            agent.ResetPath();
        }
        else
        {
            // RESUME CHASE:
            // Restore normal acceleration properties and track the player
            agent.acceleration = originalAcceleration;
            agent.isStopped = false;

            if (Time.time >= nextUpdateTime)
            {
                nextUpdateTime = Time.time + pathUpdateRate;
                agent.SetDestination(playerTarget.position);
            }
        }
    }
}