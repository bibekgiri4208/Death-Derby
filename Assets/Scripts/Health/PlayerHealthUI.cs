using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthUI : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Slider driven by this player's health. Leave empty to use the Slider on this object.")]
    [SerializeField] private Slider healthSlider;

    [Header("Split Screen")]
    [Tooltip("Which player this bar belongs to (0/1). Inferred from the object name when left as -1.")]
    [SerializeField] private int playerIndex = -1;

    private Health health;

    private void Awake()
    {
        if (healthSlider == null)
            healthSlider = GetComponent<Slider>();

        if (playerIndex < 0)
            playerIndex = gameObject.name.Contains("(Player 2)") ? 1 : 0;

        if (healthSlider != null)
            healthSlider.value = 1f;
    }

    private void Start()
    {
        BindToPlayer();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void BindToPlayer()
    {
        if (health != null) return;

        Transform player = SplitScreenMode.GetPlayer(playerIndex);
        if (player == null) return;

        health = player.GetComponent<Health>();
        bool isNew = health == null;
        if (isNew)
            health = player.gameObject.AddComponent<PlayerHealth>();

        health.OnHealthChanged += OnHealthChanged;

        if (isNew || health.CurrentHealth <= 0f)
            health.ResetHealth();
        else
            OnHealthChanged(health.CurrentHealth, health.MaxHealth);
    }

    private void Unbind()
    {
        if (health != null)
        {
            health.OnHealthChanged -= OnHealthChanged;
            health = null;
        }
    }

    private void OnHealthChanged(float current, float max)
    {
        if (healthSlider == null) return;
        healthSlider.value = max > 0f ? current / max : 0f;
    }
}