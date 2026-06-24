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
    public Animator rightExtensionAnimator;
    public Animator leftExtensionAnimator;

    [Header("Extension Dynamic Content")]
    public TMP_Text extensionActionText;
    public Image extensionActionIcon;
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
    public Sprite rightBarFinishedOpened;

    private const string ANIM_PARAM = "IsInRange";
    private MissionStateController stateController;
    private bool isMissionCompleteDisplayActive = false;

    public void Initialize(MissionStateController controller)
    {
        stateController = controller;
        stateController.OnMissionStarted += HandleMissionStarted;
        stateController.OnMissionCompleted += HandleMissionCompleted;
        stateController.OnStepCompleted += HandleStepCompleted;
        stateController.OnMissionReset += HandleMissionReset;
        stateController.OnProximityChanged += HandleProximityDisplay;

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
    }

    public void UpdateMissionUI()
    {
        if (stateController == null || stateController.selectedMissionIndex == -1) return;
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
        if (isMissionCompleteDisplayActive) return;
        if (statusText != null) statusText.text = statusLabel;

        // Forceer de HUD-sprites continu op basis van de werkelijke status zodat er nooit een zwart gat valt
        SetHUDState(EvaluateIndexStateFinished());

        if (actionButton != null) actionButton.SetActive(false);
    }

    private void HandleMissionStarted(int index)
    {
        isMissionCompleteDisplayActive = false;

        // Bovenpaneel animator uit zodat hij niet uit zichzelf opent
        if (panelAnimator != null)
        {
            panelAnimator.SetBool("isOpen", false);
            panelAnimator.enabled = false;
        }

        if (actionButton != null) actionButton.SetActive(false);

        // Alleen RECHTS uitschakelen zodat de sprite stabiel blijft
        if (rightExtensionAnimator != null) rightExtensionAnimator.enabled = false;

        // LINKS LATEN WE HIER VOLLEDIG MET RUST! Geen .enabled = false meer.
        // Zo blijft de admin lade gewoon reageren op je swipes.

        UpdateMissionUI();
        SetHUDState(false);
    }

    private void HandleMissionCompleted(int index)
    {
        isMissionCompleteDisplayActive = true;

        if (panelAnimator != null) panelAnimator.enabled = true; // Weer aan voor het eindscherm

        if (PopupManager.Instance != null) PopupManager.Instance.ClosePopup();

        StartCoroutine(ShowMissionCompletePanel());
    }

    private void HandleStepCompleted()
    {
        if (PopupManager.Instance != null) PopupManager.Instance.ClosePopup();
        UpdateMissionUI();
        SetHUDState(EvaluateIndexStateFinished());
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
        if (panelAnimator != null)
        {
            panelAnimator.enabled = true;
            panelAnimator.SetBool("isOpen", false);
        }

        if (missionTitleText != null) missionTitleText.text = "";
        if (missionTaskText != null) missionTaskText.text = "";
        if (missionDescriptionText != null) missionDescriptionText.text = "";
        if (statusText != null) statusText.text = "";

        if (actionButton != null) actionButton.SetActive(false);

        if (rightExtensionAnimator != null) rightExtensionAnimator.enabled = false;

        // Ook hier de linker animator NIET meer aanraken of uitzetten.

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
        if (rightBar != null) rightBar.sprite = isFinished ? rightBarFinished : rightBarNormal;

        // RECHTS: Geforceerd aan op de default image
        if (extensionRight != null)
        {
            extensionRight.gameObject.SetActive(true);
            extensionRight.sprite = isFinished ? extensionRightFinished : extensionRightNormal;
        }

        // LINKS: Alleen zorgen dat het GameObject op Active(true) staat.
        // We overschrijven de sprite en de positie NIET, zodat de animator van je admin drag 
        // vloeiend zijn eigen animaties en sprites kan afspelen.
        if (extensionLeft != null)
        {
            extensionLeft.gameObject.SetActive(true);
        }
    }

    private bool EvaluateIndexStateFinished()
    {
        if (stateController == null || stateController.selectedMissionIndex == -1) return false;
        return stateController.missions[stateController.selectedMissionIndex].isCompleted;
    }
}