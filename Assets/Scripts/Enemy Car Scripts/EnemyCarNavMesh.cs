using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyCarNavMesh : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform playerTarget;
    public float pathUpdateRate = 0.2f;

    [Header("Proximity & Braking")]
    [Tooltip("Distance at which the enemy starts applying brakes to prevent ramping under you.")]
    public float stopDistance = 4.5f;
    public float brakingDeceleration = 30f;

    [Header("Ramming / Buzzing Physics")]
    [Tooltip("Minimum impact speed from player required to knock this enemy back.")]
    public float ramImpactThreshold = 6f;
    [Tooltip("Force multiplier applied to enemy when player rams it.")]
    public float knockbackForce = 12f;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private float nextUpdateTime;
    private float defaultAcceleration;
    private bool isKnockedOut = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();

        rb.isKinematic = true; // Keep kinematic while NavMesh controls movement
        defaultAcceleration = agent.acceleration;

        // Snap to NavMesh on start
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 2.0f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }

        if (playerTarget == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTarget = player.transform;
        }
    }

    void Update()
    {
        if (playerTarget == null || isKnockedOut) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);

        // 1. BRAKING SYSTEM (Prevents driving under player when player brakes)
        if (distanceToPlayer <= stopDistance)
        {
            agent.acceleration = brakingDeceleration;
            agent.isStopped = true;
            agent.velocity = Vector3.Lerp(agent.velocity, Vector3.zero, Time.deltaTime * 5f);
        }
        else
        {
            agent.acceleration = defaultAcceleration;
            agent.isStopped = false;

            if (Time.time >= nextUpdateTime)
            {
                nextUpdateTime = Time.time + pathUpdateRate;
                agent.SetDestination(playerTarget.position);
            }
        }
    }

    // 2. RAMMING / BUZZING DETECTOR
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            float impactSpeed = collision.relativeVelocity.magnitude;

            // If the player rams the enemy fast enough, knock the enemy out of pathfinding
            if (impactSpeed >= ramImpactThreshold)
            {
                StopAllCoroutines();
                StartCoroutine(HandleKnockback(collision, impactSpeed));
            }
        }
    }

    System.Collections.IEnumerator HandleKnockback(Collision collision, float speed)
    {
        isKnockedOut = true;

        // Disable agent & activate physics
        agent.enabled = false;
        rb.isKinematic = false;

        // Calculate push vector away from player impact
        Vector3 pushDir = (transform.position - collision.transform.position).normalized;
        pushDir.y = 0.1f; // Slight upward pop for dramatic visual hit

        rb.AddForce(pushDir * speed * knockbackForce, ForceMode.Impulse);

        // Let the physics slide happen for 1.2 seconds before AI recovers
        yield return new WaitForSeconds(1.2f);

        // Reset physics and restore agent control
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        agent.enabled = true;
        isKnockedOut = false;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 3.0f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
    }
}