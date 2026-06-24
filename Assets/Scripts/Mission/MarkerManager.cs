using System.Collections.Generic;
using UnityEngine;
using TMPro;
using JetBrains.Annotations;

public class MarkerManager : MonoBehaviour
{
    [Header("Physical Object Prefabs")]
    public GameObject markerPrefab;
    public GameObject waypointPrefab;

    [Header("UI Waypoint Settings")]
    public float hoverHeight = 2.5f;

    [Header("AR On-Screen Debugger")]
    public TextMeshProUGUI debugTextBox;

    private List<GameObject> spawnedMarkers = new List<GameObject>();
    private List<GameObject> searchWaypoints = new List<GameObject>();

    private GameObject activeWaypoint;
    private Transform mainCameraTransform;
    private MissionStateController stateController;
    private HelicopterManager manager;

    public void Initialize(MissionStateController controller, HelicopterManager heliManager)
    {
        stateController = controller;
        manager = heliManager;

        if (Camera.main != null) mainCameraTransform = Camera.main.transform;

        if (waypointPrefab != null)
        {
            activeWaypoint = Instantiate(waypointPrefab, transform);
            activeWaypoint.SetActive(false);
        }

        stateController.OnMissionStarted += HandleMissionStarted;
        stateController.OnMissionCompleted += HandleMissionCompleted;
        stateController.OnStepCompleted += HandleStepCompleted;
        stateController.OnMissionReset += HandleMissionReset;
    }

    private void OnDestroy()
    {
        if (stateController == null) return;

        stateController.OnMissionStarted -= HandleMissionStarted;
        stateController.OnMissionCompleted -= HandleMissionCompleted;
        stateController.OnStepCompleted -= HandleStepCompleted;
        stateController.OnMissionReset -= HandleMissionReset;
    }

    public void EvaluateMarkerVisualPlacement()
    {
        if (stateController == null || manager == null) return;

        if (stateController.selectedMissionIndex == -1)
        {
            if (activeWaypoint != null) activeWaypoint.SetActive(false);
            HideSearchWaypoints();
            return;
        }

        HideAllStartMarkers();

        MissionStateController.Mission mission =
            stateController.missions[stateController.selectedMissionIndex];

        if (IsAnyOrderSearch(mission))
        {
            if (activeWaypoint != null) activeWaypoint.SetActive(false);

            EnsureSearchWaypoints(mission);
            UpdateSearchWaypoints();
            return;
        }

        HideSearchWaypoints();

        if (activeWaypoint == null) return;

        Transform anchor = GetActiveARAnchor();
        if (activeWaypoint.transform.parent != anchor)
            activeWaypoint.transform.SetParent(anchor, true);

        activeWaypoint.SetActive(true);
        UpdateWaypointPositionAndSprite();
        FaceCamera(activeWaypoint);
    }

    public void SpawnWorldMarkers(List<MissionStateController.Mission> currentMissions)
    {
        foreach (GameObject marker in spawnedMarkers)
            if (marker != null) Destroy(marker);

        spawnedMarkers.Clear();
        Transform activeARAnchor = GetActiveARAnchor();

        for (int i = 0; i < currentMissions.Count; i++)
        {
            if (currentMissions[i].isCompleted) continue;

            Vector2 gridPos = stateController.GetFirstTargetPosition(currentMissions[i]);
            

            float preciseLocalX = Mathf.Lerp(manager.minX, manager.maxX, gridPos.x / 100f);
            float preciseLocalZ = Mathf.Lerp(manager.minZ, manager.maxZ, gridPos.y / 100f);

            GameObject marker = Instantiate(markerPrefab, activeARAnchor);
            marker.transform.localPosition = new Vector3(preciseLocalX, 0.01f, preciseLocalZ);
            marker.transform.localRotation = Quaternion.identity;
            marker.SetActive(true);

            spawnedMarkers.Add(marker);
        }
    }

