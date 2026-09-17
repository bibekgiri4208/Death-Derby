using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class Menu3DController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject graphicsMenuPanel;
    [SerializeField] private bool showMainMenuOnStart = true;

    [Header("Auto Wiring")]
    [SerializeField] private string graphicsButtonName = "Graphics Cube";
    [SerializeField] private string returnButtonName = "Return Cube";

    [Header("Gamepad / Keyboard")]
    [SerializeField] private bool enableNavigation = true;
    [SerializeField] private bool wrapNavigation = true;
    [SerializeField] private float stickDeadzone = 0.5f;
    [SerializeField] private float repeatDelay = 0.4f;
    [SerializeField] private float repeatRate = 0.15f;

    private readonly List<Interactable3DButton> buttons = new List<Interactable3DButton>();
    private int selectedIndex = -1;
    private int lastDirection;
    private float nextRepeatTime;
    private bool wired;

    public GameObject MainMenuPanel => mainMenuPanel;
    public GameObject GraphicsMenuPanel => graphicsMenuPanel;

    void Awake()
    {
        ResolvePanels();
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
        if (!enableNavigation || buttons.Count == 0) return;
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null) return;

        HandleNavigation();
        HandleSubmit();
        HandleCancel();
    }

    void OnDestroy()
    {
        UnsubscribeButtons();
    }

    public void ShowMainMenu()
    {
        SetPanelActive(graphicsMenuPanel, false);
        SetPanelActive(mainMenuPanel, true);
        RefreshButtons();
    }

    public void ShowGraphicsMenu()
    {
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(graphicsMenuPanel, true);
        RefreshButtons();
    }

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
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

        if (mainMenuPanel == null || graphicsMenuPanel == null)
        {
            Debug.LogWarning("Menu3DController: could not resolve Main Menu / Graphics Menu panels.", this);
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
                graphics.onClick.AddListener(ShowGraphicsMenu);
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

        GameObject activePanel = null;
        if (mainMenuPanel != null && mainMenuPanel.activeInHierarchy) activePanel = mainMenuPanel;
        else if (graphicsMenuPanel != null && graphicsMenuPanel.activeInHierarchy) activePanel = graphicsMenuPanel;

        if (activePanel != null)
        {
            Interactable3DButton[] found = activePanel.GetComponentsInChildren<Interactable3DButton>(true);
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
        if (button != null) button.Press();
    }

    private void HandleCancel()
    {
        bool cancel = false;
        Gamepad gamepad = Gamepad.current;
        Keyboard keyboard = Keyboard.current;

        if (gamepad != null && gamepad.buttonEast.wasPressedThisFrame) cancel = true;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) cancel = true;

        if (!cancel) return;

        if (graphicsMenuPanel != null && graphicsMenuPanel.activeInHierarchy)
        {
            ShowMainMenu();
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
