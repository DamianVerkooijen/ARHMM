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
    public RadarMarker radarMarker;

    private bool initialized = false;

    private void Awake()
    {
        if (stateController == null) stateController = GetComponentInChildren<MissionStateController>();
        if (uiController == null) uiController = GetComponentInChildren<MissionUIController>();
        if (markerManager == null) markerManager = GetComponentInChildren<MarkerManager>();
        if (audioManager == null) audioManager = GetComponentInChildren<MissionAudioManager>();
    }

    private void Start()
    {
        if (stateController != null) stateController.Initialize(manager, registry);
        if (uiController != null) uiController.Initialize(stateController);
        if (markerManager != null) markerManager.Initialize(stateController, manager);
        if (audioManager != null) audioManager.Initialize(stateController, this);
    }

    private void Update()
    {
        if (!initialized && manager != null && manager.hasSpawned) initialized = true;

        if (!initialized || manager == null || manager.helicopter == null) return;
        if (manager.helicopter.transform.localPosition == Vector3.zero) return;

        if (stateController != null) stateController.EvaluateProgressionTick();
        if (markerManager != null) markerManager.EvaluateMarkerVisualPlacement();
    }

    public void OnActionButtonPressed()
    {
        if (stateController == null || stateController.selectedMissionIndex == -1) return;
        if (manager == null || manager.helicopter == null) return;

        if (audioManager != null) audioManager.PlayClickSound();

        stateController.ProcessMissionStep();
    }

    public bool IsMissionActive()
    {
        return stateController != null &&
               stateController.selectedMissionIndex != -1;
    }

    public Vector3 GetCurrentTargetWorldPos()
    {
        return stateController != null
            ? stateController.GetCurrentTargetWorldPos()
            : Vector3.zero;
    }
}