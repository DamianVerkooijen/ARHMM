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

    [Tooltip("De minimale alpha tussen pings")]
    public float minAlpha = 0f;

    public RectTransform radarRoot;

    private readonly Dictionary<string, RadarMarker> activeMarkers =
        new Dictionary<string, RadarMarker>();

    private MissionStateController cachedState;
    private RectTransform blipContainer;
    private bool autoDistanceCalibrated;

    private void Start()
    {
        if (missionController != null)
            cachedState = missionController.GetComponentInChildren<MissionStateController>();

        if (cachedState == null)
            Debug.LogError("[Radar] MissionStateController niet gevonden als child van MissionController!");

        GameObject containerObject =
            new GameObject("BlipContainer", typeof(RectTransform));

        containerObject.transform.SetParent(transform, false);

        blipContainer = containerObject.GetComponent<RectTransform>();
        blipContainer.anchorMin = new Vector2(0.5f, 0.5f);
        blipContainer.anchorMax = new Vector2(0.5f, 0.5f);
        blipContainer.pivot = new Vector2(0.5f, 0.5f);
        blipContainer.anchoredPosition = Vector2.zero;
    }

    private void Update()
    {
        if (!IsReady())
        {
            ClearAllMarkers();
            return;
        }

        GameObject helicopter = missionController.manager.helicopter;
        Dictionary<string, BlipData> desiredBlips = BuildDesiredBlips();

        UpdateAutomaticDistance(helicopter.transform, desiredBlips);

        if (maxVisualDistance <= 0f) maxVisualDistance = 15f;

        if (radarRoot != null)
            radarRoot.localEulerAngles = new Vector3(0f, 0f, helicopter.transform.eulerAngles.y);

        RemoveObsoleteMarkers(desiredBlips);

        foreach (KeyValuePair<string, BlipData> entry in desiredBlips)
        {
            if (!activeMarkers.TryGetValue(entry.Key, out RadarMarker marker) || marker == null)
            {
                marker = CreateMarker(entry.Key);
                activeMarkers[entry.Key] = marker;
            }

            marker.UpdatePosition(
                entry.Value.worldPosition,
                helicopter.transform,
                radarRadius,
                maxVisualDistance
            );

            HandlePing(marker, entry.Value.isActiveTarget);
        }
    }

    private struct BlipData
    {
        public Vector3 worldPosition;
        public bool isActiveTarget;
    }

    private Dictionary<string, BlipData> BuildDesiredBlips()
    {
        Dictionary<string, BlipData> result =
            new Dictionary<string, BlipData>();

        if (cachedState == null || !missionController.IsMissionActive()) return result;
        if (cachedState.selectedMissionIndex < 0 || cachedState.selectedMissionIndex >= cachedState.missions.Count) return result;

        MissionStateController.Mission mission =
            cachedState.missions[cachedState.selectedMissionIndex];

        bool isAnyOrderSearch =
            mission.missionType == MissionStateController.MissionType.SearchFind &&
            mission.searchCollectionMode == MissionStateController.SearchCollectionMode.AnyOrder;

        if (isAnyOrderSearch)
        {
            AddAnyOrderSearchBlips(result, mission);
            return result;
        }

        Vector3 currentTargetPosition =
            missionController.GetCurrentTargetWorldPos();

        if (currentTargetPosition != Vector3.zero)
        {
            result["active_target"] = new BlipData
            {
                worldPosition = currentTargetPosition,
                isActiveTarget = true
            };
        }

        return result;
    }

    private void AddAnyOrderSearchBlips(
        Dictionary<string, BlipData> result,
        MissionStateController.Mission mission)
    {
        if (mission.searchTargets == null) return;

        for (int i = 0; i < mission.searchTargets.Count; i++)
        {
            if (cachedState.IsSearchTargetCollected(i)) continue;

            Vector3 targetPosition =
                cachedState.GetSearchTargetWorldPos(i);

            if (targetPosition == Vector3.zero) continue;

            result[$"search_target_{i}"] = new BlipData
            {
                worldPosition = targetPosition,
                isActiveTarget = true
            };
        }
    }

    private void UpdateAutomaticDistance(
        Transform helicopter,
        Dictionary<string, BlipData> desiredBlips)
    {
        if (autoDistanceCalibrated || maxVisualDistance > 0f || desiredBlips.Count == 0) return;

        float furthestDistance = 0f;

        foreach (KeyValuePair<string, BlipData> entry in desiredBlips)
        {
            float distance = Vector3.Distance(
                helicopter.position,
                entry.Value.worldPosition
            );

            if (distance > furthestDistance)
                furthestDistance = distance;
        }

        if (furthestDistance <= 1f) return;

        maxVisualDistance = furthestDistance * 1.2f;
        autoDistanceCalibrated = true;
    }

    private void RemoveObsoleteMarkers(
        Dictionary<string, BlipData> desiredBlips)
    {
        List<string> markersToRemove = new List<string>();

        foreach (string markerId in activeMarkers.Keys)
        {
            if (!desiredBlips.ContainsKey(markerId))
                markersToRemove.Add(markerId);
        }

        foreach (string markerId in markersToRemove)
        {
            if (activeMarkers[markerId] != null)
                Destroy(activeMarkers[markerId].gameObject);

            activeMarkers.Remove(markerId);
        }
    }

    private void HandlePing(RadarMarker marker, bool isActive)
    {
        if (marker == null) return;

        if (rotatingScanner == null)
        {
            marker.SetAlpha(1f);
            marker.SetLabelVisible(true);
            return;
        }

        Vector2 markerPosition = marker.GetAnchoredPos();

        float markerAngle =
            Mathf.Atan2(markerPosition.x, markerPosition.y) *
            Mathf.Rad2Deg;

        if (markerAngle < 0f) markerAngle += 360f;

        if (radarRoot != null)
        {
            markerAngle += radarRoot.localEulerAngles.z;
            markerAngle %= 360f;
        }

        float scannerAngle =
            -rotatingScanner.localEulerAngles.z;

        if (scannerAngle < 0f) scannerAngle += 360f;

        float difference =
            Mathf.Abs(
                Mathf.DeltaAngle(scannerAngle, markerAngle)
            );

        if (difference < scanWidth)
        {
            marker.SetAlpha(1f);
            marker.SetLabelVisible(true);
            return;
        }

        float targetAlpha =
            isActive ? minAlpha * 4f : minAlpha;

        float speed =
            isActive ? fadeSpeed * 0.5f : fadeSpeed;

        float nextAlpha = Mathf.MoveTowards(
            marker.GetAlpha(),
            targetAlpha,
            Time.deltaTime * speed
        );

        marker.SetAlpha(nextAlpha);
        marker.SetLabelVisible(nextAlpha > 0.3f);
    }

    private RadarMarker CreateMarker(string id)
    {
        GameObject markerObject;

        if (radarMarkerPrefab != null)
        {
            markerObject = Instantiate(
                radarMarkerPrefab,
                blipContainer
            );
        }
        else
        {
            markerObject = new GameObject(
                $"Blip_{id}",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image)
            );

            markerObject.transform.SetParent(
                blipContainer,
                false
            );

            Image image = markerObject.GetComponent<Image>();
            image.color = Color.green;
        }

        RectTransform rectTransform =
            markerObject.GetComponent<RectTransform>();

        if (rectTransform == null)
            rectTransform = markerObject.AddComponent<RectTransform>();

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        if (rectTransform.sizeDelta.magnitude < 1f)
            rectTransform.sizeDelta = new Vector2(12f, 12f);

        RadarMarker radarMarker =
            markerObject.GetComponent<RadarMarker>();

        if (radarMarker == null)
            radarMarker = markerObject.AddComponent<RadarMarker>();

        return radarMarker;
    }

    private bool IsReady()
    {
        return missionController != null &&
               missionController.manager != null &&
               missionController.manager.hasSpawned &&
               missionController.manager.helicopter != null &&
               cachedState != null;
    }

    private void ClearAllMarkers()
    {
        foreach (RadarMarker marker in activeMarkers.Values)
        {
            if (marker != null)
                Destroy(marker.gameObject);
        }

        activeMarkers.Clear();
    }
}