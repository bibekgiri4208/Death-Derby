using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Auto-wires the "Co-op Cube" button in the Garage's Start Menu so that
/// pressing it loads the Coop scene (which enables split-screen mode on start).
/// </summary>
public class CoopSceneLoader : MonoBehaviour
{
    private const string SourceSceneName = "Garage";
    private const string ButtonObjectName = "Co-op Cube";
    private const string CoopSceneName = "Coop";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoWire()
    {
        if (SceneManager.GetActiveScene().name != SourceSceneName)
            return;

        if (FindAnyObjectByType<CoopSceneLoader>() != null)
            return;

        new GameObject("Coop Scene Loader").AddComponent<CoopSceneLoader>();
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
            Debug.LogError("CoopSceneLoader: could not find '" + ButtonObjectName + "'.", this);
            return;
        }

        button.onClick.AddListener(LoadCoopScene);
    }

    private void LoadCoopScene()
    {
        LoadingScreenManager.LoadScene(CoopSceneName);
    }
}