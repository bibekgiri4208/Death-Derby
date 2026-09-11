using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class CarAudio : MonoBehaviour
{
    [Header("References")]
    public Rigidbody carRigidbody;

    [Header("Engine Audio")]
    public float minPitch = 0.8f;
    public float maxPitch = 2.2f;
    public float maxSpeed = 40f;

    [Header("Volume")]
    public float minVolume = 0.35f;
    public float maxVolume = 1f;

    [Header("Smoothing")]
    public float pitchSmoothSpeed = 5f;
    public float volumeSmoothSpeed = 5f;

    [Header("Burnout Audio")]
    [Tooltip("Engine pitch while burning out (revving up).")]
    public float burnoutPitch = 2.2f;
    [Tooltip("Engine volume while burning out.")]
    public float burnoutVolume = 0.9f;

    private AudioSource engineAudio;
    private CarController carController;

    private void Awake()
    {
        engineAudio = GetComponent<AudioSource>();
        carController = GetComponent<CarController>();

        if (carRigidbody == null)
        {
            carRigidbody = GetComponent<Rigidbody>();
        }

        engineAudio.loop = true;
        engineAudio.playOnAwake = true;
    }

    private void Start()
    {
        // Old serialized data may have saved these as 0, so enforce sensible defaults
        if (burnoutPitch < maxPitch) burnoutPitch = maxPitch;
        if (burnoutVolume < 0.05f) burnoutVolume = Mathf.Max(maxVolume, 0.9f);

        if (!engineAudio.isPlaying)
        {
            engineAudio.Play();
        }
    }

    private void Update()
    {
        if (carRigidbody == null)
            return;

        UpdateEngineSound();
    }

    private void UpdateEngineSound()
    {
        float speed = carRigidbody.linearVelocity.magnitude;

        float speedPercent = Mathf.Clamp01(speed / maxSpeed);

        float targetPitch = Mathf.Lerp(minPitch, maxPitch, speedPercent);
        float targetVolume = Mathf.Lerp(minVolume, maxVolume, speedPercent);

        // Rev the engine while burning out
        if (carController != null && carController.IsBurningOut)
        {
            targetPitch = burnoutPitch;
            targetVolume = burnoutVolume;
        }

        engineAudio.pitch = Mathf.Lerp(
            engineAudio.pitch,
            targetPitch,
            pitchSmoothSpeed * Time.deltaTime
        );

        engineAudio.volume = Mathf.Lerp(
            engineAudio.volume,
            targetVolume,
            volumeSmoothSpeed * Time.deltaTime
        );
    }
}