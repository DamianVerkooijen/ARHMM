using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MissionUIController : MonoBehaviour
{
    // --- EXACT ORIGINAL VARIABLE NAMES RETAINED ---
    public TMP_Text statusText;
    public GameObject actionButton;

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

    private MissionStateController stateController;
    private bool isExtensionOpen = false;

    public void Initialize(MissionStateController controller)
    {
        stateController = controller;

        // Event hooks
        stateController.OnMissionStarted += HandleMissionStarted;
        stateController.OnMissionCompleted += HandleMissionCompleted;
        stateController.OnStepCompleted += HandleStepCompleted;
        stateController.OnMissionReset += HandleMissionReset;
        stateController.OnProximityChanged += HandleProximityDisplay;
        stateController.OnScanProgressUpdated += HandleScanProgress;

        if (actionButton != null) actionButton.SetActive(false);
        isExtensionOpen = false;
        SetHUDState(false);
        UpdateExtensionVisualState(false, false);
    }

    private void OnDestroy()
    {
        if (stateController == null) return;
        stateController.OnMissionStarted -= HandleMissionStarted;
        stateController.OnMissionCompleted -= HandleMissionCompleted;
        stateController.OnStepCompleted -= HandleStepCompleted;
        stateController.OnMissionReset -= HandleMissionReset;
        stateController.OnProximityChanged -= HandleProximityDisplay;
        stateController.OnScanProgressUpdated -= HandleScanProgress;
    }

    public void UpdateMissionUI()
    {
        if (stateController.selectedMissionIndex == -1) return;
        var activeMission = stateController.missions[stateController.selectedMissionIndex];
        
        if (missionTitleText != null) missionTitleText.text = activeMission.missionName;

        MissionStateController.MissionTarget currentTarget = null;
        switch (activeMission.missionType)
        {
            case MissionStateController.MissionType.Delivery: 
                currentTarget = !stateController.missionActive ? activeMission.startLocation : activeMission.endLocation; 
                break;
            case MissionStateController.MissionType.SearchFind: 
                if (stateController.currentTargetIndex < activeMission.searchTargets.Count) 
                    currentTarget = activeMission.searchTargets[stateController.currentTargetIndex]; 
                break;
            case MissionStateController.MissionType.Scan: 
                if (stateController.currentTargetIndex < activeMission.scanTargets.Count) 
                    currentTarget = activeMission.scanTargets[stateController.currentTargetIndex]; 
                break;
        }

        if (currentTarget != null)
        {
            if (missionTaskText != null) missionTaskText.text = currentTarget.shortInstruction;
            if (missionDescriptionText != null) missionDescriptionText.text = currentTarget.description;
        }
    }

    public void HandleProximityDisplay(bool isInRange, string instruction, string statusLabel)
    {
        if (statusText != null) statusText.text = statusLabel;
        if (actionButton != null) actionButton.SetActive(isInRange);

        if (isInRange)
        {
            UpdateExtensionContent(instruction, defaultActionIcon);
            UpdateExtensionVisualState(true, EvaluateIndexStateFinished());
        }
        else
        {
            UpdateExtensionVisualState(false, EvaluateIndexStateFinished());
        }
    }

    private void HandleScanProgress(float percentage)
    {
        if (statusText != null) statusText.text = $"Scanning... {percentage}%";
        UpdateExtensionContent($"Bezig met scannen... {percentage}%", defaultActionIcon);
        UpdateExtensionVisualState(true, false);
    }

    private void HandleMissionStarted(int index)
    {
        if (panelAnimator != null) panelAnimator.SetBool("isOpen", true);
        UpdateMissionUI();
    }

    private void HandleMissionCompleted(int index)
    {
        UpdateExtensionVisualState(false, true);
        StartCoroutine(ShowMissionCompletePanel());
    }

    private void HandleStepCompleted() => UpdateMissionUI();

    private void HandleMissionReset()
    {
        StopAllCoroutines();
        ResetUI();
    }

    public IEnumerator ShowMissionCompletePanel()
    {
        if (panelAnimator != null) panelAnimator.SetBool("isOpen", false);
        SetHUDState(true);

        if (missionTitleText != null) missionTitleText.text = "MISSIE VOLTOOID";
        if (missionTaskText != null) missionTaskText.text = "Goed gedaan!";

        yield return new WaitForSeconds(5f);
        ResetUI();
    }

    public void ResetUI()
    {
        if (missionTitleText != null) missionTitleText.text = "Start een missie";
        if (missionTaskText != null) missionTaskText.text = "Volg de radar voor een missie";
        if (missionDescriptionText != null) missionDescriptionText.text = "";
        if (statusText != null) statusText.text = "Fly to a marker to start a mission";
        if (actionButton != null) actionButton.SetActive(false);
        
        UpdateExtensionVisualState(false, false);
        SetHUDState(false);
    }

    private void UpdateExtensionContent(string instruction, Sprite icon)
    {
        if (extensionActionText != null) extensionActionText.text = instruction;
        if (extensionActionIcon != null && icon != null) extensionActionIcon.sprite = icon;
    }

    private void UpdateExtensionVisualState(bool open, bool isFinished)
    {
        isExtensionOpen = open;
        if (rightExtensionAnimator != null) rightExtensionAnimator.SetBool("isInRange", open);
        if (leftExtensionAnimator != null) leftExtensionAnimator.SetBool("isInRange", open);
        SetHUDState(isFinished);
    }

    public void SetHUDState(bool isFinished)
    {
        if (leftBar != null) leftBar.sprite = isFinished ? leftBarFinished : leftBarNormal;
        if (botBar != null) botBar.sprite = isFinished ? botBarFinished : botBarNormal;
        if (topBarR != null) topBarR.sprite = isFinished ? topBarFinished : topBarNormal;
        if (topBarL != null) topBarL.sprite = isFinished ? topBarFinished : topBarNormal;
        if (missionPanelTopImage != null) missionPanelTopImage.sprite = isFinished ? panelFinished : panelNormal;
        if (radarBackground != null) radarBackground.sprite = isFinished ? radarFinished : radarNormal;

        if (rightBar != null)
        {
            if (isFinished)
                rightBar.sprite = isExtensionOpen ? rightBarFinishedOpened : rightBarFinished;
            else
                rightBar.sprite = isExtensionOpen ? rightBarNormalOpened : rightBarNormal;
        }

        if (extensionRight != null) extensionRight.sprite = isFinished ? extensionRightFinished : extensionRightNormal;
        if (extensionLeft != null) extensionLeft.sprite = isFinished ? extensionLeftFinished : extensionLeftNormal;
    }

    private bool EvaluateIndexStateFinished()
    {
        if (stateController.selectedMissionIndex == -1) return false;
        return stateController.missions[stateController.selectedMissionIndex].isCompleted;
    }
}