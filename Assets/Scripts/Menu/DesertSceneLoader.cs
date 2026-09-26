using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Auto-wires the "Desert Cube" button in the Garage's Start Menu so that
/// pressing it saves the currently selected car and loads the Desert scene
/// through the loading screen. The host object persists across scene loads so
/// the button is re-wired every time the Garage is entered, not just the first
/// time the game is launched.
/// </summary>
public class DesertSceneLoader : MonoBehaviour
{
    private const string SourceSceneName = "Garage";
    private const string ButtonObjectName = "Desert Cube";
    private const string DesertSceneName = "Desert";
    private const string CarIndexKey = "CarIndexValue";

    private Interactable3DButton wiredButton;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoWire()
    {
        if (FindAnyObjectByType<DesertSceneLoader>() != null)
            return;

        GameObject host = new GameObject("Desert Scene Loader");
        DontDestroyOnLoad(host);
        host.AddComponent<DesertSceneLoader>();
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
            Debug.LogError("DesertSceneLoader: could not find '" + ButtonObjectName + "'.", this);
            return;
        }

        if (wiredButton == button)
            return;

        wiredButton = button;
        button.onClick.AddListener(LoadDesertScene);
    }

    private void LoadDesertScene()
    {
        SaveSelectedCarIndex();
        LoadingScreenManager.LoadScene(DesertSceneName);
    }

    private void SaveSelectedCarIndex()
    {
        CarSelection selection = FindAnyObjectByType<CarSelection>(FindObjectsInactive.Include);

        if (selection == null)
        {
            Debug.LogWarning("DesertSceneLoader: no CarSelection found, using the last saved car index.", this);
            return;
        }

        PlayerPrefs.SetInt(CarIndexKey, selection.CurrentCarIndex);
        PlayerPrefs.Save();
    }
}
