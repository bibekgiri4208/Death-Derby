using System;
using System.Collections;
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
    [SerializeField] private Color selectionColor = new Color(0.75f, 0.85f, 1f, 1f); // Tint applied while selected via gamepad
    [SerializeField] private float colorSpeed = 15f;

    [Header("Audio Settings (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;

    [Header("Click Events")]
    [Tooltip("Drag components or manager scripts here to call their functions when clicked.")]
    public UnityEvent onClick;

    public event Action<Interactable3DButton> OnHovered;

    private Vector3 originalScale;
    private Vector3 targetScale;

    private Material targetMaterial;
    private Color targetColor;

    private bool initialized;
    private bool highlighted;
    private bool interactable = true;
    private Coroutine clickRoutine;

    public bool IsHighlighted => highlighted;
    public bool IsInteractable => interactable;

    void Awake()
    {
        EnsureInitialized();
    }

    public void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        originalScale = transform.localScale;
        targetScale = originalScale;

        if (TryGetComponent<Renderer>(out Renderer objectRenderer))
        {
            targetMaterial = objectRenderer.material;

            if (defaultColor == Color.white && targetMaterial.HasProperty("_Color"))
            {
                defaultColor = targetMaterial.color;
            }
            targetColor = defaultColor;
        }

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
        if (transform.localScale != targetScale)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
        }

        if (useClickTint && targetMaterial != null && targetMaterial.color != targetColor)
        {
            targetMaterial.color = Color.Lerp(targetMaterial.color, targetColor, Time.deltaTime * colorSpeed);
        }
    }

    public void SetHighlighted(bool value, bool playHoverSound = true)
    {
        EnsureInitialized();

        if (highlighted == value) return;

        highlighted = value;
        targetScale = value ? Vector3.Scale(originalScale, hoverScaleMultiplier) : originalScale;

        if (value && playHoverSound && audioSource != null && hoverSound != null)
        {
            audioSource.PlayOneShot(hoverSound);
        }

        if (useClickTint)
        {
            targetColor = value ? selectionColor : defaultColor;
        }
    }

    public void Press()
    {
        EnsureInitialized();

        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound);
        }

        if (useClickTint)
        {
            targetColor = clickColor;

            if (clickRoutine != null) StopCoroutine(clickRoutine);
            if (isActiveAndEnabled) clickRoutine = StartCoroutine(RestoreColorAfterClick());
        }

        onClick?.Invoke();
    }

    private IEnumerator RestoreColorAfterClick()
    {
        yield return new WaitForSeconds(0.12f);
        targetColor = highlighted ? selectionColor : defaultColor;
        clickRoutine = null;
    }

    private void OnMouseEnter()
    {
        if (!interactable) return;
        SetHighlighted(true);
        OnHovered?.Invoke(this);
    }

    private void OnMouseExit()
    {
        if (!interactable) return;
        SetHighlighted(false);
    }

    private void OnMouseDown()
    {
        if (!interactable) return;
        Press();
    }

    public void SetInteractable(bool value)
    {
        if (interactable == value) return;
        interactable = value;

        if (!value)
        {
            highlighted = false;
            targetScale = originalScale;
        }

        if (!useClickTint || targetMaterial == null) return;

        targetColor = value ? (highlighted ? selectionColor : defaultColor) : clickColor;
    }

    private void OnDisable()
    {
        highlighted = false;

        if (clickRoutine != null)
        {
            StopCoroutine(clickRoutine);
            clickRoutine = null;
        }

        if (!initialized) return;

        transform.localScale = originalScale;
        targetScale = originalScale;

        if (targetMaterial != null)
        {
            targetMaterial.color = defaultColor;
            targetColor = defaultColor;
        }
    }
}
