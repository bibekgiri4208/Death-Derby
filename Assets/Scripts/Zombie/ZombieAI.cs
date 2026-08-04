using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ZombieAI : MonoBehaviour
{
    [Header("Targeting")]
    public Transform playerCar;
    public string playerTag = "Player";

    [Header("Status")]
    public bool isDead = false;

    [Header("Pivot Offset Adjustment")]
    [Tooltip("If the 3D model pivot is at chest level, adjust this (e.g., 0.9) to raise feet above ground.")]
    public float baseVerticalOffset = 0f;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private Collider col;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // Configure NavMeshAgent to handle movement completely
        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.baseOffset = baseVerticalOffset;

        // Configure Rigidbody as Kinematic so physics engine NEVER pushes the car
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Set collider as a Trigger so it detects car hits without physical pushing
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
    }

    void Update()
    {
        if (isDead || playerCar == null) return;

        // Drive agent position directly toward player car
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.SetDestination(playerCar.position);
        }
    }

    /// <summary>
    /// Call this when the car hits the zombie with sufficient speed.
    /// </summary>
    public void KillZombie(Vector3 launchForce)
    {
        if (isDead) return;
        isDead = true;

        // Disable AI navigation
        if (agent != null) agent.enabled = false;

        // Turn on ragdoll/physics launch upon death
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.None;
            rb.AddForce(launchForce, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 15f, ForceMode.Impulse);
        }

        Destroy(gameObject, 4f);
    }
}