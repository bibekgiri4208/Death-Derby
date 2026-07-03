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

    [Header("Audio Settings")]
    public AudioSource helicopterAudio;      // Drag your AudioSource here
    [Tooltip("Time in seconds to fade audio in/out")]
    public float audioFadeDuration = 0.5f;

    // Private state machine
    private enum State { FlyingToB, WaitingAtB, WaitingAtA }
    private State currentState;
    private float stateTimer;
    private float progress;
    private Vector3 previousPos;
    private Quaternion targetRotation;

    // Audio state
    private float audioTargetVolume = 0f;
    private float audioCurrentVolume = 0f;

    void Start()
    {
        transform.position = pointA;
        previousPos = pointA;
        targetRotation = Quaternion.LookRotation((pointB - pointA).normalized, Vector3.up);
        transform.rotation = targetRotation;
        currentState = State.FlyingToB;
        progress = 0f;

        // Audio setup
        if (helicopterAudio == null)
        {
            helicopterAudio = GetComponent<AudioSource>();
            if (helicopterAudio == null)
            {
                Debug.LogWarning("No AudioSource found! Add an AudioSource component.");
            }
        }

        if (helicopterAudio != null)
        {
            helicopterAudio.loop = true;
            helicopterAudio.volume = 0f;
            helicopterAudio.Play();
            audioCurrentVolume = 0f;
            audioTargetVolume = 1f; // Start with audio on since we're flying initially
        }
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
                    audioTargetVolume = 0f; // Fade out when reaching B
                }
                MoveAlongPath(progress, chaseNoseAngle);
                break;

            case State.WaitingAtB:
                stateTimer += Time.deltaTime;
                HoverInPlace(pointB, hoverNoseAngle);

                if (stateTimer >= waitTimeAtB)
                {
                    transform.position = pointA;
                    previousPos = pointA;
                    progress = 0f;
                    targetRotation = Quaternion.LookRotation((pointB - pointA).normalized, Vector3.up);
                    transform.rotation = targetRotation;
                    currentState = State.WaitingAtA;
                    stateTimer = 0f;
                    audioTargetVolume = 0f; // Keep silent while waiting at A
                }
                break;

            case State.WaitingAtA:
                stateTimer += Time.deltaTime;
                HoverInPlace(pointA, hoverNoseAngle);

                if (stateTimer >= waitTimeAtA)
                {
                    currentState = State.FlyingToB;
                    progress = 0f;
                    audioTargetVolume = 1f; // Fade in when starting chase
                }
                break;
        }

        // --- Handle Audio Fade ---
        if (helicopterAudio != null)
        {
            // Smoothly interpolate volume
            audioCurrentVolume = Mathf.MoveTowards(audioCurrentVolume, audioTargetVolume, Time.deltaTime / audioFadeDuration);
            helicopterAudio.volume = audioCurrentVolume;

            // Stop audio if completely silent (optional optimization)
            if (audioCurrentVolume <= 0.01f && helicopterAudio.isPlaying)
            {
                // Keep playing but at 0 volume - this prevents click/pop when restarting
                // You can also pause it, but pausing can cause issues with restarting
            }
        }
    }

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

            Quaternion lookRot = Quaternion.LookRotation(dir, Vector3.up);
            Quaternion attitudeRot = Quaternion.Euler(noseAngle, 0, -sideForce * maxBankAngle);
            targetRotation = lookRot * attitudeRot;
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothness);
        previousPos = targetPos;
    }

    void HoverInPlace(Vector3 center, float noseAngle)
    {
        float idleWeave = Mathf.Sin(Time.time * 0.8f + 1.2f) * 0.8f;
        float idleBob = Mathf.Sin(Time.time * 0.6f + 3.4f) * 0.4f;
        Vector3 sideDir = Vector3.Cross((pointB - pointA).normalized, Vector3.up).normalized;

        transform.position = center + sideDir * idleWeave + Vector3.up * idleBob;

        Vector3 forwardDir = (pointB - pointA).normalized;
        Quaternion lookRot = Quaternion.LookRotation(forwardDir, Vector3.up);
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