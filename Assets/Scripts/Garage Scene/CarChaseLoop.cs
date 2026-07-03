using UnityEngine;

public class CarStraightChase : MonoBehaviour
{
    [Header("Highway Path (Straight Line)")]
    public Vector3 pointA = new Vector3(-40f, 0.2f, -10f);
    public Vector3 pointB = new Vector3(40f, 0.2f, -10f);

    [Header("Loop Timing (MUST MATCH HELICOPTER)")]
    public float travelDuration = 8f;
    public float waitTimeAtB = 5f;
    public float waitTimeAtA = 3f;

    [Header("Suspension Bounce (Optional)")]
    public float bounceAmplitude = 0.15f;
    public float bounceFrequency = 4f;

    // Private state
    private enum State { Driving, WaitingAtB, WaitingAtA }
    private State currentState;
    private float stateTimer;
    private float progress;
    private Vector3 forwardDir;

    void Start()
    {
        // Calculate the forward direction once (it never changes)
        forwardDir = (pointB - pointA).normalized;

        transform.position = pointA;
        transform.rotation = Quaternion.LookRotation(forwardDir, Vector3.up);
        currentState = State.Driving;
    }

    void Update()
    {
        switch (currentState)
        {
            case State.Driving:
                progress += Time.deltaTime / travelDuration;
                if (progress >= 1f)
                {
                    progress = 1f;
                    currentState = State.WaitingAtB;
                    stateTimer = 0f;
                }
                MoveStraight(progress);
                break;

            case State.WaitingAtB:
                stateTimer += Time.deltaTime;
                // Idle with tiny vibration
                transform.position = pointB + Vector3.up * (Mathf.Sin(Time.time * 3f) * 0.03f);
                transform.rotation = Quaternion.LookRotation(forwardDir, Vector3.up);

                if (stateTimer >= waitTimeAtB)
                {
                    // Instantly reset to A
                    transform.position = pointA;
                    progress = 0f;
                    currentState = State.WaitingAtA;
                    stateTimer = 0f;
                }
                break;

            case State.WaitingAtA:
                stateTimer += Time.deltaTime;
                transform.position = pointA + Vector3.up * (Mathf.Sin(Time.time * 3f + 1f) * 0.03f);
                transform.rotation = Quaternion.LookRotation(forwardDir, Vector3.up);

                if (stateTimer >= waitTimeAtA)
                {
                    currentState = State.Driving;
                    progress = 0f;
                }
                break;
        }
    }

    void MoveStraight(float t)
    {
        // PERFECT STRAIGHT LINE: Only moves between A and B on X and Z
        Vector3 targetPos = Vector3.Lerp(pointA, pointB, t);

        // Add suspension bounce (only affects Y, no sideways movement)
        float bounce = Mathf.Sin(t * Mathf.PI * 2 * bounceFrequency + 1.2f) * bounceAmplitude;
        targetPos.y += bounce;

        transform.position = targetPos;

        // LOCK ROTATION: Always faces exactly forward, never turns
        transform.rotation = Quaternion.LookRotation(forwardDir, Vector3.up);
    }
}