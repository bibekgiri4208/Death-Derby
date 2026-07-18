using UnityEngine;
using UnityEngine.AI; // Required for NavMesh

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyCarNavMesh : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform playerTarget;

    [Header("Update Frequency")]
    [Tooltip("How often (in seconds) the path recalculates. Lower is more accurate, higher saves CPU performance.")]
    public float pathUpdateRate = 0.2f;

    private NavMeshAgent agent;
    private float nextUpdateTime;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

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

        // ADD THIS LINE: If the agent isn't snapped to the NavMesh yet, wait!
        if (!agent.isOnNavMesh) return;

        // Only recalculate the path occasionally to save performance
        if (Time.time >= nextUpdateTime)
        {
            nextUpdateTime = Time.time + pathUpdateRate;

            // Tell the NavMesh agent to calculate a path to the player
            agent.SetDestination(playerTarget.position);
        }
    }
}