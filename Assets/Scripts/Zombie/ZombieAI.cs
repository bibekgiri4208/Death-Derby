using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class ZombieAI : MonoBehaviour
{
    [Header("Target & Movement")]
    public Transform playerCar;
    public string playerTag = "Player";
    public float moveSpeed = 4f;

    [Header("Ramming Physics")]
    public float killSpeedThreshold = 6f; // Speed needed to crush
    public bool isDead = false;

    private NavMeshAgent agent;
    private Rigidbody rb;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();

        // IMPORTANT: Unlink NavMeshAgent from directly moving the transform
        agent.updatePosition = false;
        agent.updateRotation = false;

        // Make sure Rigidbody is physics-enabled
        rb.isKinematic = false;
        rb.mass = 70f; // Realistic human weight so car pushes through easily
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        if (playerCar == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObj != null) playerCar = playerObj.transform;
        }
    }

    void Update()
    {
        if (isDead || playerCar == null) return;

        // Update NavMesh destination
        if (agent.isOnNavMesh)
        {
            agent.SetDestination(playerCar.position);

            // Sync NavMeshAgent position with the actual Rigidbody physics position
            agent.nextPosition = transform.position;
        }
    }

    void FixedUpdate()
    {
        if (isDead || playerCar == null) return;

        // Move zombie using physics toward the NavMesh target direction
        if (agent.hasPath)
        {
            Vector3 direction = (agent.steeringTarget - transform.position).normalized;
            direction.y = 0; // Keep movement horizontal

            if (direction != Vector3.zero)
            {
                // Rotate toward movement direction
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * 10f));

                // Move physics body forward
                Vector3 moveVelocity = direction * moveSpeed;
                moveVelocity.y = rb.linearVelocity.y; // Keep gravity working
                rb.linearVelocity = moveVelocity;
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDead) return;

        if (collision.gameObject.CompareTag(playerTag) || collision.transform.root.CompareTag(playerTag))
        {
            // Get car speed
            float impactSpeed = collision.relativeVelocity.magnitude;

            if (impactSpeed >= killSpeedThreshold)
            {
                Die(collision);
            }
        }
    }

    private void Die(Collision collision)
    {
        isDead = true;

        // Turn off AI steering completely
        agent.enabled = false;

        // Unfreeze rotation so body rolls/tumbles dynamically
        rb.constraints = RigidbodyConstraints.None;

        // Launch body with car momentum + upward pop
        Vector3 impactForce = collision.relativeVelocity * 1.5f + Vector3.up * 3f;
        rb.AddForce(impactForce, ForceMode.Impulse);

        Debug.Log("ZOMBIE CRUSHED BY CAR!");
        Destroy(gameObject, 5f);
    }
}