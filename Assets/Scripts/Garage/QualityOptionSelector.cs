using System.Collections;
using TMPro;
using UnityEngine;

public class QualityOptionSelector : MonoBehaviour, IOptionSelector
{
    public const string DefaultPlayerPrefsKey = "GraphicsQuality";

    [Header("Options")]
    [SerializeField] private string[] presetNames = { "Low", "Medium", "High" };
    [SerializeField] private int defaultPresetIndex = 2;

    [Header("Scene Light")]
    [Tooltip("The directional light whose shadows are controlled by this preset.")]
    [SerializeField] private Light mainLight;

    [Header("Medium Preset")]
    [Tooltip("Shadow distance used while Medium is active, capped by the original value.")]
    [SerializeField] private float mediumShadowDistance = 40f;
    [Range(0f, 1f)][SerializeField] private float mediumAmbientScale = 0.6f;
    [Range(0f, 1f)][SerializeField] private float mediumReflectionScale = 0.7f;

    [Header("Display")]
    [SerializeField] private TMP_Text label;

    [Header("Slide Animation")]
    [SerializeField] private float slideDuration = 0.15f;
    [SerializeField] private float slideDistanceScale = 1f;

    [Header("Persistence")]
    [SerializeField] private string playerPrefsKey = DefaultPlayerPrefsKey;

    private int index;
    private Vector2 labelHome;
    private Coroutine slideRoutine;

    private float originalShadowDistance;
    private LightShadows originalLightShadows;
    private float originalAmbientIntensity;
    private float originalReflectionIntensity;

    void Awake()
    {
        if (mainLight == null) mainLight = FindAnyObjectByType<Light>();
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        if (label != null) labelHome = label.rectTransform.anchoredPosition;

        originalShadowDistance = QualitySettings.shadowDistance;
        originalLightShadows = mainLight != null ? mainLight.shadows : LightShadows.Soft;
        originalAmbientIntensity = RenderSettings.ambientIntensity;
        originalReflectionIntensity = RenderSettings.reflectionIntensity;
    }

    void Start()
    {
        index = LoadIndex();

        if (index != defaultPresetIndex) ApplyPreset();
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
        ApplyPreset();
        Save();
    }

    private void Step(int direction)
    {
        if (presetNames == null || presetNames.Length == 0) return;

        index = ((index + direction) % presetNames.Length + presetNames.Length) % presetNames.Length;

        ApplyPreset();
        Save();
        AnimateLabel(direction);
    }

    private void ApplyPreset()
    {
        switch (index)
        {
            case 0:
                ApplyLow();
                break;
            case 1:
                ApplyMedium();
                break;
            default:
                ApplyHigh();
                break;
        }
    }

    private void ApplyHigh()
    {
        QualitySettings.shadowDistance = originalShadowDistance;

        if (mainLight != null) mainLight.shadows = originalLightShadows;

        RenderSettings.ambientIntensity = originalAmbientIntensity;
        RenderSettings.reflectionIntensity = originalReflectionIntensity;
    }

    private void ApplyMedium()
    {
        QualitySettings.shadowDistance = Mathf.Min(originalShadowDistance, mediumShadowDistance);

        if (mainLight != null) mainLight.shadows = LightShadows.Hard;

        RenderSettings.ambientIntensity = originalAmbientIntensity * mediumAmbientScale;
        RenderSettings.reflectionIntensity = originalReflectionIntensity * mediumReflectionScale;
    }

    private void ApplyLow()
    {
        QualitySettings.shadowDistance = 0f;

        if (mainLight != null) mainLight.shadows = LightShadows.None;

        RenderSettings.ambientIntensity = 0f;
        RenderSettings.reflectionIntensity = 0f;
    }

    private string FormatLabel()
    {
        if (presetNames == null || presetNames.Length == 0) return string.Empty;
        if (index < 0 || index >= presetNames.Length) return string.Empty;

        return presetNames[index];
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
        if (presetNames == null || presetNames.Length == 0) return 0;

        int startIndex = defaultPresetIndex;
        if (startIndex < 0 || startIndex >= presetNames.Length) startIndex = presetNames.Length - 1;

        if (!string.IsNullOrEmpty(playerPrefsKey) && PlayerPrefs.HasKey(playerPrefsKey))
        {
            int saved = PlayerPrefs.GetInt(playerPrefsKey);
            if (saved >= 0 && saved < presetNames.Length) startIndex = saved;
        }

        return startIndex;
    }

    private void Save()
    {
        if (string.IsNullOrEmpty(playerPrefsKey)) return;

        PlayerPrefs.SetInt(playerPrefsKey, index);
        PlayerPrefs.Save();
    }
}