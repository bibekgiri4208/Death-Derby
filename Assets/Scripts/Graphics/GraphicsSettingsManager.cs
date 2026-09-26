using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Single global owner of the game's graphics options: frame rate, quality
/// preset and the shadow toggle. Options chosen in the Garage are stored in
/// PlayerPrefs and re-applied every time a scene loads, so they also apply in
/// Coop, Desert and any other scene.
///
/// Frame rate and QualitySettings.shadowDistance are engine-global and survive
/// scene loads on their own. Light.shadows and RenderSettings.* are stored
/// per scene, so those are re-applied from the freshly loaded scene's own
/// authored values on every sceneLoaded event.
/// </summary>
public class GraphicsSettingsManager : MonoBehaviour
{
    public const string FpsPrefsKey = "FpsLimit";
    public const string QualityPrefsKey = "GraphicsQuality";
    public const string ShadowsPrefsKey = "ShadowsEnabled";

    private const int LowPreset = 0;
    private const int MediumPreset = 1;

    // Where the Inspector-editable tuning values live. Matches the project's
    // existing "Resources/<Category>/<Name>" convention, e.g. BloodSplash.
    private const string ConfigResourcePath = "Graphics/GraphicsSettingsConfig";

    // Fallbacks, used only if the config asset is missing or incomplete.
    private static readonly int[] FallbackFrameRates = { 30, 60, 120, 180, -1 };
    private const int FallbackDefaultFrameRate = 180;
    private const bool FallbackDisableVSync = true;
    private static readonly string[] FallbackPresetNames = { "Low", "Medium", "High" };
    private const int FallbackDefaultPresetIndex = 2;
    private const float FallbackLowShadowDistance = 20f;
    private const float FallbackMediumShadowDistance = 40f;
    private const float FallbackMediumAmbientScale = 0.6f;
    private const float FallbackMediumReflectionScale = 0.7f;

    private static GraphicsSettingsManager instance;

    public static bool Exists => instance != null;
    public static GraphicsSettingsManager Instance => instance;

    private GraphicsSettingsConfig config;

    private int[] frameRates = FallbackFrameRates;
    private int defaultFrameRate = FallbackDefaultFrameRate;
    private bool disableVSync = FallbackDisableVSync;
    private string[] presetNames = FallbackPresetNames;
    private int defaultPresetIndex = FallbackDefaultPresetIndex;
    private float lowShadowDistance = FallbackLowShadowDistance;
    private float mediumShadowDistance = FallbackMediumShadowDistance;
    private float mediumAmbientScale = FallbackMediumAmbientScale;
    private float mediumReflectionScale = FallbackMediumReflectionScale;

    public GraphicsSettingsConfig Config => config;
    public int[] FrameRateOptions => frameRates;
    public string[] QualityPresetNames => presetNames;

    public int FrameRateIndex { get; private set; }
    public int QualityIndex { get; private set; }
    public bool ShadowsEnabled { get; private set; }

    public int FrameRate => FrameRateIndex >= 0 && FrameRateIndex < frameRates.Length
        ? frameRates[FrameRateIndex]
        : -1;

    public bool IsNoLimit => FrameRate < 0;

    // QualitySettings.shadowDistance is engine-global, so its authored value is
    // captured once and reused for every scene.
    private bool hasShadowBaseline;
    private float baselineShadowDistance;

    // Captured fresh on every scene load, because these come from the scene.
    private bool hasSceneBaseline;
    private float baselineAmbientIntensity;
    private float baselineReflectionIntensity;
    private Light mainLight;
    private LightShadows baselineLightShadows;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (instance != null) return;

        GameObject host = new GameObject("Graphics Settings Manager");
        DontDestroyOnLoad(host);
        host.AddComponent<GraphicsSettingsManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        LoadConfig();

        FrameRateIndex = IndexOfValue(frameRates, defaultFrameRate);
        if (FrameRateIndex < 0) FrameRateIndex = 0;

        QualityIndex = ClampIndex(defaultPresetIndex);
        ShadowsEnabled = true;

        LoadState();

