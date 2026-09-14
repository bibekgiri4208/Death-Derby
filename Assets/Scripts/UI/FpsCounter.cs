using TMPro;
using UnityEngine;

public class FpsCounter : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI fpsText;
    [SerializeField] private string textPrefix = "FPS: ";

    [Header("Settings")]
    [SerializeField] private float updateInterval = 0.5f;

    private float timer;
    private int frameCount;

    private void Update()
    {
        frameCount++;
        timer += Time.unscaledDeltaTime;

        if (timer >= updateInterval)
        {
            float fps = frameCount / timer;
            fpsText.text = textPrefix + Mathf.RoundToInt(fps);
            frameCount = 0;
            timer = 0f;
        }
    }
}
