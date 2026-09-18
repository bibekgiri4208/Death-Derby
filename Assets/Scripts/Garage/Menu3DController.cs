using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class Menu3DController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject graphicsMenuPanel;
    [SerializeField] private GameObject startMenuPanel;
    [SerializeField] private GameObject musicMenuPanel;
    [SerializeField] private bool showMainMenuOnStart = true;

    [Header("Auto Wiring")]
    [SerializeField] private string graphicsButtonName = "Graphics Cube";
    [SerializeField] private string startButtonName = "Start Cube";
    [SerializeField] private string musicButtonName = "Music Cube";
    [SerializeField] private string returnButtonName = "Return Cube";

    [Header("Gamepad / Keyboard")]
    [SerializeField] private bool enableNavigation = true;
    [SerializeField] private bool wrapNavigation = true;
    [SerializeField] private float stickDeadzone = 0.5f;
    [SerializeField] private float repeatDelay = 0.4f;
    [SerializeField] private float repeatRate = 0.15f;

    [Header("Slide Animation")]
    [SerializeField] private bool useSlideAnimation = true;
    [SerializeField] private float slideDistance = 10f;
    [SerializeField] private float slideDuration = 0.35f;
    [SerializeField] private float slideStagger = 0.06f;

    [Header("Horizontal Options")]
    [SerializeField] private bool enableHorizontalOptions = true;

    private readonly List<Interactable3DButton> buttons = new List<Interactable3DButton>();
    private readonly List<Interactable3DButton> mainMenuButtons = new List<Interactable3DButton>();
    private readonly List<Vector3> mainMenuHomePositions = new List<Vector3>();
    private readonly List<Interactable3DButton> graphicsButtons = new List<Interactable3DButton>();
    private readonly List<Vector3> graphicsHomePositions = new List<Vector3>();
    private readonly List<Interactable3DButton> startMenuButtons = new List<Interactable3DButton>();
    private readonly List<Vector3> startMenuHomePositions = new List<Vector3>();
    private readonly List<Interactable3DButton> musicMenuButtons = new List<Interactable3DButton>();
    private readonly List<Vector3> musicMenuHomePositions = new List<Vector3>();
    private GameObject currentPanel;
    private Interactable3DButton graphicsMainButton;
    private Interactable3DButton startMainButton;
    private Interactable3DButton musicMainButton;
    private int selectedIndex = -1;
    private int lastDirection;
    private float nextRepeatTime;
    private int lastHorizontalDirection;
    private float nextHorizontalRepeatTime;
    private bool wired;
    private Coroutine slideRoutine;

    public GameObject MainMenuPanel => mainMenuPanel;
    public GameObject GraphicsMenuPanel => graphicsMenuPanel;
    public GameObject StartMenuPanel => startMenuPanel;
    public GameObject MusicMenuPanel => musicMenuPanel;
    public GameObject CurrentPanel => currentPanel;

    void Awake()
    {
        ResolvePanels();
        CachePanelButtons(mainMenuPanel, mainMenuButtons, mainMenuHomePositions);
        CachePanelButtons(graphicsMenuPanel, graphicsButtons, graphicsHomePositions);
        CachePanelButtons(startMenuPanel, startMenuButtons, startMenuHomePositions);
        CachePanelButtons(musicMenuPanel, musicMenuButtons, musicMenuHomePositions);
        WirePanelButtons();
    }

    void Start()
    {
        if (showMainMenuOnStart)
        {
            ShowMainMenu();
        }
        else
        {
            SetSelected(0);
        }
    }

    void Update()
    {
        HandleCancel();

        if (!enableNavigation || buttons.Count == 0) return;
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null) return;

        HandleNavigation();
        HandleOptionSelection();
        HandleSubmit();
    }

    void OnDestroy()
    {
        UnsubscribeButtons();
    }

    public void ShowMainMenu()
    {
        StopSlide();
        SetPanelActive(graphicsMenuPanel, false);
        SetPanelActive(startMenuPanel, false);
        SetPanelActive(musicMenuPanel, false);
        SetPanelActive(mainMenuPanel, true);
        SetButtonsInteractable(mainMenuButtons, true);
        currentPanel = mainMenuPanel;
        RefreshButtons();

        StartSlideIn(mainMenuButtons, mainMenuHomePositions);
    }

    public void ShowGraphicsMenu()
    {
        StopSlide();
        SetPanelActive(startMenuPanel, false);
        SetPanelActive(musicMenuPanel, false);
        SetPanelActive(graphicsMenuPanel, true);
        SetPanelActive(mainMenuPanel, true);
        SetButtonsInteractable(mainMenuButtons, false);
        currentPanel = graphicsMenuPanel;
        RefreshButtons();

        StartSlideIn(graphicsButtons, graphicsHomePositions);
    }

    public void ShowStartMenu()
    {
        StopSlide();
        SetPanelActive(graphicsMenuPanel, false);
        SetPanelActive(musicMenuPanel, false);
        SetPanelActive(startMenuPanel, true);
        SetPanelActive(mainMenuPanel, true);
        SetButtonsInteractable(mainMenuButtons, false);
        currentPanel = startMenuPanel;
        RefreshButtons();

        StartSlideIn(startMenuButtons, startMenuHomePositions);
    }

    public void ShowMusicMenu()
    {
        StopSlide();
        SetPanelActive(graphicsMenuPanel, false);
        SetPanelActive(startMenuPanel, false);
        SetPanelActive(musicMenuPanel, true);
        SetPanelActive(mainMenuPanel, true);
        SetButtonsInteractable(mainMenuButtons, false);
        currentPanel = musicMenuPanel;
        RefreshButtons();

        StartSlideIn(musicMenuButtons, musicMenuHomePositions);
    }

    public void CloseStartMenu()
    {
        if (currentPanel != startMenuPanel) return;

        StopSlide();
        SetPanelActive(graphicsMenuPanel, false);
        SetPanelActive(musicMenuPanel, false);
        SetPanelActive(mainMenuPanel, true);
        SetButtonsInteractable(mainMenuButtons, true);
        currentPanel = mainMenuPanel;
        RefreshButtons();
        SetSelected(GetButtonIndex(startMainButton));

        StartSlideOut(startMenuButtons, startMenuHomePositions, startMenuPanel);
    }

    public void CloseGraphicsMenu()
    {
        if (currentPanel != graphicsMenuPanel) return;

        StopSlide();
        SetPanelActive(startMenuPanel, false);
        SetPanelActive(musicMenuPanel, false);
        SetPanelActive(mainMenuPanel, true);
        SetButtonsInteractable(mainMenuButtons, true);
        currentPanel = mainMenuPanel;
        RefreshButtons();
        SetSelected(GetButtonIndex(graphicsMainButton));

        StartSlideOut(graphicsButtons, graphicsHomePositions, graphicsMenuPanel);
    }

    public void CloseMusicMenu()
    {
        if (currentPanel != musicMenuPanel) return;

        StopSlide();
        SetPanelActive(graphicsMenuPanel, false);
        SetPanelActive(startMenuPanel, false);
        SetPanelActive(mainMenuPanel, true);
        SetButtonsInteractable(mainMenuButtons, true);
        currentPanel = mainMenuPanel;
        RefreshButtons();
        SetSelected(GetButtonIndex(musicMainButton));

        StartSlideOut(musicMenuButtons, musicMenuHomePositions, musicMenuPanel);
    }

    private int GetButtonIndex(Interactable3DButton target)
    {
        if (target == null) return 0;

        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] == target) return i;
        }

        return 0;
    }

    private void StartSlideIn(List<Interactable3DButton> list, List<Vector3> homePositions)
    {
        if (!useSlideAnimation || list.Count == 0) return;

        slideRoutine = StartCoroutine(PlaySlideIn(list, homePositions));
    }

    private void StartSlideOut(List<Interactable3DButton> list, List<Vector3> homePositions, GameObject panel)
    {
        if (list.Count == 0)
        {
            SetPanelActive(panel, false);
            return;
        }

        if (!useSlideAnimation)
        {
            SetPanelActive(panel, false);
            return;
        }

        slideRoutine = StartCoroutine(PlaySlideOut(list, homePositions, panel));
    }

    private void CachePanelButtons(GameObject panel, List<Interactable3DButton> list, List<Vector3> homePositions)
    {
        list.Clear();
        homePositions.Clear();

        if (panel == null) return;

        Interactable3DButton[] found = panel.GetComponentsInChildren<Interactable3DButton>(true);
        System.Array.Sort(found, (a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] == null) continue;

            list.Add(found[i]);
            homePositions.Add(found[i].transform.localPosition);
        }
    }

    private void StopSlide()
    {
        if (slideRoutine != null)
        {
            StopCoroutine(slideRoutine);
            slideRoutine = null;
        }

        ResetPositions(mainMenuButtons, mainMenuHomePositions);
        ResetPositions(graphicsButtons, graphicsHomePositions);
        ResetPositions(startMenuButtons, startMenuHomePositions);
        ResetPositions(musicMenuButtons, musicMenuHomePositions);
    }

    private void ResetPositions(List<Interactable3DButton> list, List<Vector3> homePositions)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
            {
                list[i].transform.localPosition = homePositions[i];
            }
        }
    }

    private IEnumerator PlaySlideIn(List<Interactable3DButton> list, List<Vector3> homePositions)
    {
        if (list.Count == 0)
        {
            slideRoutine = null;
            yield break;
        }

        Vector3 offset = new Vector3(slideDistance, 0f, 0f);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
            {
                list[i].transform.localPosition = homePositions[i] + offset;
            }
        }

        float total = slideDuration + slideStagger * (list.Count - 1);
        float elapsed = 0f;

        while (elapsed < total)
        {
            elapsed += Time.unscaledDeltaTime;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;

                float t = Mathf.Clamp01((elapsed - slideStagger * i) / slideDuration);
                float eased = t * t * (3f - 2f * t);
                list[i].transform.localPosition = Vector3.Lerp(
                    homePositions[i] + offset,
                    homePositions[i], eased);
            }

            yield return null;
        }

        ResetPositions(list, homePositions);
        slideRoutine = null;
    }

    private IEnumerator PlaySlideOut(List<Interactable3DButton> list, List<Vector3> homePositions, GameObject panel)
    {
        Vector3 offset = new Vector3(slideDistance, 0f, 0f);

        float total = slideDuration + slideStagger * (list.Count - 1);
        float elapsed = 0f;

        while (elapsed < total)
        {
            elapsed += Time.unscaledDeltaTime;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;

                float t = Mathf.Clamp01((elapsed - slideStagger * i) / slideDuration);
                float eased = t * t * (3f - 2f * t);
                list[i].transform.localPosition = Vector3.Lerp(
                    homePositions[i],
                    homePositions[i] + offset, eased);
            }

            yield return null;
        }

        SetPanelActive(panel, false);
        slideRoutine = null;
    }

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }

    private void SetButtonsInteractable(List<Interactable3DButton> list, bool interactable)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
            {
                list[i].EnsureInitialized();
                list[i].SetInteractable(interactable);
            }
        }
    }

    private void ResolvePanels()
    {
        if (mainMenuPanel == null)
        {
            Transform main = FindDeepChild(transform, "Main Menu");
            if (main != null) mainMenuPanel = main.gameObject;
        }

        if (graphicsMenuPanel == null)
        {
            Transform graphics = FindDeepChild(transform, "Graphics Menu");
            if (graphics != null) graphicsMenuPanel = graphics.gameObject;
        }

        if (startMenuPanel == null)
        {
            Transform start = FindDeepChild(transform, "Start Menu");
            if (start != null) startMenuPanel = start.gameObject;
        }

        if (musicMenuPanel == null)
        {
            Transform music = FindDeepChild(transform, "Music Menu");
            if (music != null) musicMenuPanel = music.gameObject;
        }

        if (mainMenuPanel == null || graphicsMenuPanel == null || startMenuPanel == null)
        {
            Debug.LogWarning("Menu3DController: could not resolve Main Menu / Graphics Menu / Start Menu panels.", this);
        }
    }

    private void WirePanelButtons()
    {
        if (wired) return;
        wired = true;

        if (mainMenuPanel != null)
        {
            Transform graphicsButton = FindDeepChild(mainMenuPanel.transform, graphicsButtonName);
            if (graphicsButton != null && graphicsButton.TryGetComponent(out Interactable3DButton graphics))
            {
                graphicsMainButton = graphics;
                graphics.onClick.AddListener(ShowGraphicsMenu);
            }

            Transform startButton = FindDeepChild(mainMenuPanel.transform, startButtonName);
            if (startButton != null && startButton.TryGetComponent(out Interactable3DButton start))
            {
                startMainButton = start;
                start.onClick.AddListener(ShowStartMenu);
            }

            Transform musicButton = FindDeepChild(mainMenuPanel.transform, musicButtonName);
            if (musicButton != null && musicButton.TryGetComponent(out Interactable3DButton music))
            {
                musicMainButton = music;
                music.onClick.AddListener(ShowMusicMenu);
            }
        }

        if (graphicsMenuPanel != null)
        {
            Transform returnButton = FindDeepChild(graphicsMenuPanel.transform, returnButtonName);
            if (returnButton != null && returnButton.TryGetComponent(out Interactable3DButton back))
            {
                back.onClick.AddListener(ShowMainMenu);
            }
        }
    }

    private void RefreshButtons()
    {
        UnsubscribeButtons();
        buttons.Clear();

        if (currentPanel != null)
        {
            Interactable3DButton[] found = currentPanel.GetComponentsInChildren<Interactable3DButton>(true);
            for (int i = 0; i < found.Length; i++)
            {
                Interactable3DButton button = found[i];
                if (button == null || !button.gameObject.activeInHierarchy) continue;

                button.EnsureInitialized();
                button.OnHovered += HandleHovered;
                buttons.Add(button);
            }

            buttons.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
        }

        selectedIndex = -1;
        SetSelected(0);
    }

    private void UnsubscribeButtons()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] != null) buttons[i].OnHovered -= HandleHovered;
        }
    }

    private void HandleHovered(Interactable3DButton button)
    {
        int index = buttons.IndexOf(button);
        if (index >= 0 && index != selectedIndex) SetSelected(index);
    }

    private void SetSelected(int index)
    {
        if (buttons.Count == 0)
        {
            selectedIndex = -1;
            return;
        }

        index = Mathf.Clamp(index, 0, buttons.Count - 1);

        if (selectedIndex != index)
        {
            if (selectedIndex >= 0 && selectedIndex < buttons.Count && buttons[selectedIndex] != null)
            {
                buttons[selectedIndex].SetHighlighted(false, false);
            }

            selectedIndex = index;

            if (buttons[selectedIndex] != null)
            {
                buttons[selectedIndex].SetHighlighted(true, false);
            }
        }
    }

    private void HandleNavigation()
    {
        int direction = ReadNavigationDirection();

        if (direction == 0)
        {
            lastDirection = 0;
            return;
        }

        bool directionChanged = lastDirection != direction;
        if (directionChanged || Time.unscaledTime >= nextRepeatTime)
        {
            MoveSelection(direction);
            nextRepeatTime = Time.unscaledTime + (directionChanged ? repeatDelay : repeatRate);
        }

        lastDirection = direction;
    }

    private void MoveSelection(int direction)
    {
        int count = buttons.Count;
        if (count == 0) return;

        int index = selectedIndex;
        if (index < 0)
        {
            index = direction > 0 ? 0 : count - 1;
        }
        else
        {
            index += direction;

            if (wrapNavigation)
            {
                index = ((index % count) + count) % count;
            }
            else
            {
                index = Mathf.Clamp(index, 0, count - 1);
            }
        }

        SetSelected(index);
    }

    private int ReadNavigationDirection()
    {
        Gamepad gamepad = Gamepad.current;
        Keyboard keyboard = Keyboard.current;

        if (gamepad != null)
        {
            if (gamepad.dpad.down.wasPressedThisFrame) return 1;
            if (gamepad.dpad.up.wasPressedThisFrame) return -1;

            float y = gamepad.leftStick.ReadValue().y;
            if (y <= -stickDeadzone) return 1;
            if (y >= stickDeadzone) return -1;
        }

        if (keyboard != null)
        {
            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) return 1;
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) return -1;
        }

        return 0;
    }

    private void HandleOptionSelection()
    {
        if (!enableHorizontalOptions) return;

        int direction = ReadHorizontalDirection();
        if (direction == 0)
        {
            lastHorizontalDirection = 0;
            return;
        }

        if (selectedIndex < 0 || selectedIndex >= buttons.Count || buttons[selectedIndex] == null) return;

        if (!buttons[selectedIndex].TryGetComponent(out IOptionSelector selector))
        {
            lastHorizontalDirection = 0;
            return;
        }

        bool directionChanged = lastHorizontalDirection != direction;
        if (directionChanged || Time.unscaledTime >= nextHorizontalRepeatTime)
        {
            if (direction > 0) selector.Next();
            else selector.Previous();

            nextHorizontalRepeatTime = Time.unscaledTime + (directionChanged ? repeatDelay : repeatRate);
        }

        lastHorizontalDirection = direction;
    }

    private int ReadHorizontalDirection()
    {
        Gamepad gamepad = Gamepad.current;
        Keyboard keyboard = Keyboard.current;

        if (gamepad != null)
        {
            if (gamepad.dpad.right.wasPressedThisFrame) return 1;
            if (gamepad.dpad.left.wasPressedThisFrame) return -1;

            float x = gamepad.leftStick.ReadValue().x;
            if (x <= -stickDeadzone) return -1;
            if (x >= stickDeadzone) return 1;
        }

        if (keyboard != null)
        {
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) return 1;
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) return -1;
        }

        return 0;
    }

    private void HandleSubmit()
    {
        bool submit = false;
        Gamepad gamepad = Gamepad.current;
        Keyboard keyboard = Keyboard.current;

        if (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame) submit = true;

        if (keyboard != null &&
            (keyboard.enterKey.wasPressedThisFrame ||
             keyboard.numpadEnterKey.wasPressedThisFrame ||
             keyboard.spaceKey.wasPressedThisFrame))
        {
            submit = true;
        }

        if (!submit) return;
        if (selectedIndex < 0 || selectedIndex >= buttons.Count) return;

        Interactable3DButton button = buttons[selectedIndex];
        if (button == null) return;

        if (button.TryGetComponent(out IOptionSelector selector)) selector.Apply();

        button.Press();
    }

    private void HandleCancel()
    {
        bool cancel = false;
        Gamepad gamepad = Gamepad.current;
        Keyboard keyboard = Keyboard.current;

        if (gamepad != null && gamepad.buttonEast.wasPressedThisFrame) cancel = true;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) cancel = true;

        if (!cancel) return;

        if (currentPanel == startMenuPanel)
        {
            CloseStartMenu();
        }
        else if (currentPanel == graphicsMenuPanel)
        {
            CloseGraphicsMenu();
        }
        else if (currentPanel == musicMenuPanel)
        {
            CloseMusicMenu();
        }
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null || string.IsNullOrEmpty(name)) return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) return child;

            Transform result = FindDeepChild(child, name);
            if (result != null) return result;
        }

        return null;
    }
}
