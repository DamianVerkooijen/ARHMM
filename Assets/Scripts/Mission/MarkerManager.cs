using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // REQUIRED for TextMeshPro UI text boxes

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
    [Tooltip("Assign a TextMeshPro - Text (UI) object from your canvas here to see logs live on the tablet")]
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

        //LogToScreen("Initializing Marker Manager...");

        if (waypointPrefab != null)
        {
            activeWaypoint = Instantiate(waypointPrefab, transform);
            Transform iconTransform = activeWaypoint.transform.Find(imageChildPath);
            if (iconTransform != null)
            {
                innerIconComponent = iconTransform.GetComponent<Image>();
                //LogToScreen("Waypoint prefab successfully loaded and cached.");
            }
            else
            {
                //LogToScreen($"WARNING: InnerIcon not found at path: '{imageChildPath}'");
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
            if (!activeWaypoint.activeSelf)
            {
                //LogToScreen($"Mission {stateController.selectedMissionIndex} active. Activating floating waypoint prefab.");
                activeWaypoint.SetActive(true);
            }

            UpdateWaypointPositionAndSprite();

            if (mainCameraTransform != null)
            {
                activeWaypoint.transform.LookAt(activeWaypoint.transform.position + mainCameraTransform.rotation * Vector3.forward, mainCameraTransform.rotation * Vector3.up);
            }
        }
        else if (activeWaypoint != null && activeWaypoint.activeSelf)
        {
            //LogToScreen("No active mission. Deactivating floating waypoint.");
            activeWaypoint.SetActive(false);
        }
    }

    public void SpawnWorldMarkers(List<MissionStateController.Mission> currentMissions)
    {
        foreach (var marker in spawnedMarkers) if (marker != null) Destroy(marker);
        spawnedMarkers.Clear();

        //LogToScreen($"Spawning selection rings for {currentMissions.Count} missions.");

        for (int i = 0; i < currentMissions.Count; i++)
        {
            if (currentMissions[i].isCompleted) continue;
            Vector2 gridPos = stateController.GetFirstTargetPosition(currentMissions[i]);
            Vector3 worldPos = manager.GetWorldPositionFromGrid(gridPos.x, gridPos.y);
            
            Vector3 markerPos = worldPos;
            markerPos.y += 0.01f;

            GameObject marker = Instantiate(markerPrefab, markerPos, Quaternion.identity, transform);
            spawnedMarkers.Add(marker);
        }
    }

    private void UpdateWaypointPositionAndSprite()
    {
        if (activeWaypoint == null || stateController == null) return;

        // Calculate and apply positions
        Vector3 targetPos = stateController.GetCurrentTargetWorldPos();
        Vector3 calculatedWaypointPos = new Vector3(targetPos.x, targetPos.y + hoverHeight, targetPos.z);
        activeWaypoint.transform.position = calculatedWaypointPos;

        // Resolve data structures
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

        // LIVE ONSCREEN TELEMETRY FEEDBACK
        string spriteStatus = (targetSprite != null) ? "FOUND custom photo" : "USING fallback circle";
        //LogToScreen($"WAYPOINT UPDATED:\n• Target Name: '{activeLocationName}'\n• World Coords: {calculatedWaypointPos}\n• Sprite Status: {spriteStatus}");
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

    /// <summary>
    /// Custom logging method that updates both the Unity Editor Console AND your on-screen UI text box.
    /// </summary>
    // private void LogToScreen(string message)
    // {
    //     Debug.Log($"[MarkerManager] {message}");

    //     if (debugTextBox != null)
    //     {
    //         // Prepends timestamps so you see the newest events rolling in sequentially at the top
    //         debugTextBox.text = $"[{Time.time:F2}] {message}\n\n" + debugTextBox.text;

    //         // Optional safety boundary: trims text if it gets too long for a single layout container
    //         if (debugTextBox.text.Length > 1500)
    //         {
    //             debugTextBox.text = debugTextBox.text.Substring(0, 1000);
    //         }
    //     }
    // }

    private void HandleMissionStarted(int index)
    {
        //LogToScreen($"Event Fired: HandleMissionStarted for Index {index}. Hiding select rings.");
        foreach (var marker in spawnedMarkers) if (marker != null) marker.SetActive(false);
    }

    private void HandleMissionCompleted(int index)
    {
        //LogToScreen($"Event Fired: HandleMissionCompleted for Index {index}.");
        if (activeWaypoint != null) activeWaypoint.SetActive(false);
        SpawnWorldMarkers(stateController.missions);
    }

    private void HandleStepCompleted()
    {
        //LogToScreen("Event Fired: HandleStepCompleted.");
        UpdateWaypointPositionAndSprite();
    }

    private void HandleMissionReset()
    {
        //LogToScreen("Event Fired: HandleMissionReset.");
        if (activeWaypoint != null) activeWaypoint.SetActive(false);
        SpawnWorldMarkers(stateController.missions);
    }

    // Context menu helper tool for editor quick testing 
    [ContextMenu("Developer Tools / Force Start Mission 0")]
    public void DebugForceStartMission()
    {
        if (stateController != null)
        {
            //LogToScreen("CRITICAL: Developer context menu option triggered inside inspector!");
            stateController.StartMission(0);
            SpawnWorldMarkers(stateController.missions);
        }
    }
}