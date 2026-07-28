using System.Collections;
using UnityEngine;

public class OilTank : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    [Header("Visual Effects")]
    [SerializeField] private Renderer tankRenderer;
    [SerializeField] private Color damageColor = Color.red;
    [SerializeField] private ParticleSystem fireParticleSystem;
    [SerializeField] private float fireDuration = 5f;

    [Header("Explosion FX (Optional)")]
    [SerializeField] private GameObject explosionEffectPrefab;

    private Coroutine fireCoroutine;
    private bool isDestroyed = false;

    private void Start()
    {
        currentHealth = maxHealth;

        // Auto-assign renderer if not dragged in Inspector
        if (tankRenderer == null)
            tankRenderer = GetComponent<Renderer>();

        // Ensure fire particles don't auto-play on start
        if (fireParticleSystem != null)
            fireParticleSystem.Stop();
    }

    // Required by IDamageable interface
    public void TakeDamage(float damageAmount)
    {
        if (isDestroyed) return;

        currentHealth -= damageAmount;

        // 1. Change Material Color
        if (tankRenderer != null)
        {
            tankRenderer.material.color = damageColor;
        }

        // 2. Play Fire Particle Effect for set duration
        if (fireParticleSystem != null)
        {
            if (fireCoroutine != null)
                StopCoroutine(fireCoroutine);

            fireCoroutine = StartCoroutine(PlayFireRoutine());
        }

        // 3. Destroy check
        if (currentHealth <= 0)
        {
            Explode();
        }
    }

    private IEnumerator PlayFireRoutine()
    {
        fireParticleSystem.Play();

        yield return new WaitForSeconds(fireDuration);

        if (fireParticleSystem != null)
            fireParticleSystem.Stop();
    }

    private void Explode()
    {
        isDestroyed = true;

        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, transform.position, transform.rotation);
        }

        Destroy(gameObject);
    }
}