using TMPro;
using UnityEngine;

public class MusicMenuToggle : MonoBehaviour
{
    [Header("Audio Reference")]
    public AudioSource musicAudioSource;

    [Header("Status Label")]
    public TMP_Text statusLabel;

    void Start()
    {
        RefreshLabel();
    }

    public void ToggleMusic()
    {
        if (musicAudioSource == null) return;

        musicAudioSource.mute = !musicAudioSource.mute;

        RefreshLabel();

        Debug.Log(musicAudioSource.mute ? "Music Off" : "Music On");
    }

    private void RefreshLabel()
    {
        if (statusLabel == null) return;

        bool isOn = musicAudioSource != null && !musicAudioSource.mute;
        statusLabel.text = isOn ? "On" : "Off";
    }
}