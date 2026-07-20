using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PhysicsEnemyCar : MonoBehaviour
{
    [Header("Target")]
    public Transform playerTarget;

    [Header("Car Driving Physics")]
    public float acceleration = 25f;
    public float maxSpeed = 15f;
    public float turnSpeed = 5f;
    public float stopDistance = 4f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;

        // Helps prevent the car from catching edges/friction on the ground
        rb.interpolation = RigidbodyInterpolation.Interpolate;

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
        targetDirection.y = 0;

        float distanceToPlayer = targetDirection.magnitude;

        // 1. ROTATE TOWARDS PLAYER
        if (distanceToPlayer > 0.5f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);
        }

        // 2. DRIVE FORWARD USING VELOCITY CHANGE
        if (distanceToPlayer > stopDistance)
        {
            if (rb.linearVelocity.magnitude < maxSpeed)
            {
                // ForceMode.VelocityChange ensures snappy acceleration regardless of car mass
                rb.AddForce(transform.forward * acceleration, ForceMode.Acceleration);
            }
        }
        else
        {
            // Smoothly slow down near player
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * 4f);
        }
    }
}