        // Apply straight away so the saved frame rate is in effect before the
        // first scene's Start, rather than one frame later.
        ApplyFrameRate();

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        if (!hasSceneBaseline) CaptureSceneBaseline();
        ApplyAll();
    }

    /// <summary>
    /// Pulls the tuning values out of the editable config asset. Falls back to
    /// hard-coded defaults if the asset is missing or a field was left blank.
    /// </summary>
    private void LoadConfig()
    {
        config = Resources.Load<GraphicsSettingsConfig>(ConfigResourcePath);

        if (config == null)
        {
            Debug.LogWarning(
                $"GraphicsSettingsManager: no config asset at \"Resources/{ConfigResourcePath}\", " +
                "using built-in defaults. Create one via Assets > Create > Death Derby > Graphics Settings.");
            return;
        }

        if (config.frameRates != null && config.frameRates.Length > 0)
            frameRates = config.frameRates;

        if (config.presetNames != null && config.presetNames.Length > 0)
            presetNames = config.presetNames;

        defaultFrameRate = config.defaultFrameRate;
        disableVSync = config.disableVSync;
        defaultPresetIndex = config.defaultPresetIndex;
        lowShadowDistance = config.lowShadowDistance;
        mediumShadowDistance = config.mediumShadowDistance;
        mediumAmbientScale = config.mediumAmbientScale;
        mediumReflectionScale = config.mediumReflectionScale;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CaptureSceneBaseline();
        ApplyAll();
    }

    // Records what the newly loaded scene asked for, before we scale it.
    private void CaptureSceneBaseline()
    {
        if (!hasShadowBaseline)
        {
            baselineShadowDistance = QualitySettings.shadowDistance;
            hasShadowBaseline = true;
        }

        baselineAmbientIntensity = RenderSettings.ambientIntensity;
        baselineReflectionIntensity = RenderSettings.reflectionIntensity;

        mainLight = FindMainLight();
        baselineLightShadows = mainLight != null ? mainLight.shadows : LightShadows.Soft;

        hasSceneBaseline = true;
    }

    public void ApplyAll()
    {
        ApplyFrameRate();
        ApplyShadowsAndQuality();
    }

    private void ApplyFrameRate()
    {
        if (disableVSync) QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = IsNoLimit ? -1 : FrameRate;
    }

    /// <summary>
    /// The shadow toggle is the master on/off for shadows. The quality preset
    /// only decides how expensive they are, and scales ambient/reflection.
    /// </summary>
    private void ApplyShadowsAndQuality()
    {
        if (!hasSceneBaseline) return;

        if (!ShadowsEnabled)
        {
            QualitySettings.shadowDistance = 0f;
            if (mainLight != null) mainLight.shadows = LightShadows.None;
        }
        else
        {
            switch (QualityIndex)
            {
                case LowPreset:
                    QualitySettings.shadowDistance = lowShadowDistance;
                    if (mainLight != null) mainLight.shadows = LightShadows.Hard;
                    break;

                case MediumPreset:
                    QualitySettings.shadowDistance = Mathf.Min(baselineShadowDistance, mediumShadowDistance);
                    if (mainLight != null) mainLight.shadows = LightShadows.Hard;
                    break;

                default:
                    QualitySettings.shadowDistance = baselineShadowDistance;
                    if (mainLight != null) mainLight.shadows = baselineLightShadows;
                    break;
            }
        }

        float ambientScale = QualityIndex == LowPreset ? 0f
            : QualityIndex == MediumPreset ? mediumAmbientScale
            : 1f;

        float reflectionScale = QualityIndex == LowPreset ? 0f
            : QualityIndex == MediumPreset ? mediumReflectionScale
            : 1f;

        RenderSettings.ambientIntensity = baselineAmbientIntensity * ambientScale;
        RenderSettings.reflectionIntensity = baselineReflectionIntensity * reflectionScale;
    }

    // ---- Frame rate ----

    public void StepFrameRate(int direction)
    {
        if (frameRates == null || frameRates.Length == 0) return;

        FrameRateIndex = ((FrameRateIndex + direction) % frameRates.Length + frameRates.Length) % frameRates.Length;
        SaveFps();
        ApplyAll();
    }

    public void SetFrameRateByIndex(int index)
    {
        if (frameRates == null || frameRates.Length == 0) return;

        FrameRateIndex = Mathf.Clamp(index, 0, frameRates.Length - 1);
        SaveFps();
        ApplyAll();
    }

    // ---- Quality preset ----

    public void StepQuality(int direction)
    {
        if (presetNames == null || presetNames.Length == 0) return;

        QualityIndex = ((QualityIndex + direction) % presetNames.Length + presetNames.Length) % presetNames.Length;
        SaveQuality();
        ApplyAll();
    }

    public void SetQualityByIndex(int index)
    {
        if (presetNames == null || presetNames.Length == 0) return;

        QualityIndex = Mathf.Clamp(index, 0, presetNames.Length - 1);
        SaveQuality();
        ApplyAll();
    }

    // ---- Shadow toggle ----

    public void SetShadowsEnabled(bool enabled)
    {
        ShadowsEnabled = enabled;
        SaveShadows();
        ApplyShadowsAndQuality();
    }

    public void ToggleShadows()
    {
        SetShadowsEnabled(!ShadowsEnabled);
    }

    // ---- Persistence ----

    private void LoadState()
    {
        if (PlayerPrefs.HasKey(FpsPrefsKey))
        {
            int found = IndexOfValue(frameRates, PlayerPrefs.GetInt(FpsPrefsKey));
            if (found >= 0) FrameRateIndex = found;
        }

        if (PlayerPrefs.HasKey(QualityPrefsKey))
        {
            int saved = PlayerPrefs.GetInt(QualityPrefsKey);
            if (saved >= 0 && saved < presetNames.Length) QualityIndex = saved;
        }

        if (PlayerPrefs.HasKey(ShadowsPrefsKey))
        {
            ShadowsEnabled = PlayerPrefs.GetInt(ShadowsPrefsKey, 1) != 0;
        }
    }

    private void SaveFps()
    {
        PlayerPrefs.SetInt(FpsPrefsKey, FrameRate);
        PlayerPrefs.Save();
    }

    private void SaveQuality()
    {
        PlayerPrefs.SetInt(QualityPrefsKey, QualityIndex);
        PlayerPrefs.Save();
    }

    private void SaveShadows()
    {
        PlayerPrefs.SetInt(ShadowsPrefsKey, ShadowsEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    private int ClampIndex(int index)
    {
        if (presetNames == null || presetNames.Length == 0) return 0;
        if (index < 0 || index >= presetNames.Length) return presetNames.Length - 1;
        return index;
    }

    private static int IndexOfValue(int[] values, int value)
    {
        if (values == null) return -1;
        return System.Array.IndexOf(values, value);
    }

    private static Light FindMainLight()
    {
        Light best = null;

        foreach (Light candidate in FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (candidate.type != LightType.Directional) continue;
            if (best == null || candidate.intensity > best.intensity) best = candidate;
        }

        return best;
    }
}
