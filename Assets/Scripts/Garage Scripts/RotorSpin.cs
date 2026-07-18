using UnityEngine;

public class RotorSpin : MonoBehaviour
{
    [Header("Spin Settings")]
    public float rpm = 300f;          // Rotations per minute
    public Vector3 axis = Vector3.up; // Which direction to spin (usually Y for main rotor)

    private float speed;

    void Start()
    {
        // Convert RPM to degrees per second
        speed = (rpm / 60f) * 360f;
    }

    void Update()
    {
        // Rotate the rotor around its local axis
        transform.Rotate(axis, speed * Time.deltaTime);
    }
}