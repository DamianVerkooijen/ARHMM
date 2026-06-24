using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissionStateController : MonoBehaviour
{
    public enum MissionType { Delivery, SearchFind, Scan }
    public enum SearchCollectionMode { InOrder, AnyOrder }
    public enum DeliveryMode { SingleDestination, MultipleDestinations }

    [Serializable]
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
        public bool isCompleted;

        [Header("Delivery Settings")]
        public DeliveryMode deliveryMode = DeliveryMode.SingleDestination;
        public bool useDeliveryTimer;

        [Min(1f)]
        public float deliveryTimeLimit = 30f;

        [Tooltip("Used for Single Destination delivery missions")]
        public MissionTarget endLocation;
        public List<MissionTarget> searchTargets;
        public List<MissionTarget> scanTargets;
        
        [Header("Popup Content")]
        public PopupContent missionIntroPopup;
        public List<PopupContent> missionPopups = new List<PopupContent>();
        public PopupContent missionCompletionPopup;
    }

    [Header("Mission Configuration")]
    public List<Mission> missions = new List<Mission>();
    public float interactionRange = 0.1f;
    public float scanDuration = 3f;

    [Header("Mission Chain")]
    [Tooltip("Delay before the next mission starts")]
    public float missionTransitionDelay = 5f;

    [Tooltip("Delay before a failed delivery restarts")]
    public float missionRetryDelay = 2f;

    public int selectedMissionIndex { get; private set; } = -1;
    public int currentTargetIndex { get; private set; } = 0;
    public int currentPopupIndex { get; private set; } = 0;
    public bool missionActive { get; private set; } = false;
    public float scanTimer { get; private set; } = 0f;
    public bool isScanning { get; private set; } = false;
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
    private bool wasInRange;
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
            return;
        }

        UpdateDeliveryTimer();

        // The timer may have failed the mission.
        if (selectedMissionIndex == -1)
            return;

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

        deliveryTimeRemaining = Mathf.Max(0f, deliveryTimeRemaining - Time.deltaTime);
        OnDeliveryTimerUpdated?.Invoke(deliveryTimeRemaining, mission.deliveryTimeLimit);

        if (deliveryTimeRemaining <= 0f)
            FailMission();
    }

    private void EvaluateActiveMissionRange()
    {
        Mission mission = missions[selectedMissionIndex];

        if (mission.missionType == MissionType.SearchFind &&
            mission.searchCollectionMode == SearchCollectionMode.AnyOrder)
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

            if (mission.missionType == MissionType.Scan && isScanning)
                UpdateScanProgress();
        }
        else if (wasInRange)
        {
            ResetProximityState();
        }
    }

    private void EvaluateAnyOrderSearchRange(Mission mission)
    {
        int closestTargetIndex = FindClosestSearchTargetInRange(mission);

        if (closestTargetIndex != -1)
        {
            bool targetChanged = currentTargetIndex != closestTargetIndex;
            currentTargetIndex = closestTargetIndex;

            if (!wasInRange || targetChanged)
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
            ResetProximityState();
        }
    }

    private void ShowTargetPopup(MissionTarget target)
    {
        OnProximityChanged?.Invoke(
            true,
            target.actionText,
            target.targetIcon,
            target.description
        );

        if (PopupManager.Instance == null)
            return;

        PopupManager.Instance.ShowPopup(
            "📍 BESTEMMING BEREIKT",
            target.description,
            target.targetIcon,
            string.IsNullOrEmpty(target.actionText) ? "Doorgaan" : target.actionText,
            ProcessMissionStep
        );
    }

    private void ShowScanPopup(MissionTarget target)
    {
        OnProximityChanged?.Invoke(
            true,
            target.actionText,
            target.targetIcon,
            target.description
        );

        if (PopupManager.Instance == null)
            return;

        PopupManager.Instance.ShowPopup(
            "📡 SCANNER GEACTIVEERD",
            $"{target.description}\n\nBreng de helikopter tot stilstand om de scan uit te voeren.",
            target.targetIcon,
            "Start Scannen",
            ProcessMissionStep
        );
    }

    private void UpdateScanProgress()
    {
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
        PopupManager.Instance?.ClosePopup();

        string label = selectedMissionIndex != -1
            ? $"Doel: {missions[selectedMissionIndex].missionName}"
            : "";

        OnProximityChanged?.Invoke(false, "", null, label);
    }

    public void StartMission(int index)
    {
        if (index < 0 || index >= missions.Count)
            return;

        Mission mission = missions[index];

        selectedMissionIndex = index;
        currentTargetIndex = 0;
        currentPopupIndex = 0;
        scanTimer = 0f;
        isScanning = false;
        wasInRange = false;

        mission.isCompleted = false;
        collectedSearchTargets.Clear();
        ResetDeliveryTimer();

        if (mission.missionType == MissionType.Delivery && mission.useDeliveryTimer)
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
        if (selectedMissionIndex == -1)
            return;

        Mission mission = missions[selectedMissionIndex];

        switch (mission.missionType)
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

    private void ProcessDelivery(Mission mission)
    {
        if (!IsCurrentTargetInRange())
            return;

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
        if (mission.searchTargets == null || mission.searchTargets.Count == 0)
            return;

        int collectedIndex;

        if (mission.searchCollectionMode == SearchCollectionMode.AnyOrder)
        {
            collectedIndex = FindClosestSearchTargetInRange(mission);
        }
        else
        {
            if (!IsCurrentTargetInRange())
                return;

            collectedIndex = currentTargetIndex;
        }

        if (collectedIndex == -1)
            return;

        collectedSearchTargets.Add(collectedIndex);
        wasInRange = false;

        if (collectedSearchTargets.Count >= mission.searchTargets.Count)
        {
            FinishMission();
            return;
        }

        currentTargetIndex =
            mission.searchCollectionMode == SearchCollectionMode.InOrder
                ? currentTargetIndex + 1
                : GetFirstUncollectedTarget(mission);

        OnStepCompleted?.Invoke();
    }

    private void ProcessScan(Mission mission)
    {
        if (!IsCurrentTargetInRange() || isScanning)
            return;

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
        if (selectedMissionIndex == -1)
            return;

        Mission mission = missions[selectedMissionIndex];

        currentTargetIndex++;
        currentPopupIndex++;
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
        if (selectedMissionIndex == -1)
            return;

        int completedIndex = selectedMissionIndex;
        missions[completedIndex].isCompleted = true;

        PopupManager.Instance?.ClosePopup();
        ClearCurrentMissionState();
        OnMissionCompleted?.Invoke(completedIndex);

        int nextMissionIndex = completedIndex + 1;

        if (nextMissionIndex < missions.Count)
            ScheduleMission(nextMissionIndex, missionTransitionDelay);
    }

    public void FailMission()
    {
        if (selectedMissionIndex == -1)
            return;

        int failedIndex = selectedMissionIndex;

        PopupManager.Instance?.ClosePopup();
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
        transitionCoroutine = StartCoroutine(StartMissionAfterDelay(missionIndex, delay));
    }

    private IEnumerator StartMissionAfterDelay(int missionIndex, float delay)
    {
        yield return new WaitForSeconds(delay);

        transitionCoroutine = null;
        StartMission(missionIndex);
    }

    private void StopTransition()
    {
        if (transitionCoroutine == null)
            return;

        StopCoroutine(transitionCoroutine);
        transitionCoroutine = null;
    }

    private void ClearCurrentMissionState()
    {
        selectedMissionIndex = -1;
        currentTargetIndex = 0;
        currentPopupIndex = 0;
        scanTimer = 0f;
        isScanning = false;
        wasInRange = false;

        collectedSearchTargets.Clear();
        ResetDeliveryTimer();
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

        foreach (Mission mission in missions)
            mission.isCompleted = false;

        OnMissionReset?.Invoke();
    }

    public Vector2 GetCurrentTargetGrid(Mission mission)
    {
        MissionTarget target = GetCurrentTarget(mission);

        if (target == null || registry == null)
            return Vector2.zero;

        return registry.GetPosition(target.locationName);
    }

    public Vector3 GetCurrentTargetWorldPos()
    {
        if (selectedMissionIndex == -1 || manager == null)
            return Vector3.zero;

        Vector2 grid = GetCurrentTargetGrid(missions[selectedMissionIndex]);
        return manager.GetWorldPositionFromGrid(grid.x, grid.y);
    }

    public Vector3 GetSearchTargetWorldPos(int targetIndex)
    {
        if (selectedMissionIndex == -1 || manager == null || registry == null)
            return Vector3.zero;

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

    public string GetCurrentTargetLocationName()
    {
        if (selectedMissionIndex == -1)
            return string.Empty;

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
        if (selectedMissionIndex == -1)
            return 0;

        List<MissionTarget> targets =
            missions[selectedMissionIndex].searchTargets;

        return targets != null ? targets.Count : 0;
    }

    private MissionTarget GetCurrentTarget(Mission mission)
    {
        switch (mission.missionType)
        {
            case MissionType.Delivery:
                if (UsesMultipleDeliveryTargets(mission))
                {
                    return currentTargetIndex < mission.deliveryTargets.Count
                        ? mission.deliveryTargets[currentTargetIndex]
                        : null;
                }

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

    private bool UsesMultipleDeliveryTargets(Mission mission)
    {
        return mission.deliveryMode == DeliveryMode.MultipleDestinations &&
               mission.deliveryTargets != null &&
               mission.deliveryTargets.Count > 0;
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
            if (collectedSearchTargets.Contains(i))
                continue;

            float distance = GetFlatDistance(
                manager.helicopter.transform.position,
                GetSearchTargetWorldPos(i)
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