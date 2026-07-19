using UnityEngine;
using UnityEngine.AI;

public class EnemyWheelAnimator : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private NavMeshAgent agent;

    [Header("Front Wheels (Steer & Spin)")]
    [SerializeField] private Transform frontLeftWheel;
    [SerializeField] private Transform frontRightWheel;

    [Header("Rear Wheels (Spin Only)")]
    [SerializeField] private Transform rearLeftWheel;
    [SerializeField] private Transform rearRightWheel;

    [Header("Visual Settings")]
    [Tooltip("Match this roughly to the physical radius of your car's wheel model.")]
    [SerializeField] private float wheelRadius = 0.4f;
    [Tooltip("Maximum visual steering angle for the front wheels.")]
    [SerializeField] private float maxSteerAngle = 35f;
    [Tooltip("How smoothly the front wheels snap back or turn.")]
    [SerializeField] private float steerSmoothing = 10f;

    private float currentSpinRotation = 0f;
    private float currentSteerAngle = 0f;

    void Start()
    {
        // Auto-grab agent if not assigned
        if (agent == null)
        {
            agent = GetComponentInParent<NavMeshAgent>();
        }
    }

    void Update()
    {
        if (agent == null) return;

        // 1. CALCULATE WHEEL SPIN (Based on actual forward speed)
        // Formula: Speed / Radius gives radians per second. Convert to degrees.
        float forwardSpeed = Vector3.Dot(agent.velocity, transform.forward);
        float rotationDegreePerSecond = (forwardSpeed / wheelRadius) * Mathf.Rad2Deg;
        currentSpinRotation += rotationDegreePerSecond * Time.deltaTime;

        // Keep the rotation float clean
        currentSpinRotation %= 360f;

        // 2. CALCULATE WHEEL STEERING (Based on path cornering)
        float targetSteerAngle = 0f;

        if (agent.velocity.magnitude > 0.1f)
        {
            // Find the angle difference between where the car is facing vs where the agent wants to go
            Vector3 localVelocity = transform.InverseTransformDirection(agent.velocity);
            float angleToTarget = Mathf.Atan2(localVelocity.x, localVelocity.z) * Mathf.Rad2Deg;

            // Clamp it so the wheels don't turn unrealistically sideways
            targetSteerAngle = Mathf.Clamp(angleToTarget, -maxSteerAngle, maxSteerAngle);
        }

        // Smoothly interpolate the steering so it doesn't jitter
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetSteerAngle, Time.deltaTime * steerSmoothing);

        // 3. APPLY ROTATIONS TO WHEEL TRANSFORMS
        // Apply both steering (Y-axis) and spinning (X-axis) to front wheels
        if (frontLeftWheel != null)
            frontLeftWheel.localRotation = Quaternion.Euler(currentSpinRotation, currentSteerAngle, 0f);

        if (frontRightWheel != null)
            frontRightWheel.localRotation = Quaternion.Euler(currentSpinRotation, currentSteerAngle, 0f);

        // Apply only spinning (X-axis) to rear wheels
        if (rearLeftWheel != null)
            rearLeftWheel.localRotation = Quaternion.Euler(currentSpinRotation, 0f, 0f);

        if (rearRightWheel != null)
            rearRightWheel.localRotation = Quaternion.Euler(currentSpinRotation, 0f, 0f);
    }
}