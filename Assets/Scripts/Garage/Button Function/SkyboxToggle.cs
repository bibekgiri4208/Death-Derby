using UnityEngine;

public class SkyboxToggle : MonoBehaviour
{
    public enum TimeOfDayState { Day, Night, Rainy }

    [Header("Current State")]
    [SerializeField] private TimeOfDayState currentState = TimeOfDayState.Day;

    [Header("Skybox Materials")]
    [SerializeField] private Material daySkybox;
    [SerializeField] private Material nightSkybox;
    [SerializeField] private Material rainySkybox;

    [Header("Fog Settings (Built-in)")]
    [SerializeField] private Color dayFogColor = new Color(0.5f, 0.6f, 0.7f);
    [SerializeField] private Color nightFogColor = new Color(0.05f, 0.05f, 0.1f);
    [SerializeField] private Color rainyFogColor = new Color(0.2f, 0.25f, 0.3f);

    [Range(0f, 1f)][SerializeField] private float dayFogDensity = 0.01f;
    [Range(0f, 1f)][SerializeField] private float nightFogDensity = 0.05f;
    [Range(0f, 1f)][SerializeField] private float rainyFogDensity = 0.08f;

    [Header("Optional Directional Light")]
    [SerializeField] private Light sunLight;
    [Range(0f, 1f)][SerializeField] private float rainyLightIntensity = 0.2f;
    private float defaultSunIntensity = 1f;

    [Header("Rain Effects")]
    [SerializeField] private ParticleSystem rainParticleEffect;

    void Start()
    {
        if (sunLight != null)
        {
            defaultSunIntensity = sunLight.intensity;
        }

        ApplyState(currentState);
    }

    private void OnMouseDown()
    {
        ToggleDayNight();
    }

    public void ToggleDayNight()
    {
        // Cycle through states: Day -> Night -> Rainy -> Day
        switch (currentState)
        {
            case TimeOfDayState.Day:
                currentState = TimeOfDayState.Night;
                break;
            case TimeOfDayState.Night:
                currentState = TimeOfDayState.Rainy;
                break;
            case TimeOfDayState.Rainy:
                currentState = TimeOfDayState.Day;
                break;
        }

        ApplyState(currentState);
    }

    private void ApplyState(TimeOfDayState state)
    {
        switch (state)
        {
            case TimeOfDayState.Day:
                RenderSettings.skybox = daySkybox;
                RenderSettings.fogColor = dayFogColor;
                RenderSettings.fogDensity = dayFogDensity;

                if (sunLight != null)
                {
                    sunLight.gameObject.SetActive(true);
                    sunLight.intensity = defaultSunIntensity;
                }

                if (rainParticleEffect != null)
                {
                    rainParticleEffect.Stop();
                }
                break;

            case TimeOfDayState.Night:
                RenderSettings.skybox = nightSkybox;
                RenderSettings.fogColor = nightFogColor;
                RenderSettings.fogDensity = nightFogDensity;

                if (sunLight != null)
                {
                    sunLight.gameObject.SetActive(false);
                }

                if (rainParticleEffect != null)
                {
                    rainParticleEffect.Stop();
                }
                break; 

            case TimeOfDayState.Rainy:
                RenderSettings.skybox = rainySkybox;
                RenderSettings.fogColor = rainyFogColor;
                RenderSettings.fogDensity = rainyFogDensity;

                if (sunLight != null)
                {
                    sunLight.gameObject.SetActive(true);
                    sunLight.intensity = rainyLightIntensity; // Dim the light for overcast look
                }

                if (rainParticleEffect != null)
                {
                    rainParticleEffect.Play();
                }
                break;
        }

        DynamicGI.UpdateEnvironment();
    }
}