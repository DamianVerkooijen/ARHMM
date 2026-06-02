using UnityEngine;

public class RadarMarker : MonoBehaviour
{
    public float radarRadius = 60f;
    public float maxVisualDistance = 10f; // Zet deze op 10 voor festival gebruik
    private CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
    }

    public void UpdatePosition(Vector3 targetWorldPos, Vector3 playerWorldPos, float playerRotationY)
    {
        Vector3 relativePos = targetWorldPos - playerWorldPos;
        // Draai de positie mee met de helikopter
        Vector3 rotatedPos = Quaternion.Euler(0, -playerRotationY, 0) * relativePos;

        // UI X = Wereld X, UI Y = Wereld Z
        Vector2 radarPos = new Vector2(rotatedPos.x, rotatedPos.z);
        float realDist = radarPos.magnitude;

        // Bepaal de positie op de radar (clamp op de radius)
        if (realDist > maxVisualDistance)
            transform.localPosition = radarPos.normalized * radarRadius;
        else
            transform.localPosition = radarPos.normalized * (realDist / maxVisualDistance * radarRadius);
    }

    public void SetAlpha(float a)
    {
        if (canvasGroup != null) canvasGroup.alpha = a;
    }
}