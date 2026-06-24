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
    public class PopupContent
    {
        public string title;
        [TextArea] public string description;
        public Sprite icon;
        public string actionButtonText;
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
        
        [Header("Popup Content")]
        public PopupContent missionIntroPopup;
        public List<PopupContent> missionPopups = new List<PopupContent>();
        public PopupContent missionCompletionPopup;
    }

    [Header("Missions Configuration")]
    public List<Mission> missions = new List<Mission>();
    public float interactionRange = 0.1f;
    public float scanDuration = 3f; // Tijd in seconden voor de scan

    public Sprite defaultStartIcon;

    public int selectedMissionIndex { get; private set; } = -1;
    public int currentTargetIndex { get; private set; } = 0;
    public int currentPopupIndex { get; private set; } = 0;
    public bool missionActive { get; private set; } = false;
    public float scanTimer { get; private set; } = 0f;
    public bool isScanning { get; private set; } = false;
    public int closestAvailableMissionIndex { get; private set; } = -1;

    // Events
    public event Action<int> OnMissionStarted;
    public event Action<int> OnMissionCompleted;
    public event Action OnStepCompleted;
    public event Action OnMissionReset;
    public event Action<bool, string, Sprite, string> OnProximityChanged;
    public event Action<float> OnScanProgressUpdated;

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

        if (manager.helicopter.transform.localPosition == Vector3.zero)
        {
            if (wasInRange)
            {
                ResetProximityState();
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

            if (dist < closestDist)
            {
                closestDist = dist;
                closestIndex = i;
            }
        }

        closestAvailableMissionIndex = closestIndex;
        bool isInRange = closestIndex != -1;

        if (isInRange != wasInRange)
        {
            wasInRange = isInRange;
            if (isInRange)
            {
                OnProximityChanged?.Invoke(false, "", null, $"[ {missions[closestIndex].missionName} ] beschikbaar.");
            }
            else
            {
                OnProximityChanged?.Invoke(false, "", null, "Vlieg naar een marker om te beginnen.");
            }
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
            if (currentMission.missionType == MissionType.Scan)
            {
                if (!wasInRange)
                {
                    wasInRange = true;
                    scanTimer = 0f;
                    isScanning = false;
                    OnScanProgressUpdated?.Invoke(0f);
                    
                    // Show location popup from missionPopups list
                    ShowCurrentPopup(() => { isScanning = true; });
                }

                if (isScanning)
                {
                    scanTimer += Time.deltaTime;
                    float progress = Mathf.Clamp((scanTimer / scanDuration) * 100f, 0f, 100f);
                    OnScanProgressUpdated?.Invoke(Mathf.Round(progress));

                    if (scanTimer >= scanDuration)
                    {
                        CompleteStep();
                    }
                }
            }
            else
            {
                // Delivery or SearchFind
                if (!wasInRange)
                {
                    wasInRange = true;
                    // Show location popup from missionPopups list
                    ShowCurrentPopup(() => { ProcessMissionStep(); });
                }
            }
        }
        else if (wasInRange)
        {
            // Speler vliegt weg van de locatie -> Sluit pop-up en reset scan status
            ResetProximityState();
        }
    }

    private void ResetProximityState()
    {
        wasInRange = false;
        isScanning = false;
        scanTimer = 0f;
        OnScanProgressUpdated?.Invoke(0f);

        if (PopupManager.Instance != null) PopupManager.Instance.ClosePopup();

        string label = selectedMissionIndex != -1 ? $"Doel: {missions[selectedMissionIndex].missionName}" : "Vlieg naar een marker.";
        OnProximityChanged?.Invoke(false, "", null, label);
    }

    public void StartMission(int index)
    {
        selectedMissionIndex = index;
        missionActive = false;
        currentTargetIndex = 0;
        currentPopupIndex = 0;
        scanTimer = 0f;
        isScanning = false;
        wasInRange = false;

        OnScanProgressUpdated?.Invoke(0f);
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
                currentPopupIndex++;
                wasInRange = false;
                OnStepCompleted?.Invoke();
            }
            else
            {
                FinishMission();
            }
        }
        else if (m.missionType == MissionType.SearchFind)
        {
            currentTargetIndex++;
            currentPopupIndex++;
            wasInRange = false;
            if (currentTargetIndex >= m.searchTargets.Count)
                FinishMission();
            else
                OnStepCompleted?.Invoke();
        }
    }

    public void CompleteStep()
    {
        currentTargetIndex++;
        currentPopupIndex++;
        scanTimer = 0f;
        isScanning = false;
        wasInRange = false;
        OnScanProgressUpdated?.Invoke(0f);

        if (currentTargetIndex >= missions[selectedMissionIndex].scanTargets.Count)
            FinishMission();
        else
            OnStepCompleted?.Invoke();
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
        currentPopupIndex = 0;
        scanTimer = 0f;
        isScanning = false;
        wasInRange = false;
        OnScanProgressUpdated?.Invoke(0f);
    }

    public void TriggerFullReset()
    {
        selectedMissionIndex = -1;
        currentTargetIndex = 0;
        currentPopupIndex = 0;
        missionActive = false;
        scanTimer = 0f;
        isScanning = false;
        wasInRange = false;
        closestAvailableMissionIndex = -1;

        foreach (var m in missions) m.isCompleted = false;

        OnScanProgressUpdated?.Invoke(0f);
        OnMissionReset?.Invoke();
    }

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
            case MissionType.Delivery:
                return registry.GetPosition(missionActive ? currentMission.endLocation.locationName : currentMission.startLocation.locationName);
            case MissionType.SearchFind:
                return registry.GetPosition(currentMission.searchTargets[currentTargetIndex].locationName);
            case MissionType.Scan:
                return registry.GetPosition(currentMission.scanTargets[currentTargetIndex].locationName);
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

            if (d < closestDist)
            {
                closestDist = d;
                closestPos = worldPos;
            }
        }
        return closestPos;
    }

    private MissionTarget GetCurrentTarget(Mission currentMission)
    {
        if (currentMission.missionType == MissionType.Delivery)
            return missionActive ? currentMission.endLocation : currentMission.startLocation;
        if (currentMission.missionType == MissionType.SearchFind)
            return (currentMission.searchTargets != null && currentMission.searchTargets.Count > 0) ? currentMission.searchTargets[currentTargetIndex] : null;
        if (currentMission.missionType == MissionType.Scan)
            return (currentMission.scanTargets != null && currentMission.scanTargets.Count > 0) ? currentMission.scanTargets[currentTargetIndex] : null;

        return null;
    }

    private float GetFlatDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }

    private void ShowCurrentPopup(Action onConfirmCallback = null)
    {
        if (selectedMissionIndex == -1 || PopupManager.Instance == null) return;

        Mission mission = missions[selectedMissionIndex];
        if (mission.missionPopups == null || mission.missionPopups.Count == 0) return;

        // Clamp popup index to valid range
        int popupIndex = Mathf.Clamp(currentPopupIndex, 0, mission.missionPopups.Count - 1);
        PopupContent popup = mission.missionPopups[popupIndex];

        PopupManager.Instance.ShowPopup(
            popup.title,
            popup.description,
            popup.icon,
            popup.actionButtonText,
            onConfirmCallback
        );
    }

    public void ResetAllMissionsToStart()
    {
        // 1. Wipe all active tracking parameters completely
        selectedMissionIndex = -1;
        currentTargetIndex = 0;
        currentPopupIndex = 0;
        missionActive = false;
        scanTimer = 0f;
        isScanning = false;
        wasInRange = false;
        closestAvailableMissionIndex = -1;

        // 2. Set all structural mission progression data back to uncompleted
        foreach (var mission in missions)
        {
            mission.isCompleted = false;
        }

        // 3. Force-clear the progress bars
        OnScanProgressUpdated?.Invoke(0f);

        // 4. MAGIC SPARK: Tell the MissionUIController to run its ResetUI routine!
        OnMissionReset?.Invoke();
    }
}