using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject mainPauseMenu;
    [SerializeField] private GameObject optionsMenu;

    [Header("First Selected Buttons")]
    [SerializeField] private GameObject mainMenuFirstButton;
    [SerializeField] private GameObject optionsMenuFirstButton;

    [Header("Settings")]
    [SerializeField] private float doublePressWindow = 0.4f;
    [Tooltip("Gamepad button that toggles the pause menu (e.g. PlayStation Options / Xbox Menu).")]
    [SerializeField] private KeyCode[] gamepadPauseButtons = new KeyCode[]
    {
        KeyCode.JoystickButton7,
        KeyCode.JoystickButton9
    };

    public static bool IsPaused { get; private set; }

    private float lastEscPressTime = -Mathf.Infinity;
    private AudioSource audioSource;
    private bool cursorWasVisible;
    private CursorLockMode previousLockMode;
    private List<MonoBehaviour> frozenBehaviours;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.ignoreListenerPause = true;
    }

    private void Start()
    {
        PauseGame(false);
    }

    private void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        // Keyboard: Escape (double press)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Time.unscaledTime - lastEscPressTime <= doublePressWindow)
            {
                if (!IsPaused)
                    PauseGame(true);
                else
                    PauseGame(false);

                lastEscPressTime = -Mathf.Infinity;
            }
            else
            {
                lastEscPressTime = Time.unscaledTime;
            }
        }

        // Gamepad: Options/Menu button (single press)
        for (int i = 0; i < gamepadPauseButtons.Length; i++)
        {
            if (Input.GetKeyDown(gamepadPauseButtons[i]))
            {
                if (!IsPaused)
                    PauseGame(true);
                else
                    PauseGame(false);
                break;
            }
        }
    }

    public void PauseGame(bool pause)
    {
        IsPaused = pause;
        Time.timeScale = pause ? 0f : 1f;
        AudioListener.pause = pause;

        mainPauseMenu.SetActive(pause);
        optionsMenu.SetActive(false);

        if (pause)
        {
            previousLockMode = Cursor.lockState;
            cursorWasVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = previousLockMode;
            Cursor.visible = cursorWasVisible;
        }

        if (pause)
        {
            FreezeScene(true);
        }
        else
        {
            FreezeScene(false);
        }

        if (pause && mainMenuFirstButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(mainMenuFirstButton);
        }

        if (!pause && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void FreezeScene(bool freeze)
    {
        if (freeze)
        {
            if (frozenBehaviours != null)
                UnfreezeScene();

            frozenBehaviours = new List<MonoBehaviour>();

            MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null)
                    continue;

                // Only live objects from a loaded scene (skips prefab assets)
                if (!behaviour.gameObject.scene.IsValid() || !behaviour.gameObject.scene.isLoaded)
                    continue;

                // Only currently running behaviours (skips inactive objects)
                if (!behaviour.isActiveAndEnabled)
                    continue;

                // Keep all UI (buttons, EventSystem, input modules, etc.) interactive
                if (behaviour is UIBehaviour)
                    continue;

                // Keep the pause menu and its helper scripts alive
                if (behaviour == this)
                    continue;

                if (IsPartOfPauseUI(behaviour.transform))
                    continue;

                frozenBehaviours.Add(behaviour);
                behaviour.enabled = false;
            }
        }
        else
        {
            UnfreezeScene();
        }
    }

    private void UnfreezeScene()
    {
        if (frozenBehaviours == null)
            return;

        foreach (MonoBehaviour behaviour in frozenBehaviours)
        {
            if (behaviour != null)
                behaviour.enabled = true;
        }

        frozenBehaviours = null;
    }

    private bool IsPartOfPauseUI(Transform t)
    {
        Transform root = transform;

        while (t != null)
        {
            if (t == root)
                return true;
            t = t.parent;
        }

        return false;
    }

    public void Resume()
    {
        PlayButtonSound();
        PauseGame(false);
    }

    public void Restart()
    {
        PlayButtonSound();
        AudioListener.pause = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OpenOptions()
    {
        PlayButtonSound();
        mainPauseMenu.SetActive(false);
        optionsMenu.SetActive(true);

        if (optionsMenuFirstButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(optionsMenuFirstButton);
        }
    }

    public void CloseOptions()
    {
        PlayButtonSound();
        optionsMenu.SetActive(false);
        mainPauseMenu.SetActive(true);

        if (mainMenuFirstButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(mainMenuFirstButton);
        }
    }

    public void ExitGame()
    {
        PlayButtonSound();
        AudioListener.pause = false;
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void PlayButtonSound()
    {
        if (audioSource != null && audioSource.clip != null)
            audioSource.PlayOneShot(audioSource.clip);
    }
}