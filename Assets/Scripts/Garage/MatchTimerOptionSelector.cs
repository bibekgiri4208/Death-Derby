using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Scene button for the match length. The preference itself lives in
/// MatchTimerSettings, so the choice follows the player into every scene.
/// </summary>
public class MatchTimerOptionSelector : MonoBehaviour, IOptionSelector
{
    public const string DefaultPlayerPrefsKey = MatchTimerSettings.DurationPrefsKey;

    [Header("Display")]
    [Tooltip("Shown before the duration. Leave empty for a bare number of minutes.")]
    [SerializeField] private string textPrefix = "";
    [SerializeField] private TMP_Text label;

    [Header("Slide Animation")]
    [SerializeField] private float slideDuration = 0.15f;
    [SerializeField] private float slideDistanceScale = 1f;

    private Vector2 labelHome;
    private Coroutine slideRoutine;

    public int CurrentIndex => MatchTimerSettings.Index;

    void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        if (label != null) labelHome = label.rectTransform.anchoredPosition;
    }

    void Start()
    {
        if (label != null) label.text = FormatLabel();
        SnapLabelHome();
    }

    public void Next()
    {
        Step(1);
    }

    public void Previous()
    {
        Step(-1);
    }

    public void Apply()
    {
        // MatchTimerSettings already writes to PlayerPrefs and notifies any running
        // RoundTimer the moment the index changes, so there is nothing left to do.
    }

    private void Step(int direction)
    {
        MatchTimerSettings.Step(direction);
        AnimateLabel(direction);
    }

    private string FormatLabel()
    {
        return textPrefix + MatchTimerSettings.GetLabel(MatchTimerSettings.Index);
    }

    private void AnimateLabel(int direction)
    {
        if (label == null) return;

        if (slideRoutine != null) StopCoroutine(slideRoutine);

        if (isActiveAndEnabled) slideRoutine = StartCoroutine(SlideLabel(direction));
        else
        {
            label.text = FormatLabel();
            SnapLabelHome();
        }
    }

    private IEnumerator SlideLabel(int direction)
    {
        string nextText = FormatLabel();
        float half = Mathf.Max(0.01f, slideDuration * 0.5f);
        float distance = GetSlideDistance();

        float t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / half);
            label.rectTransform.anchoredPosition = labelHome + new Vector2(-direction * distance * k, 0f);
            yield return null;
        }

        label.text = nextText;

        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / half);
            label.rectTransform.anchoredPosition = labelHome + new Vector2(direction * distance * (1f - k), 0f);
            yield return null;
        }

        SnapLabelHome();
        slideRoutine = null;
    }

    private float GetSlideDistance()
    {
        float cubeWorldWidth = 1f;
        if (TryGetComponent(out Renderer cubeRenderer))
        {
            cubeWorldWidth = cubeRenderer.localBounds.size.x * transform.lossyScale.x;
        }

        Transform parent = label.rectTransform.parent;
        float parentScale = parent != null ? Mathf.Abs(parent.lossyScale.x) : 1f;
        if (parentScale < 0.0001f) parentScale = 1f;

        return Mathf.Max(cubeWorldWidth / parentScale * slideDistanceScale, 0.0001f);
    }

    private void SnapLabelHome()
    {
        if (label != null) label.rectTransform.anchoredPosition = labelHome;
    }
}
