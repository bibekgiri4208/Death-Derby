using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealthUI : MonoBehaviour
{
    [Header("References")]
    public Health playerHealth;
    public Slider healthSlider;
    public TMP_Text healthText;

    private void Start()
    {
        if (playerHealth == null)
            return;

        playerHealth.OnHealthChanged.AddListener(UpdateHealthUI);
        UpdateHealthUI(playerHealth.CurrentHealth, playerHealth.maxHealth);
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged.RemoveListener(UpdateHealthUI);
        }
    }

    private void UpdateHealthUI(int currentHealth, int maxHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        if (healthText != null)
        {
            healthText.text = "Health: " + currentHealth + " / " + maxHealth;
        }
    }
}