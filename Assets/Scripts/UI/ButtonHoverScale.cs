using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHoverScale : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Scale")]
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float scaleSpeed = 14f;

    private Vector3 baseScale;
    private float currentScale = 1f;
    private float targetScale = 1f;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        currentScale = 1f;
        targetScale = 1f;
        transform.localScale = baseScale;
    }

    private void Update()
    {
        currentScale = Mathf.Lerp(currentScale, targetScale, scaleSpeed * Time.unscaledDeltaTime);
        transform.localScale = baseScale * currentScale;
    }

    public void OnSelect(BaseEventData eventData)
    {
        targetScale = hoverScale;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        targetScale = 1f;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = 1f;
    }
}