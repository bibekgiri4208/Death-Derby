using System;
using UnityEngine;

/// <summary>
/// Player preference for how long a match lasts. The choice is made on the
/// Garage's "Timer Cube" and stored in PlayerPrefs, so it follows the player
/// into every scene.
///
/// RoundTimer subscribes to <see cref="DurationChanged"/> so changing the
/// preference re-arms a round that is already running instead of only taking
/// effect on the next scene load.
/// </summary>
public static class MatchTimerSettings
{
    public const string DurationPrefsKey = "MatchDurationSeconds";
    public const int DefaultIndex = 2;

    private static readonly float[] DurationOptions = { 60f, 120f, 180f, 300f, 600f };

    // -1 means "not read from PlayerPrefs yet".
    private static int index = -1;

    /// <summary>Raised with the new duration whenever the preference changes.</summary>
    public static event Action<float> DurationChanged;

    public static float[] Options => DurationOptions;

    public static int Index
    {
        get
        {
            EnsureLoaded();
            return index;
        }
    }

    /// <summary>Chosen match length, in seconds.</summary>
    public static float Duration
    {
        get
        {
            EnsureLoaded();
            return DurationOptions[index];
        }
    }

    public static void Step(int direction)
    {
        EnsureLoaded();

        int count = DurationOptions.Length;
        SetIndex(((index + direction) % count + count) % count);
    }

    public static void SetIndex(int newIndex)
    {
        EnsureLoaded();

        newIndex = Mathf.Clamp(newIndex, 0, DurationOptions.Length - 1);
        if (newIndex == index) return;

        index = newIndex;

        PlayerPrefs.SetFloat(DurationPrefsKey, DurationOptions[index]);
        PlayerPrefs.Save();

        DurationChanged?.Invoke(DurationOptions[index]);
    }

    /// <summary>
    /// Label for the menu cube in whole minutes, e.g. "3 min". Every option is a
    /// multiple of 60, so minutes alone are enough to read on the cube.
    /// </summary>
    public static string GetLabel(int optionIndex)
    {
        if (optionIndex < 0 || optionIndex >= DurationOptions.Length)
            return string.Empty;

        return Mathf.RoundToInt(DurationOptions[optionIndex] / 60f) + " min";
    }

    private static void EnsureLoaded()
    {
        if (index >= 0) return;

        index = DefaultIndex;

        if (!PlayerPrefs.HasKey(DurationPrefsKey)) return;

        float saved = PlayerPrefs.GetFloat(DurationPrefsKey, DurationOptions[DefaultIndex]);
        index = NearestIndex(saved);
    }

    /// <summary>Snaps a stored duration back onto the closest available option.</summary>
    private static int NearestIndex(float seconds)
    {
        int best = 0;
        float bestDelta = float.MaxValue;

        for (int i = 0; i < DurationOptions.Length; i++)
        {
            float delta = Mathf.Abs(DurationOptions[i] - seconds);
            if (delta >= bestDelta) continue;

            bestDelta = delta;
            best = i;
        }

        return best;
    }
}
