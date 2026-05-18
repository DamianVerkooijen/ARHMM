using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MissionController : MonoBehaviour
{
    [Header("Settings")]
    public HelicopterManager manager;
    public GameObject padPrefab;
    public GameObject markerPrefab;
    public TMP_Text statusText;
    public GameObject actionButton;
    public float interactionRange = 0.5f;
    public RadarMarker radarMarker;
    public float scanDuration = 2f;
    public LocationRegistry registry;

    [Header("UI Animation")]
    public Animator panelAnimator;
    public TMP_Text missionTitleText;
    public TMP_Text missionTaskText;
    public TMP_Text missionDescriptionText;

    [Header("HUD Elements")]
    public Image leftBar;
    public Image rightBar;
    public Image botBar;
    public Image topBarR;
    public Image topBarL;
    public Image missionPanelTopImage;
    public Image radarBackground;

    [Header("Sprites Normal (Blauw)")]
    public Sprite leftBarNormal;
    public Sprite rightBarNormal;
    public Sprite botBarNormal;
    public Sprite topBarNormal;
    public Sprite panelNormal;
    public Sprite radarNormal;

    [Header("Sprites Finished (Groen)")]
    public Sprite leftBarFinished;
    public Sprite rightBarFinished;
    public Sprite botBarFinished;
    public Sprite topBarFinished;
    public Sprite panelFinished;
    public Sprite radarFinished;

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
        public string shortInstruction;
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

        // Start HUD on the blue images
        SetHUDState(false);
    }

    void Update()
    {
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

    // --- HUD AND FINISH LOGIC ---

    private void FinishMission()
    {
        if (selectedMissionIndex == -1) return;
        StartCoroutine(ShowMissionCompleteRoutine());
    }

    private IEnumerator ShowMissionCompleteRoutine()
    {
        missions[selectedMissionIndex].isCompleted = true;

        // IMMEDIATELY close the panel to hide the description
        if (panelAnimator != null)
        {
            panelAnimator.SetBool("isOpen", false);
        }

        // Immediately turn the HUD to green for the 'YES' feeling
        SetHUDState(true);

        // Update the texts while the panel closes (invisible to the user)
        if (missionTitleText != null) missionTitleText.text = "MISSIE VOLTOOID";
        if (missionTaskText != null) missionTaskText.text = "Goed gedaan!";

        // Wait 5 seconds in the 'Green HUD' status
        yield return new WaitForSeconds(5f);

        // Reset everything to the starting state for the next round
        ResetUIToDefault();

        selectedMissionIndex = -1;
        missionActive = false;
        if (activePad != null) activePad.SetActive(false);

        // Back to blue
        SetHUDState(false);
        SpawnWorldMarkers();
    }

    private void ResetUIToDefault()
    {
        if (missionTitleText != null) missionTitleText.text = "Start een missie";
        if (missionTaskText != null) missionTaskText.text = "Volg de radar voor een missie";
        if (missionDescriptionText != null) missionDescriptionText.text = "";
    }

    private void SetHUDState(bool isFinished)
    {
        // Switches the sprites of all HUD elements based on their status
        if (leftBar != null) leftBar.sprite = isFinished ? leftBarFinished : leftBarNormal;
        if (rightBar != null) rightBar.sprite = isFinished ? rightBarFinished : rightBarNormal;
        if (botBar != null) botBar.sprite = isFinished ? botBarFinished : botBarNormal;
        if (topBarR != null) topBarR.sprite = isFinished ? topBarFinished : topBarNormal;
        if (topBarL != null) topBarL.sprite = isFinished ? topBarFinished : topBarNormal;
        if (missionPanelTopImage != null) missionPanelTopImage.sprite = isFinished ? panelFinished : panelNormal;
        if (radarBackground != null) radarBackground.sprite = isFinished ? radarFinished : radarNormal;
    }

    // --- EXISTING MISSION LOGIC ---

    public void SpawnWorldMarkers()
    {
        foreach (var marker in spawnedMarkers) if (marker != null) Destroy(marker);
        spawnedMarkers.Clear();

        for (int i = 0; i < missions.Count; i++)
        {
            if (missions[i].isCompleted) continue;
            Vector2 gridPos = GetFirstTargetPosition(missions[i]);
            Vector3 worldPos = manager.GetWorldPositionFromGrid(gridPos.x, gridPos.y);
            worldPos.y += 0.1f;
            GameObject marker = Instantiate(markerPrefab, worldPos, Quaternion.identity, transform);
            Renderer r = marker.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                r.material = new Material(r.material);
                r.material.color = missions[i].missionColor;
            }
            spawnedMarkers.Add(marker);
        }
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
            if (dist < closestDist) { closestDist = dist; closestIndex = i; }
        }

        if (closestIndex != -1)
        {
            statusText.text = $"[ {missions[closestIndex].missionName} ]\nPress Button to Start";
            if (actionButton != null) actionButton.SetActive(true);
        }
        else if (actionButton != null) actionButton.SetActive(false);
    }

    public void OnActionButtonPressed()
    {
        if (selectedMissionIndex == -1)
        {
            for (int i = 0; i < missions.Count; i++)
            {
                if (missions[i].isCompleted) continue;
                Vector2 pos = GetFirstTargetPosition(missions[i]);
                float d = GetFlatDistance(manager.helicopter.transform.position, manager.GetWorldPositionFromGrid(pos.x, pos.y));
                if (d < interactionRange) { StartMission(i); return; }
            }
        }
        else ProcessMissionStep();
    }

    private void StartMission(int index)
    {
        selectedMissionIndex = index;
        missionActive = false;
        currentTargetIndex = 0;
        scanTimer = 0f;
        foreach (var m in spawnedMarkers) if (m != null) m.SetActive(false);
        if (panelAnimator != null) panelAnimator.SetBool("isOpen", true);
        UpdateMissionUI();
    }

    private void UpdateMissionUI()
    {
        if (selectedMissionIndex == -1) return;
        Mission m = missions[selectedMissionIndex];
        if (missionTitleText != null) missionTitleText.text = m.missionName;

        MissionTarget currentTarget = null;
        switch (m.missionType)
        {
            case MissionType.Delivery: currentTarget = !missionActive ? m.startLocation : m.endLocation; break;
            case MissionType.SearchFind: if (currentTargetIndex < m.searchTargets.Count) currentTarget = m.searchTargets[currentTargetIndex]; break;
            case MissionType.Scan: if (currentTargetIndex < m.scanTargets.Count) currentTarget = m.scanTargets[currentTargetIndex]; break;
        }

        if (currentTarget != null)
        {
            if (missionTaskText != null) missionTaskText.text = currentTarget.shortInstruction;
            if (missionDescriptionText != null) missionDescriptionText.text = currentTarget.description;
        }
    }

    public bool IsMissionActive()
    {
        return selectedMissionIndex != -1;
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

        // if (radarMarker != null)
        //    radarMarker.UpdatePosition(targetWorldPos, manager.helicopter.transform.position, manager.helicopter.transform.eulerAngles.y);

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
            if (!missionActive) { missionActive = true; scanTimer = 0; UpdateMissionUI(); }
            else FinishMission();
        }
        else if (m.missionType == MissionType.SearchFind)
        {
            currentTargetIndex++;
            if (currentTargetIndex >= m.searchTargets.Count) FinishMission();
            else UpdateMissionUI();
        }
    }

    private void CompleteStep()
    {
        currentTargetIndex++;
        scanTimer = 0f;
        if (currentTargetIndex >= missions[selectedMissionIndex].scanTargets.Count) FinishMission();
        else UpdateMissionUI();
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
            case MissionType.Delivery: return registry.GetPosition(missionActive ? currentMission.endLocation.locationName : currentMission.startLocation.locationName);
            case MissionType.SearchFind: return registry.GetPosition(currentMission.searchTargets[currentTargetIndex].locationName);
            case MissionType.Scan: return registry.GetPosition(currentMission.scanTargets[currentTargetIndex].locationName);
        }
        return registry.GetPosition(currentMission.startLocation.locationName);
    }

    public void TriggerFullReset()
    {
        StopAllCoroutines();

        if (manager != null) manager.SoftResetHeli();

        selectedMissionIndex = -1;
        currentTargetIndex = 0;
        missionActive = false;
        scanTimer = 0f;

        foreach (var m in missions) m.isCompleted = false;

        if (activePad != null) activePad.SetActive(false);
        if (actionButton != null) actionButton.SetActive(false);

        if (panelAnimator != null) panelAnimator.SetBool("isOpen", false);

        // Reset to blue and default text
        ResetUIToDefault();
        SetHUDState(false);

        SpawnWorldMarkers();
        Debug.Log("Sessie gereset naar beginteksten.");
    }

    private float GetFlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
}