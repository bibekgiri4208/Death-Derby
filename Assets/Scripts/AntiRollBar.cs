using UnityEngine;

public class AntiRollBar4Wheel : MonoBehaviour
{
    // Front Axle
    public WheelCollider FrontLeftWheel;
    public WheelCollider FrontRightWheel;

    // Rear Axle
    public WheelCollider RearLeftWheel;
    public WheelCollider RearRightWheel;

    // Adjust these separately if needed. Trucks often need stiffer rear anti-roll.
    public float FrontAntiRollStiffness = 5000f;
    public float RearAntiRollStiffness = 5000f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        // --- 1. Calculate Suspension Travel for each wheel ---
        float travelFL = GetWheelTravel(FrontLeftWheel);
        float travelFR = GetWheelTravel(FrontRightWheel);
        float travelRL = GetWheelTravel(RearLeftWheel);
        float travelRR = GetWheelTravel(RearRightWheel);

        // --- 2. Front Anti-Roll Force ---
        float frontForce = (travelFL - travelFR) * FrontAntiRollStiffness;
        if (FrontLeftWheel.isGrounded)
            rb.AddForceAtPosition(FrontLeftWheel.transform.up * -frontForce, FrontLeftWheel.transform.position);
        if (FrontRightWheel.isGrounded)
            rb.AddForceAtPosition(FrontRightWheel.transform.up * frontForce, FrontRightWheel.transform.position);

        // --- 3. Rear Anti-Roll Force ---
        float rearForce = (travelRL - travelRR) * RearAntiRollStiffness;
        if (RearLeftWheel.isGrounded)
            rb.AddForceAtPosition(RearLeftWheel.transform.up * -rearForce, RearLeftWheel.transform.position);
        if (RearRightWheel.isGrounded)
            rb.AddForceAtPosition(RearRightWheel.transform.up * rearForce, RearRightWheel.transform.position);
    }

    // Helper function to get how compressed the suspension is (0 = fully extended, 1 = fully compressed)
    float GetWheelTravel(WheelCollider wheel)
    {
        if (!wheel.isGrounded) return 1.0f; // If in air, treat as fully drooped

        WheelHit hit;
        if (wheel.GetGroundHit(out hit))
        {
            // Calculate the compression based on the hit point relative to the wheel center
            float compression = (-wheel.transform.InverseTransformPoint(hit.point).y - wheel.radius) / wheel.suspensionDistance;
            return Mathf.Clamp01(compression);
        }
        return 1.0f;
    }
}