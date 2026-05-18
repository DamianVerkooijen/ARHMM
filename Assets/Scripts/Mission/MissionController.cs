using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MissionController : MonoBehaviour
{
    [Header("Settings")]
    public HelicopterManager manager;
    public GameObject padPrefab;
    public GameObject markerPrefab;
    public TMP_Text statusText;
    public GameObject actionButton;
    public float interactionRange = 0.5f; // Increased slightly for easier detection
    public RadarMarker radarMarker;
    public float scanDuration = 2f;
    public LocationRegistry registry;

    [Header("Mission List")]
    public List<Mission> missions = new List<Mission>();

    private int selectedMissionIndex = -1;
    private int currentTargetIndex = 0;
    private bool missionActive = false;
    private GameObject activePad;
    private float scanTimer = 0f;
    private List<GameObject> spawnedMarkers = new List<GameObject>();
    private bool initialized = false;

    [System.Serializable]
    public class MissionTarget
    {
        [LocationName] public string locationName;
        [TextArea] public string description;
        public int reward;
    }

    [System.Serializable]
    public class Mission
    {
        public string missionName;
        public MissionType missionType;
        public Color missionColor = Color.yellow;
        public bool isCompleted = false;
        public MissionTarget startLocation;
        public MissionTarget endLocation;
        public List<MissionTarget> searchTargets;
        public List<MissionTarget> scanTargets;
    }

    public enum MissionType { Delivery, SearchFind, Scan, Free }

    void Start()
    {
        if (actionButton != null) actionButton.SetActive(false);

        if (padPrefab != null)
        {
            activePad = Instantiate(padPrefab);
            activePad.SetActive(false);
        }
    }

    void Update()
    {
        // SAFETY: Only spawn markers once the manager is actually ready
        if (!initialized && manager != null && manager.hasSpawned)
        {
            SpawnWorldMarkers();
            initialized = true;
        }

        if (!initialized || manager.helicopter == null) return;

        if (selectedMissionIndex == -1)
            HandleMissionSelection();
        else
            HandleActiveMission();
    }

    public void SpawnWorldMarkers()
    {
        // Clean up old ones
        foreach (var marker in spawnedMarkers) if (marker != null) Destroy(marker);
        spawnedMarkers.Clear();

        for (int i = 0; i < missions.Count; i++)
        {
            if (missions[i].isCompleted) continue;

            Vector2 gridPos = GetFirstTargetPosition(missions[i]);
            Vector3 worldPos = manager.GetWorldPositionFromGrid(gridPos.x, gridPos.y);

            // Offset Y so they don't hide in the floor
            worldPos.y += 0.1f;

            GameObject marker = Instantiate(markerPrefab, worldPos, Quaternion.identity, transform);

            // Set Color
            Renderer r = marker.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                // Create a unique instance of the material to avoid changing the prefab color
                r.material = new Material(r.material);
                r.material.color = missions[i].missionColor;
            }

            spawnedMarkers.Add(marker);
        }
        Debug.Log($"Spawned {spawnedMarkers.Count} mission markers.");
    }

    private void HandleMissionSelection()
    {
        if (activePad != null) activePad.SetActive(false);
        statusText.text = "Fly to a marker to start a mission";

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

        if (closestIndex != -1)
        {
            statusText.text = $"[ {missions[closestIndex].missionName} ]\nPress Button to Start";
            if (actionButton != null) actionButton.SetActive(true);
        }
        else
        {
            if (actionButton != null) actionButton.SetActive(false);
        }
    }

    public void OnActionButtonPressed()
    {
        if (selectedMissionIndex == -1)
        {
            // Find which one we are hovering over
            for (int i = 0; i < missions.Count; i++)
            {
                if (missions[i].isCompleted) continue;
                Vector2 pos = GetFirstTargetPosition(missions[i]);
                float d = GetFlatDistance(manager.helicopter.transform.position, manager.GetWorldPositionFromGrid(pos.x, pos.y));
                if (d < interactionRange)
                {
                    StartMission(i);
                    return;
                }
            }
        }
        else
        {
            ProcessMissionStep();
        }
    }

    private void StartMission(int index)
    {
        selectedMissionIndex = index;
        missionActive = false;
        currentTargetIndex = 0;
        foreach (var m in spawnedMarkers) if (m != null) m.SetActive(false);
        Debug.Log("Mission Started: " + missions[index].missionName);
    }

    private void HandleActiveMission()
    {
        Mission currentMission = missions[selectedMissionIndex];
        Vector2 targetGrid = GetCurrentTargetGrid(currentMission);
        Vector3 targetWorldPos = manager.GetWorldPositionFromGrid(targetGrid.x, targetGrid.y);

        if (activePad != null)
        {
            activePad.SetActive(true);
            activePad.transform.position = targetWorldPos;
        }

        if (radarMarker != null)
            radarMarker.UpdatePosition(targetWorldPos, manager.helicopter.transform.position);

        float dist = GetFlatDistance(manager.helicopter.transform.position, targetWorldPos);

        if (dist < interactionRange)
        {
            if (actionButton != null) actionButton.SetActive(true);
            statusText.text = missionActive ? currentMission.endLocation.description : currentMission.startLocation.description;

            if (currentMission.missionType == MissionType.Scan)
            {
                scanTimer += Time.deltaTime;
                statusText.text = $"Scanning... {Mathf.Round((scanTimer / scanDuration) * 100)}%";
                if (scanTimer >= scanDuration) CompleteStep();
            }
        }
        else
        {
            if (actionButton != null) actionButton.SetActive(false);
            statusText.text = $"Goal: {currentMission.missionName}";
        }
    }

    private void ProcessMissionStep()
    {
        Mission m = missions[selectedMissionIndex];
        if (m.missionType == MissionType.Delivery)
        {
            if (!missionActive) { missionActive = true; scanTimer = 0; }
            else FinishMission();
        }
        else if (m.missionType == MissionType.SearchFind)
        {
            currentTargetIndex++;
            if (currentTargetIndex >= m.searchTargets.Count) FinishMission();
        }
    }

    private void CompleteStep()
    {
        currentTargetIndex++;
        scanTimer = 0f;
        if (currentTargetIndex >= missions[selectedMissionIndex].scanTargets.Count) FinishMission();
    }

    private void FinishMission()
    {
        missions[selectedMissionIndex].isCompleted = true;
        selectedMissionIndex = -1;
        missionActive = false;
        if (activePad != null) activePad.SetActive(false);
        SpawnWorldMarkers();
    }

    private Vector2 GetFirstTargetPosition(Mission m)
    {
        if (m.missionType == MissionType.SearchFind && m.searchTargets != null && m.searchTargets.Count > 0)
            return registry.GetPosition(m.searchTargets[0].locationName);
        if (m.missionType == MissionType.Scan && m.scanTargets != null && m.scanTargets.Count > 0)
            return registry.GetPosition(m.scanTargets[0].locationName);

        return registry.GetPosition(m.startLocation.locationName);
    }

    private Vector2 GetCurrentTargetGrid(Mission currentMission)
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

    private float GetFlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
}