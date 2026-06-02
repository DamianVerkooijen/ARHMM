using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RadarMarker : MonoBehaviour
{
    public TMP_Text labelText;

    private CanvasGroup canvasGroup;
    private RectTransform rt;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        if (rt.sizeDelta.magnitude < 1f) rt.sizeDelta = new Vector2(12f, 12f);

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f; // Start onzichtbaar
    }

    public void UpdatePosition(Vector3 targetWorldPos, Transform heliTransform, float radarRadius, float maxVisualDistance)
    {
        if (heliTransform == null) return;

        // Transformeer de wereldpositie van het doel naar de LOKALE ruimte van de helikopter.
        // x = links/rechts, y = omhoog/omlaag, z = voor/achter ten opzichte van de neus!
        Vector3 localPos = heliTransform.InverseTransformPoint(targetWorldPos);

        // Map dit direct naar 2D UI coördinaten (localPos.x = Horizontaal, localPos.z = Verticaal)
        Vector2 flat = new Vector2(localPos.x, localPos.z);
        float dist = flat.magnitude;

        if (dist < 0.0001f)
        {
            rt.anchoredPosition = Vector2.zero;
            return;
        }

        // Bereken de schaalfactor binnen de maximale radar-afstand
        float scale = Mathf.Min(dist / maxVisualDistance, 1f);

        // Zet de positie op de radarschijf
        rt.anchoredPosition = flat.normalized * (scale * radarRadius);
    }

    public void SetAlpha(float alpha)
    {
        if (canvasGroup != null) canvasGroup.alpha = alpha;
    }

    public float GetAlpha() => canvasGroup != null ? canvasGroup.alpha : 0f;

    public void SetLabel(string text)
    {
        if (labelText != null) labelText.text = text;
    }

    public void SetLabelVisible(bool visible)
    {
        if (labelText != null) labelText.gameObject.SetActive(visible);
    }

    public Vector2 GetAnchoredPos() => rt != null ? rt.anchoredPosition : Vector2.zero;
}