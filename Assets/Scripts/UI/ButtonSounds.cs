using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public class ButtonSounds : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, ISelectHandler
{
    [SerializeField] private AudioClip clickSound;
    [SerializeField] private AudioClip hoverSound;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.ignoreListenerPause = true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PlayClip(clickSound);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        PlayClip(hoverSound);
    }

    public void OnSelect(BaseEventData eventData)
    {
        PlayClip(hoverSound);
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip != null)
            audioSource.PlayOneShot(clip);
    }
}