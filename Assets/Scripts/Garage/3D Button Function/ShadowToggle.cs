using UnityEngine;

/// <summary>
/// Scene button for the shadow toggle. All state and application live in
/// GraphicsSettingsManager, so the choice follows the player into every scene.
/// </summary>
public class ShadowToggle : MonoBehaviour
{
    public const string DefaultPlayerPrefsKey = GraphicsSettingsManager.ShadowsPrefsKey;

    // Fallback only, used if the manager is somehow absent. The manager is
    // created before any scene loads, so normally this is never consulted.
    [SerializeField] private bool shadowsEnabled = true;

    public bool IsEnabled => GraphicsSettingsManager.Exists
        ? GraphicsSettingsManager.Instance.ShadowsEnabled
        : shadowsEnabled;

    private void OnMouseDown()
    {
        ToggleShadows();
    }

    public void ToggleShadows()
    {
        SetShadowsEnabled(!IsEnabled);
    }

    public void SetShadowsEnabled(bool enabled)
    {
        shadowsEnabled = enabled;

        if (GraphicsSettingsManager.Exists)
        {
            GraphicsSettingsManager.Instance.SetShadowsEnabled(enabled);
            return;
        }

        Debug.LogError("ShadowToggle: GraphicsSettingsManager is missing, shadow toggle is inactive.", this);
    }
}
