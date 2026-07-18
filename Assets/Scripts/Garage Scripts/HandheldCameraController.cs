using UnityEngine;

public class HandheldCamera : MonoBehaviour
{
    [Header("Wandering (Cameraman Stepping Around)")]
    public float wanderRadius = 0.5f;      // How far the camera drifts from its start position
    public float wanderSpeed = 0.3f;       // How fast it wanders

    [Header("Handheld Sway (Subtle Movement)")]
    public float swayAmplitude = 0.08f;    // Tiny hand movement
    public float swaySpeed = 0.8f;

    [Header("Handheld Shake (Micro Tremors)")]
    public float shakeAmplitude = 0.3f;    // Degrees of rotation shake
    public float shakeSpeed = 1.2f;

    // Private
    private Vector3 startPosition;
    private Quaternion startRotation;
    private float timeOffsetX, timeOffsetY, timeOffsetZ;
    private float timeOffsetPitch, timeOffsetYaw, timeOffsetRoll;

    void Start()
    {
        // SAVE your exact Scene View position
        startPosition = transform.position;
        startRotation = transform.rotation;

        // Random seeds for natural movement
        timeOffsetX = Random.Range(0f, 100f);
        timeOffsetY = Random.Range(0f, 100f);
        timeOffsetZ = Random.Range(0f, 100f);
        timeOffsetPitch = Random.Range(0f, 100f);
        timeOffsetYaw = Random.Range(0f, 100f);
        timeOffsetRoll = Random.Range(0f, 100f);

        Debug.Log($"Camera locked at: {startPosition}");
    }

    void LateUpdate()
    {
        float time = Time.time;

        // --- 1. Wander: Slow drift from the start position ---
        float wanderX = (Mathf.PerlinNoise(timeOffsetX + time * wanderSpeed, 0f) - 0.5f) * 2f * wanderRadius;
        float wanderY = (Mathf.PerlinNoise(0f, timeOffsetY + time * wanderSpeed * 0.7f) - 0.5f) * 2f * wanderRadius * 0.3f; // Less vertical
        float wanderZ = (Mathf.PerlinNoise(timeOffsetZ + time * wanderSpeed * 0.9f, timeOffsetZ + time * wanderSpeed * 0.9f + 10f) - 0.5f) * 2f * wanderRadius;

        Vector3 wanderOffset = new Vector3(wanderX, wanderY, wanderZ);

        // --- 2. Handheld Sway (micro movements) ---
        float swayX = (Mathf.PerlinNoise(timeOffsetX + 100f + time * swaySpeed, 0f) - 0.5f) * 2f * swayAmplitude;
        float swayY = (Mathf.PerlinNoise(0f, timeOffsetY + 100f + time * swaySpeed) - 0.5f) * 2f * swayAmplitude;
        float swayZ = (Mathf.PerlinNoise(timeOffsetZ + 100f + time * swaySpeed * 0.7f, timeOffsetZ + 100f + time * swaySpeed * 0.7f + 10f) - 0.5f) * 2f * swayAmplitude;

        Vector3 swayOffset = new Vector3(swayX, swayY, swayZ);

        // --- Apply position (start + wander + sway) ---
        transform.position = startPosition + wanderOffset + swayOffset;

        // --- 3. Handheld Shake (rotation tremors) ---
        float shakePitch = (Mathf.PerlinNoise(timeOffsetPitch + time * shakeSpeed, 0f) - 0.5f) * 2f * shakeAmplitude;
        float shakeYaw = (Mathf.PerlinNoise(0f, timeOffsetYaw + time * shakeSpeed * 1.1f) - 0.5f) * 2f * shakeAmplitude * 0.5f;
        float shakeRoll = (Mathf.PerlinNoise(timeOffsetRoll + time * shakeSpeed * 0.8f, timeOffsetRoll + time * shakeSpeed * 1.2f) - 0.5f) * 2f * shakeAmplitude * 0.3f;

        Quaternion shakeRotation = Quaternion.Euler(shakePitch, shakeYaw, shakeRoll);

        // Apply rotation (start rotation + shake)
        transform.rotation = startRotation * shakeRotation;
    }
}