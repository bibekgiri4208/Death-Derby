using UnityEngine;

public class CarBumper : MonoBehaviour
{
    [Header("References")]
    public Rigidbody carRigidbody;

    [Header("Speed Settings")]
    [Tooltip("Minimum vehicle speed in meters/second needed to crush the zombie.")]
    public float killSpeedThreshold = 5f; // Set your default speed threshold here

    [Header("Impact FX")]
    public float launchForceMultiplier = 3f;
    public float upwardForce = 4f;

    void Start()
    {
        if (carRigidbody == null)
        {
            carRigidbody = GetComponentInParent<Rigidbody>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        ZombieAI zombie = other.GetComponentInParent<ZombieAI>();

        if (zombie != null && !zombie.isDead)
        {
            // Get the current speed magnitude from the car's Rigidbody
            float currentSpeed = carRigidbody != null ? carRigidbody.linearVelocity.magnitude : 0f;

            // Check if speed meets or exceeds your threshold
            if (currentSpeed >= killSpeedThreshold)
            {
                // Calculate hit force based on speed and launch the zombie
                Vector3 launchDirection = (carRigidbody.linearVelocity * launchForceMultiplier) + (Vector3.up * upwardForce);
                zombie.KillZombie(launchDirection);
            }
            else
            {
                Debug.Log($"Car speed ({currentSpeed:F1} m/s) is below threshold ({killSpeedThreshold:F1} m/s). Zombie survived!");
            }
        }
    }
}