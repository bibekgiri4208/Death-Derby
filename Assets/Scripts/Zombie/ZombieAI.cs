using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class ZombieAI : MonoBehaviour
{
    [Header("Targeting")]
    public Transform playerCar;
    public string playerTag = "Player";

    [Header("Status")]
    public bool isDead = false;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private Collider zombieCollider;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        zombieCollider = GetComponent<Collider>();

        // 1. Keep solid physics enabled so zombie collides with car body
        if (zombieCollider != null)
        {
            zombieCollider.isTrigger = false;

            // Apply slick physics material so zombie slides smoothly off car paint
            PhysicsMaterial slickMat = new PhysicsMaterial("SlickZombie")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            zombieCollider.sharedMaterial = slickMat;
        }

        // 2. Kinematic while navigating so NavMesh drives movement
        rb.isKinematic = true;

        // 3. Prevent micro-stutter on fast physical contact
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // 4. Warp onto NavMesh if slightly off-grid
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }

        if (playerCar == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObj != null) playerCar = playerObj.transform;
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

    public void KillZombie(Vector3 impactVelocity, float upwardForce = 4f)
    {
        if (isDead) return;
        isDead = true;

        if (agent != null) agent.enabled = false;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.None;

            Vector3 launchForce = impactVelocity + (Vector3.up * upwardForce);
            rb.AddForce(launchForce, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 20f, ForceMode.Impulse);
        }

        Destroy(gameObject, 5f);
    }
}