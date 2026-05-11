using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MissionController : MonoBehaviour
{
    [Header("Settings")]
    public HelicopterManager manager;
    public GameObject padPrefab;
    public TMP_Text statusText;
    public GameObject actionButton;
    public float interactionRange = 0.15f;
    public RadarMarker missionMarker;
    public float scanDuration = 2f;
    public LocationRegistry registry;

    [Header("Mission List")]
    public List<Mission> missions = new List<Mission>();

    private int currentMissionIndex = 0;
    private int currentTargetIndex = 0;
    private bool missionActive = false;
    private GameObject activePad;
    private float scanTimer = 0f;

    [System.Serializable]
    public class Mission
    {
        public string missionName;
        public MissionType missionType;

        // Using names instead of coordinates
        [LocationName] public string startLocation;
        [LocationName] public string endLocation;

        [LocationName] public List<string> searchTargets;
        [LocationName] public List<string> scanTargets;
    }

    public enum MissionType
    {
        Delivery,
        SearchFind,
        Scan,
        Free
    }
    void Start()
    {
        if (actionButton != null)
            actionButton.SetActive(false);

        if (padPrefab != null)
        {
            activePad = Instantiate(padPrefab);
            activePad.SetActive(false);
        }
    }

    void Update()
    {
        // 1. Safety Checks
        if (manager == null || !manager.hasSpawned || missions.Count == 0)
            return;

        if (manager.helicopter == null)
            return;

        // 2. Check if all missions are complete
        if (currentMissionIndex >= missions.Count)
        {
            if (statusText != null)
                statusText.text = "All Missions Complete!";

            if (activePad != null)
                activePad.SetActive(false);

            if (actionButton != null)
                actionButton.SetActive(false);

            return;
        }

        Mission currentMission = missions[currentMissionIndex];
        Vector2 targetGrid = GetCurrentTargetGrid(currentMission);

        // 3. Update Pad Position
        if (activePad != null)
        {
            activePad.SetActive(true);
            activePad.transform.position = manager.GetWorldPositionFromGrid(targetGrid.x, targetGrid.y);
        }

        // 4. Range Check
        float dist = GetFlatDistance(manager.helicopter.transform.position, activePad.transform.position);

        if (dist < interactionRange)
        {
            HandleInRange(currentMission);
        }
        else
        {
            HandleOutOfRange(currentMission);
        }

        // 5. Update Radar Marker
        if (missionMarker != null)
        {
            Vector3 targetPos = manager.GetWorldPositionFromGrid(targetGrid.x, targetGrid.y);

            // We sturen nu ook de rotatie van de helikopter mee (eulerAngles.y)
            missionMarker.UpdatePosition(
                targetPos,
                manager.helicopter.transform.position,
                manager.helicopter.transform.eulerAngles.y
            );
        }
    }

    private Vector2 GetCurrentTargetGrid(Mission currentMission)
{
    switch (currentMission.missionType)
    {
        case MissionType.Delivery:
            string targetName = missionActive ? currentMission.endLocation : currentMission.startLocation;
            return registry.GetPosition(targetName);

        case MissionType.SearchFind:
            if (currentMission.searchTargets != null && currentTargetIndex < currentMission.searchTargets.Count)
                return registry.GetPosition(currentMission.searchTargets[currentTargetIndex]);
            break;

        case MissionType.Scan:
            if (currentMission.scanTargets != null && currentTargetIndex < currentMission.scanTargets.Count)
                return registry.GetPosition(currentMission.scanTargets[currentTargetIndex]);
            break;

        case MissionType.Free:
            return registry.GetPosition(currentMission.startLocation);
    }
    return Vector2.zero;
}

    private void HandleInRange(Mission currentMission)
    {
        if (actionButton != null)
            actionButton.SetActive(true);

        switch (currentMission.missionType)
        {
            case MissionType.Delivery:
                if (statusText != null)
                {
                    statusText.text = missionActive
                        ? $"Arrived at Destination: {currentMission.missionName}"
                        : "Ready to Start Mission?";
                }
                break;

            case MissionType.SearchFind:
                if (statusText != null)
                    statusText.text = $"Found target {currentTargetIndex + 1}! Press button to collect.";
                break;

            case MissionType.Scan:
                if (statusText != null)
                    statusText.text = $"Scanning target {currentTargetIndex + 1}...";

                scanTimer += Time.deltaTime;

                if (scanTimer >= scanDuration)
                {
                    CompleteScanTarget();
                }
                break;

            case MissionType.Free:
                if (statusText != null)
                    statusText.text = currentMission.missionName;
                break;
        }
    }

    private void HandleOutOfRange(Mission currentMission)
    {
        if (actionButton != null)
            actionButton.SetActive(false);

        scanTimer = 0f;

        switch (currentMission.missionType)
        {
            case MissionType.Delivery:
                if (statusText != null)
                {
                    statusText.text = missionActive
                        ? $"Fly to Destination ({currentMission.endLocation})"
                        : $"Fly to Start ({currentMission.startLocation})";
                }
                break;

            case MissionType.SearchFind:
                if (statusText != null)
                    statusText.text = $"Fly to Search Target {currentTargetIndex + 1}";
                break;

            case MissionType.Scan:
                if (statusText != null)
                    statusText.text = $"Fly to Scan Target {currentTargetIndex + 1}";
                break;

            case MissionType.Free:
                if (statusText != null)
                    statusText.text = $"Fly freely: {currentMission.missionName}";
                break;
        }
    }

    private float GetFlatDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }

    public void OnActionButtonPressed()
    {
        if (actionButton != null)
            actionButton.SetActive(false);

        if (currentMissionIndex >= missions.Count)
            return;

        Mission currentMission = missions[currentMissionIndex];

        switch (currentMission.missionType)
        {
            case MissionType.Delivery:
                if (!missionActive)
                {
                    missionActive = true;
                    Debug.Log("MISSION STARTED: " + currentMission.missionName);
                }
                else
                {
                    Debug.Log("MISSION COMPLETE: " + currentMission.missionName);
                    missionActive = false;
                    currentMissionIndex++;
                    ResetMissionState();
                }
                break;

            case MissionType.SearchFind:
                CollectSearchTarget();
                break;

            case MissionType.Free:
                Debug.Log("FREE MISSION COMPLETE: " + currentMission.missionName);
                currentMissionIndex++;
                ResetMissionState();
                break;
        }
    }

    private void CollectSearchTarget()
    {
        Mission currentMission = missions[currentMissionIndex];

        Debug.Log("SEARCH TARGET FOUND: " + currentMission.missionName + " | Target " + (currentTargetIndex + 1));

        currentTargetIndex++;

        if (currentMission.searchTargets == null || currentTargetIndex >= currentMission.searchTargets.Count)
        {
            Debug.Log("SEARCH MISSION COMPLETE: " + currentMission.missionName);
            currentMissionIndex++;
            ResetMissionState();
        }
    }

    private void CompleteScanTarget()
    {
        if (currentMissionIndex >= missions.Count)
            return;

        Mission currentMission = missions[currentMissionIndex];

        Debug.Log("SCAN TARGET COMPLETE: " + currentMission.missionName + " | Target " + (currentTargetIndex + 1));

        scanTimer = 0f;
        currentTargetIndex++;

        if (currentMission.scanTargets == null || currentTargetIndex >= currentMission.scanTargets.Count)
        {
            Debug.Log("SCAN MISSION COMPLETE: " + currentMission.missionName);
            currentMissionIndex++;
            ResetMissionState();
        }
    }

    private void ResetMissionState()
    {
        currentTargetIndex = 0;
        scanTimer = 0f;
        missionActive = false;
    }
}