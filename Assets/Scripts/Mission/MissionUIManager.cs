using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionUIController : MonoBehaviour
{
    public TMP_Text statusText;
    public GameObject actionButton;

    [Header("UI Animation")]
    public Animator panelAnimator;
    public TMP_Text missionTitleText;
    public TMP_Text missionTaskText;
    public TMP_Text missionDescriptionText;

    [Header("Delivery Timer Test UI")]
    [Tooltip("Assign a TextMeshPro UI text placed in the top-left corner of the Canvas.")]
    public TMP_Text deliveryTimerText;

    [Min(10)]
    public int deliveryTimerFontSize = 36;

    [Header("Intro Settings")]
    public IntroSequenceController introSequence;

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

    private MissionStateController stateController;
    private bool isMissionCompleteDisplayActive;

    public void Initialize(MissionStateController controller)
    {
        stateController = controller;

        if (stateController == null)
        {
            Debug.LogError("MissionUIController: MissionStateController ontbreekt.", this);
            return;
        }

        stateController.OnMissionStarted += HandleMissionStarted;
        stateController.OnMissionCompleted += HandleMissionCompleted;
        stateController.OnMissionFailed += HandleMissionFailed;
        stateController.OnStepCompleted += HandleStepCompleted;
        stateController.OnMissionReset += HandleMissionReset;
        stateController.OnProximityChanged += HandleProximityDisplay;
        stateController.OnScanProgressUpdated += HandleScanProgressUpdated;
        stateController.OnDeliveryTimerUpdated += HandleDeliveryTimerUpdated;

        ResetUI();
    }

    private void OnDestroy()
    {
        if (stateController == null) return;

        stateController.OnMissionStarted -= HandleMissionStarted;
        stateController.OnMissionCompleted -= HandleMissionCompleted;
        stateController.OnMissionFailed -= HandleMissionFailed;
        stateController.OnStepCompleted -= HandleStepCompleted;
        stateController.OnMissionReset -= HandleMissionReset;
        stateController.OnProximityChanged -= HandleProximityDisplay;
        stateController.OnScanProgressUpdated -= HandleScanProgressUpdated;
        stateController.OnDeliveryTimerUpdated -= HandleDeliveryTimerUpdated;
    }

    public void UpdateMissionUI()
    {
        if (stateController == null || stateController.selectedMissionIndex == -1) return;

        MissionStateController.Mission activeMission =
            stateController.missions[stateController.selectedMissionIndex];

        if (missionTitleText != null) missionTitleText.text = activeMission.missionName;

        MissionStateController.MissionTarget currentTarget = GetCurrentTarget(activeMission);

        if (currentTarget == null)
        {
            if (missionTaskText != null) missionTaskText.text = "";
            if (missionDescriptionText != null) missionDescriptionText.text = "";
            return;
        }

        if (missionTaskText != null) missionTaskText.text = currentTarget.shortInstruction;
        if (missionDescriptionText != null) missionDescriptionText.text = currentTarget.description;
    }

    private MissionStateController.MissionTarget GetCurrentTarget(
        MissionStateController.Mission mission)
    {
        int index = stateController.currentTargetIndex;

        switch (mission.missionType)
        {
            case MissionStateController.MissionType.Delivery:
                if (mission.deliveryMode == MissionStateController.DeliveryMode.MultipleDestinations &&
                    mission.deliveryTargets != null &&
                    index >= 0 &&
                    index < mission.deliveryTargets.Count)
                {
                    return mission.deliveryTargets[index];
                }

                return mission.endLocation;

            case MissionStateController.MissionType.SearchFind:
                if (mission.searchTargets != null && index >= 0 && index < mission.searchTargets.Count)
                    return mission.searchTargets[index];

                break;

            case MissionStateController.MissionType.Scan:
                if (mission.scanTargets != null && index >= 0 && index < mission.scanTargets.Count)
                    return mission.scanTargets[index];

                break;
        }

        return null;
    }

    private void HandleProximityDisplay(
        bool isInRange,
        string actionText,
        Sprite actionIcon,
        string statusLabel)
    {
        if (isMissionCompleteDisplayActive) return;

        if (statusText != null) statusText.text = statusLabel;

        SetHUDState(EvaluateIndexStateFinished());

        // PopupManager handles the interaction button.
        if (actionButton != null) actionButton.SetActive(false);
    }

    private void HandleScanProgressUpdated(float progressPercent)
    {
        if (isMissionCompleteDisplayActive) return;

        if (progressPercent <= 0f)
        {
            if (statusText != null && statusText.text.StartsWith("Scanning"))
                statusText.text = "";

            if (extensionActionText != null && extensionActionText.text.StartsWith("Bezig met scannen"))
                extensionActionText.text = "";

            return;
        }

        int roundedProgress = Mathf.RoundToInt(progressPercent);

        if (statusText != null) statusText.text = $"Scanning... {roundedProgress}%";
        if (extensionActionText != null) extensionActionText.text = $"Bezig met scannen... {roundedProgress}%";
    }

    private void HandleDeliveryTimerUpdated(float remainingTime, float totalTime)
    {
        if (deliveryTimerText == null) return;

        bool shouldShow =
            !isMissionCompleteDisplayActive &&
            totalTime > 0f &&
            remainingTime > 0f;

        deliveryTimerText.gameObject.SetActive(shouldShow);

        if (!shouldShow)
        {
            deliveryTimerText.text = "";
            return;
        }

        deliveryTimerText.fontSize = deliveryTimerFontSize;
        deliveryTimerText.color = Color.green;
        deliveryTimerText.text = Mathf.CeilToInt(remainingTime).ToString();
    }

    private void HandleMissionStarted(int index)
    {
        StopAllCoroutines();

        isMissionCompleteDisplayActive = false;
        HideDeliveryTimer();

        if (panelAnimator != null)
        {
            panelAnimator.SetBool("isOpen", false);
            panelAnimator.enabled = false;
        }

        if (rightExtensionAnimator != null) rightExtensionAnimator.enabled = false;
        if (leftExtensionAnimator != null) leftExtensionAnimator.enabled = false;
        if (actionButton != null) actionButton.SetActive(false);

        UpdateMissionUI();
        SetHUDState(false);
    }

    private void HandleMissionCompleted(int index)
    {
        isMissionCompleteDisplayActive = true;
        HideDeliveryTimer();

        if (PopupManager.Instance != null) PopupManager.Instance.ClosePopup();
        if (panelAnimator != null) panelAnimator.enabled = true;

        StopAllCoroutines();
        StartCoroutine(ShowMissionCompletePanel());
    }

    private void HandleMissionFailed(int index)
    {
        StopAllCoroutines();

        isMissionCompleteDisplayActive = false;
        HideDeliveryTimer();

        if (PopupManager.Instance != null) PopupManager.Instance.ClosePopup();

        if (missionTitleText != null) missionTitleText.text = "MISSIE MISLUKT";
        if (missionTaskText != null) missionTaskText.text = "De missie wordt opnieuw gestart.";
        if (missionDescriptionText != null) missionDescriptionText.text = "";
        if (statusText != null) statusText.text = "De bezorgtijd is verstreken.";

        SetHUDState(false);
    }

    private void HandleStepCompleted()
    {
        if (PopupManager.Instance != null) PopupManager.Instance.ClosePopup();

        UpdateMissionUI();
        SetHUDState(false);

        if (statusText != null) statusText.text = "";
    }

    private void HandleMissionReset()
    {
        StopAllCoroutines();

        HideDeliveryTimer();

        if (PopupManager.Instance != null) PopupManager.Instance.ClosePopup();
        if (introSequence != null) introSequence.ResetIntroSequence();

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

    private void HideDeliveryTimer()
    {
        if (deliveryTimerText == null) return;

        deliveryTimerText.text = "";
        deliveryTimerText.gameObject.SetActive(false);
    }

    public void ResetUI()
    {
        HideDeliveryTimer();

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
        if (extensionActionText != null) extensionActionText.text = "";
        if (extensionActionIcon != null) extensionActionIcon.sprite = null;
        if (rightExtensionAnimator != null) rightExtensionAnimator.enabled = false;
        if (leftExtensionAnimator != null) leftExtensionAnimator.enabled = false;

        SetHUDState(false);
    }

    public void SetHUDState(bool isFinished)
    {
        if (leftBar != null) leftBar.sprite = isFinished ? leftBarFinished : leftBarNormal;
        if (rightBar != null) rightBar.sprite = isFinished ? rightBarFinished : rightBarNormal;
        if (botBar != null) botBar.sprite = isFinished ? botBarFinished : botBarNormal;
        if (topBarR != null) topBarR.sprite = isFinished ? topBarFinished : topBarNormal;
        if (topBarL != null) topBarL.sprite = isFinished ? topBarFinished : topBarNormal;
        if (missionPanelTopImage != null) missionPanelTopImage.sprite = isFinished ? panelFinished : panelNormal;
        if (radarBackground != null) radarBackground.sprite = isFinished ? radarFinished : radarNormal;

        if (extensionRight != null)
        {
            extensionRight.gameObject.SetActive(true);
            extensionRight.sprite = isFinished ? extensionRightFinished : extensionRightNormal;
        }

        if (extensionLeft != null)
        {
            extensionLeft.gameObject.SetActive(true);
            extensionLeft.sprite = isFinished ? extensionLeftFinished : extensionLeftNormal;
        }
    }

    private bool EvaluateIndexStateFinished()
    {
        if (stateController == null || stateController.selectedMissionIndex == -1) return false;

        return stateController
            .missions[stateController.selectedMissionIndex]
            .isCompleted;
    }
}