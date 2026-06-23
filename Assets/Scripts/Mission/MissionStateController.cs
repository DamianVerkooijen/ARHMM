using System;
using System.Collections.Generic;
using UnityEngine;

public class MissionStateController : MonoBehaviour
{
    public enum MissionType { Delivery, SearchFind, Scan }
    public enum SearchCollectionMode { InOrder, AnyOrder }

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

        [Header("Delivery Settings")]
        public bool useDeliveryTimer = false;

        [Min(1f)]
        public float deliveryTimeLimit = 30f;

        [Header("Search & Find Settings")]
        public SearchCollectionMode searchCollectionMode = SearchCollectionMode.InOrder;

        [Header("Mission Locations")]
        public MissionTarget startLocation;
        public MissionTarget endLocation;
        public List<MissionTarget> searchTargets;
        public List<MissionTarget> scanTargets;
    }

    [Header("Mission Configuration")]
    public List<Mission> missions = new List<Mission>();
    public float interactionRange = 0.1f;
    public float scanDuration = 2f;

    [Tooltip("Standard icon for starting a mission")]
    public Sprite defaultStartIcon;

    public int selectedMissionIndex { get; private set; } = -1;
    public int currentTargetIndex { get; private set; } = 0;
    public bool missionActive { get; private set; } = false;
    public float scanTimer { get; private set; } = 0f;
    public bool isScanning { get; private set; } = false;

    public float deliveryTimeRemaining { get; private set; } = 0f;
    public bool isDeliveryTimerRunning { get; private set; } = false;

    public int closestAvailableMissionIndex { get; private set; } = -1;

    public event Action<int> OnMissionStarted;
    public event Action<int> OnMissionCompleted;
    public event Action<int> OnMissionFailed;
    public event Action OnStepCompleted;
    public event Action OnMissionReset;

    public event Action<bool, string, Sprite, string> OnProximityChanged;
    public event Action<float> OnScanProgressUpdated;
    public event Action<float, float> OnDeliveryTimerUpdated;

    private HelicopterManager manager;
    private LocationRegistry registry;
    private bool wasInRange = false;

    private readonly HashSet<int> collectedSearchTargets = new HashSet<int>();

    public void Initialize(HelicopterManager heliManager, LocationRegistry locRegistry)
    {
        manager = heliManager;
        registry = locRegistry;
    }

    public void EvaluateProgressionTick()
    {
        if (manager == null || manager.helicopter == null || registry == null) return;

        UpdateDeliveryTimer();

        if (manager.helicopter.transform.localPosition == Vector3.zero)
        {
            if (wasInRange)
            {
                ResetProximity();
                OnProximityChanged?.Invoke(
                    false,
                    "",
                    null,
                    "Vlieg naar een markering om een missie te starten"
                );
            }

            return;
        }

        if (selectedMissionIndex == -1)
            EvaluateSelectionRange();
        else
            EvaluateActiveMissionRange();
    }

    private void UpdateDeliveryTimer()
    {
        if (selectedMissionIndex == -1) return;

        Mission mission = missions[selectedMissionIndex];

        if (mission.missionType != MissionType.Delivery ||
            !mission.useDeliveryTimer ||
            !missionActive ||
            !isDeliveryTimerRunning)
        {
            return;
        }

        deliveryTimeRemaining = Mathf.Max(0f, deliveryTimeRemaining - Time.deltaTime);

        OnDeliveryTimerUpdated?.Invoke(
            deliveryTimeRemaining,
            mission.deliveryTimeLimit
        );

        if (deliveryTimeRemaining <= 0f)
            FailMission();
    }

    private void EvaluateSelectionRange()
    {
        int closestIndex = -1;
        float closestDistance = interactionRange;

        for (int i = 0; i < missions.Count; i++)
        {
            if (missions[i].isCompleted || missions[i].startLocation == null) continue;

            Vector2 gridPosition = GetFirstTargetPosition(missions[i]);
            Vector3 worldPosition = manager.GetWorldPositionFromGrid(
                gridPosition.x,
                gridPosition.y
            );

            float distance = GetFlatDistance(
                manager.helicopter.transform.position,
                worldPosition
            );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        closestAvailableMissionIndex = closestIndex;
        bool isInRange = closestIndex != -1;

        if (isInRange == wasInRange) return;

        wasInRange = isInRange;

        if (isInRange)
        {
            OnProximityChanged?.Invoke(
                true,
                "Start Missie",
                defaultStartIcon,
                $"[ {missions[closestIndex].missionName} ]\nDruk op de knop om te starten"
            );
        }
        else
        {
            OnProximityChanged?.Invoke(
                false,
                "",
                null,
                "Vlieg naar een markering om een missie te starten"
            );
        }
    }

    private void EvaluateActiveMissionRange()
    {
        Mission mission = missions[selectedMissionIndex];

        if (mission.missionType == MissionType.SearchFind &&
            mission.searchCollectionMode == SearchCollectionMode.AnyOrder)
        {
            EvaluateAnyOrderSearchRange(mission);
            return;
        }

        MissionTarget currentTarget = GetCurrentTarget(mission);
        if (currentTarget == null) return;

        Vector3 targetPosition = GetCurrentTargetWorldPos();
        float distance = GetFlatDistance(
            manager.helicopter.transform.position,
            targetPosition
        );

        if (distance < interactionRange)
        {
            if (mission.missionType == MissionType.Scan)
            {
                EvaluateScan(currentTarget);
            }
            else if (!wasInRange)
            {
                wasInRange = true;

                OnProximityChanged?.Invoke(
                    true,
                    currentTarget.actionText,
                    currentTarget.targetIcon,
                    currentTarget.description
                );
            }
        }
        else if (wasInRange)
        {
            ResetProximity();

            OnProximityChanged?.Invoke(
                false,
                "",
                null,
                $"Doel: {mission.missionName}"
            );
        }
    }

    private void EvaluateAnyOrderSearchRange(Mission mission)
    {
        int closestTarget = FindClosestSearchTargetInRange(mission);

        if (closestTarget != -1)
        {
            bool targetChanged = currentTargetIndex != closestTarget;
            currentTargetIndex = closestTarget;

            if (!wasInRange || targetChanged)
            {
                wasInRange = true;

                MissionTarget target = mission.searchTargets[closestTarget];

                OnProximityChanged?.Invoke(
                    true,
                    target.actionText,
                    target.targetIcon,
                    target.description
                );
            }
        }
        else if (wasInRange)
        {
            wasInRange = false;

            OnProximityChanged?.Invoke(
                false,
                "",
                null,
                $"Doel: {mission.missionName}"
            );
        }
    }

    private void EvaluateScan(MissionTarget target)
    {
        if (!wasInRange)
        {
            wasInRange = true;
            scanTimer = 0f;
            isScanning = false;

            OnScanProgressUpdated?.Invoke(0f);

            OnProximityChanged?.Invoke(
                true,
                target.actionText,
                target.targetIcon,
                target.description
            );
        }

        if (!isScanning) return;

        scanTimer += Time.deltaTime;

        float progress = Mathf.Clamp(
            Mathf.Round(scanTimer / scanDuration * 100f),
            0f,
            100f
        );

        OnScanProgressUpdated?.Invoke(progress);

        if (scanTimer >= scanDuration)
            CompleteStep();
    }

    public void StartMission(int index)
    {
        if (index < 0 || index >= missions.Count) return;

        Mission mission = missions[index];

        selectedMissionIndex = index;
        currentTargetIndex = 0;
        scanTimer = 0f;
        isScanning = false;
        wasInRange = false;

        collectedSearchTargets.Clear();
        ResetDeliveryTimer();

        /*
         * The player starts all missions at startLocation.
         * After starting:
         * Delivery points to endLocation.
         * Search points to searchTargets[0].
         * Scan points to scanTargets[0].
         */
        missionActive = mission.missionType == MissionType.Delivery;

        if (mission.missionType == MissionType.Delivery &&
            mission.useDeliveryTimer)
        {
            deliveryTimeRemaining = Mathf.Max(1f, mission.deliveryTimeLimit);
            isDeliveryTimerRunning = true;

            OnDeliveryTimerUpdated?.Invoke(
                deliveryTimeRemaining,
                mission.deliveryTimeLimit
            );
        }

        OnScanProgressUpdated?.Invoke(0f);
        OnMissionStarted?.Invoke(index);
    }

    public void ProcessMissionStep()
    {
        if (selectedMissionIndex == -1) return;

        Mission mission = missions[selectedMissionIndex];

        switch (mission.missionType)
        {
            case MissionType.Delivery:
                ProcessDelivery();
                break;

            case MissionType.SearchFind:
                ProcessSearchFind(mission);
                break;

            case MissionType.Scan:
                ProcessScan(mission);
                break;
        }
    }

    private void ProcessDelivery()
    {
        if (!IsCurrentTargetInRange()) return;
        FinishMission();
    }

    private void ProcessSearchFind(Mission mission)
    {
        if (mission.searchTargets == null || mission.searchTargets.Count == 0)
            return;

        int collectedIndex;

        if (mission.searchCollectionMode == SearchCollectionMode.AnyOrder)
        {
            collectedIndex = FindClosestSearchTargetInRange(mission);
        }
        else
        {
            if (!IsCurrentTargetInRange()) return;
            collectedIndex = currentTargetIndex;
        }

        if (collectedIndex == -1) return;

        collectedSearchTargets.Add(collectedIndex);
        wasInRange = false;

        if (collectedSearchTargets.Count >= mission.searchTargets.Count)
        {
            FinishMission();
            return;
        }

        if (mission.searchCollectionMode == SearchCollectionMode.InOrder)
            currentTargetIndex++;
        else
            currentTargetIndex = GetFirstUncollectedTarget(mission);

        OnStepCompleted?.Invoke();
    }

    private void ProcessScan(Mission mission)
    {
        if (!IsCurrentTargetInRange() || isScanning) return;

        isScanning = true;
        scanTimer = 0f;

        OnScanProgressUpdated?.Invoke(0f);

        MissionTarget target = GetCurrentTarget(mission);

        if (target != null)
        {
            OnProximityChanged?.Invoke(
                true,
                "Scannen...",
                target.targetIcon,
                target.description
            );
        }
    }

    public void CompleteStep()
    {
        if (selectedMissionIndex == -1) return;

        Mission mission = missions[selectedMissionIndex];

        currentTargetIndex++;
        scanTimer = 0f;
        isScanning = false;
        wasInRange = false;

        OnScanProgressUpdated?.Invoke(0f);

        if (mission.scanTargets == null ||
            currentTargetIndex >= mission.scanTargets.Count)
        {
            FinishMission();
        }
        else
        {
            OnStepCompleted?.Invoke();
        }
    }

    public void FinishMission()
    {
        if (selectedMissionIndex == -1) return;

        int completedIndex = selectedMissionIndex;
        missions[completedIndex].isCompleted = true;

        ClearCurrentMissionState();
        OnMissionCompleted?.Invoke(completedIndex);
    }

    public void FailMission()
    {
        if (selectedMissionIndex == -1) return;

        int failedIndex = selectedMissionIndex;

        ClearCurrentMissionState();

        OnProximityChanged?.Invoke(
            false,
            "",
            null,
            "Missie mislukt: de bezorgtijd is verstreken"
        );

        OnMissionFailed?.Invoke(failedIndex);
        OnMissionReset?.Invoke();
    }

    private void ClearCurrentMissionState()
    {
        selectedMissionIndex = -1;
        currentTargetIndex = 0;
        missionActive = false;
        scanTimer = 0f;
        isScanning = false;
        wasInRange = false;
        closestAvailableMissionIndex = -1;

        collectedSearchTargets.Clear();

        ResetDeliveryTimer();
        OnScanProgressUpdated?.Invoke(0f);
    }

    private void ResetProximity()
    {
        wasInRange = false;
        isScanning = false;
        scanTimer = 0f;
        OnScanProgressUpdated?.Invoke(0f);
    }

    private void ResetDeliveryTimer()
    {
        deliveryTimeRemaining = 0f;
        isDeliveryTimerRunning = false;
        OnDeliveryTimerUpdated?.Invoke(0f, 0f);
    }

    public void TriggerFullReset()
    {
        ClearCurrentMissionState();

        foreach (Mission mission in missions)
            mission.isCompleted = false;

        OnMissionReset?.Invoke();
    }

    public void ResetAllMissionsToStart()
    {
        TriggerFullReset();
    }

    /*
     * This is now always the mission start marker.
     * It no longer returns searchTargets[0] or scanTargets[0].
     */
    public Vector2 GetFirstTargetPosition(Mission mission)
    {
        if (mission == null || mission.startLocation == null)
            return Vector2.zero;

        return registry.GetPosition(mission.startLocation.locationName);
    }

    public Vector2 GetCurrentTargetGrid(Mission mission)
    {
        switch (mission.missionType)
        {
            case MissionType.Delivery:
                return registry.GetPosition(
                    mission.endLocation.locationName
                );

            case MissionType.SearchFind:
                return registry.GetPosition(
                    mission.searchTargets[currentTargetIndex].locationName
                );

            case MissionType.Scan:
                return registry.GetPosition(
                    mission.scanTargets[currentTargetIndex].locationName
                );
        }

        return Vector2.zero;
    }

    public Vector3 GetCurrentTargetWorldPos()
    {
        if (selectedMissionIndex == -1) return Vector3.zero;

        Vector2 grid = GetCurrentTargetGrid(
            missions[selectedMissionIndex]
        );

        return manager.GetWorldPositionFromGrid(grid.x, grid.y);
    }

    public Vector3 GetSearchTargetWorldPos(int targetIndex)
    {
        if (selectedMissionIndex == -1) return Vector3.zero;

        Mission mission = missions[selectedMissionIndex];

        if (mission.searchTargets == null ||
            targetIndex < 0 ||
            targetIndex >= mission.searchTargets.Count)
        {
            return Vector3.zero;
        }

        Vector2 grid = registry.GetPosition(
            mission.searchTargets[targetIndex].locationName
        );

        return manager.GetWorldPositionFromGrid(grid.x, grid.y);
    }

    public Vector3 GetClosestAvailableMissionPos()
    {
        float closestDistance = float.MaxValue;
        Vector3 closestPosition = Vector3.zero;

        foreach (Mission mission in missions)
        {
            if (mission.isCompleted || mission.startLocation == null)
                continue;

            Vector2 grid = GetFirstTargetPosition(mission);
            Vector3 worldPosition = manager.GetWorldPositionFromGrid(
                grid.x,
                grid.y
            );

            float distance = GetFlatDistance(
                manager.helicopter.transform.position,
                worldPosition
            );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPosition = worldPosition;
            }
        }

        return closestPosition;
    }

    public string GetCurrentTargetLocationName()
    {
        if (selectedMissionIndex == -1) return string.Empty;

        MissionTarget target = GetCurrentTarget(
            missions[selectedMissionIndex]
        );

        return target != null ? target.locationName : string.Empty;
    }

    public bool IsSearchTargetCollected(int targetIndex)
    {
        return collectedSearchTargets.Contains(targetIndex);
    }

    public int GetCollectedSearchTargetCount()
    {
        return collectedSearchTargets.Count;
    }

    public int GetSearchTargetCount()
    {
        if (selectedMissionIndex == -1) return 0;

        List<MissionTarget> targets =
            missions[selectedMissionIndex].searchTargets;

        return targets != null ? targets.Count : 0;
    }

    private MissionTarget GetCurrentTarget(Mission mission)
    {
        switch (mission.missionType)
        {
            case MissionType.Delivery:
                return mission.endLocation;

            case MissionType.SearchFind:
                if (mission.searchTargets == null ||
                    currentTargetIndex >= mission.searchTargets.Count)
                {
                    return null;
                }

                return mission.searchTargets[currentTargetIndex];

            case MissionType.Scan:
                if (mission.scanTargets == null ||
                    currentTargetIndex >= mission.scanTargets.Count)
                {
                    return null;
                }

                return mission.scanTargets[currentTargetIndex];
        }

        return null;
    }

    private bool IsCurrentTargetInRange()
    {
        if (selectedMissionIndex == -1 ||
            manager == null ||
            manager.helicopter == null)
        {
            return false;
        }

        return GetFlatDistance(
            manager.helicopter.transform.position,
            GetCurrentTargetWorldPos()
        ) < interactionRange;
    }

    private int FindClosestSearchTargetInRange(Mission mission)
    {
        int closestIndex = -1;
        float closestDistance = interactionRange;

        for (int i = 0; i < mission.searchTargets.Count; i++)
        {
            if (collectedSearchTargets.Contains(i)) continue;

            Vector3 worldPosition = GetSearchTargetWorldPos(i);

            float distance = GetFlatDistance(
                manager.helicopter.transform.position,
                worldPosition
            );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    private int GetFirstUncollectedTarget(Mission mission)
    {
        for (int i = 0; i < mission.searchTargets.Count; i++)
        {
            if (!collectedSearchTargets.Contains(i))
                return i;
        }

        return 0;
    }

    private float GetFlatDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(
            new Vector2(a.x, a.z),
            new Vector2(b.x, b.z)
        );
    }
}