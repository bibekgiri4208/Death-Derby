using UnityEngine;

public class HelicopterChaseLoop : MonoBehaviour
{
    [Header("Chase Path (Sky Positions)")]
    public Vector3 pointA = new Vector3(-40f, 18f, -30f);
    public Vector3 pointB = new Vector3(40f, 18f, -30f);

    [Header("Loop Timing")]
    public float travelDuration = 8f;
    public float waitTimeAtB = 5f;
    public float waitTimeAtA = 3f;

    [Header("Realistic Weaving & Bobbing")]
    public float weaveWidth = 4f;
    public float weaveFrequency = 1.2f;
    public float bobAmplitude = 2f;
    public float bobFrequency = 0.8f;

    [Header("Nose Attitude (Pitch)")]
    [Tooltip("Negative = nose down. How much it tilts forward while chasing.")]
    public float chaseNoseAngle = -12f;
    [Tooltip("Negative = nose down. Gentler tilt while waiting/hovering.")]
    public float hoverNoseAngle = -3f;

    [Header("Visual Banking (Tilting)")]
    public float maxBankAngle = 15f;
    public float rotationSmoothness = 6f;

    // Private state machine
    private enum State { FlyingToB, WaitingAtB, WaitingAtA }
    private State currentState;
    private float stateTimer;
    private float progress;
    private Vector3 previousPos;
    private Quaternion targetRotation;

    void Start()
    {
        transform.position = pointA;
        previousPos = pointA;
        targetRotation = Quaternion.LookRotation((pointB - pointA).normalized, Vector3.up);
        transform.rotation = targetRotation;
        currentState = State.FlyingToB;
        progress = 0f;
    }

    void Update()
    {
        switch (currentState)
        {
            case State.FlyingToB:
                progress += Time.deltaTime / travelDuration;
                if (progress >= 1f)
                {
                    progress = 1f;
                    currentState = State.WaitingAtB;
                    stateTimer = 0f;
                }
                MoveAlongPath(progress, chaseNoseAngle); // <-- Passing the chase tilt
                break;

            case State.WaitingAtB:
                stateTimer += Time.deltaTime;
                HoverInPlace(pointB, hoverNoseAngle); // <-- Passing the hover tilt

                if (stateTimer >= waitTimeAtB)
                {
                    transform.position = pointA;
                    previousPos = pointA;
                    progress = 0f;
                    targetRotation = Quaternion.LookRotation((pointB - pointA).normalized, Vector3.up);
                    transform.rotation = targetRotation;
                    currentState = State.WaitingAtA;
                    stateTimer = 0f;
                }
                break;

            case State.WaitingAtA:
                stateTimer += Time.deltaTime;
                HoverInPlace(pointA, hoverNoseAngle);

                if (stateTimer >= waitTimeAtA)
                {
                    currentState = State.FlyingToB;
                    progress = 0f;
                }
                break;
        }
    }

    // --- UPDATED: Now accepts a noseAngle parameter ---
    void MoveAlongPath(float t, float noseAngle)
    {
        Vector3 basePos = Vector3.Lerp(pointA, pointB, t);
        Vector3 offset = GetPathOffset(t);
        Vector3 targetPos = basePos + offset;

        transform.position = targetPos;

        Vector3 dir = (targetPos - previousPos).normalized;
        if (dir.sqrMagnitude > 0.001f)
        {
            Vector3 localDir = transform.InverseTransformDirection(dir);
            float sideForce = Mathf.Clamp(localDir.x, -1f, 1f);

            // 1. Face the direction of movement
            Quaternion lookRot = Quaternion.LookRotation(dir, Vector3.up);

            // 2. Combine the Nose Tilt (pitch) and Banking (roll) into one attitude
            //    Negative X = nose down, Z is the roll for banking.
            Quaternion attitudeRot = Quaternion.Euler(noseAngle, 0, -sideForce * maxBankAngle);

            targetRotation = lookRot * attitudeRot;
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothness);
        previousPos = targetPos;
    }

    // --- UPDATED: Now accepts a noseAngle parameter ---
    void HoverInPlace(Vector3 center, float noseAngle)
    {
        // Subtle idle drift while waiting
        float idleWeave = Mathf.Sin(Time.time * 0.8f + 1.2f) * 0.8f;
        float idleBob = Mathf.Sin(Time.time * 0.6f + 3.4f) * 0.4f;
        Vector3 sideDir = Vector3.Cross((pointB - pointA).normalized, Vector3.up).normalized;

        transform.position = center + sideDir * idleWeave + Vector3.up * idleBob;

        // Keep facing forward, but apply a gentle nose-down for hovering
        Vector3 forwardDir = (pointB - pointA).normalized;
        Quaternion lookRot = Quaternion.LookRotation(forwardDir, Vector3.up);

        // Apply the hover-specific nose tilt (no banking while idle)
        Quaternion attitudeRot = Quaternion.Euler(noseAngle, 0, 0);
        targetRotation = lookRot * attitudeRot;

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothness);
    }

    Vector3 GetPathOffset(float t)
    {
        Vector3 dir = (pointB - pointA).normalized;
        Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;

        float weave = Mathf.Sin(t * Mathf.PI * 2 * weaveFrequency) * weaveWidth;
        float bob = Mathf.Sin(t * Mathf.PI * 2 * bobFrequency + 0.7f) * bobAmplitude;

        return side * weave + Vector3.up * bob;
    }
}