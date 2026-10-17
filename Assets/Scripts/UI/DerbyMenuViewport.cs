using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Fits a reference composition to the whole canvas, including split-screen games.</summary>
public class DerbyMenuViewport : UIBehaviour
{
    private RectTransform viewport;
    private RectTransform composition;

    public void Initialize(RectTransform target)
    {
        composition = target;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) viewport = canvas.rootCanvas.transform as RectTransform;
        Fit();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        Fit();
    }

    private void LateUpdate() => Fit();

    private void Fit()
    {
        if (viewport == null || composition == null) return;
        float scale = Mathf.Min(viewport.rect.width / 1280f, viewport.rect.height / 720f);
        if (scale <= 0f) return;
        composition.sizeDelta = viewport.rect.size / scale;
        composition.position = viewport.TransformPoint(viewport.rect.center);
        // Cancel legacy panel scaling without changing any serialized scene transforms.
        Vector3 parentScale = composition.parent.lossyScale;
        Vector3 canvasScale = viewport.lossyScale;
        composition.localScale = new Vector3(scale * canvasScale.x / Mathf.Max(.0001f, parentScale.x),
                                             scale * canvasScale.y / Mathf.Max(.0001f, parentScale.y), 1f);
    }
}
