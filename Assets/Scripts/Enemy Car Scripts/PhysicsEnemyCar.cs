using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PhysicsEnemyCar : MonoBehaviour
{
    [Header("Target")]
    public Transform playerTarget;

    [Header("Car Driving Physics")]
    public float motorForce = 1500f;
    public float maxSpeed = 15f;
    public float turnSpeed = 3f;
    public float stopDistance = 4f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // Ensure Rigidbody is non-kinematic
        rb.isKinematic = false;

        if (playerTarget == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTarget = player.transform;
        }
    }

    void FixedUpdate()
    {
        if (playerTarget == null) return;

        Vector3 targetDirection = playerTarget.position - transform.position;
        targetDirection.y = 0; // Keep driving horizontal

        float distanceToPlayer = targetDirection.magnitude;

        // 1. STEERING / TURNING (Rotate towards player using Physics)
        if (distanceToPlayer > 0.5f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);
        }

        // 2. FORWARD ACCELERATION & BRAKING
        if (distanceToPlayer > stopDistance)
        {
            // Only apply forward force if under max speed
            if (rb.linearVelocity.magnitude < maxSpeed)
            {
                rb.AddForce(transform.forward * motorForce, ForceMode.Force);
            }
        }
        else
        {
            // Apply gentle brake drag when close so it doesn't ram relentlessly
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * 3f);
        }
    }
}