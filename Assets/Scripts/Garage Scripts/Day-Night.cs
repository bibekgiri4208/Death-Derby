using UnityEngine;

public class SkyboxToggle : MonoBehaviour
{
    [Header("Skybox Materials")]
    [SerializeField] private Material daySkybox;
    [SerializeField] private Material nightSkybox;

    [Header("Fog Settings (Built-in)")]
    [SerializeField] private Color dayFogColor = new Color(0.5f, 0.6f, 0.7f);
    [SerializeField] private Color nightFogColor = new Color(0.05f, 0.05f, 0.1f);
    [Range(0f, 1f)][SerializeField] private float dayFogDensity = 0.01f;
    [Range(0f, 1f)][SerializeField] private float nightFogDensity = 0.05f;

    [Header("Optional Directional Light")]
    [SerializeField] private Light sunLight;

    private bool isNight = false;

    void Start()
    {
        // Set initial day states
        RenderSettings.skybox = daySkybox;
        RenderSettings.fogColor = dayFogColor;
        RenderSettings.fogDensity = dayFogDensity;
        DynamicGI.UpdateEnvironment();
    }

    public void ToggleDayNight()
    {
        isNight = !isNight;

        if (isNight)
        {
            RenderSettings.skybox = nightSkybox;
            RenderSettings.fogColor = nightFogColor;
            RenderSettings.fogDensity = nightFogDensity;
            if (sunLight != null) sunLight.gameObject.SetActive(false);
        }
        else
        {
            RenderSettings.skybox = daySkybox;
            RenderSettings.fogColor = dayFogColor;
            RenderSettings.fogDensity = dayFogDensity;
            if (sunLight != null) sunLight.gameObject.SetActive(true);
        }

        DynamicGI.UpdateEnvironment();
    }
}