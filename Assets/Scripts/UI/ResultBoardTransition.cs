using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Shared, unscaled result-screen entrance and input handoff for both game modes.
/// </summary>
[Serializable]
public class ResultBoardTransition
{
    [Tooltip("Duration of the result board's fade, slide and zoom, in real-time seconds.")]
    [Min(0.05f)]
    [SerializeField] private float duration = 0.6f;

    [Tooltip("Distance in UI units that the board slides upward from.")]
    [Min(0f)]
    [SerializeField] private float slideDistance = 80f;

    [Tooltip("Scale multiplier at the start of the entrance.")]
    [Range(0.5f, 1f)]
    [SerializeField] private float startScale = 0.85f;

    [Tooltip("Minimum real-time seconds after game over before menu buttons can be used.")]
    [Min(0f)]
    [SerializeField] private float inputDelay = 1f;

    [Tooltip("Submit/boost and mouse click must stay released for this long before enabling the menu.")]
    [Min(0.05f)]
    [SerializeField] private float releaseDelay = 0.25f;

    private MonoBehaviour owner;
    private CanvasGroup group;
    private Transform panel;
    private Vector3 restingPosition;
    private Vector3 restingScale;
    private Coroutine entrance;

    public void Initialize(MonoBehaviour host, GameObject board)
    {
        owner = host;
        panel = board.transform;
        restingPosition = panel.localPosition;
        restingScale = panel.localScale;
        group = board.GetComponent<CanvasGroup>();
        if (group == null)
            group = board.AddComponent<CanvasGroup>();
    }

    public void Show(Action selectFirstButton)
    {
        Hide();
        group.blocksRaycasts = true;
        ApplyProgress(0f);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        entrance = owner.StartCoroutine(Reveal(selectFirstButton));
    }

    public void Hide()
    {
        if (entrance != null)
        {
            owner.StopCoroutine(entrance);
            entrance = null;
        }

        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        if (panel != null)
        {
            panel.localPosition = restingPosition;
            panel.localScale = restingScale;
        }
    }

    private IEnumerator Reveal(Action selectFirstButton)
    {
        float elapsed = 0f;
        float releasedFor = 0f;
        float animationDuration = Mathf.Max(0.05f, duration);
        float minimumDelay = Mathf.Max(animationDuration, inputDelay);
        float quietPeriod = Mathf.Max(0.05f, releaseDelay);

        // Always defer the input handoff past the frame that ended the match.
        do
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / animationDuration);
            ApplyProgress(Mathf.SmoothStep(0f, 1f, progress));

            releasedFor = SubmitIsActive() ? 0f : releasedFor + Time.unscaledDeltaTime;
        }
        while (elapsed < minimumDelay || releasedFor < quietPeriod);

        ApplyProgress(1f);
        group.interactable = true;
        entrance = null;
        selectFirstButton?.Invoke();
    }

    private void ApplyProgress(float progress)
    {
        group.alpha = progress;
        panel.localPosition = restingPosition + Vector3.down * (slideDistance * (1f - progress));
        panel.localScale = restingScale * Mathf.Lerp(startScale, 1f, progress);
    }

    private static bool SubmitIsActive()
    {
        // Use the actual UI bindings too, so remapped submit buttons are protected.
        var module = EventSystem.current != null
            ? EventSystem.current.currentInputModule as InputSystemUIInputModule
            : null;
        if (module != null && module.submit != null && module.submit.action != null)
        {
            foreach (var control in module.submit.action.controls)
            {
                if (control is ButtonControl button && IsActive(button))
                    return true;
            }
        }

        // Either player's boost button can still be held when a Coop round ends.
        foreach (Gamepad pad in Gamepad.all)
        {
            if (IsActive(pad.buttonSouth))
                return true;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (IsActive(keyboard.enterKey) ||
                                 IsActive(keyboard.numpadEnterKey) || IsActive(keyboard.spaceKey)))
            return true;

        return Mouse.current != null && IsActive(Mouse.current.leftButton);
    }

    private static bool IsActive(ButtonControl button)
    {
        // Include the release frame so it cannot activate a newly enabled button.
        return button.isPressed || button.wasPressedThisFrame || button.wasReleasedThisFrame;
    }
}
