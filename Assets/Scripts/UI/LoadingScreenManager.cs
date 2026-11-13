using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds a persistent loading overlay in the current scene, then loads the
/// destination asynchronously. No dedicated loading scene or scene wiring is needed.
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    public static LoadingScreenManager Instance { get; private set; }

    private LoadingScreenSettings settings;
    private TextMeshProUGUI loadingText;
    private TextMeshProUGUI progressText;
    private RectTransform progressFill;
    private bool isLoading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Shows the destination's artwork before beginning the scene load.</summary>
    public static void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || (Instance != null && Instance.isLoading))
            return;

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("LoadingScreenManager: scene '" + sceneName + "' is not enabled in the build scene list.");
            return;
        }

        if (Instance == null)
            new GameObject("Loading Screen Overlay", typeof(RectTransform)).AddComponent<LoadingScreenManager>();

        Instance.isLoading = true;
        Instance.settings = Resources.Load<LoadingScreenSettings>("DerbyUI/LoadingScreenSettings");
        Instance.BuildUI(sceneName);
        Instance.StartCoroutine(Instance.LoadSceneAsync(sceneName));
    }

    private void Update()
    {
        if (isLoading && loadingText != null)
            loadingText.text = "LOADING" + new string('.', Mathf.FloorToInt(Mathf.Repeat(Time.unscaledTime * 2f, 4f)));
    }

    private void BuildUI(string sceneName)
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        RectTransform backdrop = Rect("Input Blocker", transform, Vector2.zero, Vector2.one);
        Image blocker = backdrop.gameObject.AddComponent<Image>();
        blocker.color = Color.black;
        blocker.raycastTarget = true;

        Sprite artwork = settings != null ? settings.GetArtwork(sceneName) : null;
        if (artwork != null)
        {
            RectTransform background = Rect("Loading Artwork", backdrop, Vector2.zero, Vector2.one);
            Image image = background.gameObject.AddComponent<Image>();
            image.sprite = artwork;
            image.raycastTarget = false;
            // Fill any screen aspect ratio without stretching the artwork.
            AspectRatioFitter fitter = background.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = artwork.rect.width / artwork.rect.height;
        }

        RectTransform footer = Rect("Loading Status", backdrop, Vector2.zero, new Vector2(1, 0));
        footer.pivot = new Vector2(0.5f, 0);
        footer.sizeDelta = new Vector2(0, 174);
        Image shade = footer.gameObject.AddComponent<Image>();
        shade.color = new Color(0.025f, 0.03f, 0.04f, 0.88f);
        shade.raycastTarget = false;

        TextMeshProUGUI mode = Label("Mode", footer, GetModeLabel(sceneName), 34,
            TextAlignmentOptions.MidlineLeft, new Vector2(0.05f, 0.57f), new Vector2(0.6f, 0.92f));
        mode.fontStyle = FontStyles.Bold;
        mode.color = new Color(1f, 0.78f, 0.18f);
        loadingText = Label("Loading Text", footer, "LOADING", 24,
            TextAlignmentOptions.MidlineLeft, new Vector2(0.05f, 0.3f), new Vector2(0.7f, 0.56f));
        progressText = Label("Progress Text", footer, "0%", 26,
            TextAlignmentOptions.MidlineRight, new Vector2(0.75f, 0.3f), new Vector2(0.95f, 0.56f));

        RectTransform track = Rect("Progress Track", footer, new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.23f));
        Image trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = new Color(1, 1, 1, 0.18f);
        trackImage.raycastTarget = false;
        progressFill = Rect("Progress Fill", track, Vector2.zero, new Vector2(0, 1));
        Image fillImage = progressFill.gameObject.AddComponent<Image>();
        fillImage.color = new Color(1f, 0.65f, 0.08f);
        fillImage.raycastTarget = false;
        SetProgress(0);
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        float previousTimeScale = Time.timeScale;
        bool previousAudioPause = AudioListener.pause;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        // Let the overlay render before scene loading can occupy the main thread.
        yield return null;

        AsyncOperation operation = null;
        try
        {
            operation = SceneManager.LoadSceneAsync(sceneName);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }

        if (operation == null)
        {
            Time.timeScale = previousTimeScale;
            AudioListener.pause = previousAudioPause;
            isLoading = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
            yield break;
        }

        operation.allowSceneActivation = false;
        float elapsed = 0f;
        float minimumTime = settings != null ? Mathf.Max(0, settings.minimumDisplayTime) : 1f;
        while (operation.progress < 0.9f || elapsed < minimumTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float timedProgress = minimumTime > 0 ? Mathf.Clamp01(elapsed / minimumTime) : 1f;
            SetProgress(Mathf.Min(operation.progress / 0.9f, timedProgress, 0.99f));
            yield return null;
        }

        SetProgress(1);
        yield return new WaitForSecondsRealtime(0.15f);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        operation.allowSceneActivation = true;
        while (!operation.isDone)
            yield return null;

        // Keep covering the screen until the new scene's Start methods have run.
        yield return null;
        isLoading = false;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void SetProgress(float progress)
    {
        progressFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1);
        progressText.text = Mathf.FloorToInt(Mathf.Clamp01(progress) * 100) + "%";
    }

    private static string GetModeLabel(string sceneName)
    {
        switch (sceneName)
        {
            case "Desert": return "DESERT";
            case "Coop":
            case "PVP": return "PVP";
            case "Training": return "TRAINING";
            default: return sceneName.ToUpperInvariant();
        }
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static TextMeshProUGUI Label(string name, Transform parent, string text, float size,
        TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax)
    {
        TextMeshProUGUI label = Rect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }
}
