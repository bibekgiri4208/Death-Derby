using UnityEngine;

public class QuitToggle : MonoBehaviour
{
    public void QuitGame()
    {
        Debug.Log("Quit Game Triggered!");

        // Works in built application (.exe)
        Application.Quit();

        // Stops Play Mode inside Unity Editor for testing
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}