using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Fixed-duration loading screen: shows a fill bar climbing to 100% over
/// `minimumLoadTime` seconds, then activates the requested target scene.
/// The manager auto-spawns when the "Loading Screen" scene loads and
/// auto-wires its UI, so no inspector setup is required.
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    public static LoadingScreenManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private Slider progressBar;

    [Header("Load Settings")]
    [SerializeField] private float minimumLoadTime = 2f;

    [Header("Loading Artwork")]
    [Tooltip("Full-screen image that shows the artwork for the scene being loaded.")]
    [SerializeField] private Image backgroundImage;
    [Tooltip("Artwork shown while loading the Desert. Leave empty to keep the scene's default image.")]
    [SerializeField] private Sprite desertLoadingImage;
    [Tooltip("Artwork shown while loading the Coop scene. Leave empty to keep the scene's default image.")]
    [SerializeField] private Sprite coopLoadingImage;
    [Tooltip("Artwork shown while loading the Training scene. Leave empty to keep the scene's default image.")]
    [SerializeField] private Sprite trainingLoadingImage;

    private static string targetScene;
    private bool isLoading;

    private const string LoadingSceneName = "Loading Screen";
    private const string DesertSceneName = "Desert";
    private const string CoopSceneName = "Coop";
    private const string TrainingSceneName = "Training";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        AutoWireReferences();
    }

    void Start()
    {
        if (string.IsNullOrEmpty(targetScene))
            return;

        ApplyLoadingImage(targetScene);

        if (loadingPanel != null)
            loadingPanel.SetActive(true);

        StartCoroutine(LoadSceneAsync(targetScene));
    }

    /// <summary>Swaps the full-screen artwork to match the scene being loaded.</summary>
    private void ApplyLoadingImage(string sceneName)
    {
        if (backgroundImage == null) return;

        Sprite sprite = GetLoadingImage(sceneName);

        // A null result means "no dedicated artwork", so the image keeps whatever
        // the Loading Screen scene already shows.
        if (sprite != null)
            backgroundImage.sprite = sprite;
    }

    private Sprite GetLoadingImage(string sceneName)
    {
        if (sceneName == DesertSceneName) return desertLoadingImage;
        if (sceneName == CoopSceneName) return coopLoadingImage;
        if (sceneName == TrainingSceneName) return trainingLoadingImage;
        return null;
    }

    void Update()
    {
        if (!isLoading || loadingText == null)
            return;

        float dots = Mathf.Repeat(Time.unscaledTime * 2f, 4f);
        string dotStr = new string('.', Mathf.FloorToInt(dots));
        loadingText.text = "LOADING" + dotStr;
    }

    /// <summary>Loads the loading screen first, then switches to `sceneName`.</summary>
    public static void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;

        targetScene = sceneName;
        SceneManager.LoadScene(LoadingSceneName);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        targetScene = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (SceneManager.GetActiveScene().name != LoadingSceneName)
            return;

        if (FindAnyObjectByType<LoadingScreenManager>() != null)
            return;

        new GameObject("Loading Screen Manager").AddComponent<LoadingScreenManager>();
    }

    private void AutoWireReferences()
    {
        if (backgroundImage == null)
        {
            foreach (Image candidate in FindObjectsByType<Image>(FindObjectsInactive.Include))
            {
                if (candidate.gameObject.name == "Background")
                {
                    backgroundImage = candidate;
                    break;
                }
            }
        }

        if (progressBar == null)
        {
            progressBar = FindAnyObjectByType<Slider>();

            if (progressBar != null)
            {
                Transform fill = FindDeepChild(progressBar.transform, "Fill Color");
                if (fill != null)
                    progressBar.fillRect = fill as RectTransform;

                progressBar.minValue = 0f;
                progressBar.maxValue = 1f;
            }
        }

        if (loadingText == null)
        {
            foreach (TextMeshProUGUI candidate in FindObjectsByType<TextMeshProUGUI>())
            {
                if (candidate.gameObject.name == "Loading Text")
                {
                    loadingText = candidate;
                    break;
                }
            }
        }

        if (progressText == null)
        {
            foreach (TextMeshProUGUI candidate in FindObjectsByType<TextMeshProUGUI>())
            {
                if (candidate.gameObject.name == "Progress Text")
                {
                    progressText = candidate;
                    break;
                }
            }
        }
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        isLoading = true;

        if (progressBar != null)
            progressBar.value = 0f;

        if (progressText != null)
            progressText.text = "0%";

        Time.timeScale = 1f;
        AudioListener.pause = false;

        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
        if (asyncOp == null)
        {
            yield return new WaitForSecondsRealtime(minimumLoadTime);
            SceneManager.LoadScene(sceneName);
            yield break;
        }

        asyncOp.allowSceneActivation = false;

        while (asyncOp.progress < 0.9f)
            yield return null;

        float fakeProgress = 0f;
        float elapsedTime = 0f;

        while (elapsedTime < minimumLoadTime)
        {
            elapsedTime += Time.unscaledDeltaTime;
            fakeProgress = Mathf.Clamp01(elapsedTime / minimumLoadTime);

            if (progressBar != null)
                progressBar.value = fakeProgress;

            if (progressText != null)
                progressText.text = Mathf.RoundToInt(fakeProgress * 100f) + "%";

            yield return null;
        }

        fakeProgress = 1f;

        if (progressBar != null)
            progressBar.value = fakeProgress;

        if (progressText != null)
            progressText.text = "100%";

        yield return new WaitForSecondsRealtime(0.3f);

        asyncOp.allowSceneActivation = true;
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null || string.IsNullOrEmpty(name))
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
                return child;

            Transform result = FindDeepChild(child, name);
            if (result != null)
                return result;
        }

        return null;
    }
}