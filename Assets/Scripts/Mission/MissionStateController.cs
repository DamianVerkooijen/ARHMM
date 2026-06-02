using System;
using System.Collections.Generic;
using UnityEngine;

public class MissionStateController : MonoBehaviour
{
    public enum MissionType { Delivery, SearchFind, Scan, Free }

    [System.Serializable]
    public class MissionTarget
    {
        [LocationName] public string locationName;
        public string actionText;
        public Sprite targetIcon;
        public string shortInstruction;
        [TextArea] public string description;
        public int reward;
    }

    [System.Serializable]
    public class Mission
    {
        public string missionName;
        public MissionType missionType;
        public bool isCompleted = false;
        public MissionTarget startLocation;
        public MissionTarget endLocation;
        public List<MissionTarget> searchTargets;
        public List<MissionTarget> scanTargets;
    }

    [Header("Missions Configuration")]
    public List<Mission> missions = new List<Mission>();
    public float interactionRange = 0.1f;
    public float scanDuration = 2f;
    [Tooltip("Standard icon for starting a new mission")]
    public Sprite defaultStartIcon;

    // Progression properties (State Engine)
    public int selectedMissionIndex { get; private set; } = -1;
    public int currentTargetIndex { get; private set; } = 0;
    public bool missionActive { get; private set; } = false;
    public float scanTimer { get; private set; } = 0f;

    // Cached closest mission index for button press handling
    public int closestAvailableMissionIndex { get; private set; } = -1;

    // Structural decoupled architecture communication events
    public event Action<int> OnMissionStarted;
    public event Action<int> OnMissionCompleted;
    public event Action OnStepCompleted;
    public event Action OnMissionReset;

    // UI Notification bindings
    public event Action<bool, string, Sprite, string> OnProximityChanged; // isInRange, stepInstruction, displayDescription
    public event Action<float> OnScanProgressUpdated; // Progress percentage

    private HelicopterManager manager;
    private LocationRegistry registry;
    private bool wasInRange = false;

    public void Initialize(HelicopterManager heliManager, LocationRegistry locRegistry)
    {
        manager = heliManager;
        registry = locRegistry;
    }

    private bool hasMovedFromSpawn = false;

    public void EvaluateProgressionTick()
    {
        if (manager == null || manager.helicopter == null || registry == null) return;

        if (manager.helicopter.transform.localPosition == Vector3.zero)
        {
            if (wasInRange)
            {
                wasInRange = false;
                OnProximityChanged?.Invoke(false, "", null, "Fly to a marker to start a mission");
            }
            return;
        }

        if (selectedMissionIndex == -1)
            EvaluateSelectionRange();
        else
            EvaluateActiveMissionRange();
    }

    private void EvaluateSelectionRange()
    {
        int closestIndex = -1;
        float closestDist = interactionRange;

        for (int i = 0; i < missions.Count; i++)
        {
            if (missions[i].isCompleted) continue;
            Vector2 gridPos = GetFirstTargetPosition(missions[i]);
            Vector3 worldPos = manager.GetWorldPositionFromGrid(gridPos.x, gridPos.y);
            float dist = GetFlatDistance(manager.helicopter.transform.position, worldPos);
            if (dist < closestDist) { closestDist = dist; closestIndex = i; }
        }

        // Cache so OnActionButtonPressed always knows which mission to start
        closestAvailableMissionIndex = closestIndex;

        bool isInRange = closestIndex != -1;
        if (isInRange != wasInRange)
        {
            wasInRange = isInRange;
            if (isInRange)
                OnProximityChanged?.Invoke(true, "Start Missie", defaultStartIcon, $"[ {missions[closestIndex].missionName} ]\nPress Button to Start");
            else
                OnProximityChanged?.Invoke(false, "", null, "Fly to a marker to start a mission");
        }
    }

    private void EvaluateActiveMissionRange()
    {
        Mission currentMission = missions[selectedMissionIndex];
        Vector2 targetGrid = GetCurrentTargetGrid(currentMission);
        Vector3 targetWorldPos = manager.GetWorldPositionFromGrid(targetGrid.x, targetGrid.y);
        float dist = GetFlatDistance(manager.helicopter.transform.position, targetWorldPos);

        if (dist < interactionRange)
        {
            MissionTarget currentTarget = null;
            if (currentMission.missionType == MissionType.Delivery)
            {
                currentTarget = missionActive ? currentMission.endLocation : currentMission.startLocation;
            }
            else if (currentMission.missionType == MissionType.SearchFind)
            {
                currentTarget = currentMission.searchTargets[currentTargetIndex];
            }
            else if (currentMission.missionType == MissionType.Scan)
            {
                currentTarget = currentMission.scanTargets[currentTargetIndex];
            }

            if (currentTarget != null)
            {
                if (currentMission.missionType == MissionType.Scan)
                {
                    scanTimer += Time.deltaTime;
                    float progress = Mathf.Round((scanTimer / scanDuration) * 100);
                    OnScanProgressUpdated?.Invoke(progress);
                    if (scanTimer >= scanDuration) CompleteStep();
                }
                else if (!wasInRange)
                {
                    // Only fire once when entering range, not every frame
                    wasInRange = true;
                    OnProximityChanged?.Invoke(true, currentTarget.actionText, currentTarget.targetIcon, currentTarget.description);
                }
            }
        }
        else if (wasInRange)
        {
            wasInRange = false;
            OnProximityChanged?.Invoke(false, "", null, $"Goal: {currentMission.missionName}");
        }
    }

