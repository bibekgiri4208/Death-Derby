using UnityEngine;

public class ShadowToggle : MonoBehaviour
{
    public const string DefaultPlayerPrefsKey = "ShadowsEnabled";

    [Header("Shadows")]
    [Tooltip("The directional light whose shadows are toggled on and off.")]
    [SerializeField] private Light mainLight;
    [SerializeField] private bool shadowsEnabled = true;

    [Header("Persistence")]
    [SerializeField] private string playerPrefsKey = DefaultPlayerPrefsKey;

    private float originalShadowDistance;
    private LightShadows originalLightShadows;

    void Awake()
    {
        if (mainLight == null) mainLight = FindAnyObjectByType<Light>();

        originalShadowDistance = QualitySettings.shadowDistance;
        originalLightShadows = mainLight != null ? mainLight.shadows : LightShadows.Soft;
    }

    void Start()
    {
        shadowsEnabled = LoadEnabled();
        ApplyShadows();
    }

    private void OnMouseDown()
    {
        ToggleShadows();
    }

    public void ToggleShadows()
    {
        SetShadowsEnabled(!shadowsEnabled);
    }

    public void SetShadowsEnabled(bool enabled)
    {
        shadowsEnabled = enabled;
        ApplyShadows();
        Save();
    }

    public bool IsEnabled => shadowsEnabled;

    private void ApplyShadows()
    {
        if (shadowsEnabled)
        {
            QualitySettings.shadowDistance = originalShadowDistance;
            if (mainLight != null) mainLight.shadows = originalLightShadows;
        }
        else
        {
            QualitySettings.shadowDistance = 0f;
            if (mainLight != null) mainLight.shadows = LightShadows.None;
        }
    }

    private bool LoadEnabled()
    {
        if (string.IsNullOrEmpty(playerPrefsKey) || !PlayerPrefs.HasKey(playerPrefsKey)) return shadowsEnabled;

        return PlayerPrefs.GetInt(playerPrefsKey, shadowsEnabled ? 1 : 0) != 0;
    }

    private void Save()
    {
        if (string.IsNullOrEmpty(playerPrefsKey)) return;

        PlayerPrefs.SetInt(playerPrefsKey, shadowsEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }
}