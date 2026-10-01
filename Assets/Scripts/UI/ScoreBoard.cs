using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Desert-mode result board. Separate from the PvP ResultBoard on purpose: that one
/// names the winning player, this one reports the round's kill score, so neither has
/// to carry a mode flag for the other's behaviour.
/// </summary>
public class ScoreBoard : MonoBehaviour
{
    [Header("Board")]
    [Tooltip("Panel hidden during the match and revealed on game over. Defaults to this GameObject.")]
    [SerializeField] private GameObject board;

    [Tooltip("Centre label. Shows the score, e.g. 'Score: 12'.")]
    [SerializeField] private TextMeshProUGUI resultText;

    [Header("Board Text")]
    [Tooltip("Leave empty to look up the headline label by name under the board.")]
    [SerializeField] private TextMeshProUGUI headerText;

    [Tooltip("Name of the big top label under the board, used when no Header Text reference is set.")]
    [SerializeField] private string headerTextName = "Winner logo";

    [Tooltip("Headline when the round ends because the player was destroyed.")]
    [SerializeField] private string diedHeaderText = "You Died";

    [Tooltip("Headline when the round ends because the clock ran out.")]
    [SerializeField] private string timeUpHeaderText = "Time Up";

    [Tooltip("{0} is replaced with the final kill count.")]
    [SerializeField] private string scoreFormat = "Score: {0}";

    [Header("Menu Buttons")]
    [Tooltip("Result screen buttons in left-to-right order. The first one is focused when the board " +
             "appears. Navigation is restricted to this row so HUD elements cannot be selected " +
             "while the board is up.")]
    [SerializeField] private Button[] menuButtons;

    [Header("Player")]
    [Tooltip("Tag of the spawned player car. Its Health drives the board when the car is destroyed.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Game Over")]
    [Tooltip("Stop gameplay time while the result screen is up. Menu animations use unscaled time, so buttons still react.")]
    [SerializeField] private bool freezeTime = true;

    [Tooltip("Disable the cars' controller, audio, weapon and effects on game over.")]
    [SerializeField] private bool disableCarSystems = true;

    [Tooltip("Unlock and show the mouse cursor while the result screen is up.")]
    [SerializeField] private bool showCursor = true;

    public bool IsShowing { get; private set; }

    public int Score { get; private set; }

    public static bool AnyShowing
    {
        get
        {
            ScoreBoard board = FindAnyObjectByType<ScoreBoard>();
            return board != null && board.IsShowing;
        }
    }

    private CanvasGroup boardGroup;
    private CursorLockMode previousLockMode;
    private bool cursorWasVisible;
    private bool frozeTime;
    private bool cursorUnlocked;
    private Navigation[] savedNavigation;
    private TextMeshProUGUI cachedHeaderLabel;
    private Health playerHealth;

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

    // The Desert car is spawned by DesertCarSpawner in Awake and carries the plain
    // Health component, not PlayerHealth, so watch its health for the death that
    // should bring up the board.
    private void Start()
    {
        BindToPlayerHealth();
    }

    private void OnDisable()
    {
        PlayerHealth.OnPlayerDied -= HandlePlayerDied;
        UnbindPlayerHealth();
    }

    private void OnDestroy()
    {
        PlayerHealth.OnPlayerDied -= HandlePlayerDied;
        UnbindPlayerHealth();
        RestoreState();
    }

    private void BindToPlayerHealth()
    {
        if (playerHealth != null || string.IsNullOrEmpty(playerTag))
            return;

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
            return;

        playerHealth = player.GetComponentInChildren<Health>(true);
        if (playerHealth != null)
            playerHealth.OnHealthChanged += HandlePlayerHealthChanged;
    }

    private void UnbindPlayerHealth()
    {
        if (playerHealth == null)
            return;

        playerHealth.OnHealthChanged -= HandlePlayerHealthChanged;
        playerHealth = null;
    }

    private void HandlePlayerHealthChanged(float current, float max)
    {
        if (current <= 0f)
            Show(false);
    }

    private void HandlePlayerDied(int deadPlayerIndex)
    {
        Show(false);
    }

    /// <summary>
    /// Reveals the board with the current kill count.
    /// </summary>
    /// <param name="timeExpired">True when the round timer ran out rather than the player dying.</param>
    public void Show(bool timeExpired)
    {
        if (IsShowing)
            return;

        IsShowing = true;
        Score = CurrentScore();

        ApplyHeaderText(timeExpired);
        ApplyScoreText(Score);

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
        RestoreState();
        SetBoardVisible(false);
    }

    private void ApplyHeaderText(bool timeExpired)
    {
        TextMeshProUGUI label = GetHeaderLabel();
        if (label == null)
            return;

        label.text = timeExpired ? timeUpHeaderText : diedHeaderText;
    }

    private void ApplyScoreText(int score)
    {
        if (resultText == null)
            return;

        resultText.text = scoreFormat.Contains("{0}")
            ? string.Format(scoreFormat, score)
            : scoreFormat + score;
    }

    // Desert is single player, so the one counter in the scene is always the one to report.
    private static int CurrentScore()
    {
        int best = 0;

        foreach (KillCounter counter in FindObjectsByType<KillCounter>())
        {
            if (counter != null && counter.KillCount > best)
                best = counter.KillCount;
        }

        return best;
    }

    private TextMeshProUGUI GetHeaderLabel()
    {
        if (headerText != null)
            return headerText;

        if (cachedHeaderLabel != null)
            return cachedHeaderLabel;

        if (board == null || string.IsNullOrEmpty(headerTextName))
            return null;

        Transform labelTransform = FindChildByName(board.transform, headerTextName);
        if (labelTransform != null)
            cachedHeaderLabel = labelTransform.GetComponentInChildren<TextMeshProUGUI>(true);

        return cachedHeaderLabel;
    }

    private static Transform FindChildByName(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;

            Transform nested = FindChildByName(child, name);
            if (nested != null)
                return nested;
        }

        return null;
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

    // Automatic navigation searches every Selectable by screen position, so pushing the
    // stick up from a bottom-centre button can land on a HUD slider instead.
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