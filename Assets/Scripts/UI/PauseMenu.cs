using System.Collections;
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

    [Header("Panel Transition")]
    [Tooltip("Duration of the fade/slide when switching between menus.")]
    [Min(0.05f)]
    [SerializeField] private float panelTransitionDuration = 0.35f;
    [Tooltip("How far (in units) the incoming panel slides in from below.")]
    [SerializeField] private float panelSlideDistance = 60f;
    [Tooltip("Scale multiplier the incoming panel starts at (e.g. 0.95 = slightly smaller).")]
    [Range(0.8f, 1f)]
    [SerializeField] private float panelStartScale = 0.95f;
    [Tooltip("Fade/slide easing over time. Should start at 0 and end at 1.")]
    [SerializeField] private AnimationCurve panelTransitionCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(0.4f, 0.08f, 0.55f, 0.55f),
        new Keyframe(0.6f, 0.92f, 0.55f, 0.55f),
        new Keyframe(1f, 1f, 0f, 0f));

    public static bool IsPaused { get; private set; }

    private float lastEscPressTime = -Mathf.Infinity;
    private AudioSource audioSource;
    private bool cursorWasVisible;
    private CursorLockMode previousLockMode;
    private List<MonoBehaviour> frozenBehaviours;
    private CanvasGroup mainMenuGroup;
    private CanvasGroup optionsGroup;
    private Coroutine panelTransition;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.ignoreListenerPause = true;

        mainMenuGroup = GetOrAddCanvasGroup(mainPauseMenu);
        optionsGroup = GetOrAddCanvasGroup(optionsMenu);
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject panel)
    {
        if (panel == null)
            return null;

        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        if (group == null)
            group = panel.AddComponent<CanvasGroup>();

        return group;
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

        if (!pause && panelTransition != null)
        {
            StopCoroutine(panelTransition);
            panelTransition = null;
        }

        if (mainMenuGroup != null)
        {
            mainMenuGroup.alpha = 1f;
            mainMenuGroup.blocksRaycasts = true;
            mainMenuGroup.interactable = true;
        }

        if (optionsGroup != null)
        {
            optionsGroup.alpha = 1f;
            optionsGroup.blocksRaycasts = false;
            optionsGroup.interactable = false;
        }

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

                // Keep the pause menu and its helper scripts alive
                if (behaviour == this)
                    continue;

                if (IsPartOfPauseUI(behaviour.transform))
                    continue;

                // Keep all camera-attached scripts (post-processing layer, camera data, etc.)
                if (behaviour.GetComponent<Camera>() != null)
                    continue;

                // Keep all rendering / post-processing scripts alive (URP Volume,
                // Post Processing Stack v2, custom render features, etc.)
                string ns = behaviour.GetType().Namespace;
                if (!string.IsNullOrEmpty(ns) && ns.StartsWith("UnityEngine.Rendering"))
                    continue;

                // Keep all UI (buttons, EventSystem, input modules, etc.) interactive
                if (behaviour is UIBehaviour)
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
        if (panelTransition != null)
            return;

        PlayButtonSound();
        panelTransition = StartCoroutine(SwitchPanels(
            mainPauseMenu, mainMenuGroup,
            optionsMenu, optionsGroup,
            optionsMenuFirstButton));
    }

    public void CloseOptions()
    {
        if (panelTransition != null)
            return;

        PlayButtonSound();
        panelTransition = StartCoroutine(SwitchPanels(
            optionsMenu, optionsGroup,
            mainPauseMenu, mainMenuGroup,
            mainMenuFirstButton));
    }

    private IEnumerator SwitchPanels(GameObject hidePanel, CanvasGroup hideGroup,
                                     GameObject showPanel, CanvasGroup showGroup,
                                     GameObject firstButton)
    {
        if (hideGroup != null)
        {
            hideGroup.interactable = false;
            hideGroup.blocksRaycasts = false;
        }

        if (showGroup != null)
        {
            showGroup.interactable = false;
            showGroup.blocksRaycasts = false;
        }

        showPanel.SetActive(true);

        Vector3 showBasePos = showPanel.transform.localPosition;
        Vector3 showBaseScale = showPanel.transform.localScale;

        float t = 0f;

        while (t < panelTransitionDuration)
        {
            t += Time.unscaledDeltaTime;
            float progress = panelTransitionCurve.Evaluate(Mathf.Clamp01(t / panelTransitionDuration));

            if (hideGroup != null)
                hideGroup.alpha = 1f - progress;

            if (showGroup != null)
                showGroup.alpha = progress;

            showPanel.transform.localPosition = showBasePos + Vector3.down * (panelSlideDistance * (1f - progress));
            showPanel.transform.localScale = showBaseScale * Mathf.Lerp(panelStartScale, 1f, progress);

            yield return null;
        }

        if (hideGroup != null)
            hideGroup.alpha = 0f;
        if (showGroup != null)
            showGroup.alpha = 1f;

        if (hidePanel != null)
            hidePanel.SetActive(false);

        showPanel.transform.localPosition = showBasePos;
        showPanel.transform.localScale = showBaseScale;

        if (showGroup != null)
        {
            showGroup.interactable = true;
            showGroup.blocksRaycasts = true;
        }

        if (firstButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstButton);
        }

        panelTransition = null;
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