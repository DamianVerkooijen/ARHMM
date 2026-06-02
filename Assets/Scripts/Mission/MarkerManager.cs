using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MarkerManager : MonoBehaviour
{
    [Header("Physical Object Prefabs")]
    public GameObject markerPrefab;
    public GameObject waypointPrefab;

    [Header("UI Waypoint Settings")]
    public string imageChildPath = "TeardropBase/ImageMask/InnerIcon";
    public float hoverHeight = 2.5f;
    public Sprite defaultFallbackSprite;

    [Header("AR On-Screen Debugger")]
    public TextMeshProUGUI debugTextBox;

    private List<GameObject> spawnedMarkers = new List<GameObject>();
    private GameObject activeWaypoint;
    private Image innerIconComponent;

    private LocationRegistry registry;
    private Transform mainCameraTransform;
    private MissionStateController stateController;
    private HelicopterManager manager;

    public void Initialize(MissionStateController controller, HelicopterManager heliManager)
    {
        stateController = controller;
        manager = heliManager;

        registry = FindFirstObjectByType<LocationRegistry>();
        if (Camera.main != null) mainCameraTransform = Camera.main.transform;

        if (waypointPrefab != null)
        {
            activeWaypoint = Instantiate(waypointPrefab, transform);
            Transform iconTransform = activeWaypoint.transform.Find(imageChildPath);
            if (iconTransform != null)
            {
                innerIconComponent = iconTransform.GetComponent<Image>();
            }

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

        if (stateController.selectedMissionIndex != -1 && activeWaypoint != null)
        {
            if (manager.helicopter != null && manager.helicopter.transform.parent != null)
            {
                Transform activeARAnchor = manager.helicopter.transform.parent;

                if (activeWaypoint.transform.parent != activeARAnchor)
                {
                    activeWaypoint.transform.SetParent(activeARAnchor, true);
                }
            }

            if (!activeWaypoint.activeSelf)
            {
                activeWaypoint.SetActive(true);
            }

            UpdateWaypointPositionAndSprite();

            if (mainCameraTransform != null)
            {
                activeWaypoint.transform.LookAt(activeWaypoint.transform.position + mainCameraTransform.rotation * Vector3.forward, mainCameraTransform.rotation * Vector3.up);
            }

            HideAllStartMarkers();
        }
        else if (activeWaypoint != null && activeWaypoint.activeSelf)
        {
            activeWaypoint.SetActive(false);
        }
    }

    public void SpawnWorldMarkers(List<MissionStateController.Mission> currentMissions)
    {
        foreach (var marker in spawnedMarkers) if (marker != null) Destroy(marker);
        spawnedMarkers.Clear();

        Transform activeARAnchor = transform;
        if (manager != null && manager.helicopter != null && manager.helicopter.transform.parent != null)
        {
            activeARAnchor = manager.helicopter.transform.parent;
        }

        for (int i = 0; i < currentMissions.Count; i++)
        {
            Vector2 gridPos = stateController.GetFirstTargetPosition(currentMissions[i]);

            float percentX = gridPos.x / 100f;
            float percentZ = gridPos.y / 100f;

            float preciseLocalX = Mathf.Lerp(manager.minX, manager.maxX, percentX);
            float preciseLocalZ = Mathf.Lerp(manager.minZ, manager.maxZ, percentZ);

            Vector3 pureLocalPosition = new Vector3(preciseLocalX, 0.01f, preciseLocalZ);

            GameObject marker = Instantiate(markerPrefab, activeARAnchor);
            marker.transform.localPosition = pureLocalPosition;
            marker.transform.localRotation = Quaternion.identity;
            if (currentMissions[i].isCompleted)
            {
                marker.SetActive(false);
            }

            spawnedMarkers.Add(marker);
        }
        if (stateController != null && stateController.selectedMissionIndex != -1)
        {
            HideAllStartMarkers();
        }
    }

    private void UpdateWaypointPositionAndSprite()
    {
        if (activeWaypoint == null || stateController == null) return;

        Vector3 targetPos = stateController.GetCurrentTargetWorldPos();
        Vector3 calculatedWaypointPos = new Vector3(targetPos.x, targetPos.y + hoverHeight, targetPos.z);
        activeWaypoint.transform.position = calculatedWaypointPos;

        string activeLocationName = GetCurrentLocationNameFromState();

        Sprite targetSprite = null;
        if (registry != null && !string.IsNullOrEmpty(activeLocationName))
        {
            targetSprite = registry.GetLocationSprite(activeLocationName);
        }

        if (innerIconComponent != null)
        {
            innerIconComponent.sprite = (targetSprite != null) ? targetSprite : defaultFallbackSprite;
        }
    }
    private void HideAllStartMarkers()
    {
        for (int i = 0; i < spawnedMarkers.Count; i++)
        {
            if (spawnedMarkers[i] != null)
            {
                spawnedMarkers[i].SetActive(false);
            }
        }
    }

    private string GetCurrentLocationNameFromState()
    {
        if (stateController == null || stateController.selectedMissionIndex == -1) return string.Empty;

        var activeMission = stateController.missions[stateController.selectedMissionIndex];

        switch (activeMission.missionType)
        {
            case MissionStateController.MissionType.Delivery:
                return stateController.missionActive ? activeMission.endLocation.locationName : activeMission.startLocation.locationName;

            case MissionStateController.MissionType.SearchFind:
                if (activeMission.searchTargets != null && stateController.currentTargetIndex < activeMission.searchTargets.Count)
                    return activeMission.searchTargets[stateController.currentTargetIndex].locationName;
                break;

            case MissionStateController.MissionType.Scan:
                if (activeMission.scanTargets != null && stateController.currentTargetIndex < activeMission.scanTargets.Count)
                    return activeMission.scanTargets[stateController.currentTargetIndex].locationName;
                break;
        }
        return string.Empty;
    }

    private void HandleMissionStarted(int index)
    {
        HideAllStartMarkers();
    }

    private void HandleMissionCompleted(int index)
    {
        if (activeWaypoint != null) activeWaypoint.SetActive(false);
        SpawnWorldMarkers(stateController.missions);
    }

    private void HandleStepCompleted()
    {
        UpdateWaypointPositionAndSprite();
    }

    private void HandleMissionReset()
    {
        if (activeWaypoint != null) activeWaypoint.SetActive(false);
        SpawnWorldMarkers(stateController.missions);
    }

    [ContextMenu("Developer Tools / Force Start Mission 0")]
    public void DebugForceStartMission()
    {
        if (stateController != null)
        {
            stateController.StartMission(0);
            SpawnWorldMarkers(stateController.missions);
        }
    }

    public void ClearAllActiveMarkers()
{
    foreach (var marker in spawnedMarkers) 
    {
        if (marker != null) Destroy(marker);
    }
    spawnedMarkers.Clear();

    if (activeWaypoint != null)
    {
        activeWaypoint.SetActive(false);
    }
}
}