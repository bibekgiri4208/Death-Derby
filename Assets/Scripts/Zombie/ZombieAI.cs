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

    [Header("Elevation Fix")]
    [Tooltip("Adjust this until zombie feet rest perfectly on top of the ground plane.")]
    public float agentBaseOffset = 1.0f;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private Collider col;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // 1. Force NavMeshAgent base offset so model isn't half buried
        agent.baseOffset = agentBaseOffset;

        // 2. Let NavMeshAgent drive movement directly
        agent.updatePosition = true;
        agent.updateRotation = true;

        // 3. Make Rigidbody kinematic so zombies don't push or stop the car
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // 4. Set collider as trigger while alive
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

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.SetDestination(playerCar.position);
        }
    }

    public void KillZombie(Vector3 launchForce)
    {
        if (isDead) return;
        isDead = true;

        if (agent != null) agent.enabled = false;

        if (col != null) col.isTrigger = false;

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