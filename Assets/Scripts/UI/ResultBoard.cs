using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Owns the result board. Stays hidden while the match runs, then reveals the
/// surviving player as the winner once a PlayerHealth dies. Both cars have their
/// controller / effects switched off on game over so driving input cannot overlap
/// the result screen button input.
/// </summary>
public class ResultBoard : MonoBehaviour
{
    [Header("Board")]
    [Tooltip("Panel hidden during the match and revealed on game over. Defaults to this GameObject.")]
    [SerializeField] private GameObject board;

    [Tooltip("Label that shows the winner, e.g. 'Player 1' or 'Player 2'.")]
    [SerializeField] private TextMeshProUGUI resultText;

    [Header("Menu Buttons")]
    [Tooltip("Result screen buttons in left-to-right order. The first one is focused when the board " +
             "appears. Navigation is restricted to this row so HUD elements such as the health bar " +
             "sliders cannot be selected while the board is up.")]
    [SerializeField] private Button[] menuButtons;

    [Header("Game Over")]
    [Tooltip("Stop gameplay time while the result screen is up. Menu animations use unscaled time, so buttons still react.")]
    [SerializeField] private bool freezeTime = true;

    [Tooltip("Disable both cars' controller, audio, weapon and effects on game over.")]
    [SerializeField] private bool disableCarSystems = true;

    [Tooltip("Unlock and show the mouse cursor while the result screen is up.")]
    [SerializeField] private bool showCursor = true;

    public bool IsShowing { get; private set; }

    /// <summary>
    /// True only while a result board in the current scene is on screen. Resolved from
    /// live objects rather than a cached flag so it can never survive a scene change.
    /// </summary>
    public static bool AnyShowing
    {
        get
        {
            ResultBoard board = FindAnyObjectByType<ResultBoard>();
            return board != null && board.IsShowing;
        }
    }

    public int WinnerIndex { get; private set; } = -1;

    private CanvasGroup boardGroup;
    private CursorLockMode previousLockMode;
    private bool cursorWasVisible;
    private bool frozeTime;
    private bool cursorUnlocked;
    private Navigation[] savedNavigation;

    private void Awake()
    {
        if (board == null)
            board = gameObject;

        boardGroup = board.GetComponent<CanvasGroup>();
        if (boardGroup == null)
            boardGroup = board.AddComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        Hide();
        PlayerHealth.OnPlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        PlayerHealth.OnPlayerDied -= HandlePlayerDied;
    }

    private void OnDestroy()
    {
        PlayerHealth.OnPlayerDied -= HandlePlayerDied;
        RestoreState();
    }

    private void HandlePlayerDied(int deadPlayerIndex)
    {
        if (IsShowing)
            return;

        // Player 1 is the left-hand car (index 0), Player 2 the right-hand one (index 1).
        int winnerIndex = deadPlayerIndex == 0 ? 1 : 0;
        Show(winnerIndex);
    }

    public void Show(int winnerIndex)
    {
        if (IsShowing)
            return;

        IsShowing = true;
        WinnerIndex = winnerIndex;

        // A negative winner means nobody out-scored the other player, e.g. a tied round.
        if (resultText != null)
            resultText.text = winnerIndex >= 0 ? $"Player {winnerIndex + 1}" : "Draw";

        if (disableCarSystems)
            DisableAllCars();

        if (freezeTime)
        {
            Time.timeScale = 0f;
            frozeTime = true;
        }

        if (showCursor)
        {
            previousLockMode = Cursor.lockState;
            cursorWasVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            cursorUnlocked = true;
        }

        SetBoardVisible(true);
        RestrictNavigation();
        SelectFirstButton();
    }

    public void Hide()
    {
        IsShowing = false;
        WinnerIndex = -1;
        RestoreState();
        SetBoardVisible(false);
    }

    // Undo everything Show() changed on global or shared state.
    private void RestoreState()
    {
        if (frozeTime)
        {
            frozeTime = false;
            Time.timeScale = 1f;
        }

        if (cursorUnlocked)
        {
            cursorUnlocked = false;
            Cursor.lockState = previousLockMode;
            Cursor.visible = cursorWasVisible;
        }

        RestoreNavigation();
    }

    private void SetBoardVisible(bool visible)
    {
        if (boardGroup == null)
            return;

        boardGroup.alpha = visible ? 1f : 0f;
        boardGroup.interactable = visible;
        boardGroup.blocksRaycasts = visible;
    }

    // Automatic navigation searches every Selectable in the scene by screen position, so
    // pushing the stick up from a bottom-centre button lands on a health bar slider.
    // An explicit left/right row keeps selection on the buttons only.
    private void RestrictNavigation()
    {
        if (menuButtons == null || menuButtons.Length == 0)
            return;

        savedNavigation = new Navigation[menuButtons.Length];

        for (int i = 0; i < menuButtons.Length; i++)
        {
            Button button = menuButtons[i];
            if (button == null)
                continue;

            savedNavigation[i] = button.navigation;

            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = null;
            navigation.selectOnDown = null;
            navigation.selectOnLeft = i > 0 ? menuButtons[i - 1] : null;
            navigation.selectOnRight = i < menuButtons.Length - 1 ? menuButtons[i + 1] : null;
            button.navigation = navigation;
        }
    }

    private void RestoreNavigation()
    {
        if (savedNavigation == null || menuButtons == null)
            return;

        for (int i = 0; i < menuButtons.Length && i < savedNavigation.Length; i++)
        {
            if (menuButtons[i] != null)
                menuButtons[i].navigation = savedNavigation[i];
        }

        savedNavigation = null;
    }

    private void DisableAllCars()
    {
        foreach (CarController car in FindObjectsByType<CarController>())
        {
            if (car == null)
                continue;

            PlayerHealth.DisableCarSystems(car.gameObject);
        }
    }

    private void SelectFirstButton()
    {
        Button first = FirstButton();
        if (first == null || EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    private Button FirstButton()
    {
        if (menuButtons == null)
            return null;

        foreach (Button button in menuButtons)
        {
            if (button != null)
                return button;
        }

        return null;
    }
}
