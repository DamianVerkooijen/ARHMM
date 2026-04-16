using UnityEngine;

public class RadarScannerLogic : MonoBehaviour
{
    public RadarMarker marker;
    public float scanWidth = 20f;
    private CanvasGroup markerCanvasGroup;

    void Start()
    {
        markerCanvasGroup = marker.GetComponent<CanvasGroup>();
    }

    void Update()
    {
        // Bereken de hoek van de marker (0-360)
        Vector3 dir = marker.transform.localPosition;
        float markerAngle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
        if (markerAngle < 0) markerAngle += 360;

        // Haal de hoek van de scannerhouder op
        float scannerAngle = (transform.localEulerAngles.z % 360 + 360) % 360;

        // Check of de hoeken overeenkomen (met scanWidth marge)
        float diff = Mathf.DeltaAngle(scannerAngle, markerAngle);

        if (Mathf.Abs(diff) < scanWidth)
        {
            markerCanvasGroup.alpha = 1f; // Oplichten
        }
        else
        {
            markerCanvasGroup.alpha = Mathf.Max(0.2f, markerCanvasGroup.alpha - Time.deltaTime * 2f); // Faden
        }
    }
}