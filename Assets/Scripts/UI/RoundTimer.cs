using TMPro;
using UnityEngine;

/// <summary>
/// Counts the round down and, when it runs out, awards the round to whichever player
/// has the most kills. Rounds normally end sooner because one of the cars is destroyed,
/// which freezes this clock along with the rest of the gameplay.
/// </summary>
public class RoundTimer : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Label showing the remaining time as MM:SS. Leave empty to use a TMP text on this object.")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Round")]
    [Tooltip("Length of a round in seconds.")]
    [Min(1f)]
    [SerializeField] private float roundDuration = 180f;

    [Tooltip("When the clock runs out, award the round to the player with the most kills. " +
             "Equal kill counts are a draw.")]
    [SerializeField] private bool decideRoundOnTimeout = true;

    public float Remaining => remaining;

    private float remaining;
    private bool running;
    private int lastDisplayedSecond = -1;

    private void Awake()
    {
        if (timerText == null)
            timerText = GetComponentInChildren<TextMeshProUGUI>(true);

        remaining = roundDuration;
    }

    private void Start()
    {
        running = true;
        UpdateText(force: true);
    }

    private void Update()
    {
        if (!running)
            return;

        // Scaled time, so both the pause menu and the result screen stop the clock.
        remaining -= Time.deltaTime;

        if (remaining > 0f)
        {
            UpdateText(force: false);
            return;
        }

        remaining = 0f;
        running = false;
        UpdateText(force: true);
        OnRoundTimedOut();
    }

    // Only touches the label when the displayed second changes, so the text mesh is
    // rebuilt once a second rather than every frame.
    private void UpdateText(bool force)
    {
        if (timerText == null)
            return;

        int second = Mathf.CeilToInt(remaining);
        if (!force && second == lastDisplayedSecond)
            return;

        lastDisplayedSecond = second;
        timerText.text = $"{second / 60:00}:{second % 60:00}";
    }

    private void OnRoundTimedOut()
    {
        if (!decideRoundOnTimeout)
            return;

        ScoreBoard board = FindAnyObjectByType<ScoreBoard>();
        if (board == null)
        {
            Debug.LogWarning("RoundTimer: no ScoreBoard in this scene, cannot show the result.", this);
            return;
        }

        board.Show(timeExpired: true);
    }
}
