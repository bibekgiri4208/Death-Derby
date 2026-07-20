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
    public float stopDistance = 4.5f;
    public float brakingDeceleration = 30f;

    [Header("Buzzing / Ramming Physics")]
    [Tooltip("Radius around the enemy car to detect incoming fast player hits.")]
    public float buzzDetectionRadius = 2.5f;
    [Tooltip("Minimum player speed required to trigger a buzz/knockback.")]
    public float minBuzzSpeed = 5f;
    [Tooltip("Force multiplier applied to enemy when player buzzes it.")]
    public float knockbackForce = 15f;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private Rigidbody playerRb;
    private float nextUpdateTime;
    private float defaultAcceleration;
    private bool isKnockedOut = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();

        rb.isKinematic = true;
        defaultAcceleration = agent.acceleration;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 2.0f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }

        if (playerTarget == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTarget = player.transform;
            }
        }

        if (playerTarget != null)
        {
            playerRb = playerTarget.GetComponent<Rigidbody>();
        }
    }

    void Update()
    {
        if (playerTarget == null || isKnockedOut) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);

        // --- BUZZ SENSOR CHECK ---
        // If the player is within buzzing distance AND moving fast, trigger knockback instantly
        if (distanceToPlayer <= buzzDetectionRadius && playerRb != null)
        {
            float playerSpeed = playerRb.linearVelocity.magnitude;
            if (playerSpeed >= minBuzzSpeed)
            {
                TriggerBuzz(playerRb.linearVelocity);
                return;
            }
        }

        // --- BRAKING / CHASE SYSTEM ---
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

    void TriggerBuzz(Vector3 playerVelocity)
    {
        StopAllCoroutines();
        StartCoroutine(HandleBuzzKnockback(playerVelocity));
    }

    System.Collections.IEnumerator HandleBuzzKnockback(Vector3 playerVelocity)
    {
        isKnockedOut = true;

        // Immediately drop NavMesh control and unlock physics
        agent.enabled = false;
        rb.isKinematic = false;

        // Apply directional knockback based on the player's momentum
        Vector3 pushDirection = playerVelocity.normalized;
        pushDirection.y = 0.05f; // Slight pop so wheels unstick from ground

        rb.AddForce(pushDirection * playerVelocity.magnitude * knockbackForce, ForceMode.Impulse);
        rb.AddTorque(transform.up * Random.Range(-50f, 50f), ForceMode.Impulse); // Visual spin out

        // Let the enemy car physically spin/slide for 1.2s
        yield return new WaitForSeconds(1.2f);

        // Reset velocities and restore AI
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

    // Visual debugging ring in Scene view to check your buzz radius
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, buzzDetectionRadius);
    }
}