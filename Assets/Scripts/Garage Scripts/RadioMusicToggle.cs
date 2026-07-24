using UnityEngine;

public class RadioMusicToggle : MonoBehaviour
{
    [Header("Audio Reference")]
    public AudioSource radioAudioSource;

    [Header("Particle Effects")]
    public ParticleSystem particleEffect1;
    public ParticleSystem particleEffect2;

    void Start()
    {
        // Auto-get AudioSource if not assigned in Inspector
        if (radioAudioSource == null)
        {
            radioAudioSource = GetComponent<AudioSource>();
        }

        // Sync particle state with initial audio state on game start
        UpdateParticles();
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