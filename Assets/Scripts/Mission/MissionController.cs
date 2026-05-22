using UnityEngine;

public class MissionController : MonoBehaviour
{
    [Header("Modular Component Handlers")]
    [SerializeField] private MissionStateController stateController;
    [SerializeField] private MissionUIController uiController;
    [SerializeField] private MarkerManager markerManager;

    [Header("AR World Scene Tracking References")]
    public HelicopterManager manager;
    public LocationRegistry registry;
    public RadarMarker radarMarker; // Retained field reference for external systems

    private bool initialized = false;

    private void Awake()
    {
        // Enforce component gathering across child objects if needed
        if (stateController == null) stateController = GetComponentInChildren<MissionStateController>();
        if (uiController == null) uiController = GetComponentInChildren<MissionUIController>();
        if (markerManager == null) markerManager = GetComponentInChildren<MarkerManager>();
    }

    private void Start()
    {
        if (stateController != null) stateController.Initialize(manager, registry);
        if (uiController != null) uiController.Initialize(stateController);
        if (markerManager != null) markerManager.Initialize(stateController, manager);
    }

    private void Update()
    {
        if (!initialized && manager != null && manager.hasSpawned)
        {
            if (markerManager != null && stateController != null)
            {
                markerManager.SpawnWorldMarkers(stateController.missions);
            }
            initialized = true;
        }

        if (!initialized || manager.helicopter == null) return;

        if (stateController != null) stateController.EvaluateProgressionTick();
        if (markerManager != null) markerManager.EvaluateMarkerVisualPlacement();
    }

    public void OnActionButtonPressed()
    {
        if (stateController == null || manager == null || manager.helicopter == null) return;

        if (stateController.selectedMissionIndex == -1)
        {
            // Process starting context checks
            for (int i = 0; i < stateController.missions.Count; i++)
            {
                if (stateController.missions[i].isCompleted) continue;
                Vector2 pos = stateController.GetFirstTargetPosition(stateController.missions[i]);
                float d = Vector2.Distance(
                    new Vector2(manager.helicopter.transform.position.x, manager.helicopter.transform.position.z), 
                    manager.GetWorldPositionFromGrid(pos.x, pos.y)
                );
                
                if (d < stateController.interactionRange) 
                { 
                    stateController.StartMission(i); 
                    return; 
                }
            }
        }
        else
        {
            stateController.ProcessMissionStep();
        }
    }

    public void TriggerFullReset()
    {
        if (stateController != null) stateController.TriggerFullReset();
        if (manager != null) manager.SoftResetHeli();
    }

    // Pass-through calls to keep dependencies intact across external scripts
    public bool IsMissionActive() => stateController != null && stateController.selectedMissionIndex != -1;
    public Vector3 GetCurrentTargetWorldPos() => stateController != null ? stateController.GetCurrentTargetWorldPos() : Vector3.zero;
    public Vector3 GetClosestAvailableMissionPos() => stateController != null ? stateController.GetClosestAvailableMissionPos() : Vector3.zero;
}