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
    public float interactionRange = 0.5f;
    public float scanDuration = 2f;

    // Progression properties (State Engine)
    public int selectedMissionIndex { get; private set; } = -1;
    public int currentTargetIndex { get; private set; } = 0;
    public bool missionActive { get; private set; } = false;
    public float scanTimer { get; private set; } = 0f;

    // Structural decoupled architecture communication events
    public event Action<int> OnMissionStarted;
    public event Action<int> OnMissionCompleted;
    public event Action OnStepCompleted;
    public event Action OnMissionReset;
    
    // UI Notification bindings
    public event Action<bool, string, string> OnProximityChanged; // isInRange, stepInstruction, displayDescription
    public event Action<float> OnScanProgressUpdated; // Progress percentage

    private HelicopterManager manager;
    private LocationRegistry registry;
    private bool wasInRange = false;

    public void Initialize(HelicopterManager heliManager, LocationRegistry locRegistry)
    {
        manager = heliManager;
        registry = locRegistry;
    }

    public void EvaluateProgressionTick()
    {
        if (manager == null || manager.helicopter == null || registry == null) return;

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

        bool isInRange = closestIndex != -1;
        if (isInRange != wasInRange)
        {
            wasInRange = isInRange;
            if (isInRange)
                OnProximityChanged?.Invoke(true, "Druk op de knop om de missie te starten.", $"[ {missions[closestIndex].missionName} ]\nPress Button to Start");
            else
                OnProximityChanged?.Invoke(false, "", "Fly to a marker to start a mission");
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
            string activeDescription = missionActive ? currentMission.endLocation.description : currentMission.startLocation.description;
            string activeInstruction = missionActive ? currentMission.endLocation.shortInstruction : currentMission.startLocation.shortInstruction;

            if (currentMission.missionType == MissionType.Scan)
            {
                scanTimer += Time.deltaTime;
                float progress = Mathf.Round((scanTimer / scanDuration) * 100);
                OnScanProgressUpdated?.Invoke(progress);
                if (scanTimer >= scanDuration) CompleteStep();
            }
            else
            {
                OnProximityChanged?.Invoke(true, activeInstruction, activeDescription);
            }
        }
        else
        {
            OnProximityChanged?.Invoke(false, "", $"Goal: {currentMission.missionName}");
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
                OnStepCompleted?.Invoke(); 
            }
            else FinishMission();
        }
        else if (m.missionType == MissionType.SearchFind)
        {
            currentTargetIndex++;
            if (currentTargetIndex >= m.searchTargets.Count) FinishMission();
            else OnStepCompleted?.Invoke();
        }
    }

    public void CompleteStep()
    {
        currentTargetIndex++;
        scanTimer = 0f;
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

        // Loop door alle missies heen en zet ze weer open
        foreach (var mission in missions)
        {
            mission.isCompleted = false;
        }

        // Activeer het event zodat de UI en MarkerManager weten dat alles gereset is
        if (OnMissionReset != null) OnMissionReset();
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