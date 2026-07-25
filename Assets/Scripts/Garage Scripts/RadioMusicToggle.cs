using UnityEngine;

public class RadioMusicToggle : MonoBehaviour
{
    [Header("Audio Reference")]
    public AudioSource radioAudioSource;

    [Header("Particle Effects")]
    public ParticleSystem particleEffect1;
    public ParticleSystem particleEffect2;

    [Header("Speaker Bounce Settings")]
    [Tooltip("Assign your left and right speaker cone meshes here.")]
    public Transform leftSpeaker;
    public Transform rightSpeaker;

    public float pulseSpeed = 10f; // How fast the speakers bounce
    public float pulseAmount = 0.05f; // How much they expand (0.05 = 5% bigger)

    private Vector3 originalLeftScale;
    private Vector3 originalRightScale;

    void Start()
    {
        // Auto-get AudioSource if not assigned in Inspector
        if (radioAudioSource == null)
        {
            radioAudioSource = GetComponent<AudioSource>();
        }

        // Store original scales if assigned
        if (leftSpeaker != null) originalLeftScale = leftSpeaker.localScale;
        if (rightSpeaker != null) originalRightScale = rightSpeaker.localScale;

        // Sync particle state with initial audio state on game start
        UpdateParticles();
    }

    void Update()
    {
        // Check if music is actively playing and unmuted
        bool isMusicPlaying = radioAudioSource != null && !radioAudioSource.mute && radioAudioSource.isPlaying;

        if (isMusicPlaying)
        {
            // Smooth bouncing scale using a sine wave
            float scaleOffset = Mathf.Abs(Mathf.Sin(Time.time * pulseSpeed)) * pulseAmount;
            Vector3 bounceOffset = new Vector3(scaleOffset, scaleOffset, scaleOffset);

            if (leftSpeaker != null)
                leftSpeaker.localScale = originalLeftScale + bounceOffset;

            if (rightSpeaker != null)
                rightSpeaker.localScale = originalRightScale + bounceOffset;
        }
        else
        {
            // Smoothly reset back to original scales when muted/stopped
            ResetSpeakerScale(leftSpeaker, originalLeftScale);
            ResetSpeakerScale(rightSpeaker, originalRightScale);
        }
    }

    private void ResetSpeakerScale(Transform speaker, Vector3 originalScale)
    {
        if (speaker == null) return;

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