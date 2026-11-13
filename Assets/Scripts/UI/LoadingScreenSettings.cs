using UnityEngine;

/// <summary>Build-safe artwork references for the runtime loading overlay.</summary>
[CreateAssetMenu(fileName = "LoadingScreenSettings", menuName = "Death Derby/Loading Screen Settings")]
public class LoadingScreenSettings : ScriptableObject
{
    [Min(0f)] public float minimumDisplayTime = 1f;
    public Sprite desertImage;
    public Sprite pvpImage;
    public Sprite trainingImage;

    public Sprite GetArtwork(string sceneName)
    {
        switch (sceneName)
        {
            case "Coop":
            case "PVP": return pvpImage;
            case "Training": return trainingImage;
            default: return desertImage;
        }
    }
}