    public void StartMission(int index)
    {
        selectedMissionIndex = index;
        missionActive = false;
        currentTargetIndex = 0;
        scanTimer = 0f;
        wasInRange = false;
        OnMissionStarted?.Invoke(index);
    }

    public void ProcessMissionStep()
    {
        if (selectedMissionIndex == -1) return;
        Mission m = missions[selectedMissionIndex];

        if (m.missionType == MissionType.Delivery)
        {
            if (!missionActive)
            {
                missionActive = true;
                scanTimer = 0;
                // Reset wasInRange zodat de proximity-check de volgende tick
                // opnieuw evalueert (speler kan nog op de marker staan).
                wasInRange = false;
                OnStepCompleted?.Invoke();
            }
            else FinishMission();
        }
        else if (m.missionType == MissionType.SearchFind)
        {
            currentTargetIndex++;
            wasInRange = false; // Zelfde reden: hercheck proximity voor volgend target
            if (currentTargetIndex >= m.searchTargets.Count) FinishMission();
            else OnStepCompleted?.Invoke();
        }
    }

    public void CompleteStep()
    {
        currentTargetIndex++;
        scanTimer = 0f;
        wasInRange = false; // Hercheck proximity voor volgend scan-target
        if (currentTargetIndex >= missions[selectedMissionIndex].scanTargets.Count) FinishMission();
        else OnStepCompleted?.Invoke();
    }

    public void FinishMission()
    {
        if (selectedMissionIndex == -1) return;
        int activeIndex = selectedMissionIndex;
        missions[activeIndex].isCompleted = true;

        OnMissionCompleted?.Invoke(activeIndex);

        selectedMissionIndex = -1;
        missionActive = false;
        currentTargetIndex = 0;
        scanTimer = 0f;
        wasInRange = false;
    }

    public void TriggerFullReset()
    {
        selectedMissionIndex = -1;
        currentTargetIndex = 0;
        missionActive = false;
        scanTimer = 0f;
        wasInRange = false;
        closestAvailableMissionIndex = -1;

        foreach (var m in missions) m.isCompleted = false;
        OnMissionReset?.Invoke();
    }

    // Help Getters
    public Vector2 GetFirstTargetPosition(Mission m)
    {
        if (m.missionType == MissionType.SearchFind && m.searchTargets != null && m.searchTargets.Count > 0)
            return registry.GetPosition(m.searchTargets[0].locationName);
        if (m.missionType == MissionType.Scan && m.scanTargets != null && m.scanTargets.Count > 0)
            return registry.GetPosition(m.scanTargets[0].locationName);
        return registry.GetPosition(m.startLocation.locationName);
    }

    public Vector2 GetCurrentTargetGrid(Mission currentMission)
    {
        switch (currentMission.missionType)
        {
            case MissionType.Delivery: return registry.GetPosition(missionActive ? currentMission.endLocation.locationName : currentMission.startLocation.locationName);
            case MissionType.SearchFind: return registry.GetPosition(currentMission.searchTargets[currentTargetIndex].locationName);
            case MissionType.Scan: return registry.GetPosition(currentMission.scanTargets[currentTargetIndex].locationName);
        }
        return registry.GetPosition(currentMission.startLocation.locationName);
    }

    public Vector3 GetCurrentTargetWorldPos()
    {
        if (selectedMissionIndex == -1) return Vector3.zero;
        Vector2 grid = GetCurrentTargetGrid(missions[selectedMissionIndex]);
        return manager.GetWorldPositionFromGrid(grid.x, grid.y);
    }

    public Vector3 GetClosestAvailableMissionPos()
    {
        float closestDist = float.MaxValue;
        Vector3 closestPos = Vector3.zero;

        foreach (var m in missions)
        {
            if (m.isCompleted) continue;
            Vector2 grid = GetFirstTargetPosition(m);
            Vector3 worldPos = manager.GetWorldPositionFromGrid(grid.x, grid.y);
            float d = GetFlatDistance(manager.helicopter.transform.position, worldPos);
            if (d < closestDist) { closestDist = d; closestPos = worldPos; }
        }
        return closestPos;
    }

    private float GetFlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
}