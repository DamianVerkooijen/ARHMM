using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RadarScannerLogic : MonoBehaviour
{
    [Header("Verplichte referenties")]
    public MissionController missionController;
    public Transform rotatingScanner;

    [Header("Marker prefab (leeg = witte cirkel fallback)")]
    public GameObject radarMarkerPrefab;

    [Header("Radar formaat")]
    public float radarRadius = 60f;
    [Tooltip("Auto-calibreer op de echte afstand van de eerste blip. Laat op 0 voor automatisch.")]
    public float maxVisualDistance = 0f;

    [Header("Ping / Scan instellingen")]
    [Tooltip("Hoeveel graden breed is de scanner-straal waarin de marker oplicht")]
    public float scanWidth = 15f;
    [Tooltip("Hoe snel vervaagt de blip nadat de scanner is gepasseerd")]
    public float fadeSpeed = 2.5f;
    [Tooltip("De minimale alpha tussen pings (0 = volledig onzichtbaar tot de volgende veeg!)")]
    public float minAlpha = 0.0f;

    public RectTransform radarRoot;

    // intern
    private Dictionary<string, RadarMarker> activeMarkers = new Dictionary<string, RadarMarker>();
    private MissionStateController cachedState;
    private RectTransform blipContainer;
    private bool autoDistanceCalibrated = false;

    // -------------------------------------------------------------------------
    void Start()
    {
        if (missionController != null)
            cachedState = missionController.GetComponentInChildren<MissionStateController>();

        if (cachedState == null)
            Debug.LogError("[Radar] MissionStateController NIET gevonden als child van MissionController!");

        // Maak een schone BlipContainer aan gecentreerd op dit object
        var containerGO = new GameObject("BlipContainer", typeof(RectTransform));
        containerGO.transform.SetParent(transform, false);
        blipContainer = containerGO.GetComponent<RectTransform>();
        blipContainer.anchorMin = new Vector2(0.5f, 0.5f);
        blipContainer.anchorMax = new Vector2(0.5f, 0.5f);
        blipContainer.pivot = new Vector2(0.5f, 0.5f);
        blipContainer.anchoredPosition = Vector2.zero;
    }

    void Update()
    {
        if (!IsReady())
        {
            HideAllMarkers();
            return;
        }


        var heli = missionController.manager.helicopter;
        var desired = BuildDesiredBlips();

        // Automatische afstands-kalibratie op basis van eerste verre marker
        if (!autoDistanceCalibrated && maxVisualDistance <= 0f && desired.Count > 0)
        {
            foreach (var kvp in desired)
            {
                float d = Vector3.Distance(heli.transform.position, kvp.Value.worldPos);
                if (d > 1f)
                {
                    maxVisualDistance = d * 1.2f;
                    autoDistanceCalibrated = true;
                    break;
                }
            }
        }
        if (maxVisualDistance <= 0f) maxVisualDistance = 15f;

        if (radarRoot != null)
        {
            radarRoot.localEulerAngles =
                new Vector3(0f, 0f, heli.transform.eulerAngles.y);
        }

        // Verwijder verouderde markers
        var toRemove = new List<string>();
        foreach (var id in activeMarkers.Keys)
            if (!desired.ContainsKey(id)) toRemove.Add(id);
        foreach (var id in toRemove)
        {
            if (activeMarkers[id] != null) Destroy(activeMarkers[id].gameObject);
            activeMarkers.Remove(id);
        }

        // Update of maak markers
        foreach (var kvp in desired)
        {
            if (!activeMarkers.TryGetValue(kvp.Key, out RadarMarker marker) || marker == null)
            {
                marker = CreateMarker(kvp.Key);
                activeMarkers[kvp.Key] = marker;
            }

            // Voer de transform van de heli in voor perfecte AR-lokale positiebepaling
            marker.UpdatePosition(
                kvp.Value.worldPos,
                heli.transform,
                radarRadius,
                maxVisualDistance);

            // Verwerk de radar-ping veeg en het oplichten
            HandlePing(marker, kvp.Value.isActiveTarget);
        }
    }

    private struct BlipData
    {
        public Vector3 worldPos;
        public bool isActiveTarget;
    }

    private Dictionary<string, BlipData> BuildDesiredBlips()
    {
        var result = new Dictionary<string, BlipData>();
        if (cachedState == null) return result;

        if (!missionController.IsMissionActive())
        {
            foreach (var mission in cachedState.missions)
            {
                if (mission.isCompleted) continue;
                Vector2 grid = cachedState.GetFirstTargetPosition(mission);
                Vector3 worldPos = missionController.manager.GetWorldPositionFromGrid(grid.x, grid.y);

                result[$"m_{mission.missionName}"] = new BlipData
                {
                    worldPos = worldPos,
                    isActiveTarget = false
                };
            }
        }
        else
        {
            Vector3 pos = missionController.GetCurrentTargetWorldPos();
            if (pos != Vector3.zero)
            {
                result["active"] = new BlipData
                {
                    worldPos = pos,
                    isActiveTarget = true
                };
            }
        }

        return result;
    }

    private void HandlePing(RadarMarker marker, bool isActive)
    {
        if (rotatingScanner == null)
        {
            marker.SetAlpha(1f);
            return;
        }

        // Pak de lokale UI positie van de marker op de radarschijf
        Vector2 mPos = marker.GetAnchoredPos();

        // Bereken de hoek (0 tot 360 graden) vanaf de Noord-as (omhoog) met de klok mee.
        // Mathf.Atan2(x, y) geeft exact 0 graden bij (0, 1), wat perfect matcht met UI-boven.
        float markerAngle = Mathf.Atan2(mPos.x, mPos.y) * Mathf.Rad2Deg;

        if (markerAngle < 0f)
            markerAngle += 360f;

        if (radarRoot != null)
        {
            markerAngle += radarRoot.localEulerAngles.z;
            markerAngle %= 360f;
        }

        // Bereken de actuele hoek van de roterende scanner-balk.
        // Omdat de scanner in Unity meestal met een negatieve Z-rotatie met de klok mee draait, inverteren we deze.
        float scannerAngle = -rotatingScanner.localEulerAngles.z;
        if (scannerAngle < 0f) scannerAngle += 360f;

        // Kortste hoekverschil berekenen
        float diff = Mathf.Abs(Mathf.DeltaAngle(scannerAngle, markerAngle));

        if (diff < scanWidth)
        {
            // DE VEEG RAAKT DE MARKER: Knal direct naar 100% zichtbaarheid!
            marker.SetAlpha(1f);
            marker.SetLabelVisible(true);
        }
        else
        {
            // DE VEEG IS VOORBIJ: Vervaag geleidelijk naar minAlpha (0.0f = onzichtbaar)
            float targetAlpha = isActive ? minAlpha * 4f : minAlpha; // Actieve missiedoelen mogen eventueel iets zichtbaarder blijven indien gewenst
            float speed = isActive ? fadeSpeed * 0.5f : fadeSpeed;

            float nextAlpha = Mathf.MoveTowards(marker.GetAlpha(), targetAlpha, Time.deltaTime * speed);
            marker.SetAlpha(nextAlpha);

            // Verberg het label als de blip bijna is uitgefaded
            marker.SetLabelVisible(nextAlpha > 0.3f);
        }
    }

    private RadarMarker CreateMarker(string id)
    {
        GameObject obj;
        if (radarMarkerPrefab != null)
        {
            obj = Instantiate(radarMarkerPrefab, blipContainer);
        }
        else
        {
            obj = new GameObject($"Blip_{id}", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            obj.transform.SetParent(blipContainer, false);
            var img = obj.GetComponent<Image>();
            img.color = Color.green; // Standaard felle radar-kleur fallback
        }

        var rt = obj.GetComponent<RectTransform>();
        if (rt == null) rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        if (rt.sizeDelta.magnitude < 1f) rt.sizeDelta = new Vector2(12f, 12f);

        var rm = obj.GetComponent<RadarMarker>();
        if (rm == null) rm = obj.AddComponent<RadarMarker>();
        return rm;
    }

    private bool IsReady() =>
        missionController != null &&
        missionController.manager != null &&
        missionController.manager.hasSpawned &&
        missionController.manager.helicopter != null &&
        cachedState != null;

    private void HideAllMarkers()
    {
        foreach (var m in activeMarkers.Values)
            if (m != null) m.SetAlpha(0f);
    }
}