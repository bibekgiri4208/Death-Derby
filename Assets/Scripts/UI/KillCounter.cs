using System.Collections;
using TMPro;
using UnityEngine;

public class KillCounter : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI killCountText;
    [SerializeField] private string textPrefix = "";

    [Header("Split Screen")]
    [Tooltip("Which player this counter belongs to (-1 = count environment kills, 0/1 = specific player).")]
    [SerializeField] private int playerIndex = 0;

    [Header("Pop Animation")]
    [Min(0.05f)]
    [SerializeField] private float popDuration = 0.35f;
    [Min(1f)]
    [SerializeField] private float popScale = 1.35f;
    [SerializeField] private AnimationCurve popCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.25f, 1f),
        new Keyframe(0.5f, 1.1f),
        new Keyframe(1f, 0f));

    private int killCount;
    private Vector3 baseScale;
    private Coroutine popRoutine;

    public TextMeshProUGUI KillCountText => killCountText;

    public int PlayerIndex => playerIndex;

    public void Configure(TextMeshProUGUI text, int player)
    {
        killCountText = text;
        playerIndex = player;
    }

    private void OnEnable()
    {
        ZombieAI.OnZombieKilled += OnZombieKilled;
    }

    private void OnDisable()
    {
        ZombieAI.OnZombieKilled -= OnZombieKilled;
        if (popRoutine != null)
        {
            StopCoroutine(popRoutine);
            popRoutine = null;
        }
        if (killCountText != null)
            killCountText.transform.localScale = baseScale;
    }

    private void Start()
    {
        if (killCountText != null)
            baseScale = killCountText.transform.localScale;
        UpdateText();
    }

    private void OnZombieKilled(int killerPlayerIndex)
    {
        if (playerIndex >= 0 && killerPlayerIndex != playerIndex)
            return;

        killCount++;
        UpdateText();
        PlayPopAnimation();
    }

    private void UpdateText()
    {
        if (killCountText != null)
            killCountText.text = textPrefix + killCount.ToString();
    }

    private void PlayPopAnimation()
    {
        if (popRoutine != null)
            StopCoroutine(popRoutine);
        popRoutine = StartCoroutine(PopAnimation());
    }

    private IEnumerator PopAnimation()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / popDuration;
            float eased = popCurve.Evaluate(t);
            float factor = 1f + (popScale - 1f) * eased;
            killCountText.transform.localScale = baseScale * factor;
            yield return null;
        }
        killCountText.transform.localScale = baseScale;
        popRoutine = null;
    }
}