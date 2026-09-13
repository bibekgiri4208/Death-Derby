using TMPro;
using UnityEngine;

public class KillCounter : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI killCountText;
    [SerializeField] private string textPrefix = "";

    private int killCount;

    private void OnEnable()
    {
        ZombieAI.OnZombieKilled += OnZombieKilled;
    }

    private void OnDisable()
    {
        ZombieAI.OnZombieKilled -= OnZombieKilled;
    }

    private void Start()
    {
        UpdateText();
    }

    private void OnZombieKilled()
    {
        killCount++;
        UpdateText();
    }

    private void UpdateText()
    {
        if (killCountText != null)
            killCountText.text = textPrefix + killCount.ToString();
    }
}