using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MissionControllerDeprecated : MonoBehaviour
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

    [Space]
    [Tooltip("Animator die op het losse rechter extensie-paneel zit")]
    public Animator rightExtensionAnimator;
    [Tooltip("Animator die op het losse linker extensie-paneel zit (voor de Reset-lade indien geanimeerd)")]
    public Animator leftExtensionAnimator;

    [Header("Extension Dynamic Content")]
    [Tooltip("De tekst die IN de uitschuifbare rechter extensie staat")]
    public TMP_Text extensionActionText;
    [Tooltip("De afbeelding/icoon IN de uitschuifbare rechter extensie")]
    public Image extensionActionIcon;
    [Tooltip("Standaard icoon voor acties (bijv. landing/interactie pijl)")]
    public Sprite defaultActionIcon;

    [Header("HUD Elements (Vaste Frames)")]
    public Image leftBar;
    public Image rightBar;
    public Image botBar;
    public Image topBarR;
    public Image topBarL;
    public Image missionPanelTopImage;
    public Image radarBackground;

    [Header("HUD Elements (Losse Extensies)")]
    public Image extensionRight;
    public Image extensionLeft;

    [Header("Sprites Normal (Blauw)")]
    public Sprite leftBarNormal;
    public Sprite rightBarNormal;
    public Sprite botBarNormal;
    public Sprite topBarNormal;
    public Sprite panelNormal;
    public Sprite radarNormal;
    public Sprite extensionRightNormal;
    public Sprite extensionLeftNormal;
    [Tooltip("Nieuw: Rechter balk (Blauw) wanneer de extensie OPEN staat")]
    public Sprite rightBarNormalOpened;

    [Header("Sprites Finished (Groen)")]
    public Sprite leftBarFinished;
    public Sprite rightBarFinished;
    public Sprite botBarFinished;
    public Sprite topBarFinished;
    public Sprite panelFinished;
    public Sprite radarFinished;
    public Sprite extensionRightFinished;
    public Sprite extensionLeftFinished;
    [Tooltip("Nieuw: Rechter balk (Groen) wanneer de extensie OPEN staat")]
    public Sprite rightBarFinishedOpened;

    [Header("Mission List")]
    public List<Mission> missions = new List<Mission>();

    private int selectedMissionIndex = -1;
    private int currentTargetIndex = 0;
    private bool missionActive = false;
    private GameObject activePad;
    private float scanTimer = 0f;
    private List<GameObject> spawnedMarkers = new List<GameObject>();
    private bool initialized = false;

    // Houdt centraal bij of de extensie momenteel geopend is
    private bool isExtensionOpen = false;

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

    public enum MissionType { Delivery, SearchFind, Scan, Free }

    void Start()
    {
        if (actionButton != null) actionButton.SetActive(false);

        if (padPrefab != null)
        {
            activePad = Instantiate(padPrefab);
            activePad.SetActive(false);
        }

        // Start HUD op blauw en gesloten
        isExtensionOpen = false;
        SetHUDState(false);
        ResetExtensions();
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

        // Sluit de extensie direct zodra de missie succesvol is afgerond
        UpdateExtensionVisualState(false);

        StartCoroutine(ShowMissionCompleteRoutine());
    }

    private IEnumerator ShowMissionCompleteRoutine()
    {
        missions[selectedMissionIndex].isCompleted = true;

        if (panelAnimator != null) panelAnimator.SetBool("isOpen", false);

        // Zet de complete HUD op groen (en houd rekening met de open/dicht status)
        SetHUDState(true);

        if (missionTitleText != null) missionTitleText.text = "MISSIE VOLTOOID";
        if (missionTaskText != null) missionTaskText.text = "Goed gedaan!";

        yield return new WaitForSeconds(5f);

        ResetUIToDefault();

        selectedMissionIndex = -1;
        missionActive = false;
        if (activePad != null) activePad.SetActive(false);

        // Terug naar blauw en gesloten
        UpdateExtensionVisualState(false);
        SetHUDState(false);
        SpawnWorldMarkers();
    }

    private void ResetUIToDefault()
    {
        if (missionTitleText != null) missionTitleText.text = "Start een missie";
        if (missionTaskText != null) missionTaskText.text = "Volg de radar voor een missie";
        if (missionDescriptionText != null) missionDescriptionText.text = "";
    }

    private void ResetExtensions()
    {
        UpdateExtensionVisualState(false);
    }

    /// <summary>
    /// Update de Animator �n de interne state zodat de RightBar weet welke sprite hij moet kiezen.
    /// </summary>
    private void UpdateExtensionVisualState(bool open)
    {
        isExtensionOpen = open;

        if (rightExtensionAnimator != null) rightExtensionAnimator.SetBool("isInRange", open);
        if (leftExtensionAnimator != null) leftExtensionAnimator.SetBool("isInRange", open);

        // Update de HUD sprites direct mee op basis van de huidige missie-status (groen of blauw)
        bool isCurrentMissionFinished = (selectedMissionIndex != -1) ? missions[selectedMissionIndex].isCompleted : false;
        SetHUDState(isCurrentMissionFinished);
    }

    private void SetHUDState(bool isFinished)
    {
        // Verandert de sprites van de vaste frames
        if (leftBar != null) leftBar.sprite = isFinished ? leftBarFinished : leftBarNormal;
        if (botBar != null) botBar.sprite = isFinished ? botBarFinished : botBarNormal;
        if (topBarR != null) topBarR.sprite = isFinished ? topBarFinished : topBarNormal;
        if (topBarL != null) topBarL.sprite = isFinished ? topBarFinished : topBarNormal;
        if (missionPanelTopImage != null) missionPanelTopImage.sprite = isFinished ? panelFinished : panelNormal;
        if (radarBackground != null) radarBackground.sprite = isFinished ? radarFinished : radarNormal;

        // DYNAMISCHE STRATECHIE VOOR DE RIGHTBAR (Kijkt naar isFinished �n isExtensionOpen)
        if (rightBar != null)
        {
            if (isFinished)
            {
                rightBar.sprite = isExtensionOpen ? rightBarFinishedOpened : rightBarFinished;
            }
            else
            {
                rightBar.sprite = isExtensionOpen ? rightBarNormalOpened : rightBarNormal;
            }
        }

        // Verandert de sprites van de losgekoppelde uitschuifbare extensies
        if (extensionRight != null) extensionRight.sprite = isFinished ? extensionRightFinished : extensionRightNormal;
        if (extensionLeft != null) extensionLeft.sprite = isFinished ? extensionLeftFinished : extensionLeftNormal;
    }

    // --- MISSION LOGIC ---

    public void SpawnWorldMarkers()
    {
        foreach (var marker in spawnedMarkers) if (marker != null) Destroy(marker);
        spawnedMarkers.Clear();

        for (int i = 0; i < missions.Count; i++)
        {
            if (missions[i].isCompleted) continue;
            Vector2 gridPos = GetFirstTargetPosition(missions[i]);
            Vector3 worldPos = manager.GetWorldPositionFromGrid(gridPos.x, gridPos.y);
            Vector3 markerPos = worldPos;
            markerPos.y += 0.01f;
            GameObject marker = Instantiate(markerPrefab, markerPos, Quaternion.identity, transform);
            Renderer r = marker.GetComponentInChildren<Renderer>();
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
            UpdateExtensionContent("Druk op de knop om de missie te starten.", defaultActionIcon);
            UpdateExtensionVisualState(true);
        }
        else
        {
            if (actionButton != null) actionButton.SetActive(false);
            if (selectedMissionIndex == -1) UpdateExtensionVisualState(false);
        }
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

    private void UpdateExtensionContent(string instruction, Sprite icon)
    {
        if (extensionActionText != null) extensionActionText.text = instruction;
        if (extensionActionIcon != null && icon != null) extensionActionIcon.sprite = icon;
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

        float dist = GetFlatDistance(manager.helicopter.transform.position, targetWorldPos);

        if (dist < interactionRange)
        {
            if (actionButton != null) actionButton.SetActive(true);

            string activeDescription = missionActive ? currentMission.endLocation.description : currentMission.startLocation.description;
            string activeInstruction = missionActive ? currentMission.endLocation.shortInstruction : currentMission.startLocation.shortInstruction;

            statusText.text = activeDescription;

            UpdateExtensionContent(activeInstruction, defaultActionIcon);
            UpdateExtensionVisualState(true);

            if (currentMission.missionType == MissionType.Scan)
            {
                scanTimer += Time.deltaTime;
                statusText.text = $"Scanning... {Mathf.Round((scanTimer / scanDuration) * 100)}%";
                UpdateExtensionContent($"Bezig met scannen... {Mathf.Round((scanTimer / scanDuration) * 100)}%", defaultActionIcon);
                if (scanTimer >= scanDuration) CompleteStep();
            }
        }
        else
        {
            if (actionButton != null) actionButton.SetActive(false);
            statusText.text = $"Goal: {currentMission.missionName}";

            UpdateExtensionVisualState(false);
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

        selectedMissionIndex = -1;
        currentTargetIndex = 0;
        missionActive = false;
        scanTimer = 0f;

        foreach (var m in missions) m.isCompleted = false;

        if (activePad != null) activePad.SetActive(false);
        if (actionButton != null) actionButton.SetActive(false);

        if (panelAnimator != null) panelAnimator.SetBool("isOpen", false);

        ResetUIToDefault();
        UpdateExtensionVisualState(false);
        SetHUDState(false);

        SpawnWorldMarkers();
        Debug.Log("Sessie gereset naar beginteksten.");
    }

    private float GetFlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
}