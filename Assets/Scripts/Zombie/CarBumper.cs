using UnityEngine;
using UnityEngine.AI;

public class CarBumper : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the main Player Car Rigidbody here.")]
    public Rigidbody carRigidbody;

    [Header("Settings")]
    [Tooltip("Minimum speed (m/s) needed to obliterate the zombie.")]
    public float killSpeedThreshold = 5f;

    [Tooltip("How far the zombie gets launched upon impact.")]
    public float launchForceMultiplier = 2.5f;

    [Tooltip("Upward lift force to make the hit feel juicy.")]
    public float upwardForce = 4f;

    void Start()
    {
        // Auto-find car Rigidbody in parent if not assigned
        if (carRigidbody == null)
        {
            carRigidbody = GetComponentInParent<Rigidbody>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Get zombie script from collision or its parent root
        ZombieAI zombie = other.GetComponentInParent<ZombieAI>();

        if (zombie != null && !zombie.isDead)
        {
            // Calculate actual current speed of the car
            float currentSpeed = carRigidbody != null ? carRigidbody.linearVelocity.magnitude : 0f;

            if (currentSpeed >= killSpeedThreshold)
            {
                SmashZombie(zombie);
            }
            else
            {
                Debug.Log($"Car speed ({currentSpeed:F1}) too slow to crush zombie.");
            }
        }
    }

    private void SmashZombie(ZombieAI zombie)
    {
        // Mark as dead so it doesn't trigger multiple times
        zombie.isDead = true;

        // 1. INSTANTLY KILL NAVMESH AGENT (This disables the brick wall effect)
        NavMeshAgent agent = zombie.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;
        }

        // 2. TURN ON ZOMBIE RIGIDBODY PHYSICS
        Rigidbody zombieRb = zombie.GetComponent<Rigidbody>();
        if (zombieRb != null)
        {
            zombieRb.isKinematic = false;
            zombieRb.constraints = RigidbodyConstraints.None; // Allow tumbles/rolls

            // 3. LAUNCH ZOMBIE IN CAR'S MOVING DIRECTION
            Vector3 carVelocity = carRigidbody != null ? carRigidbody.linearVelocity : transform.forward * killSpeedThreshold;
            Vector3 launchDirection = (carVelocity * launchForceMultiplier) + (Vector3.up * upwardForce);

            zombieRb.AddForce(launchDirection, ForceMode.Impulse);

            // Optional torque to make the body spin satisfyingly
            zombieRb.AddTorque(Random.insideUnitSphere * 20f, ForceMode.Impulse);
        }

        // 4. CLEANUP
        Destroy(zombie.gameObject, 4f);
    }
}