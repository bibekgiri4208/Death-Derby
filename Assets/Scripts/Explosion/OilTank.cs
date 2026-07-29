using System.Collections;
using UnityEngine;

public class OilTank : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    [Header("Visual Effects")]
    [SerializeField] private Renderer tankRenderer;
    [SerializeField] private Color burntColor = new Color(0.1f, 0.1f, 0.1f, 1f); // Pitch black / scorched
    [SerializeField] private ParticleSystem fireParticleSystem;
    [SerializeField] private float fireDuration = 5f;

    [Header("Explosion FX (Optional)")]
    [SerializeField] private GameObject explosionEffectPrefab;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip explosionSound;
    [SerializeField] private AudioClip fireLoopSound;
    [SerializeField][Range(0f, 1f)] private float explosionVolume = 1.0f;
    [SerializeField][Range(0f, 1f)] private float fireVolume = 0.8f;

    private Color originalColor;
    private Material tankMaterial;
    private AudioSource audioSource;
    private bool isDestroyed = false;

    private void Awake()
    {
        // 1. Setup AudioSource component
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Configure AudioSource defaults for 3D world space
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1.0f; // Full 3D sound (quieter further away)

        // 2. Forcibly stop fire from playing on scene start
        if (fireParticleSystem != null)
        {
            fireParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void Start()
    {
        currentHealth = maxHealth;

        if (tankRenderer == null)
            tankRenderer = GetComponent<Renderer>();

        if (tankRenderer != null)
        {
            tankMaterial = tankRenderer.material;
            originalColor = tankMaterial.color;
        }
    }

    public void TakeDamage(float damageAmount)
    {
        // Don't take damage if it's already completely burnt out
        if (isDestroyed) return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Max(currentHealth, 0f);

        // 1. Darken the material color relative to health loss
        if (tankMaterial != null)
        {
            float healthPercent = currentHealth / maxHealth;
            tankMaterial.color = Color.Lerp(burntColor, originalColor, healthPercent);
        }

        // 2. Trigger destruction process when health reaches 0
        if (currentHealth <= 0 && !isDestroyed)
        {
            DestroyTankProcess();
        }
    }

    private void DestroyTankProcess()
    {
        isDestroyed = true;

        // Spawn optional explosion visual effect
        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, transform.position, transform.rotation);
        }

        // 1. Play One-Shot Explosion Sound
        if (explosionSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(explosionSound, explosionVolume);
        }

        // Ensure the material is locked to the final burnt color
        if (tankMaterial != null)
        {
            tankMaterial.color = burntColor;
        }

        // 2. Play fire VFX & looping fire SFX for 5 seconds
        if (fireParticleSystem != null)
        {
            StartCoroutine(BurnAndExtinguishRoutine());
        }
    }

    private IEnumerator BurnAndExtinguishRoutine()
    {
        // Start fire particles
        fireParticleSystem.Play();

        // Start looping fire sound audio
        if (fireLoopSound != null && audioSource != null)
        {
            audioSource.clip = fireLoopSound;
            audioSource.loop = true;
            audioSource.volume = fireVolume;
            audioSource.Play();
        }

        yield return new WaitForSeconds(fireDuration);

        // Extinguish particles
        fireParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        // Stop looping fire audio
        if (fireLoopSound != null && audioSource != null && audioSource.clip == fireLoopSound)
        {
            audioSource.Stop();
            audioSource.loop = false;
        }
    }
}