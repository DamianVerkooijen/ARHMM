using UnityEngine;

public class MissionController : MonoBehaviour
{
    [Header("Modular Component Handlers")]
    [SerializeField] private MissionStateController stateController;
    [SerializeField] private MissionUIController uiController;
    [SerializeField] private MarkerManager markerManager;
    [SerializeField] private MissionAudioManager audioManager;

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
        if (audioManager == null) audioManager = GetComponentInChildren<MissionAudioManager>();
        if (audioManager != null) audioManager.Initialize(stateController, this);
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

        if (manager.helicopter.transform.localPosition == Vector3.zero)
        {
            return;
        }

        if (stateController != null) stateController.EvaluateProgressionTick();
        if (markerManager != null) markerManager.EvaluateMarkerVisualPlacement();
    }

    public void OnActionButtonPressed()
    {
        if (stateController == null || manager == null || manager.helicopter == null) return;
        if (audioManager != null) audioManager.PlayClickSound();

        if (stateController.selectedMissionIndex == -1)
        {
            // Use the cached closest mission index — no need to re-scan
            int target = stateController.closestAvailableMissionIndex;
            if (target != -1)
            {
                stateController.StartMission(target);
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