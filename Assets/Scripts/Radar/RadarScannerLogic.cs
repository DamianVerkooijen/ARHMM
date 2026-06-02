using UnityEngine;

public class RadarScannerLogic : MonoBehaviour
{
    public MissionController missionController;
    public RadarMarker radarMarker;
    public Transform rotatingScanner; // Het draaiende streepje

    [Header("Ping Settings")]
    public float scanWidth = 25f;
    public float fadeSpeed = 0.7f;
    public float minAlpha = 0.05f; // Een heel klein beetje zichtbaar blijven is vaak fijner

    void Update()
    {
        // 1. Veiligheidschecks: als de heli er niet is, marker weg.
        if (missionController == null || missionController.manager == null || !missionController.manager.hasSpawned || missionController.manager.helicopter == null)
        {
            if (radarMarker != null) radarMarker.SetAlpha(0f);
            return;
        }

        // 2. Doel bepalen
        Vector3 targetPos = missionController.IsMissionActive()
            ? missionController.GetCurrentTargetWorldPos()
            : missionController.GetClosestAvailableMissionPos();

        // 3. Update positie en ping
        if (targetPos != Vector3.zero)
        {
            radarMarker.UpdatePosition(targetPos,
                missionController.manager.helicopter.transform.position,
                missionController.manager.helicopter.transform.eulerAngles.y);

            HandlePing();
        }
        else
        {
            radarMarker.SetAlpha(0f);
        }
    }

    void HandlePing()
    {
        // 1. Verkrijg de CanvasGroup van de marker (voor transparantie)
        CanvasGroup cg = radarMarker.GetComponent<CanvasGroup>();
        if (cg == null) return;

        // 2. Bereken de hoek van de marker t.o.v. de bovenkant van de radar
        Vector2 mPos = radarMarker.transform.localPosition;
        // We gebruiken -Atan2(x,y) om de hoek te matchen met Unity's rotatie (CW)
        float markerAngle = Mathf.Atan2(mPos.x, mPos.y) * Mathf.Rad2Deg;
        if (markerAngle < 0) markerAngle += 360f;

        // 3. Bereken de hoek van de scanner
        // Unity rotatie gaat van 0 naar -360 voor de klok mee, we maken dit 0 tot 360
        float scannerAngle = -rotatingScanner.localEulerAngles.z;
        if (scannerAngle < 0) scannerAngle += 360f;

        // 4. Check of de scanner over de marker gaat
        float angleDiff = Mathf.Abs(Mathf.DeltaAngle(scannerAngle, markerAngle));

        if (angleDiff < scanWidth)
        {
            // De scanner raakt de marker: Zet hem op maximale helderheid (1.5 voor extra 'glow' effect)
            cg.alpha = 1f;
        }
        else
        {
            // De scanner is voorbij: Langzaam uitfaden naar 0
            // Verhoog 'fadeSpeed' als hij te lang zichtbaar blijft (bijv. naar 1.5f of 2.0f)
            cg.alpha = Mathf.MoveTowards(cg.alpha, 0f, Time.deltaTime * fadeSpeed);
        }
    }
}