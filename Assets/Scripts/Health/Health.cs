using System;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    // Public properties that PlayerHealthUI needs
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    // Event triggered whenever health changes (Action<currentHealth, maxHealth>)
    public event Action<float, float> OnHealthChanged;

    private void Start()
    {
        currentHealth = maxHealth;
        // Trigger initial update for UI
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    // Required by IDamageable interface for Bullet.cs
    public void TakeDamage(float damageAmount)
    {
        if (currentHealth <= 0) return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // Notify UI or other scripts listening to health changes
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        Debug.Log($"{gameObject.name} took {damageAmount} damage. Remaining: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Optional helper method if you want to heal the player or entity later
    /// </summary>
    public void Heal(float healAmount)
    {
        if (currentHealth <= 0) return;

        currentHealth += healAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} died!");
        Destroy(gameObject);
    }
}