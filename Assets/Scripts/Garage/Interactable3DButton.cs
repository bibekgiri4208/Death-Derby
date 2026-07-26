using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class Interactable3DButton : MonoBehaviour
{
    [Header("Hover Scale Settings")]
    [SerializeField] private Vector3 hoverScaleMultiplier = new Vector3(1.15f, 1.15f, 1.15f);
    [SerializeField] private float scaleSpeed = 12f;

    [Header("Audio Settings (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;

    [Header("Click Events")]
    [Tooltip("Drag components or manager scripts here to call their functions when clicked.")]
    public UnityEvent onClick;

    private Vector3 originalScale;
    private Vector3 targetScale;

    void Start()
    {
        // Save initial scale
        originalScale = transform.localScale;
        targetScale = originalScale;

        // Auto-fetch AudioSource if not assigned
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    void Update()
    {
        // Smooth scaling interpolation
        if (transform.localScale != targetScale)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
        }
    }

    private void OnMouseEnter()
    {
        targetScale = Vector3.Scale(originalScale, hoverScaleMultiplier);

        if (audioSource != null && hoverSound != null)
        {
            audioSource.PlayOneShot(hoverSound);
        }
    }

    private void OnMouseExit()
    {
        targetScale = originalScale;
    }

    private void OnMouseDown()
    {
        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound);
        }

        // Trigger any functions attached in the Unity Inspector
        onClick?.Invoke();
    }

    private void OnDisable()
    {
        // Reset scale if object gets disabled while hovering
        transform.localScale = originalScale;
        targetScale = originalScale;
    }
}