using System;
using UnityEngine;

public class PlayerHealth : Health
{
    [Header("Death")]
    [Tooltip("Brake force pushed to every wheel on death so the car cannot keep rolling.")]
    [SerializeField] private float deathBrakeForce = 10000f;

    [Header("Split Screen")]
    [Tooltip("Which player this car belongs to (0/1). Resolved from CarController/SplitScreenMode when left as -1.")]
    [SerializeField] private int playerIndex = -1;

    /// <summary>Raised once per dead player. Argument is the player index that died (0/1).</summary>
    public static event Action<int> OnPlayerDied;

    public bool IsDead { get; private set; }
    public int PlayerIndex => playerIndex;

    private void Awake()
    {
        if (playerIndex >= 0)
            return;

        CarController car = GetComponent<CarController>();
        if (car != null)
        {
            playerIndex = car.playerIndex;
            return;
        }

        for (int i = 0; i < 2; i++)
        {
            if (SplitScreenMode.GetPlayer(i) == transform)
            {
                playerIndex = i;
                return;
            }
        }

        playerIndex = 0;
    }

    protected override void Die()
    {
        if (IsDead)
            return;

        IsDead = true;
        Debug.Log($"{gameObject.name} died!");
        StopCar();
        DisableCarSystems(gameObject);
        OnPlayerDied?.Invoke(playerIndex);
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
    public static void DisableCarSystems(GameObject carRoot)
    {
        if (carRoot == null)
            return;

        DisableAll<CarController>(carRoot);
        DisableAll<CarAudio>(carRoot);
        DisableAll<CarEffects>(carRoot);
        DisableAll<CarWeapon>(carRoot);
    }

    private static void DisableAll<T>(GameObject host) where T : Behaviour
    {
        foreach (T component in host.GetComponentsInChildren<T>(true))
        {
            if (component != null)
                component.enabled = false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnPlayerDied = null;
    }
}
