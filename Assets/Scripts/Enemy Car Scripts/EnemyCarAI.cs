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

        // FORCE SNAP TO NAVMESH ON WAKEUP
        UnityEngine.AI.NavMeshHit hit;
        // Search within a 2.0 unit radius of the current position for the blue mesh
        if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out hit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
        {
            agent.Warp(hit.position); // Hard-snaps the agent's logic directly to the coordinates
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