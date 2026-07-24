using UnityEngine;

public class RadioMusicToggle : MonoBehaviour
{
    [Header("Audio Reference")]
    public AudioSource radioAudioSource;

    void Start()
    {
        // Auto-get the AudioSource if it's on the same object
        if (radioAudioSource == null)
        {
            radioAudioSource = GetComponent<AudioSource>();
        }
    }

    // Runs automatically when the user left-clicks the 3D collider
    void OnMouseDown()
    {
        if (radioAudioSource != null)
        {
            // Toggle mute on click
            radioAudioSource.mute = !radioAudioSource.mute;

            // Optional log to check in the Console
            Debug.Log(radioAudioSource.mute ? "Music Muted" : "Music Playing");
        }
    }
}