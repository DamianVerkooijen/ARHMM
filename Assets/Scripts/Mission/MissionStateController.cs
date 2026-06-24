using System;
using System.Collections.Generic;
using UnityEngine;

public class MissionStateController : MonoBehaviour
{
    public enum MissionType { Delivery, SearchFind, Scan }
    public enum SearchCollectionMode { InOrder, AnyOrder }
    public enum DeliveryMode { SingleDestination, MultipleDestinations }

    [System.Serializable]
    public class MissionTarget
    {
        [LocationName] public string locationName;
        public string actionText;
        public Sprite targetIcon;
        public string shortInstruction;

        [TextArea]
        public string description;

        public int reward;
    }

    [System.Serializable]
    public class Mission
    {
        public string missionName;
        public MissionType missionType;
        public bool isCompleted = false;

        [Header("Delivery Settings")]
        public DeliveryMode deliveryMode = DeliveryMode.SingleDestination;
        public bool useDeliveryTimer = false;

        [Min(1f)]
        public float deliveryTimeLimit = 30f;

        [Tooltip("Used for a single-destination delivery mission")]
        public MissionTarget endLocation;

        [Tooltip("Used for a multiple-destination delivery mission")]
        public List<MissionTarget> deliveryTargets = new List<MissionTarget>();

        [Header("Search & Find Settings")]
        public SearchCollectionMode searchCollectionMode = SearchCollectionMode.InOrder;
        public List<MissionTarget> searchTargets = new List<MissionTarget>();

        [Header("Scan Settings")]
        public List<MissionTarget> scanTargets = new List<MissionTarget>();
    }

    [Header("Mission Configuration")]
    public List<Mission> missions = new List<Mission>();
    public float interactionRange = 0.1f;
    public float scanDuration = 3f; // Tijd in seconden voor de scan

    [Header("Mission Chain")]
    [Tooltip("Delay before the next mission starts")]
    public float missionTransitionDelay = 5f;

    [Tooltip("Delay before a failed delivery mission restarts")]
    public float missionRetryDelay = 2f;
    public Sprite defaultStartIcon;

    public int selectedMissionIndex { get; private set; } = -1;
    public int currentTargetIndex { get; private set; } = 0;
    public bool missionActive { get; private set; } = false;
    public float scanTimer { get; private set; } = 0f;
    public bool isScanning { get; private set; } = false;

    public float deliveryTimeRemaining { get; private set; } = 0f;
    public bool isDeliveryTimerRunning { get; private set; } = false;

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
    private Coroutine transitionCoroutine;

    private readonly HashSet<int> collectedSearchTargets = new HashSet<int>();

    public void Initialize(HelicopterManager heliManager, LocationRegistry locRegistry)
    {
        manager = heliManager;
        registry = locRegistry;
    }

    public void StartMissionChain()
    {
        StopTransition();
        ClearCurrentMissionState();

        foreach (Mission mission in missions)
            mission.isCompleted = false;

        if (missions.Count > 0)
            StartMission(0);
    }

    public void EvaluateProgressionTick()
    {
        if (selectedMissionIndex == -1 ||
            manager == null ||
            manager.helicopter == null ||
            registry == null)
        {
            if (wasInRange)
            {
                ResetProximityState();
            }
            return;
        }

        UpdateDeliveryTimer();

        // The delivery timer may have failed and cleared the mission.
        if (selectedMissionIndex == -1) return;

        if (manager.helicopter.transform.localPosition == Vector3.zero)
            return;

        EvaluateActiveMissionRange();
    }

