using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Auto-wires the "Desert Cube" button in the Garage's Start Menu so that
/// pressing it saves the currently selected car and loads the Desert scene
/// through the loading screen.
/// </summary>
public class DesertSceneLoader : MonoBehaviour
{
    private const string SourceSceneName = "Garage";
    private const string ButtonObjectName = "Desert Cube";
    private const string DesertSceneName = "Desert";
    private const string CarIndexKey = "CarIndexValue";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoWire()
    {
        if (SceneManager.GetActiveScene().name != SourceSceneName)
            return;

        if (FindAnyObjectByType<DesertSceneLoader>() != null)
            return;

        new GameObject("Desert Scene Loader").AddComponent<DesertSceneLoader>();
    }

    private void Awake()
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
