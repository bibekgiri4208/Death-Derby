using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

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

    [Header("First Selected Button")]
    [Tooltip("Button focused when the board appears so controller users can submit straight away.")]
    [SerializeField] private GameObject firstSelectedButton;

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

        if (resultText != null)
            resultText.text = $"Player {winnerIndex + 1}";

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
        SelectFirstButton();
    }

    public void Hide()
    {
        IsShowing = false;
        WinnerIndex = -1;
        RestoreState();
        SetBoardVisible(false);
    }

    // Undo everything Show() changed on global state. Time.timeScale in particular
    // outlives a scene load, so it has to be restored even on OnDestroy.
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
    }

    private void SetBoardVisible(bool visible)
    {
        if (boardGroup == null)
            return;

        boardGroup.alpha = visible ? 1f : 0f;
        boardGroup.interactable = visible;
        boardGroup.blocksRaycasts = visible;
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
        if (firstSelectedButton == null || EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(firstSelectedButton);
    }
}
