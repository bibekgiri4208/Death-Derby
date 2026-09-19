using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class KillStreakPopup : MonoBehaviour
{
    [Serializable]
    public class Milestone
    {
        public int killCount = 20;
        public string label = "Killing Spree";
    }

    [Header("Milestones")]
    [SerializeField] private Milestone[] milestones;

    [Header("Popup UI")]
    [SerializeField] private TextMeshProUGUI popupText;

    [Header("Animation")]
    [Min(0.1f)]
    [SerializeField] private float displayDuration = 1.4f;
    [Min(0f)]
    [SerializeField] private float fadeInDuration = 0.2f;
    [Min(0f)]
    [SerializeField] private float fadeOutDuration = 0.5f;
    [Range(0.1f, 1f)]
    [SerializeField] private float startScale = 0.65f;
    [Range(0f, 50f)]
    [SerializeField] private float riseDistance = 8f;

    private int killCount;
    private int nextMilestoneIndex;
    private Coroutine popupRoutine;
    private Vector3 baseScale;
    private Vector2 basePosition;
    private Color baseColor;

    private void Reset()
    {
        milestones = new Milestone[]
        {
            new Milestone { killCount = 20, label = "Killing Spree" },
            new Milestone { killCount = 50, label = "Massacre" },
            new Milestone { killCount = 100, label = "Annihilation" }
        };
    }

    private void Awake()
    {
        if (popupText == null)
            popupText = CreateRuntimePopupText();
    }

    private void OnEnable()
    {
        ZombieAI.OnZombieKilled += OnZombieKilled;
    }

    private void OnDisable()
    {
        ZombieAI.OnZombieKilled -= OnZombieKilled;
        if (popupRoutine != null)
        {
            StopCoroutine(popupRoutine);
            popupRoutine = null;
        }
    }

    private void Start()
    {
        if (milestones == null || milestones.Length == 0)
            Reset();

        if (popupText != null)
        {
            baseScale = popupText.transform.localScale;
            RectTransform rt = popupText.rectTransform;
            basePosition = rt != null ? rt.anchoredPosition : Vector2.zero;
            baseColor = popupText.color;
            baseColor.a = 0f;
            popupText.color = baseColor;
        }
    }

    private void OnZombieKilled()
    {
        killCount++;
        if (milestones == null || nextMilestoneIndex >= milestones.Length) return;
        if (killCount >= milestones[nextMilestoneIndex].killCount)
        {
            ShowPopup(milestones[nextMilestoneIndex].label);
            nextMilestoneIndex++;
        }
    }

    private void ShowPopup(string label)
    {
        if (popupRoutine != null)
            StopCoroutine(popupRoutine);
        popupRoutine = StartCoroutine(PopupRoutine(label));
    }

    private IEnumerator PopupRoutine(string label)
    {
        if (popupText == null) yield break;

        float duration = fadeInDuration + displayDuration + fadeOutDuration;
        RectTransform rt = popupText.rectTransform;

        popupText.text = label;
        popupText.color = baseColor;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            float normalized = Mathf.Clamp01(t / duration);

            float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fadeInDuration));
            popupText.transform.localScale = baseScale * Mathf.Lerp(startScale, 1f, grow);

            float rise = riseDistance * normalized * normalized;
            if (rt != null)
                rt.anchoredPosition = basePosition + Vector2.up * rise;

            Color c = baseColor;
            if (t < fadeInDuration)
            {
                c.a = Mathf.Clamp01(t / fadeInDuration);
            }
            else if (t > duration - fadeOutDuration)
            {
                c.a = Mathf.Clamp01((duration - t) / fadeOutDuration);
            }
            else
            {
                c.a = 1f;
            }
            popupText.color = c;

            yield return null;
        }

        popupText.transform.localScale = baseScale;
        if (rt != null)
            rt.anchoredPosition = basePosition;
        popupText.color = baseColor;
        popupRoutine = null;
    }

    private TextMeshProUGUI CreateRuntimePopupText()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return null;

        GameObject holder = new GameObject("Kill Popup (Runtime)");
        holder.transform.SetParent(canvas.transform, false);

        RectTransform rt = holder.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -70f);
        rt.sizeDelta = new Vector2(600f, 100f);

        TextMeshProUGUI text = holder.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 56f;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;

        return text;
    }
}