using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyCarNavMesh : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform playerTarget;

    [Header("Update Frequency")]
    public float pathUpdateRate = 0.2f;

    private NavMeshAgent agent;
    private float nextUpdateTime;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        // Force snap to NavMesh on wakeup to prevent initialization errors
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
    }

    void Update()
    {
        if (playerTarget == null || agent.isStopped) return;

        if (Time.time >= nextUpdateTime)
        {
            nextUpdateTime = Time.time + pathUpdateRate;
            agent.SetDestination(playerTarget.position);
        }
    }

    // --- FORCEFUL COLLISION OVERRIDE ---
    void OnCollisionEnter(Collision collision)
    {
        // Check for the player tag (Double check your Player GameObject is tagged "Player"!)
        if (collision.gameObject.CompareTag("Player"))
        {
            // 1. Instantly kill the agent component so it has ZERO physical presence
            agent.enabled = false;

            // 2. Clear out any leftover momentum from the Rigidbody
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // Give the player 1.5 seconds to escape before the AI wakes back up
            Invoke(nameof(ResumeChasing), 1.5f);
        }
    }

    void ResumeChasing()
    {
        if (agent != null)
        {
            // Turn the navigation engine back on
            agent.enabled = true;

            // Immediately sample the ground to make sure it wakes up safely
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 2.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                agent.SetDestination(playerTarget.position);
            }
        }
    }
}