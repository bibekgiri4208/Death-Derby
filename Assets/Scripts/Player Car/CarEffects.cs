using UnityEngine;

public class CarEffects : MonoBehaviour
{
    [Header("NOS Boost Effects")]
    public ParticleSystem[] boostFlames;
    public AudioSource nosAudioSource;

    [Header("Brake Lights")]
    [Tooltip("Assign the rear Brake Lights here so they turn on when braking or using the handbrake.")]
    public Light[] brakeLights;

    [Header("Desert Smoke / Dust Effect")]
    [Tooltip("Assign the smoke particle systems for all 4 wheels here.")]
    public ParticleSystem[] desertSmokeEffects;
    public float maxSmokeEmissionRate = 120f;
    public float maxSmokeParticleSpeed = 4f;
    [Tooltip("Minimum car speed before smoke starts appearing.")]
    public float minSmokeSpeed = 8f;
    [Tooltip("How smoothly the emission rate transitions (lower = smoother).")]
    public float emissionSmoothing = 5f;
    [Tooltip("Smoke emitted per meter driven (this is what makes the trail thick).")]
    public float maxSmokeDistanceRate = 10f;

    private const float MinActiveSmokeRate = 20f;

    private CarController carController;
    private ParticleSystem.EmissionModule[] smokeEmissions;
    private ParticleSystem.MainModule[] smokeMains;
    private bool[] rearSmokeSystems;
    private bool wasBoosting;
    private bool brakeLightsOn;
    private float currentSmokeAmount;

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

        // Old serialized data may have saved these as 0, so enforce sensible defaults
        if (minSmokeSpeed < 2f) minSmokeSpeed = 8f;
        if (emissionSmoothing < 0.5f) emissionSmoothing = 5f;
        if (maxSmokeDistanceRate < 0.5f) maxSmokeDistanceRate = 10f;

        InitializeBoostEffects();
        InitializeBrakeLights();
        InitializeSmokeEffects();
    }

    private void Update()
    {
        UpdateBoostEffects();
        UpdateBrakeLights();
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
            rearSmokeSystems = new bool[desertSmokeEffects.Length];

            Vector3 carPos = transform.position;

            for (int i = 0; i < desertSmokeEffects.Length; i++)
            {
                if (desertSmokeEffects[i] != null)
                {
                    smokeEmissions[i] = desertSmokeEffects[i].emission;
                    smokeMains[i] = desertSmokeEffects[i].main;

                    // Classify as rear wheel emitter by position relative to the car's forward axis
                    Vector3 offset = desertSmokeEffects[i].transform.position - carPos;
                    rearSmokeSystems[i] = Vector3.Dot(transform.forward, offset) < 0f;

                    // Front wheel emitters stay fully off
                    if (!rearSmokeSystems[i])
                    {
                        smokeEmissions[i].rateOverDistance = 0f;
                        smokeEmissions[i].rateOverTime = 0f;
                        continue;
                    }

                    // Force simulation space to World so smoke trails behind naturally
                    smokeMains[i].simulationSpace = ParticleSystemSimulationSpace.World;
                    // Start with zero emission; UpdateDesertSmoke drives both rateOverTime and rateOverDistance per frame
                    smokeEmissions[i].rateOverDistance = 0f;
                    smokeEmissions[i].rateOverTime = 0f;
                }
            }
        }
    }

    private void UpdateBoostEffects()
    {
        bool boosting = carController.IsBoosting;

        if (boosting && !wasBoosting)
        {
            foreach (ParticleSystem flame in boostFlames)
            {
                if (flame != null)
                {
                    flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    flame.Play(true);
                }
            }

            if (nosAudioSource != null && !nosAudioSource.isPlaying)
            {
                nosAudioSource.Play();
            }
        }
        else if (!boosting && wasBoosting)
        {
            foreach (ParticleSystem flame in boostFlames)
            {
                if (flame != null)
                {
                    flame.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            if (nosAudioSource != null && nosAudioSource.isPlaying)
            {
                nosAudioSource.Stop();
            }
        }

        wasBoosting = boosting;
    }

    private void InitializeBrakeLights()
    {
        if (brakeLights == null || brakeLights.Length == 0)
            return;

        brakeLightsOn = false;

        foreach (Light brakeLight in brakeLights)
        {
            if (brakeLight != null)
            {
                brakeLight.enabled = false;
            }
        }
    }

    private void UpdateBrakeLights()
    {
        if (brakeLights == null || brakeLights.Length == 0)
            return;

        bool braking = carController.IsBraking;

        if (braking == brakeLightsOn)
            return;

        brakeLightsOn = braking;

        foreach (Light brakeLight in brakeLights)
        {
            if (brakeLight != null)
            {
                brakeLight.enabled = braking;
            }
        }
    }

    private void UpdateDesertSmoke()
    {
        if (desertSmokeEffects == null || desertSmokeEffects.Length == 0 || carController.CarRigidbody == null)
            return;

        float absoluteMaxSpeed = carController.IsBoosting ? carController.boostMaxSpeed : carController.maxForwardSpeed;

        // Ignore vertical velocity so the module only reacts to horizontal driving speed
        Vector3 flatVelocity = carController.CarRigidbody.linearVelocity;
        flatVelocity.y = 0f;
        float currentSpeed = flatVelocity.magnitude;

        // Hard dead zone: zero smoke until the car is genuinely driving
        float speedRange = Mathf.Max(absoluteMaxSpeed - minSmokeSpeed, 0.01f);
        float targetAmount = carController.IsBurningOut
            ? 1f
            : Mathf.Clamp01((currentSpeed - minSmokeSpeed) / speedRange);

        // Frame-rate independent exponential smoothing kills flicker from speed jitter
        float lerpFactor = 1f - Mathf.Exp(-Time.deltaTime * emissionSmoothing);
        float smokeAmount = Mathf.Lerp(currentSmokeAmount, targetAmount, lerpFactor);
        currentSmokeAmount = smokeAmount;

        // Once active, keep a minimum spawn rate so the trail is continuous (no sputtering puffs)
        float targetRate = smokeAmount > 0.001f
            ? Mathf.Max(smokeAmount * maxSmokeEmissionRate, MinActiveSmokeRate)
            : 0f;
        float smoothedParticleSpeed = Mathf.Lerp(1.0f, maxSmokeParticleSpeed, smokeAmount);

        for (int i = 0; i < desertSmokeEffects.Length; i++)
        {
            if (desertSmokeEffects[i] == null) continue;
            if (!rearSmokeSystems[i]) continue;

            smokeEmissions[i].rateOverTime = targetRate;
            // Restores the thick trail: only above dead zone to avoid crawl-speed puffs
            smokeEmissions[i].rateOverDistance = smokeAmount > 0.001f ? maxSmokeDistanceRate : 0f;
            smokeMains[i].startSpeed = smoothedParticleSpeed;
        }
    }
}