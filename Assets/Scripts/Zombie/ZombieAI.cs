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

    private NavMeshAgent agent;
    private Rigidbody rb;
    private Collider col;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // Set NavMeshAgent offset for Unity primitive capsule centered at (0,0,0)
        agent.baseOffset = 1.0f;
        agent.height = 2.0f;
        agent.radius = 0.5f;

        agent.updatePosition = true;
        agent.updateRotation = true;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

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