using UnityEngine;

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

    [Header("Steering Assist")]
    public float steerSmoothSpeed = 6f;
    public float minSteerAngleAtHighSpeed = 10f;
    public float steeringSpeedForMinAngle = 30f;

    private float currentSteerAngle;    

    [Header("Stability")]
    public float downForce = 80f;
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.5f, 0f);

    private float horizontalInput;
    private float verticalInput;
    private bool isBraking;
    private bool isHandbraking;

    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.centerOfMass += centerOfMassOffset;
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
        ApplyDownforce();
    }

    private void GetInput()
    {
        horizontalInput = 0f;
        verticalInput = 0f;

        if (Input.GetKey(KeyCode.A))
            horizontalInput = -1f;

        if (Input.GetKey(KeyCode.D))
            horizontalInput = 1f;

        if (Input.GetKey(KeyCode.W))
            verticalInput = 1f;

        if (Input.GetKey(KeyCode.S))
            verticalInput = -1f;

        isBraking = Input.GetKey(KeyCode.S);
        isHandbraking = Input.GetKey(KeyCode.Space);
    }

    private void HandleMotor()
    {
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        bool overForwardSpeed = forwardSpeed >= maxForwardSpeed && verticalInput > 0f;
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

        bool pressingReverse = verticalInput < 0f;
        bool movingForward = forwardSpeed > 1f;

        float currentBrakeForce = 0f;

        // If pressing S while moving forward, brake first
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