    private void UpdateDeliveryTimer()
    {
        Mission mission = missions[selectedMissionIndex];

        if (mission.missionType != MissionType.Delivery ||
            !mission.useDeliveryTimer ||
            !isDeliveryTimerRunning)
        {
            return;
        }

        deliveryTimeRemaining = Mathf.Max(
            0f,
            deliveryTimeRemaining - Time.deltaTime
        );

        OnDeliveryTimerUpdated?.Invoke(
            deliveryTimeRemaining,
            mission.deliveryTimeLimit
        );

        if (deliveryTimeRemaining <= 0f)
            FailMission();
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
            MissionTarget currentTarget = GetCurrentTarget(currentMission);
            if (currentTarget == null) return;

        float distance = GetFlatDistance(
            manager.helicopter.transform.position,
            GetCurrentTargetWorldPos()
        );

        if (distance < interactionRange)
        {
            if (mission.missionType == MissionType.Scan)
            {
                if (!wasInRange)
                {
                    wasInRange = true;
                    scanTimer = 0f;
                    isScanning = false;
                    OnScanProgressUpdated?.Invoke(0f);

                    if (PopupManager.Instance != null)
                    {
                        PopupManager.Instance.ShowPopup(
                            "📡 SCANNER GEACTIVEERD",
                            $"{currentTarget.description}\n\nBreng de helikopter tot stilstand om de scan uit te voeren.",
                            currentTarget.targetIcon,
                            "Start Scannen",
                            () => { isScanning = true; }
                        );
                    }
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
                // Delivery of SearchFind
                if (!wasInRange)
                {
                    wasInRange = true;
                    if (PopupManager.Instance != null)
                    {
                        PopupManager.Instance.ShowPopup(
                            "📍 BESTEMMING BEREIKT",
                            currentTarget.description,
                            currentTarget.targetIcon,
                            currentTarget.actionText,
                            () => { ProcessMissionStep(); }
                        );
                    }
                }
            }
        }
        else if (wasInRange)
        {
            // Speler vliegt weg van de locatie -> Sluit pop-up en reset scan status
            ResetProximityState();
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
            CompleteScanStep();
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
        if (index < 0 || index >= missions.Count) return;

        Mission mission = missions[index];

        selectedMissionIndex = index;
        currentTargetIndex = 0;
        missionActive = true;
        scanTimer = 0f;
        isScanning = false;
        wasInRange = false;

        mission.isCompleted = false;
        collectedSearchTargets.Clear();
        ResetDeliveryTimer();

        if (mission.missionType == MissionType.Delivery &&
            mission.useDeliveryTimer)
        {
            deliveryTimeRemaining = Mathf.Max(
                1f,
                mission.deliveryTimeLimit
            );

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
        Mission m = missions[selectedMissionIndex];

        switch (mission.missionType)
        {
            if (!missionActive)
            {
                missionActive = true;
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
            wasInRange = false;
            if (currentTargetIndex >= m.searchTargets.Count)
                FinishMission();
            else
                OnStepCompleted?.Invoke();
        }
    }

    private void ProcessDelivery(Mission mission)
    {
        if (!IsCurrentTargetInRange()) return;

        if (UsesMultipleDeliveryTargets(mission))
        {
            currentTargetIndex++;
            wasInRange = false;

            if (currentTargetIndex >= mission.deliveryTargets.Count)
                FinishMission();
            else
                OnStepCompleted?.Invoke();

            return;
        }

        FinishMission();
    }

    private void ProcessSearchFind(Mission mission)
    {
        if (mission.searchTargets == null ||
            mission.searchTargets.Count == 0)
        {
            return;
        }

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

    private void CompleteScanStep()
    {
        if (selectedMissionIndex == -1) return;

        Mission mission = missions[selectedMissionIndex];

        currentTargetIndex++;
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

        ClearCurrentMissionState();
        OnMissionCompleted?.Invoke(completedIndex);

        int nextMissionIndex = completedIndex + 1;

        if (nextMissionIndex < missions.Count)
            ScheduleMission(nextMissionIndex, missionTransitionDelay);
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
        ScheduleMission(failedIndex, missionRetryDelay);
    }

    private void ScheduleMission(int missionIndex, float delay)
    {
        StopTransition();

        transitionCoroutine = StartCoroutine(
            StartMissionAfterDelay(missionIndex, delay)
        );
    }

    private IEnumerator StartMissionAfterDelay(int missionIndex, float delay)
    {
        yield return new WaitForSeconds(delay);

        transitionCoroutine = null;
        StartMission(missionIndex);
    }

    private void StopTransition()
    {
        if (transitionCoroutine == null) return;

        StopCoroutine(transitionCoroutine);
        transitionCoroutine = null;
    }

    private void ClearCurrentMissionState()
    {
        selectedMissionIndex = -1;
        currentTargetIndex = 0;
        missionActive = false;
        scanTimer = 0f;
        isScanning = false;
        wasInRange = false;
        OnScanProgressUpdated?.Invoke(0f);
    }

    public void TriggerFullReset()
    {
        ResetAllMissionsToStart();
    }

        foreach (var m in missions) m.isCompleted = false;

        OnMissionReset?.Invoke();

        if (missions.Count > 0)
            StartMission(0);
    }

    public Vector2 GetCurrentTargetGrid(Mission mission)
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

    public Vector3 GetSearchTargetWorldPos(int targetIndex)
    {
        if (selectedMissionIndex == -1 ||
            manager == null ||
            registry == null)
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

    public void ResetAllMissionsToStart()
    {
        // 1. Wipe all active tracking parameters completely
        selectedMissionIndex = -1;
        currentTargetIndex = 0;
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