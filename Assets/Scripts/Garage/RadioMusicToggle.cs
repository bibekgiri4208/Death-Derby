using UnityEngine;

public class RadioMusicToggle : MonoBehaviour
{
    [Header("Audio Reference")]
    public AudioSource radioAudioSource;

    [Header("Particle Effects")]
    public ParticleSystem particleEffect1;
    public ParticleSystem particleEffect2;

    [Header("Speaker Bounce Settings")]
    [Tooltip("Drag up to 4 speaker cone transforms here.")]
    public Transform[] speakers = new Transform[4];

    public float pulseSpeed = 10f;   // How fast the speakers bounce
    public float pulseAmount = 0.05f; // How much they expand (0.05 = 5% bigger)

    private Vector3[] originalScales;

    void Start()
    {
        // Auto-get AudioSource if not assigned in Inspector
        if (radioAudioSource == null)
        {
            radioAudioSource = GetComponent<AudioSource>();
        }

        // Save original scales for each assigned speaker
        if (speakers != null)
        {
            originalScales = new Vector3[speakers.Length];
            for (int i = 0; i < speakers.Length; i++)
            {
                if (speakers[i] != null)
                {
                    originalScales[i] = speakers[i].localScale;
                }
            }
        }

        // Sync particle state with initial audio state on game start
        UpdateParticles();
    }

    void Update()
    {
        // Check if music is actively playing and unmuted
        bool isMusicPlaying = radioAudioSource != null && !radioAudioSource.mute && radioAudioSource.isPlaying;

        if (isMusicPlaying)
        {
            // Calculate a smooth bouncing scale using a sine wave
            float scaleOffset = Mathf.Abs(Mathf.Sin(Time.time * pulseSpeed)) * pulseAmount;
            Vector3 bounceOffset = new Vector3(scaleOffset, scaleOffset, scaleOffset);

            // Apply pulse to all speakers
            for (int i = 0; i < speakers.Length; i++)
            {
                if (speakers[i] != null)
                {
                    speakers[i].localScale = originalScales[i] + bounceOffset;
                }
            }
        }
        else
        {
            // Smoothly reset all speakers back to their original scale
            for (int i = 0; i < speakers.Length; i++)
            {
                if (speakers[i] != null && originalScales != null)
                {
                    ResetSpeakerScale(speakers[i], originalScales[i]);
                }
            }
        }
    }

    private void ResetSpeakerScale(Transform speaker, Vector3 originalScale)
    {
        if (speaker.localScale != originalScale)
        {
            speaker.localScale = Vector3.Lerp(
                speaker.localScale,
                originalScale,
                Time.deltaTime * 10f
            );
        }
    }

    void OnMouseDown()
    {
        if (radioAudioSource != null)
        {
            // Toggle mute on left-click
            radioAudioSource.mute = !radioAudioSource.mute;

            // Turn particles on or off based on new mute status
            UpdateParticles();

            Debug.Log(radioAudioSource.mute ? "Music Muted" : "Music Playing");
        }
    }

    private void UpdateParticles()
    {
        // If music is NOT muted (playing), play particles; otherwise stop them
        bool shouldPlay = !radioAudioSource.mute && radioAudioSource.isPlaying;

        SetParticleState(particleEffect1, shouldPlay);
        SetParticleState(particleEffect2, shouldPlay);
    }

    private void SetParticleState(ParticleSystem ps, bool play)
    {
        if (ps == null) return;

        if (play)
        {
            if (!ps.isPlaying) ps.Play();
        }
        else
        {
            // Stop emitting new particles, letting existing ones finish naturally
            if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}