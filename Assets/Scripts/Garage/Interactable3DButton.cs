using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class Interactable3DButton : MonoBehaviour
{
    [Header("Hover Scale Settings")]
    [SerializeField] private Vector3 hoverScaleMultiplier = new Vector3(1.15f, 1.15f, 1.15f);
    [SerializeField] private float scaleSpeed = 12f;

    [Header("Click Color Settings")]
    [SerializeField] private bool useClickTint = true;
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color clickColor = new Color(0.6f, 0.6f, 0.6f, 1f); // Tint applied only while clicked
    [SerializeField] private float colorSpeed = 15f;

    [Header("Audio Settings (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;

    [Header("Click Events")]
    [Tooltip("Drag components or manager scripts here to call their functions when clicked.")]
    public UnityEvent onClick;

    private Vector3 originalScale;
    private Vector3 targetScale;

    private Material targetMaterial;
    private Color targetColor;

    void Start()
    {
        // Save initial scale
        originalScale = transform.localScale;
        targetScale = originalScale;

        // Fetch Material for click tinting
        if (TryGetComponent<Renderer>(out Renderer objectRenderer))
        {
            targetMaterial = objectRenderer.material;

            // Auto-detect current color if not customized
            if (defaultColor == Color.white && targetMaterial.HasProperty("_Color"))
            {
                defaultColor = targetMaterial.color;
            }
            targetColor = defaultColor;
        }

        // Auto-fetch or create AudioSource if missing
        if (audioSource == null)
        {
            if (!TryGetComponent<AudioSource>(out audioSource))
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // Force 2D sound
            }
        }
    }

    void Update()
    {
        // Smooth scaling interpolation on hover
        if (transform.localScale != targetScale)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
        }

        // Smooth color tint interpolation on click
        if (useClickTint && targetMaterial != null && targetMaterial.color != targetColor)
        {
            targetMaterial.color = Color.Lerp(targetMaterial.color, targetColor, Time.deltaTime * colorSpeed);
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
        if (useClickTint) targetColor = defaultColor; // Reset color if mouse leaves while pressing
    }

    private void OnMouseDown()
    {
        // Change color ONLY on click
        if (useClickTint) targetColor = clickColor;

        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound);
        }

        // Trigger UnityEvent assigned in Inspector
        onClick?.Invoke();
    }

    private void OnMouseUp()
    {
        // Return to default color when mouse click is released
        if (useClickTint) targetColor = defaultColor;
    }

    private void OnDisable()
    {
        // Reset scale and color if object gets disabled
        transform.localScale = originalScale;
        targetScale = originalScale;

        if (targetMaterial != null)
        {
            targetMaterial.color = defaultColor;
            targetColor = defaultColor;
        }
    }
}