using System.Collections;
using TMPro;
using UnityEngine;

public class FpsOptionSelector : MonoBehaviour
{
    public const string DefaultPlayerPrefsKey = "FpsLimit";

    [Header("Options")]
    [SerializeField] private int[] frameRates = { 30, 60, 120, 180, -1 };
    [SerializeField] private int defaultFrameRate = 180;
    [SerializeField] private string textPrefix = "FPS ";
    [SerializeField] private string noLimitText = "No Limit";

    [Header("Display")]
    [SerializeField] private TMP_Text label;

    [Header("Slide Animation")]
    [SerializeField] private float slideDuration = 0.15f;
    [SerializeField] private float slideDistanceScale = 1f;

    [Header("Frame Rate")]
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool disableVSync = true;

    [Header("Persistence")]
    [SerializeField] private string playerPrefsKey = DefaultPlayerPrefsKey;

    private int index;
    private Vector2 labelHome;
    private Coroutine slideRoutine;

    public int CurrentFrameRate => frameRates[index];
    public bool IsNoLimit => frameRates[index] < 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedFrameRateOnLoad()
    {
        if (!PlayerPrefs.HasKey(DefaultPlayerPrefsKey)) return;

        int saved = PlayerPrefs.GetInt(DefaultPlayerPrefsKey);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = saved < 0 ? -1 : saved;
    }

    void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        if (label != null) labelHome = label.rectTransform.anchoredPosition;
    }

    void Start()
    {
        index = LoadIndex();

        if (applyOnStart) ApplyFrameRate();
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
        ApplyFrameRate();
        Save();
    }

    private void Step(int direction)
    {
        if (frameRates == null || frameRates.Length == 0) return;

        index = ((index + direction) % frameRates.Length + frameRates.Length) % frameRates.Length;

        ApplyFrameRate();
        Save();
        AnimateLabel(direction);
    }

    private void ApplyFrameRate()
    {
        if (frameRates == null || frameRates.Length == 0) return;

        if (disableVSync) QualitySettings.vSyncCount = 0;

        Application.targetFrameRate = IsNoLimit ? -1 : frameRates[index];
    }

    private string FormatLabel()
    {
        if (frameRates == null || frameRates.Length == 0) return textPrefix;
        if (IsNoLimit) return noLimitText;

        return textPrefix + frameRates[index];
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

    private int LoadIndex()
    {
        if (frameRates == null || frameRates.Length == 0) return 0;

        int startIndex = System.Array.IndexOf(frameRates, defaultFrameRate);
        if (startIndex < 0) startIndex = 0;

        if (!string.IsNullOrEmpty(playerPrefsKey) && PlayerPrefs.HasKey(playerPrefsKey))
        {
            int saved = PlayerPrefs.GetInt(playerPrefsKey);
            int found = System.Array.IndexOf(frameRates, saved);
            if (found >= 0) startIndex = found;
        }

        return startIndex;
    }

    private void Save()
    {
        if (string.IsNullOrEmpty(playerPrefsKey)) return;

        PlayerPrefs.SetInt(playerPrefsKey, frameRates[index]);
        PlayerPrefs.Save();
    }
}
