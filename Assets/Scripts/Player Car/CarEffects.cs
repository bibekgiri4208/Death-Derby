using UnityEngine;

public class CarEffects : MonoBehaviour
{
    [Header("NOS Boost Effects")]
    public ParticleSystem[] boostFlames;
    public AudioSource nosAudioSource;

    [Header("Desert Smoke / Dust Effect")]
    [Tooltip("Assign the smoke particle systems for all 4 wheels here.")]
    public ParticleSystem[] desertSmokeEffects;
    public float maxSmokeEmissionRate = 120f;
    public float maxSmokeParticleSpeed = 4f;

    private CarController carController;
    private ParticleSystem.EmissionModule[] smokeEmissions;
    private ParticleSystem.MainModule[] smokeMains;

    private void Start()
    {
        // Dynamically get the companion script component
        carController = GetComponent<CarController>();

        if (carController == null)
        {
            Debug.LogError("CarEffects needs a CarController component on the same GameObject!");
            enabled = false;
            return;
        }

        InitializeBoostEffects();
        InitializeSmokeEffects();
    }

    private void Update()
    {
        UpdateBoostEffects();
        UpdateDesertSmoke();
    }

    private void InitializeBoostEffects()
    {
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
    }

    private void InitializeSmokeEffects()
    {
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

    private void UpdateBoostEffects()
    {
        if (carController.IsBoosting)
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
        if (desertSmokeEffects == null || desertSmokeEffects.Length == 0 || carController.CarRigidbody == null)
            return;

        // Pull data from our CarController
        float currentSpeed = carController.CarRigidbody.linearVelocity.magnitude;
        float absoluteMaxSpeed = carController.IsBoosting ? carController.boostMaxSpeed : carController.maxForwardSpeed;

        // Establish relative performance ratio (0.0 to 1.0)
        float speedRatio = Mathf.Clamp01(currentSpeed / absoluteMaxSpeed);

        // Apply dynamic calculation adjustments
        for (int i = 0; i < desertSmokeEffects.Length; i++)
        {
            if (desertSmokeEffects[i] == null) continue;

            smokeEmissions[i].rateOverTime = speedRatio * maxSmokeEmissionRate;
            smokeMains[i].startSpeed = Mathf.Lerp(1.0f, maxSmokeParticleSpeed, speedRatio);
        }
    }
}