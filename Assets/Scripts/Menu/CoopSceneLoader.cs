using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Auto-wires the "Co-op Cube" button in the Garage's Start Menu so that
/// pressing it loads the Coop scene (which enables split-screen mode on start).
/// The host object persists across scene loads so the button is re-wired every
/// time the Garage is entered, not just the first time the game is launched.
/// </summary>
public class CoopSceneLoader : MonoBehaviour
{
    private const string SourceSceneName = "Garage";
    private const string ButtonObjectName = "Co-op Cube";
    private const string CoopSceneName = "Coop";

    private Interactable3DButton wiredButton;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoWire()
    {
        if (SceneManager.GetActiveScene().name != SourceSceneName)
            return;

        if (FindAnyObjectByType<CoopSceneLoader>() != null)
            return;

        GameObject host = new GameObject("Coop Scene Loader");
        DontDestroyOnLoad(host);
        host.AddComponent<CoopSceneLoader>();
    }

    private void Awake()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (SceneManager.GetActiveScene().name == SourceSceneName)
            TryWireButton();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SourceSceneName)
            return;

        TryWireButton();
    }

    private void TryWireButton()
    {
        Interactable3DButton button = null;

        foreach (Interactable3DButton candidate in FindObjectsByType<Interactable3DButton>(FindObjectsInactive.Include))
        {
            if (candidate.gameObject.name == ButtonObjectName)
            {
                button = candidate;
                break;
            }
        }

        if (button == null)
        {
            Debug.LogError("CoopSceneLoader: could not find '" + ButtonObjectName + "'.", this);
            return;
        }

        if (wiredButton == button)
            return;

        wiredButton = button;
        button.onClick.AddListener(LoadCoopScene);
    }

    private void LoadCoopScene()
    {
        LoadingScreenManager.LoadScene(CoopSceneName);
    }
}
