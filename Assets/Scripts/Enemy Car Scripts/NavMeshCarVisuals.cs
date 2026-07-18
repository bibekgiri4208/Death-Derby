using UnityEngine;
using UnityEngine.AI;

public class NavMeshCarVisuals : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform VisualModel;

    [Header("Body Roll (Tilting)")]
    public float tiltAmount = 5f;
    public float tiltSpeed = 8f;

    [Header("Drift (Visual Slide)")]
    public float maxDriftAngle = 25f;
    public float driftSmoothTime = 0.1f;
    public float driftThresholdSpeed = 2f;

    private float currentDriftAngle;
    private float driftVelocityVelocity; // Used internally by Mathf.SmoothDamp

    void Start()
    {
        if (agent == null) agent = GetComponentInParent<NavMeshAgent>();
        if (VisualModel == null) VisualModel = this.transform;
    }

    void Update()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        // Get the local velocity of the agent (how fast it moves forward vs sideways)
        Vector3 localVelocity = agent.transform.InverseTransformDirection(agent.velocity);
        float forwardSpeed = localVelocity.z;
        float lateralSpeed = localVelocity.x;

        // Only apply effects if the car is actually moving
        if (agent.velocity.magnitude > driftThresholdSpeed)
        {
            HandleBodyRoll(lateralSpeed);
            HandleDrifting(forwardSpeed, lateralSpeed);
        }
        else
        {
            // Smoothly reset rotations when stopped
            VisualModel.localRotation = Quaternion.Slerp(VisualModel.localRotation, Quaternion.identity, Time.deltaTime * tiltSpeed);
        }
    }

    void HandleBodyRoll(float lateralSpeed)
    {
        // Centrifugal force pushes the car body outward during a turn
        // If turning right (lateralSpeed is positive), the car rolls left (negative Z rotation)
        float targetTilt = -lateralSpeed * tiltAmount;

        // Clamp it so it doesn't flip over
        targetTilt = Mathf.Clamp(targetTilt, -tiltAmount, tiltAmount);

        // Apply just the tilt to the model's local Z axis
        Quaternion targetRotation = Quaternion.Euler(VisualModel.localEulerAngles.x, VisualModel.localEulerAngles.y, targetTilt);
        VisualModel.localRotation = Quaternion.Slerp(VisualModel.localRotation, targetRotation, Time.deltaTime * tiltSpeed);
    }

    void HandleDrifting(float forwardSpeed, float lateralSpeed)
    {
        // Calculate the angle between where the car is facing and where it is actually moving
        float targetDriftAngle = Mathf.Atan2(lateralSpeed, forwardSpeed) * Mathf.Rad2Deg;

        // Amplify the angle slightly for arcade-style visual dramatic effect
        targetDriftAngle *= 1.5f;
        targetDriftAngle = Mathf.Clamp(targetDriftAngle, -maxDriftAngle, maxDriftAngle);

        // Smoothly damp the angle change so it doesn't snap instantly
        currentDriftAngle = Mathf.SmoothDampAngle(currentDriftAngle, targetDriftAngle, ref driftVelocityVelocity, driftSmoothTime);

        // Rotate the visual model on the Y-axis relative to the NavMesh Agent parent
        VisualModel.localRotation = Quaternion.Euler(VisualModel.localEulerAngles.x, currentDriftAngle, VisualModel.localEulerAngles.z);
    }
}