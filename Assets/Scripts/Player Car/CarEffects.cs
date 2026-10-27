using UnityEngine;

public class CarEffects : MonoBehaviour
{
    [Header("NOS Boost Effects")]
    public ParticleSystem[] boostFlames;
    public AudioSource nosAudioSource;

    [Header("Brake Lights")]
    [Tooltip("Assign the rear Brake Lights here so they turn on when braking or using the handbrake.")]
    public Light[] brakeLights;
    [Tooltip("How smoothly brake lights fade in/out (lower = smoother).")]
    public float brakeLightSmoothing = 8f;

    [Header("Wheel Dust")]
    [Tooltip("Existing wheel emitters are reused; missing emitters are created automatically.")]
    public ParticleSystem[] desertSmokeEffects;
    public float maxSmokeEmissionRate = 64f;
    public float maxSmokeParticleSpeed = 1.4f;
    [Tooltip("Minimum ground speed for rolling dust. Wheelspin can still kick up sand at a standstill.")]
    public float minSmokeSpeed = 1.5f;
    [Tooltip("How smoothly the emission rate transitions (lower = smoother).")]
    public float emissionSmoothing = 5f;
    [Tooltip("Ground-level sand particles per metre. The finer dust wake is emitted separately.")]
    public float maxSmokeDistanceRate = 2.5f;
    [Range(0f, 0.75f)] public float dustOpacity = 0.45f;
    [Tooltip("Fine airborne sand that joins the wheel plumes into a lingering desert wake.")]
    [Range(0f, 2f)] public float dustWakeDensity = 1f;
    [Range(0f, 0.6f)] public float dustWakeOpacity = 0.26f;
    [Range(1f, 5f)] public float dustWakeLifetime = 3.4f;
    [Range(0.5f, 2f)] public float dustWakeSize = 1.1f;
    [HideInInspector] public ParticleSystem[] desertDustWakeEffects;

    [Header("Sand Tyre Imprints")]
    public bool enableTireTracks = true;
    [Range(0.15f, 0.65f)] public float tireTrackWidth = 0.32f;
    [Range(0f, 1f)] public float tireTrackStrength = 0.38f;

    private CarController carController;
    private WheelSurfaceEffects wheelSurfaceEffects;
    private float[] brakeLightIntensities;
    private bool wasBoosting;
    private float brakeLightAmount;

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
        if (minSmokeSpeed < 0.1f) minSmokeSpeed = 1.5f;
        if (emissionSmoothing < 0.5f) emissionSmoothing = 5f;
        if (brakeLightSmoothing < 0.5f) brakeLightSmoothing = 8f;

        InitializeBoostEffects();
        InitializeBrakeLights();
        wheelSurfaceEffects = new WheelSurfaceEffects(carController, this);
    }

    private void Update()
    {
        UpdateBoostEffects();
        UpdateBrakeLights();
    }

    private void FixedUpdate()
    {
        // Unity does not call Start again after a Play-mode script reload. Rebuild the
        // non-serialized helper lazily so dust does not silently disappear after recompiling.
        if (wheelSurfaceEffects == null)
        {
            if (carController == null) carController = GetComponent<CarController>();
            if (carController == null) return;
            wheelSurfaceEffects = new WheelSurfaceEffects(carController, this);
        }
        wheelSurfaceEffects?.Tick(Time.fixedDeltaTime);
    }

    private void OnEnable()
    {
        wheelSurfaceEffects?.ResetContacts();
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

    private void OnDisable()
    {
        if (nosAudioSource != null && nosAudioSource.isPlaying)
        {
            nosAudioSource.Stop();
        }

        if (boostFlames != null)
        {
            foreach (ParticleSystem flame in boostFlames)
            {
                if (flame != null)
                {
                    flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        wheelSurfaceEffects?.Stop();

        if (brakeLightIntensities != null)
        {
            for (int i = 0; i < brakeLightIntensities.Length; i++)
            {
                if (brakeLights[i] != null)
                {
                    brakeLights[i].intensity = 0f;
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

        brakeLightAmount = 0f;
        brakeLightIntensities = new float[brakeLights.Length];

        for (int i = 0; i < brakeLights.Length; i++)
        {
            if (brakeLights[i] == null) continue;

            brakeLights[i].enabled = true;
            brakeLightIntensities[i] = brakeLights[i].intensity;
            brakeLights[i].intensity = 0f;
        }
    }

    private void UpdateBrakeLights()
    {
        if (brakeLights == null || brakeLights.Length == 0)
            return;

        float target = carController.IsBraking ? 1f : 0f;
        float lerpFactor = 1f - Mathf.Exp(-Time.deltaTime * brakeLightSmoothing);
        brakeLightAmount = Mathf.Lerp(brakeLightAmount, target, lerpFactor);

        for (int i = 0; i < brakeLights.Length; i++)
        {
            Light brakeLight = brakeLights[i];
            if (brakeLight == null) continue;

            float baseIntensity = i < brakeLightIntensities.Length ? brakeLightIntensities[i] : brakeLight.intensity;
            brakeLight.intensity = baseIntensity * brakeLightAmount;
        }
    }
}
