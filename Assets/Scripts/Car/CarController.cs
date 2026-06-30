using UnityEngine;
using UnityEngine.InputSystem;

public class CarController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider frontLeftCollider;
    public WheelCollider frontRightCollider;
    public WheelCollider rearLeftCollider;
    public WheelCollider rearRightCollider;

    [Header("Wheel Meshes")]
    public Transform frontLeftMesh;
    public Transform frontRightMesh;
    public Transform rearLeftMesh;
    public Transform rearRightMesh;

    [Header("Car Settings")]
    public float motorForce = 1500f;
    public float brakeForce = 3000f;
    public float maxSteerAngle = 30f;
    public float handbrakeForce = 5000f;

    [Header("Speed Limit")]
    public float maxForwardSpeed = 35f;
    public float maxReverseSpeed = 12f;

    [Header("NOS Boost")]
    public float boostForce = 9000f;
    public float boostMaxSpeed = 55f;
    public ParticleSystem[] boostFlames;
    public AudioSource nosAudioSource;

    [Header("Desert Smoke / Dust Effect")]
    [Tooltip("Assign the smoke particle systems for all 4 wheels here.")]
    public ParticleSystem[] desertSmokeEffects;
    public float maxSmokeEmissionRate = 120f; // Maximum particles per second (per wheel) at top speed
    public float maxSmokeParticleSpeed = 4f;  // Particle start speed at top speed

    [Header("Steering Assist")]
    public float steerSmoothSpeed = 6f;
    public float minSteerAngleAtHighSpeed = 10f;
    public float steeringSpeedForMinAngle = 30f;

    [Header("Stability")]
    public float downForce = 80f;
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.5f, 0f);

    private float horizontalInput;
    private float verticalInput;
    private bool isHandbraking;
    private bool isBoosting;

    private float currentSteerAngle;
    private Rigidbody rb;

    // Cached particle system modules for performance
    private ParticleSystem.EmissionModule[] smokeEmissions;
    private ParticleSystem.MainModule[] smokeMains;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.centerOfMass += centerOfMassOffset;
        }

        // Initialize NOS flames
        foreach (ParticleSystem flame in boostFlames)
        {
            if (flame != null)
            {
                flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        if (nosAudioSource != null)
        {
            nosAudioSource.playOnAwake = false;
            nosAudioSource.loop = true;
        }

        // Initialize all wheel smoke modules
        if (desertSmokeEffects != null && desertSmokeEffects.Length > 0)
        {
            smokeEmissions = new ParticleSystem.EmissionModule[desertSmokeEffects.Length];
            smokeMains = new ParticleSystem.MainModule[desertSmokeEffects.Length];

            for (int i = 0; i < desertSmokeEffects.Length; i++)
            {
                if (desertSmokeEffects[i] != null)
                {
                    smokeEmissions[i] = desertSmokeEffects[i].emission;
                    smokeMains[i] = desertSmokeEffects[i].main;

                    // Force simulation space to World so smoke trails behind naturally
                    smokeMains[i].simulationSpace = ParticleSystemSimulationSpace.World;
                    smokeEmissions[i].rateOverTime = 0f;
                }
            }
        }
    }

    private void Update()
    {
        GetInput();
        UpdateBoostEffects();
        UpdateDesertSmoke();
        UpdateWheelMeshes();
    }

    private void FixedUpdate()
    {
        HandleMotor();
        HandleSteering();
        HandleBraking();
        HandleBoost();
        ApplyDownforce();
    }

    private void GetInput()
    {
        horizontalInput = 0f;
        verticalInput = 0f;

        // Keyboard steering
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed)
                horizontalInput -= 1f;

            if (Keyboard.current.dKey.isPressed)
                horizontalInput += 1f;

            if (Keyboard.current.wKey.isPressed)
                verticalInput += 1f;

            if (Keyboard.current.sKey.isPressed)
                verticalInput -= 1f;

            isHandbraking = Keyboard.current.spaceKey.isPressed;
            isBoosting = Keyboard.current.leftShiftKey.isPressed;
        }

        // Gamepad input
        if (Gamepad.current != null)
        {
            Vector2 leftStick = Gamepad.current.leftStick.ReadValue();

            float r2 = Gamepad.current.rightTrigger.ReadValue();
            float l2 = Gamepad.current.leftTrigger.ReadValue();

            horizontalInput += leftStick.x;
            verticalInput += r2 - l2;

            if (Gamepad.current.buttonSouth.isPressed)
            {
                isBoosting = true;
            }
        }

        horizontalInput = Mathf.Clamp(horizontalInput, -1f, 1f);
        verticalInput = Mathf.Clamp(verticalInput, -1f, 1f);
    }

    private void HandleMotor()
    {
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        bool overForwardSpeed = forwardSpeed >= maxForwardSpeed && verticalInput > 0f && !isBoosting;
        bool overReverseSpeed = forwardSpeed <= -maxReverseSpeed && verticalInput < 0f;

        if (overForwardSpeed || overReverseSpeed)
        {
            rearLeftCollider.motorTorque = 0f;
            rearRightCollider.motorTorque = 0f;
            return;
        }

        float torque = verticalInput * motorForce;

        rearLeftCollider.motorTorque = torque;
        rearRightCollider.motorTorque = torque;
    }

    private void HandleBoost()
    {
        if (!isBoosting)
            return;

        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        if (forwardSpeed >= boostMaxSpeed)
            return;

        rb.AddForce(transform.forward * boostForce, ForceMode.Force);
    }

    private void UpdateBoostEffects()
    {
        if (isBoosting)
        {
            foreach (ParticleSystem flame in boostFlames)
            {
                if (flame != null && !flame.isPlaying)
                {
                    flame.Play();
                }
            }

            if (nosAudioSource != null && !nosAudioSource.isPlaying)
            {
                nosAudioSource.Play();
            }
        }
        else
        {
            foreach (ParticleSystem flame in boostFlames)
            {
                if (flame != null && flame.isPlaying)
                {
                    flame.Stop();
                }
            }

            if (nosAudioSource != null && nosAudioSource.isPlaying)
            {
                nosAudioSource.Stop();
            }
        }
    }

    private void UpdateDesertSmoke()
    {
        if (desertSmokeEffects == null || desertSmokeEffects.Length == 0) return;

        // Calculate current speed magnitude
        float currentSpeed = rb.linearVelocity.magnitude;

        // Determine dynamic target limit based on whether the truck is boosting or driving normally
        float absoluteMaxSpeed = isBoosting ? boostMaxSpeed : maxForwardSpeed;

        // Establish relative performance ratio (0.0 to 1.0)
        float speedRatio = Mathf.Clamp01(currentSpeed / absoluteMaxSpeed);

        // Loop through all assigned smoke systems and apply calculations
        for (int i = 0; i < desertSmokeEffects.Length; i++)
        {
            if (desertSmokeEffects[i] == null) continue;

            smokeEmissions[i].rateOverTime = speedRatio * maxSmokeEmissionRate;
            smokeMains[i].startSpeed = Mathf.Lerp(1.0f, maxSmokeParticleSpeed, speedRatio);
        }
    }

    private void HandleSteering()
    {
        float speed = rb.linearVelocity.magnitude;

        float speedPercent = Mathf.Clamp01(speed / steeringSpeedForMinAngle);

        float adjustedMaxSteerAngle = Mathf.Lerp(
            maxSteerAngle,
            minSteerAngleAtHighSpeed,
            speedPercent
        );

        float targetSteerAngle = horizontalInput * adjustedMaxSteerAngle;

        currentSteerAngle = Mathf.Lerp(
            currentSteerAngle,
            targetSteerAngle,
            steerSmoothSpeed * Time.fixedDeltaTime
        );

        frontLeftCollider.steerAngle = currentSteerAngle;
        frontRightCollider.steerAngle = currentSteerAngle;
    }

    private void HandleBraking()
    {
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        bool pressingReverse = verticalInput < -0.1f;
        bool movingForward = forwardSpeed > 1f;

        float currentBrakeForce = 0f;

        if (pressingReverse && movingForward)
        {
            currentBrakeForce = brakeForce;

            rearLeftCollider.motorTorque = 0f;
            rearRightCollider.motorTorque = 0f;
        }

        frontLeftCollider.brakeTorque = currentBrakeForce;
        frontRightCollider.brakeTorque = currentBrakeForce;
        rearLeftCollider.brakeTorque = currentBrakeForce;
        rearRightCollider.brakeTorque = currentBrakeForce;

        if (isHandbraking)
        {
            rearLeftCollider.brakeTorque = handbrakeForce;
            rearRightCollider.brakeTorque = handbrakeForce;
        }
    }

    private void ApplyDownforce()
    {
        float speed = rb.linearVelocity.magnitude;

        rb.AddForce(-transform.up * downForce * speed);
    }

    private void UpdateWheelMeshes()
    {
        UpdateSingleWheel(frontLeftCollider, frontLeftMesh);
        UpdateSingleWheel(frontRightCollider, frontRightMesh);
        UpdateSingleWheel(rearLeftCollider, rearLeftMesh);
        UpdateSingleWheel(rearRightCollider, rearRightMesh);
    }

    private void UpdateSingleWheel(WheelCollider wheelCollider, Transform wheelMesh)
    {
        Vector3 position;
        Quaternion rotation;

        wheelCollider.GetWorldPose(out position, out rotation);

        wheelMesh.position = position;
        wheelMesh.rotation = rotation;
    }
}