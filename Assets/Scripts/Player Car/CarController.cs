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

    [Header("NOS Boost Settings")]
    public float boostForce = 9000f;
    public float boostMaxSpeed = 55f;

    [Header("Steering Assist")]
    public float steerSmoothSpeed = 6f;
    public float minSteerAngleAtHighSpeed = 10f;
    public float steeringSpeedForMinAngle = 30f;

    [Header("Stability")]
    public float downForce = 80f;
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.5f, 0f);

    [Header("Burnout Settings")]
    [Tooltip("Brake force applied to the front wheels so the car stays put during a burnout.")]
    public float burnoutKeepStillBrakeForce = 5000f;
    [Tooltip("Multiplier on motor torque pushed to the rear wheels during a burnout.")]
    public float burnoutMotorMultiplier = 1.5f;
    [Tooltip("Rear wheel forward friction during burnout (low = wheels spin freely).")]
    [Range(0f, 1f)]
    public float burnoutForwardFriction = 0.05f;
    [Tooltip("Rear wheel sideways friction during burnout.")]
    [Range(0f, 1f)]
    public float burnoutSidewaysFriction = 0.2f;
    [Tooltip("How fast the car spins in place while turning during a burnout (radians per second).")]
    public float burnoutTurnSpeed = 2.5f;

    [Header("Drift Settings")]
    [Tooltip("Sideways friction stiffness on rear wheels during drift (lower = more slide).")]
    [Range(0.1f, 1f)]
    public float driftSidewaysFriction = 0.3f;
    [Tooltip("Forward friction stiffness on rear wheels during drift (lower = more wheel spin).")]
    [Range(0.1f, 1f)]
    public float driftForwardFriction = 0.6f;
    [Tooltip("How much steer angle is multiplied during drift.")]
    public float driftSteerMultiplier = 1.3f;
    [Tooltip("Motor torque multiplier during drift (reduces grip-up).")]
    public float driftMotorMultiplier = 0.7f;
    [Tooltip("Minimum forward speed to enter a drift.")]
    public float driftMinSpeed = 5f;
    [Tooltip("How fast friction transitions in/out.")]
    public float driftFrictionSmoothing = 8f;

    [Header("Zombie Kill")]
    [Tooltip("Minimum speed in km/h to kill a zombie on impact.")]
    public float killSpeedKmh = 10f;

    [Header("Gamepad Haptics")]
    [Tooltip("Rumble strength (left motor) while boosting with a gamepad.")]
    public float boostHapticLow = 0.35f;
    [Tooltip("Rumble strength (right motor) while boosting with a gamepad.")]
    public float boostHapticHigh = 0.45f;
    [Tooltip("Rumble strength (left motor) when killing a zombie.")]
    public float killHapticLow = 0.4f;
    [Tooltip("Rumble strength (right motor) when killing a zombie.")]
    public float killHapticHigh = 0.35f;
    [Tooltip("How long the kill rumble lasts.")]
    public float killHapticDuration = 0.25f;

    private float horizontalInput;
    private float verticalInput;
    private float rawThrottle;
    private float rawBrakeControl;
    private bool isHandbraking;
    private float remainingKillRumble;

    public bool IsBoosting { get; private set; }
    public bool IsDrifting { get; private set; }
    public bool IsBurningOut { get; private set; }
    public bool IsBraking => isHandbraking || rawBrakeControl > 0.1f;
    public Rigidbody CarRigidbody { get; private set; }

    private float currentSteerAngle;

    private WheelFrictionCurve origRearLeftSideways;
    private WheelFrictionCurve origRearRightSideways;
    private WheelFrictionCurve origRearLeftForward;
    private WheelFrictionCurve origRearRightForward;
    private WheelFrictionCurve origFrontLeftSideways;
    private WheelFrictionCurve origFrontRightSideways;

    private void Start()
    {
        CarRigidbody = GetComponent<Rigidbody>();

        if (CarRigidbody != null)
        {
            CarRigidbody.centerOfMass += centerOfMassOffset;
        }

        origRearLeftSideways = rearLeftCollider.sidewaysFriction;
        origRearRightSideways = rearRightCollider.sidewaysFriction;
        origRearLeftForward = rearLeftCollider.forwardFriction;
        origRearRightForward = rearRightCollider.forwardFriction;
        origFrontLeftSideways = frontLeftCollider.sidewaysFriction;
        origFrontRightSideways = frontRightCollider.sidewaysFriction;

        // Old serialized data may have saved these as 0, so enforce sensible defaults
        if (burnoutKeepStillBrakeForce < 100f) burnoutKeepStillBrakeForce = 5000f;
        if (burnoutMotorMultiplier < 0.1f) burnoutMotorMultiplier = 1.5f;
        if (burnoutTurnSpeed < 0.1f) burnoutTurnSpeed = 2.5f;
    }

    private void Update()
    {
        GetInput();
        UpdateHaptics();
        UpdateWheelMeshes();
    }

    private void FixedUpdate()
    {
        IsBurningOut = rawThrottle > 0.1f && rawBrakeControl > 0.1f;

        HandleMotor();
        HandleSteering();
        HandleBraking();
        HandleDrifting();
        HandleBoost();
        ApplyDownforce();
    }

    private void GetInput()
    {
        horizontalInput = 0f;
        verticalInput = 0f;
        rawThrottle = 0f;
        rawBrakeControl = 0f;

        // Keyboard steering
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed)
                horizontalInput -= 1f;

            if (Keyboard.current.dKey.isPressed)
                horizontalInput += 1f;

            if (Keyboard.current.wKey.isPressed)
            {
                verticalInput += 1f;
                rawThrottle += 1f;
            }

            if (Keyboard.current.sKey.isPressed)
            {
                verticalInput -= 1f;
                rawBrakeControl += 1f;
            }

            isHandbraking = Keyboard.current.spaceKey.isPressed;
            IsBoosting = Keyboard.current.leftShiftKey.isPressed;
        }

        // Gamepad input
        if (Gamepad.current != null)
        {
            Vector2 leftStick = Gamepad.current.leftStick.ReadValue();

            float r2 = Gamepad.current.rightTrigger.ReadValue();
            float l2 = Gamepad.current.leftTrigger.ReadValue();

            horizontalInput += leftStick.x;
            verticalInput += r2 - l2;
            rawThrottle += r2;
            rawBrakeControl += l2;

            if (Gamepad.current.buttonSouth.isPressed)
            {
                IsBoosting = true;
            }

            if (Gamepad.current.buttonWest.isPressed)
            {
                isHandbraking = true;
            }
        }

        horizontalInput = Mathf.Clamp(horizontalInput, -1f, 1f);
        verticalInput = Mathf.Clamp(verticalInput, -1f, 1f);
        rawThrottle = Mathf.Clamp01(rawThrottle);
        rawBrakeControl = Mathf.Clamp01(rawBrakeControl);
    }

    private void HandleMotor()
    {
        if (IsBurningOut)
        {
            // Cut all speed-limit guards and spin the rear wheels at full torque
            float burnoutTorque = motorForce * burnoutMotorMultiplier;
            rearLeftCollider.motorTorque = burnoutTorque;
            rearRightCollider.motorTorque = burnoutTorque;
            return;
        }

        float forwardSpeed = Vector3.Dot(CarRigidbody.linearVelocity, transform.forward);

        bool overForwardSpeed = forwardSpeed >= maxForwardSpeed && verticalInput > 0f && !IsBoosting;
        bool overReverseSpeed = forwardSpeed <= -maxReverseSpeed && verticalInput < 0f;

        if (overForwardSpeed || overReverseSpeed)
        {
            rearLeftCollider.motorTorque = 0f;
            rearRightCollider.motorTorque = 0f;
            return;
        }

        float torque = verticalInput * motorForce;

        if (IsDrifting)
        {
            torque *= driftMotorMultiplier;
        }

        rearLeftCollider.motorTorque = torque;
        rearRightCollider.motorTorque = torque;
    }

    private void HandleDrifting()
    {
        float t = driftFrictionSmoothing * Time.fixedDeltaTime;

        // Rear wheels
        WheelFrictionCurve rearLeftSideways = rearLeftCollider.sidewaysFriction;
        WheelFrictionCurve rearRightSideways = rearRightCollider.sidewaysFriction;
        WheelFrictionCurve rearLeftForward = rearLeftCollider.forwardFriction;
        WheelFrictionCurve rearRightForward = rearRightCollider.forwardFriction;

        // Front wheels (only sideways matters for yaw)
        WheelFrictionCurve frontLeftSideways = frontLeftCollider.sidewaysFriction;
        WheelFrictionCurve frontRightSideways = frontRightCollider.sidewaysFriction;

        if (IsBurningOut)
        {
            rearLeftSideways.stiffness = Mathf.Lerp(rearLeftSideways.stiffness, burnoutSidewaysFriction, t);
            rearRightSideways.stiffness = Mathf.Lerp(rearRightSideways.stiffness, burnoutSidewaysFriction, t);
            rearLeftForward.stiffness = Mathf.Lerp(rearLeftForward.stiffness, burnoutForwardFriction, t);
            rearRightForward.stiffness = Mathf.Lerp(rearRightForward.stiffness, burnoutForwardFriction, t);
            // Soften front sideways so the yaw torque can actually pivot the car
            frontLeftSideways.stiffness = Mathf.Lerp(frontLeftSideways.stiffness, burnoutSidewaysFriction, t);
            frontRightSideways.stiffness = Mathf.Lerp(frontRightSideways.stiffness, burnoutSidewaysFriction, t);
        }
        else if (IsDrifting)
        {
            rearLeftSideways.stiffness = Mathf.Lerp(rearLeftSideways.stiffness, driftSidewaysFriction, t);
            rearRightSideways.stiffness = Mathf.Lerp(rearRightSideways.stiffness, driftSidewaysFriction, t);
            rearLeftForward.stiffness = Mathf.Lerp(rearLeftForward.stiffness, driftForwardFriction, t);
            rearRightForward.stiffness = Mathf.Lerp(rearRightForward.stiffness, driftForwardFriction, t);
            // Restore front friction
            frontLeftSideways.stiffness = Mathf.Lerp(frontLeftSideways.stiffness, origFrontLeftSideways.stiffness, t);
            frontRightSideways.stiffness = Mathf.Lerp(frontRightSideways.stiffness, origFrontRightSideways.stiffness, t);
        }
        else
        {
            rearLeftSideways.stiffness = Mathf.Lerp(rearLeftSideways.stiffness, origRearLeftSideways.stiffness, t);
            rearRightSideways.stiffness = Mathf.Lerp(rearRightSideways.stiffness, origRearRightSideways.stiffness, t);
            rearLeftForward.stiffness = Mathf.Lerp(rearLeftForward.stiffness, origRearLeftForward.stiffness, t);
            rearRightForward.stiffness = Mathf.Lerp(rearRightForward.stiffness, origRearRightForward.stiffness, t);
            frontLeftSideways.stiffness = Mathf.Lerp(frontLeftSideways.stiffness, origFrontLeftSideways.stiffness, t);
            frontRightSideways.stiffness = Mathf.Lerp(frontRightSideways.stiffness, origFrontRightSideways.stiffness, t);
        }

        rearLeftCollider.sidewaysFriction = rearLeftSideways;
        rearRightCollider.sidewaysFriction = rearRightSideways;
        rearLeftCollider.forwardFriction = rearLeftForward;
        rearRightCollider.forwardFriction = rearRightForward;
        frontLeftCollider.sidewaysFriction = frontLeftSideways;
        frontRightCollider.sidewaysFriction = frontRightSideways;
    }

    private void HandleBoost()
    {
        if (!IsBoosting || IsBurningOut)
            return;

        float forwardSpeed = Vector3.Dot(CarRigidbody.linearVelocity, transform.forward);

        if (forwardSpeed >= boostMaxSpeed)
            return;

        CarRigidbody.AddForce(transform.forward * boostForce, ForceMode.Force);
    }

    private void HandleSteering()
    {
        float speed = CarRigidbody.linearVelocity.magnitude;
        float speedPercent = Mathf.Clamp01(speed / steeringSpeedForMinAngle);

        float adjustedMaxSteerAngle = Mathf.Lerp(
            maxSteerAngle,
            minSteerAngleAtHighSpeed,
            speedPercent
        );

        if (IsDrifting)
        {
            adjustedMaxSteerAngle *= driftSteerMultiplier;
        }

        float targetSteerAngle = horizontalInput * adjustedMaxSteerAngle;

        currentSteerAngle = Mathf.Lerp(
            currentSteerAngle,
            targetSteerAngle,
            steerSmoothSpeed * Time.fixedDeltaTime
        );

        frontLeftCollider.steerAngle = currentSteerAngle;
        frontRightCollider.steerAngle = currentSteerAngle;

        // During a burnout, force the yaw angular velocity toward the steering input so
        // the car spins in place (GTA-style). Directly overriding angular velocity wins
        // against the WheelCollider friction solver that cancels plain torque.
        if (IsBurningOut)
        {
            float targetSpin = horizontalInput * burnoutTurnSpeed;
            Vector3 up = transform.up;
            Vector3 angularVel = CarRigidbody.angularVelocity;
            float currentSpin = Vector3.Dot(angularVel, up);
            float newSpin = Mathf.Lerp(currentSpin, targetSpin, Time.fixedDeltaTime * 8f);
            CarRigidbody.angularVelocity = angularVel + up * (newSpin - currentSpin);
        }
    }

    private void HandleBraking()
    {
        float forwardSpeed = Vector3.Dot(CarRigidbody.linearVelocity, transform.forward);

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

        if (IsBurningOut)
        {
            // Lock the front wheels so the car stays put, let the rear wheels spin free
            frontLeftCollider.brakeTorque = burnoutKeepStillBrakeForce;
            frontRightCollider.brakeTorque = burnoutKeepStillBrakeForce;
            rearLeftCollider.brakeTorque = 0f;
            rearRightCollider.brakeTorque = 0f;
            return;
        }

        IsDrifting = isHandbraking && Mathf.Abs(forwardSpeed) > driftMinSpeed && Mathf.Abs(horizontalInput) > 0.1f;

        if (isHandbraking)
        {
            rearLeftCollider.brakeTorque = handbrakeForce;
            rearRightCollider.brakeTorque = handbrakeForce;
        }
    }

    private void ApplyDownforce()
    {
        float speed = CarRigidbody.linearVelocity.magnitude;
        CarRigidbody.AddForce(-transform.up * downForce * speed);
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

    private void OnTriggerEnter(Collider other)
    {
        ZombieAI zombie = other.GetComponentInParent<ZombieAI>();
        if (zombie == null || zombie.isDead) return;

        float currentSpeedKmh = CarRigidbody.linearVelocity.magnitude * 3.6f;
        if (currentSpeedKmh >= killSpeedKmh)
        {
            zombie.KillZombie();
            remainingKillRumble = killHapticDuration;
        }
    }

    private void UpdateHaptics()
    {
        if (Gamepad.current == null) return;

        float low = 0f;
        float high = 0f;

        if (IsBoosting)
        {
            low += boostHapticLow;
            high += boostHapticHigh;
        }

        if (remainingKillRumble > 0f)
        {
            remainingKillRumble = Mathf.Max(0f, remainingKillRumble - Time.deltaTime);
            low += killHapticLow;
            high += killHapticHigh;
        }

        Gamepad.current.SetMotorSpeeds(Mathf.Clamp01(low), Mathf.Clamp01(high));
    }

    private void OnDisable()
    {
        if (Gamepad.current != null)
        {
            Gamepad.current.SetMotorSpeeds(0f, 0f);
        }
    }
}