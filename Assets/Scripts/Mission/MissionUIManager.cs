using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MissionUIController : MonoBehaviour
{
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

    private const string ANIM_PARAM = "IsInRange";

    private MissionStateController stateController;
    private bool isExtensionOpen = false;

    // Blokkeert ENKEL proximity-updates TIJDENS het tonen van het 5-seconden scherm
    private bool isMissionCompleteDisplayActive = false;

    public void Initialize(MissionStateController controller)
    {
        stateController = controller;

        if (rightExtensionAnimator == null)
        {
            rightExtensionAnimator = GetComponentInChildren<Animator>();
            if (rightExtensionAnimator == null)
            {
                Transform foundExtension = transform.Find("ExtensionRightB");
                if (foundExtension != null) rightExtensionAnimator = foundExtension.GetComponent<Animator>();
            }
        }

        stateController.OnMissionStarted += HandleMissionStarted;
        stateController.OnMissionCompleted += HandleMissionCompleted;
        stateController.OnStepCompleted += HandleStepCompleted;
        stateController.OnMissionReset += HandleMissionReset;
        stateController.OnProximityChanged += HandleProximityDisplay;
        stateController.OnScanProgressUpdated += HandleScanProgressUpdated;

        ResetUI();
    }

    private void OnDestroy()
    {
        if (stateController == null) return;
        stateController.OnMissionStarted -= HandleMissionStarted;
        stateController.OnMissionCompleted -= HandleMissionCompleted;
        stateController.OnStepCompleted -= HandleStepCompleted;
        stateController.OnMissionReset -= HandleMissionReset;
        stateController.OnProximityChanged -= HandleProximityDisplay;
        stateController.OnScanProgressUpdated -= HandleScanProgressUpdated;
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

    private void HandleProximityDisplay(bool isInRange, string actionText, Sprite actionIcon, string statusLabel)
    {
        // Gecorrigeerd: Alleen blokkeren als het eindscherm daadwerkelijk in beeld flitst
        if (isMissionCompleteDisplayActive) return;

        if (statusText != null) statusText.text = statusLabel;
        UpdateExtensionVisualState(isInRange, actionText, actionIcon);
    }

    public void UpdateExtensionVisualState(bool open, string actionText, Sprite actionIcon)
    {
        isExtensionOpen = open;

        if (actionButton != null) actionButton.SetActive(true);

        if (extensionActionText != null) extensionActionText.text = open ? actionText : "";
        if (extensionActionIcon != null) extensionActionIcon.sprite = open ? (actionIcon != null ? actionIcon : defaultActionIcon) : null;

        if (rightExtensionAnimator != null) rightExtensionAnimator.SetBool(ANIM_PARAM, open);
        if (leftExtensionAnimator != null) leftExtensionAnimator.SetBool(ANIM_PARAM, open);

        // Altijd de HUD-staat updaten op basis van de werkelijke voortgang
        SetHUDState(EvaluateIndexStateFinished());
    }

    private void HandleScanProgressUpdated(float progressPercent)
    {
        if (isMissionCompleteDisplayActive) return;

        if (statusText != null) statusText.text = $"Scanning... {progressPercent}%";
        if (extensionActionText != null) extensionActionText.text = $"Bezig met scannen... {progressPercent}%";

        isExtensionOpen = true;
        if (rightExtensionAnimator != null) rightExtensionAnimator.SetBool(ANIM_PARAM, true);
        if (leftExtensionAnimator != null) leftExtensionAnimator.SetBool(ANIM_PARAM, true);
    }

    private void HandleMissionStarted(int index)
    {
        isMissionCompleteDisplayActive = false;
        if (panelAnimator != null) panelAnimator.SetBool("isOpen", true);

        UpdateExtensionVisualState(false, "", null);
        UpdateMissionUI();
    }

    private void HandleMissionCompleted(int index)
    {
        isMissionCompleteDisplayActive = true;

        // Sluit de extensie animatie, maar vernietig/verberg de sprites niet vroegtijdig!
        isExtensionOpen = false;
        if (rightExtensionAnimator != null) rightExtensionAnimator.SetBool(ANIM_PARAM, false);
        if (leftExtensionAnimator != null) leftExtensionAnimator.SetBool(ANIM_PARAM, false);

        StartCoroutine(ShowMissionCompletePanel());
    }

    private void HandleStepCompleted()
    {
        // Na een tussentijdse taak sluiten we de lade netjes en wachten op de volgende range-trigger
        UpdateExtensionVisualState(false, "", null);
        UpdateMissionUI();
    }

    private void HandleMissionReset()
    {
        StopAllCoroutines();
        isMissionCompleteDisplayActive = false;
        ResetUI();
    }

    public IEnumerator ShowMissionCompletePanel()
    {
        if (panelAnimator != null) panelAnimator.SetBool("isOpen", false);

        // Forceer direct GROEN over het hele scherm
        SetHUDState(true);

        if (missionTitleText != null) missionTitleText.text = "MISSIE VOLTOOID";
        if (missionTaskText != null) missionTaskText.text = "Goed gedaan!";
        if (missionDescriptionText != null) missionDescriptionText.text = "";
        if (statusText != null) statusText.text = "";

        yield return new WaitForSeconds(5f);

        isMissionCompleteDisplayActive = false;
        ResetUI();
    }

    public void ResetUI()
    {
        if (missionTitleText != null) missionTitleText.text = "Start een missie";
        if (missionTaskText != null) missionTaskText.text = "Volg de radar voor een missie";
        if (missionDescriptionText != null) missionDescriptionText.text = "";
        if (statusText != null) statusText.text = "Fly to a marker to start a mission";

        // Zorg dat de actionButton zichtbaar/beschikbaar blijft voor de volgende missie-selectie
        if (actionButton != null) actionButton.SetActive(true);

        isExtensionOpen = false;
        if (rightExtensionAnimator != null) rightExtensionAnimator.SetBool(ANIM_PARAM, false);
        if (leftExtensionAnimator != null) leftExtensionAnimator.SetBool(ANIM_PARAM, false);

        if (extensionActionText != null) extensionActionText.text = "";
        if (extensionActionIcon != null) extensionActionIcon.sprite = null;

        // Reset de complete HUD-sprites keurig terug naar Normaal (Blauw)
        SetHUDState(false);
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

        // FIX: Voorkom dat de extensie-afbeeldingen op 'null' springen of onzichtbaar worden
        if (extensionRight != null) extensionRight.sprite = isFinished ? extensionRightFinished : extensionRightNormal;
        if (extensionLeft != null) extensionLeft.sprite = isFinished ? extensionLeftFinished : extensionLeftNormal;
    }

    private bool EvaluateIndexStateFinished()
    {
        // Veilige fallback: als de stateController denkt dat er geen missie is, is hij sowieso false (blauw)
        if (stateController == null || stateController.selectedMissionIndex == -1) return false;
        return stateController.missions[stateController.selectedMissionIndex].isCompleted;
    }
}