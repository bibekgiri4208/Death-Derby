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

    [Header("Corner & Wall Avoidance")]
    [Tooltip("How far ahead the zombie checks for wall corners to avoid clipping edges.")]
    public float wallCheckDistance = 0.8f;
    public LayerMask wallLayerMask = ~0; // Default to all layers

    [Header("Ramming Physics")]
    public float killSpeedThreshold = 6f;
    public bool isDead = false;

    private NavMeshAgent agent;
    private Rigidbody rb;

    // Anti-Stuck Tracking
    private Vector3 lastPosition;
    private float stuckTimer = 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();

        agent.updatePosition = false;
        agent.updateRotation = false;

        rb.isKinematic = false;
        rb.mass = 70f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Ensure low friction programmatically to prevent wall sticking
        Collider col = GetComponent<Collider>();
        if (col != null && col.sharedMaterial == null)
        {
            PhysicsMaterial smoothMat = new PhysicsMaterial("ZeroFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
            col.sharedMaterial = smoothMat;
        }

        if (playerCar == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObj != null) playerCar = playerObj.transform;
        }

        lastPosition = transform.position;
    }

    void Update()
    {
        if (isDead || playerCar == null) return;

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.SetDestination(playerCar.position);
            agent.nextPosition = transform.position;
        }
    }

    void FixedUpdate()
    {
        if (isDead || playerCar == null) return;

        if (agent.enabled && agent.hasPath)
        {
            Vector3 desiredDir = (agent.steeringTarget - transform.position);
            desiredDir.y = 0;
            desiredDir = desiredDir.normalized;

            if (desiredDir != Vector3.zero)
            {
                // 1. Check for wall collision ahead (Corner Nudge)
                desiredDir = CalculateWallAvoidance(desiredDir);

                // 2. Rotate toward calculated direction
                Quaternion targetRotation = Quaternion.LookRotation(desiredDir);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * 10f));

                // 3. Move physics body
                Vector3 moveVelocity = desiredDir * moveSpeed;
                moveVelocity.y = rb.linearVelocity.y; // Preserve gravity
                rb.linearVelocity = moveVelocity;
            }

            // 4. Anti-Stuck Watchdog
            CheckIfStuck();
        }
    }

    /// <summary>
    /// Casts rays to detect wall corners ahead and nudges movement direction away from the corner.
    /// </summary>
    private Vector3 CalculateWallAvoidance(Vector3 currentDir)
    {
        RaycastHit hit;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;

        // Cast ray directly in front
        if (Physics.Raycast(rayOrigin, currentDir, out hit, wallCheckDistance, wallLayerMask))
        {
            // If we hit a static wall object (not the player or another zombie)
            if (!hit.collider.CompareTag(playerTag) && hit.collider.gameObject != gameObject)
            {
                // Nudge direction along the wall normal to round the corner smoothly
                Vector3 nudgeDirection = Vector3.ProjectOnPlane(currentDir, hit.normal).normalized;
                if (nudgeDirection != Vector3.zero)
                {
                    return nudgeDirection;
                }
            }
        }
        return currentDir;
    }

    /// <summary>
    /// Detects if the zombie is stuck jittering on a corner and gives it a quick slip boost.
    /// </summary>
    private void CheckIfStuck()
    {
        float movedDistance = Vector3.Distance(transform.position, lastPosition);

        // If trying to move but barely progressing
        if (movedDistance < 0.05f)
        {
            stuckTimer += Time.fixedDeltaTime;

            if (stuckTimer > 0.4f) // Stuck for more than 0.4s
            {
                // Give a small push toward the actual NavMesh target to un-wedge it
                Vector3 unstickDir = (agent.steeringTarget - transform.position).normalized;
                rb.AddForce(unstickDir * moveSpeed * 2f, ForceMode.VelocityChange);
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        lastPosition = transform.position;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDead) return;

        if (collision.gameObject.CompareTag(playerTag) || collision.transform.root.CompareTag(playerTag))
        {
            float impactSpeed = collision.relativeVelocity.magnitude;

            if (impactSpeed >= killSpeedThreshold)
            {
                Die(collision);
            }
            else
            {
                TemporarilyStun();
            }
        }
    }

    private void TemporarilyStun()
    {
        if (agent.enabled)
        {
            agent.enabled = false;
            Invoke(nameof(ReEnableAgent), 1.5f);
        }
    }

    private void ReEnableAgent()
    {
        if (!isDead && agent != null)
        {
            agent.enabled = true;
        }
    }

    private void Die(Collision collision)
    {
        CancelInvoke(nameof(ReEnableAgent));
        isDead = true;

        agent.enabled = false;
        rb.constraints = RigidbodyConstraints.None;

        Vector3 impactForce = collision.relativeVelocity * 1.5f + Vector3.up * 3f;
        rb.AddForce(impactForce, ForceMode.Impulse);

        Debug.Log("ZOMBIE CRUSHED!");
        Destroy(gameObject, 5f);
    }
}