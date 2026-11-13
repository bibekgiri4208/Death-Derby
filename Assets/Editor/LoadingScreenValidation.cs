using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Checks the loading overlay without opening or modifying gameplay scenes.</summary>
public static class LoadingScreenValidation
{
    [MenuItem("Death Derby/Tests/Validate Loading Screens")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run the loading screen checks outside Play mode.");

        var settings = Resources.Load<LoadingScreenSettings>("DerbyUI/LoadingScreenSettings");
        Require(settings != null, "Loading screen settings are missing from Resources.");
        string[] scenes = { "Desert", "Coop", "Training" };
        string[] labels = { "DESERT", "PVP", "TRAINING" };
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

        for (int i = 0; i < scenes.Length; i++)
        {
            Sprite artwork = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/2D UI Png/Loading image " + (i + 1) + ".png");
            Require(artwork != null && settings.GetArtwork(scenes[i]) == artwork, "Incorrect artwork for " + scenes[i]);
            Require(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == "Assets/Scenes/" + scenes[i] + ".unity"),
                scenes[i] + " is not enabled in the build scene list.");

            var host = new GameObject("Loading overlay validation", typeof(RectTransform));
            host.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var manager = host.AddComponent<LoadingScreenManager>();
                typeof(LoadingScreenManager).GetField("settings", flags).SetValue(manager, settings);
                typeof(LoadingScreenManager).GetMethod("BuildUI", flags).Invoke(manager, new object[] { scenes[i] });
                Canvas canvas = host.GetComponent<Canvas>();
                Require(canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.sortingOrder == short.MaxValue,
                    "Loading canvas must cover the game's cameras and menus.");
                Require(host.GetComponent<GraphicRaycaster>() != null, "Loading overlay cannot block UI input.");
                Image image = host.GetComponentsInChildren<Image>().Single(candidate => candidate.name == "Loading Artwork");
                Require(image.sprite == artwork, "Overlay did not apply the correct image.");
                Require(image.GetComponent<AspectRatioFitter>().aspectMode == AspectRatioFitter.AspectMode.EnvelopeParent,
                    "Artwork must fill the screen without distortion.");
                Require(host.GetComponentsInChildren<TextMeshProUGUI>().Any(label => label.text == labels[i]), "Incorrect mode label.");
                Require(host.GetComponentsInChildren<Image>().Single(candidate => candidate.name == "Input Blocker").raycastTarget,
                    "Loading overlay does not block clicks.");

                foreach (float progress in new[] { 0f, 0.5f, 1f })
                {
                    typeof(LoadingScreenManager).GetMethod("SetProgress", flags).Invoke(manager, new object[] { progress });
                    RectTransform fill = (RectTransform)host.transform.Find("Input Blocker/Loading Status/Progress Track/Progress Fill");
                    Require(Mathf.Approximately(fill.anchorMax.x, progress), "Progress bar width is incorrect.");
                    Require(host.GetComponentsInChildren<TextMeshProUGUI>().Any(label => label.name == "Progress Text" &&
                        label.text == Mathf.FloorToInt(progress * 100) + "%"), "Progress percentage is incorrect.");
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        Require(settings.GetArtwork("PVP") == settings.pvpImage, "PVP must use image 2.");
        Require(!EditorBuildSettings.scenes.Any(scene => scene.path == "Assets/Scenes/Loading Screen.unity"),
            "The deleted loading scene is still listed in build settings.");
        Debug.Log("LOADING_SCREENS_PASS: Desert/PVP/Training artwork, full-screen canvas, input blocker, labels, progress, and build scene list.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
