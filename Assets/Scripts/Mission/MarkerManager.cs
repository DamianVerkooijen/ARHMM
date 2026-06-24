using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MarkerManager : MonoBehaviour
{
    [Header("Waypoint Prefab")]
    public GameObject waypointPrefab;

    [Header("Waypoint Settings")]
    public float hoverHeight = 2.5f;
    public string imageChildPath = "TeardropBase/ImageMask/InnerIcon";
    public Sprite defaultFallbackSprite;

    [Header("AR On-Screen Debugger")]
    public TextMeshProUGUI debugTextBox;

    private GameObject activeWaypoint;
    private Image activeWaypointIcon;

    private readonly List<GameObject> searchWaypoints = new List<GameObject>();

    private MissionStateController stateController;
    private HelicopterManager manager;
    private LocationRegistry registry;
    private Transform mainCameraTransform;

    public void Initialize(MissionStateController controller, HelicopterManager heliManager)
    {
        stateController = controller;
        manager = heliManager;
        registry = FindFirstObjectByType<LocationRegistry>();

        if (Camera.main != null) mainCameraTransform = Camera.main.transform;

        if (waypointPrefab != null)
        {
            activeWaypoint = Instantiate(waypointPrefab, transform);
            activeWaypointIcon = GetWaypointIcon(activeWaypoint);
            activeWaypoint.SetActive(false);
        }

        if (stateController == null)
        {
            Debug.LogError("MarkerManager: MissionStateController ontbreekt.", this);
            return;
        }

        stateController.OnMissionStarted += HandleMissionStarted;
        stateController.OnMissionCompleted += HandleMissionCompleted;
        stateController.OnMissionFailed += HandleMissionFailed;
        stateController.OnStepCompleted += HandleStepCompleted;
        stateController.OnMissionReset += HandleMissionReset;
    }

    private void OnDestroy()
    {
        if (stateController == null) return;

        stateController.OnMissionStarted -= HandleMissionStarted;
        stateController.OnMissionCompleted -= HandleMissionCompleted;
        stateController.OnMissionFailed -= HandleMissionFailed;
        stateController.OnStepCompleted -= HandleStepCompleted;
        stateController.OnMissionReset -= HandleMissionReset;
    }

    public void EvaluateMarkerVisualPlacement()
    {
        if (stateController == null || manager == null) return;

        if (stateController.selectedMissionIndex == -1)
        {
            HideAllWaypoints();
            return;
        }

        if (stateController.selectedMissionIndex >= stateController.missions.Count)
        {
            HideAllWaypoints();
            return;
        }

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
        UpdateActiveWaypoint();
    }

    private void UpdateActiveWaypoint()
    {
        if (activeWaypoint == null || stateController == null) return;

        Vector3 targetPosition = stateController.GetCurrentTargetWorldPos();

        activeWaypoint.transform.SetParent(GetActiveARAnchor(), true);
        activeWaypoint.transform.position = targetPosition + Vector3.up * hoverHeight;

        SetWaypointSprite(
            activeWaypointIcon,
            stateController.GetCurrentTargetLocationName()
        );

        activeWaypoint.SetActive(true);
        FaceCamera(activeWaypoint);
    }

    private void EnsureSearchWaypoints(MissionStateController.Mission mission)
    {
        if (mission.searchTargets == null || waypointPrefab == null) return;
        if (searchWaypoints.Count == mission.searchTargets.Count) return;

        ClearSearchWaypoints();

        for (int i = 0; i < mission.searchTargets.Count; i++)
        {
            GameObject waypoint = Instantiate(
                waypointPrefab,
                GetActiveARAnchor()
            );

            Image waypointIcon = GetWaypointIcon(waypoint);

            SetWaypointSprite(
                waypointIcon,
                mission.searchTargets[i].locationName
            );

            waypoint.SetActive(false);
            searchWaypoints.Add(waypoint);
        }
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

            if (waypoint.transform.parent != anchor) waypoint.transform.SetParent(anchor, true);

            Vector3 targetPosition = stateController.GetSearchTargetWorldPos(i);

            waypoint.transform.position =
                targetPosition + Vector3.up * hoverHeight;

            FaceCamera(waypoint);
        }
    }

    private bool IsAnyOrderSearch(MissionStateController.Mission mission)
    {
        return mission.missionType == MissionStateController.MissionType.SearchFind &&
               mission.searchCollectionMode == MissionStateController.SearchCollectionMode.AnyOrder;
    }

    private Image GetWaypointIcon(GameObject waypoint)
    {
        if (waypoint == null || string.IsNullOrEmpty(imageChildPath)) return null;

        Transform iconTransform = waypoint.transform.Find(imageChildPath);

        if (iconTransform == null)
        {
            Debug.LogWarning(
                $"MarkerManager: Icon child '{imageChildPath}' niet gevonden in waypoint prefab.",
                waypoint
            );

            return null;
        }

        return iconTransform.GetComponent<Image>();
    }

    private void SetWaypointSprite(Image icon, string locationName)
    {
        if (icon == null) return;

        Sprite locationSprite = null;

        if (registry != null && !string.IsNullOrEmpty(locationName))
            locationSprite = registry.GetLocationSprite(locationName);

        icon.sprite = locationSprite != null
            ? locationSprite
            : defaultFallbackSprite;
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
        if (waypoint == null) return;

        if (mainCameraTransform == null && Camera.main != null)
            mainCameraTransform = Camera.main.transform;

        if (mainCameraTransform == null) return;

        waypoint.transform.LookAt(
            waypoint.transform.position +
            mainCameraTransform.rotation * Vector3.forward,
            mainCameraTransform.rotation * Vector3.up
        );
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

    private void HideAllWaypoints()
    {
        if (activeWaypoint != null) activeWaypoint.SetActive(false);

        HideSearchWaypoints();
    }

    private void HandleMissionStarted(int index)
    {
        ClearSearchWaypoints();
        EvaluateMarkerVisualPlacement();
    }

    private void HandleMissionCompleted(int index)
    {
        HideAllWaypoints();
        ClearSearchWaypoints();
    }

    private void HandleMissionFailed(int index)
    {
        HideAllWaypoints();
        ClearSearchWaypoints();
    }

    private void HandleStepCompleted()
    {
        EvaluateMarkerVisualPlacement();
    }

    private void HandleMissionReset()
    {
        HideAllWaypoints();
        ClearSearchWaypoints();
    }

    [ContextMenu("Developer Tools / Start Mission Chain")]
    public void DebugStartMissionChain()
    {
        if (stateController == null) return;

        stateController.StartMissionChain();
    }

    public void ClearAllActiveMarkers()
    {
        HideAllWaypoints();
        ClearSearchWaypoints();
    }
}