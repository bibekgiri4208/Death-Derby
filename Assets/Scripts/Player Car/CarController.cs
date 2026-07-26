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

    private float horizontalInput;
    private float verticalInput;
    private bool isHandbraking;

    // Public properties so CarEffects can access them
    public bool IsBoosting { get; private set; }
    public Rigidbody CarRigidbody { get; private set; }

    private float currentSteerAngle;

    private void Start()
    {
        CarRigidbody = GetComponent<Rigidbody>();

        if (CarRigidbody != null)
        {
            CarRigidbody.centerOfMass += centerOfMassOffset;
        }
    }

    private void Update()
    {
        GetInput();
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

            if (Gamepad.current.buttonSouth.isPressed)
            {
                IsBoosting = true;
            }
        }

        horizontalInput = Mathf.Clamp(horizontalInput, -1f, 1f);
        verticalInput = Mathf.Clamp(verticalInput, -1f, 1f);
    }

    private void HandleMotor()
    {
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

        rearLeftCollider.motorTorque = torque;
        rearRightCollider.motorTorque = torque;
    }

    private void HandleBoost()
    {
        if (!IsBoosting)
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
}