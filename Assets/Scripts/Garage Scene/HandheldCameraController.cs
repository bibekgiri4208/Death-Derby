using UnityEngine;

public class CameramanWalk : MonoBehaviour
{
    [Header("Target (Cars Selection Area)")]
    public Transform target;

    [Header("Walk Parameters")]
    [Tooltip("How far from the target the camera orbits (auto-captured from your Scene View position)")]
    public float orbitRadius = 4f;
    public float orbitSpeed = 0.3f;
    public float height = 1.6f;
    public float minRadius = 3f;
    public float maxRadius = 5f;

    [Header("Walk Randomness")]
    public float directionChangeInterval = 5f;
    public float radiusChangeInterval = 4f;
    public float speedVariation = 0.15f;

    [Header("Handheld Shake & Sway")]
    public float swayAmplitude = 0.12f;
    public float swaySpeed = 0.8f;
    public float shakeAmplitude = 0.4f;
    public float shakeSpeed = 1.2f;
    public float breatheAmplitude = 0.05f;
    public float breatheSpeed = 0.6f;

    // Private state
    private float currentAngle;
    private float currentRadius;
    private float currentSpeed;
    private float directionSign = 1f;
    private float timerDirection;
    private float timerRadius;
    private float noiseOffsetX, noiseOffsetY, noiseOffsetZ;
    private float noiseOffsetPitch, noiseOffsetYaw, noiseOffsetRoll;
    private float noiseOffsetBreathe;

    // CRITICAL: Store the initial camera position
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    void Start()
    {
        if (target == null)
        {
            Debug.LogError("CameramanWalk: No target assigned!");
            return;
        }

        // Randomise noise seeds
        noiseOffsetX = Random.Range(0f, 100f);
        noiseOffsetY = Random.Range(0f, 100f);
        noiseOffsetZ = Random.Range(0f, 100f);
        noiseOffsetPitch = Random.Range(0f, 100f);
        noiseOffsetYaw = Random.Range(0f, 100f);
        noiseOffsetRoll = Random.Range(0f, 100f);
        noiseOffsetBreathe = Random.Range(0f, 100f);

        // --- CRITICAL FIX: Use the EXACT Scene View position ---
        initialPosition = transform.position;
        initialRotation = transform.rotation;

        // Calculate the angle from target to this initial position
        Vector3 dirFromTarget = initialPosition - target.position;
        currentAngle = Mathf.Atan2(dirFromTarget.z, dirFromTarget.x) * Mathf.Rad2Deg;
        currentRadius = new Vector3(dirFromTarget.x, 0, dirFromTarget.z).magnitude;
        height = initialPosition.y;

        // Store the initial radius as the base
        orbitRadius = currentRadius;
        minRadius = currentRadius * 0.7f;
        maxRadius = currentRadius * 1.3f;

        currentSpeed = orbitSpeed;
        timerDirection = directionChangeInterval;
        timerRadius = radiusChangeInterval;

        Debug.Log($"Camera position captured: {initialPosition}, angle: {currentAngle}°, radius: {currentRadius}");
    }

    void LateUpdate()
    {
        if (target == null) return;

        float dt = Time.deltaTime;

        // --- Walk logic: change direction and radius randomly ---
        timerDirection -= dt;
        if (timerDirection <= 0f)
        {
            if (Random.value < 0.6f)
                directionSign *= -1f;
            timerDirection = directionChangeInterval * Random.Range(0.7f, 1.3f);
            currentSpeed = orbitSpeed + Random.Range(-speedVariation, speedVariation);
            currentSpeed = Mathf.Max(0.1f, currentSpeed);
        }

        timerRadius -= dt;
        if (timerRadius <= 0f)
        {
            currentRadius = Random.Range(minRadius, maxRadius);
            timerRadius = radiusChangeInterval * Random.Range(0.7f, 1.3f);
        }

        // --- Update angle (orbit) ---
        currentAngle += currentSpeed * directionSign * dt;
        if (currentAngle > 360f) currentAngle -= 360f;
        if (currentAngle < 0f) currentAngle += 360f;

        // --- Calculate base orbit position (relative to target) ---
        float rad = currentAngle * Mathf.Deg2Rad;
        Vector3 basePos = target.position + new Vector3(Mathf.Cos(rad) * currentRadius, height, Mathf.Sin(rad) * currentRadius);

        // --- Handheld sway (positional) ---
        float time = Time.time;
        float swayX = (Mathf.PerlinNoise(noiseOffsetX + time * swaySpeed, 0f) - 0.5f) * 2f * swayAmplitude;
        float swayY = (Mathf.PerlinNoise(0f, noiseOffsetY + time * swaySpeed) - 0.5f) * 2f * swayAmplitude;
        float swayZ = (Mathf.PerlinNoise(noiseOffsetZ + time * swaySpeed * 0.7f, noiseOffsetZ + time * swaySpeed * 0.7f + 10f) - 0.5f) * 2f * swayAmplitude;
        Vector3 swayOffset = new Vector3(swayX, swayY, swayZ);
        Vector3 targetPos = basePos + swayOffset;

        // --- Breathing (forward/back) ---
        float breathe = Mathf.PerlinNoise(noiseOffsetBreathe + time * breatheSpeed, 0f) - 0.5f;
        Vector3 forward = transform.forward;
        targetPos += forward * breathe * breatheAmplitude;

        // --- Apply position ---
        transform.position = targetPos;

        // --- Always look at the target with shake ---
        Vector3 lookDir = target.position - transform.position;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(lookDir, Vector3.up);
            float shakePitch = (Mathf.PerlinNoise(noiseOffsetPitch + time * shakeSpeed, 0f) - 0.5f) * 2f * shakeAmplitude;
            float shakeYaw = (Mathf.PerlinNoise(0f, noiseOffsetYaw + time * shakeSpeed) - 0.5f) * 2f * shakeAmplitude;
            float shakeRoll = (Mathf.PerlinNoise(noiseOffsetRoll + time * shakeSpeed * 0.9f, noiseOffsetRoll + time * shakeSpeed * 1.1f) - 0.5f) * 2f * shakeAmplitude;
            Quaternion shakeRot = Quaternion.Euler(shakePitch, shakeYaw, shakeRoll);
            transform.rotation = lookRot * shakeRot;
        }
    }
}