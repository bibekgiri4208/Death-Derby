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
    [Tooltip("Take the round length from the player's Match Timer preference (set on the Garage's Timer Cube).")]
    [SerializeField] private bool usePlayerPreference = true;

    [Tooltip("Length of a round in seconds, used only when 'Use Player Preference' is off.")]
    [Min(1f)]
    [SerializeField] private float roundDuration = 180f;

    [Tooltip("When the clock runs out, award the round to the player with the most kills. " +
             "Equal kill counts are a draw.")]
    [SerializeField] private bool decideRoundOnTimeout = true;

    public float Remaining => remaining;
    public float Duration => roundDuration;

    private float remaining;
    private bool running;
    private int lastDisplayedSecond = -1;

    private void Awake()
    {
        if (timerText == null)
            timerText = GetComponentInChildren<TextMeshProUGUI>(true);

        if (usePlayerPreference)
            roundDuration = MatchTimerSettings.Duration;

        remaining = roundDuration;
    }

    private void OnEnable()
    {
        if (usePlayerPreference)
            MatchTimerSettings.DurationChanged += HandleDurationChanged;
    }

    private void OnDisable()
    {
        MatchTimerSettings.DurationChanged -= HandleDurationChanged;
    }

    /// <summary>
    /// Re-arms the clock when the preference changes mid-round, so the on-screen
    /// timer matches the new setting straight away instead of next scene load.
    /// </summary>
    private void HandleDurationChanged(float duration)
    {
        // A round that already timed out has handed the win over, so leave it alone.
        if (!running) return;

        roundDuration = duration;
        remaining = duration;
        UpdateText(force: true);
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