    private void UpdateWaypointPositionAndSprite()
    {
        if (activeWaypoint == null || stateController == null) return;

        Vector3 targetPos = stateController.GetCurrentTargetWorldPos();
        Vector3 calculatedWaypointPos = new Vector3(targetPos.x, targetPos.y + hoverHeight, targetPos.z);
        activeWaypoint.transform.position = calculatedWaypointPos;
    }

    private void UpdateSearchWaypoints()
    {
        Transform anchor = GetActiveARAnchor();

        for (int i = 0; i < searchWaypoints.Count; i++)
        {
            GameObject waypoint = searchWaypoints[i];
            if (waypoint == null) continue;

            bool collected = stateController.IsSearchTargetCollected(i);
            waypoint.SetActive(!collected);

            if (collected) continue;

            if (waypoint.transform.parent != anchor)
                waypoint.transform.SetParent(anchor, true);

            Vector3 targetPos = stateController.GetSearchTargetWorldPos(i);
            waypoint.transform.position =
                new Vector3(targetPos.x, targetPos.y + hoverHeight, targetPos.z);

            FaceCamera(waypoint);
        }
    }

    private void SetWaypointSprite(GameObject waypoint, string locationName)
    {
        Transform iconTransform = waypoint.transform.Find(imageChildPath);
        if (iconTransform == null) return;

        Image icon = iconTransform.GetComponent<Image>();
        if (icon == null) return;

        Sprite sprite = registry != null
            ? registry.GetLocationSprite(locationName)
            : null;

        icon.sprite = sprite != null ? sprite : defaultFallbackSprite;
    }

    private bool IsAnyOrderSearch(MissionStateController.Mission mission)
    {
        return mission.missionType == MissionStateController.MissionType.SearchFind &&
               mission.searchCollectionMode == MissionStateController.SearchCollectionMode.AnyOrder;
    }

    private Transform GetActiveARAnchor()
    {
        if (manager != null &&
            manager.helicopter != null &&
            manager.helicopter.transform.parent != null)
        {
            return manager.helicopter.transform.parent;
        }

        return transform;
    }

    private void FaceCamera(GameObject waypoint)
    {
        if (mainCameraTransform == null || waypoint == null) return;

        waypoint.transform.LookAt(
            waypoint.transform.position + mainCameraTransform.rotation * Vector3.forward,
            mainCameraTransform.rotation * Vector3.up
        );
    }

    private void HideAllStartMarkers()
    {
        foreach (GameObject marker in spawnedMarkers)
            if (marker != null) marker.SetActive(false);
    }

    private void HideSearchWaypoints()
    {
        foreach (GameObject waypoint in searchWaypoints)
            if (waypoint != null) waypoint.SetActive(false);
    }

    private void ClearSearchWaypoints()
    {
        foreach (GameObject waypoint in searchWaypoints)
            if (waypoint != null) Destroy(waypoint);

        searchWaypoints.Clear();
    }

    

    private void HandleMissionStarted(int index)
    {
        HideAllStartMarkers();
        ClearSearchWaypoints();
    }

    private void HandleMissionCompleted(int index)
    {
        if (activeWaypoint != null) activeWaypoint.SetActive(false);

        ClearSearchWaypoints();
        SpawnWorldMarkers(stateController.missions);
    }

    private void HandleStepCompleted()
    {
        EvaluateMarkerVisualPlacement();
    }

    private void HandleMissionReset()
    {
        if (activeWaypoint != null) activeWaypoint.SetActive(false);

        ClearSearchWaypoints();
        SpawnWorldMarkers(stateController.missions);
    }

    [ContextMenu("Developer Tools / Force Start Mission 0")]
    public void DebugForceStartMission()
    {
        if (stateController == null) return;

        stateController.StartMission(0);
        SpawnWorldMarkers(stateController.missions);
    }

    public void ClearAllActiveMarkers()
    {
        foreach (GameObject marker in spawnedMarkers)
            if (marker != null) Destroy(marker);

        spawnedMarkers.Clear();
        ClearSearchWaypoints();

        if (activeWaypoint != null) activeWaypoint.SetActive(false);
    }
}