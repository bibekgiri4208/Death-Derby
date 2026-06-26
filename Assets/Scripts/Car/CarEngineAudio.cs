using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class CarEngineAudio : MonoBehaviour
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

    private AudioSource engineAudio;

    private void Awake()
    {
        engineAudio = GetComponent<AudioSource>();

        if (carRigidbody == null)
        {
            carRigidbody = GetComponent<Rigidbody>();
        }

        engineAudio.loop = true;
        engineAudio.playOnAwake = true;
    }

    private void Start()
    {
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