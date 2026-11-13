using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene actions shared by runtime pause and result buttons.</summary>
public static class MatchMenuActions
{
    public static void Restart()
    {
        PrepareSceneChange();
        LoadingScreenManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public static void ReturnToGarage()
    {
        PrepareSceneChange();
        LoadingScreenManager.LoadScene("Garage");
    }

    private static void PrepareSceneChange()
    {
        // Reuse the scene's menu click audio, not a hidden legacy button.
        PauseMenu pause = Object.FindAnyObjectByType<PauseMenu>();
        AudioSource audio = pause != null ? pause.GetComponent<AudioSource>() : null;
        if (audio != null && audio.clip != null)
            audio.PlayOneShot(audio.clip);

        AudioListener.pause = false;
        Time.timeScale = 1f;
    }
}
