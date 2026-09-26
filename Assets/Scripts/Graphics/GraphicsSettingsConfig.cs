using UnityEngine;

/// <summary>
/// Inspector-editable tuning values for the graphics menu. The manager is
/// created at runtime, so its values would never be visible in the Inspector;
/// this asset is what you actually edit.
///
/// Loaded from "Resources/Graphics/GraphicsSettingsConfig" at startup. If the
/// asset is missing the manager falls back to hard-coded defaults.
/// </summary>
[CreateAssetMenu(fileName = "GraphicsSettingsConfig", menuName = "Death Derby/Graphics Settings")]
public class GraphicsSettingsConfig : ScriptableObject
{
    [Header("Frame Rate")]
    [Tooltip("Frame rate caps offered by the FPS button. Use -1 for unlimited.")]
    public int[] frameRates = { 30, 60, 120, 180, -1 };
    [Tooltip("Frame rate used when the player has never changed the setting.")]
    public int defaultFrameRate = 180;
    [Tooltip("Turn v-sync off so the frame rate cap is respected.")]
    public bool disableVSync = true;

    [Header("Quality Presets")]
    public string[] presetNames = { "Low", "Medium", "High" };
    [Tooltip("Preset used when the player has never changed the setting.")]
    public int defaultPresetIndex = 2;

    [Header("Preset Values")]
    [Tooltip("Shadow distance used while Low is active. The shadow toggle still controls whether shadows render at all.")]
    public float lowShadowDistance = 20f;
    [Tooltip("Shadow distance used while Medium is active, capped by the engine's own value.")]
    public float mediumShadowDistance = 40f;
    [Range(0f, 1f)][Tooltip("Ambient intensity multiplier used while Medium is active.")]
    public float mediumAmbientScale = 0.6f;
    [Range(0f, 1f)][Tooltip("Reflection intensity multiplier used while Medium is active.")]
    public float mediumReflectionScale = 0.7f;
}
