using UnityEngine;

public class PlayerHealth : Health
{
    [Header("Death")]
    [Tooltip("Brake force pushed to every wheel on death so the car cannot keep rolling.")]
    [SerializeField] private float deathBrakeForce = 10000f;

    protected override void Die()
    {
        Debug.Log($"{gameObject.name} died!");
        StopCar();
        DisableCarSystems();
    }

    private void StopCar()
    {
        foreach (WheelCollider wheel in GetComponentsInChildren<WheelCollider>())
        {
            wheel.motorTorque = 0f;
            wheel.steerAngle = 0f;
            wheel.brakeTorque = deathBrakeForce;
        }

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    // The car stays visible as a wreck; only its driving, audio and effect scripts are switched off.
    private void DisableCarSystems()
    {
        DisableAll<CarController>(this);
        DisableAll<CarAudio>(this);
        DisableAll<CarEffects>(this);
        DisableAll<CarWeapon>(this);
    }

    private static void DisableAll<T>(Behaviour host) where T : Behaviour
    {
        foreach (T component in host.GetComponents<T>())
        {
            component.enabled = false;
        }
    }
}
