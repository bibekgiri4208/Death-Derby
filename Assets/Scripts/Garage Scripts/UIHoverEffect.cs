using UnityEngine;
using UnityEngine.EventSystems; // Required for Pointer events

public class UIHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Scale Settings")]
    public Vector3 hoverScale = new Vector3(1.15f, 1.15f, 1.15f);
    public float transitionSpeed = 12f; // Higher = faster, Lower = smoother/slower

    [Header("Audio Effect (Optional)")]
    public AudioSource audioSource;
    public AudioClip hoverSound;

    private Vector3 originalScale;
    private Vector3 targetScale;

    void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
    }

    void Update()
    {
        // Smoothly interpolate the scale towards the target scale every frame
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * transitionSpeed);
    }

    // Triggered when mouse ENTERS the button area
    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = hoverScale;

        if (audioSource != null && hoverSound != null)
        {
            audioSource.PlayOneShot(hoverSound);
        }
    }

    // Triggered when mouse EXITS the button area
    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;
    }

    // Reset scale instantly if the button becomes inactive while hovered
    void OnDisable()
    {
        transform.localScale = originalScale;
        targetScale = originalScale;
    }
}