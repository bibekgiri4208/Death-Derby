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
    [Min(1f)]
    [SerializeField] private float fontSize = 84f;

    [Header("Timing")]
    [Min(0.1f)]
    [SerializeField] private float displayDuration = 1.4f;
    [Min(0f)]
    [SerializeField] private float fadeInDuration = 0.25f;
    [Min(0f)]
    [SerializeField] private float fadeOutDuration = 0.45f;

    [Header("Scale")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float startScale = 0.3f;
    [Range(0.1f, 0.9f)]
    [SerializeField] private float endScale = 0.5f;

    [Header("Float")]
    [Range(0f, 60f)]
    [SerializeField] private float floatDistance = 6f;
    [Range(0.5f, 8f)]
    [SerializeField] private float floatSpeed = 2.5f;

    private int killCount;
    private int nextMilestoneIndex;
    private Coroutine popupRoutine;
    private Vector3 baseScale;
    private Vector2 basePosition;
    private Color baseColor;
    private Color baseColorSolid;

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
        ResetTransforms();
    }

    private void Start()
    {
        if (milestones == null || milestones.Length == 0)
            Reset();

        if (popupText != null)
        {
            popupText.fontSize = fontSize;
            RectTransform rt = popupText.rectTransform;
            rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 900f), Mathf.Max(rt.sizeDelta.y, 140f));

            baseScale = popupText.transform.localScale;
            basePosition = rt != null ? rt.anchoredPosition : Vector2.zero;
            baseColorSolid = popupText.color;
            baseColor = baseColorSolid;
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

        RectTransform rt = popupText.rectTransform;
        popupText.text = label;

        float p = 0f;
        while (p < 1f)
        {
            p += Time.unscaledDeltaTime / fadeInDuration;
            if (p < 1f)
            {
                float ease = p * p * (3f - 2f * p);
                popupText.transform.localScale = baseScale * Mathf.Lerp(startScale, 1f, ease);
                Color c1 = baseColorSolid;
                c1.a = Mathf.Clamp01(p);
                popupText.color = c1;
            }
            yield return null;
        }

        popupText.transform.localScale = baseScale;
        popupText.color = baseColorSolid;

        float t = 0f;
        while (t < displayDuration)
        {
            t += Time.unscaledDeltaTime;
            if (rt != null)
                rt.anchoredPosition = basePosition + Vector2.up * (floatDistance * Mathf.Sin(Time.unscaledTime * floatSpeed));
            yield return null;
        }

        float end = 0f;
        while (end < 1f)
        {
            end += Time.unscaledDeltaTime / fadeOutDuration;
            if (end < 1f)
            {
                float ease = end * end * (3f - 2f * end);
                popupText.transform.localScale = baseScale * Mathf.Lerp(1f, endScale, ease);
                Color c2 = baseColorSolid;
                c2.a = Mathf.Clamp01(1f - end);
                popupText.color = c2;
            }
            yield return null;
        }

        ResetTransforms();
        popupRoutine = null;
    }

    private void ResetTransforms()
    {
        if (popupText == null) return;
        popupText.transform.localScale = baseScale;
        RectTransform rt = popupText.rectTransform;
        if (rt != null)
            rt.anchoredPosition = basePosition;
        popupText.color = baseColor;
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
        rt.sizeDelta = new Vector2(900f, 140f);

        TextMeshProUGUI text = holder.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;

        return text;
    }
}