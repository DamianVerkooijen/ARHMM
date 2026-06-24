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

    [Header("Delivery Warning Settings")]
    [Min(1)]
    public int deliveryWarningTime = 10;

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

    [Header("Sprites Warning (Rood)")]
    public Sprite leftBarWarning;
    public Sprite rightBarWarning;
    public Sprite botBarWarning;
    public Sprite topBarWarning;
    public Sprite panelWarning;
    public Sprite radarWarning;
    public Sprite extensionRightWarning;
    public Sprite extensionLeftWarning;
    public Sprite rightBarWarningOpened;

    private MissionStateController stateController;
    private bool isMissionCompleteDisplayActive;
    private bool isDeliveryWarningActive;

    private enum HUDVisualState
    {
        Normal,
        Finished,
        Warning
    }

    public void Initialize(MissionStateController controller)
    {
        stateController = controller;

        if (stateController == null)
        {
            Debug.LogError(
                "MissionUIController: MissionStateController ontbreekt.",
                this
            );

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
        if (stateController.selectedMissionIndex >= stateController.missions.Count) return;

        MissionStateController.Mission activeMission =
            stateController.missions[stateController.selectedMissionIndex];

        if (missionTitleText != null)
            missionTitleText.text = activeMission.missionName;

        MissionStateController.MissionTarget currentTarget =
            GetCurrentTarget(activeMission);

        if (currentTarget == null)
        {
            if (missionTaskText != null) missionTaskText.text = "";
            if (missionDescriptionText != null) missionDescriptionText.text = "";

            return;
        }

        if (missionTaskText != null)
            missionTaskText.text = currentTarget.shortInstruction;

        if (missionDescriptionText != null)
            missionDescriptionText.text = currentTarget.description;
    }

    private MissionStateController.MissionTarget GetCurrentTarget(
        MissionStateController.Mission mission)
    {
        int index = stateController.currentTargetIndex;

        switch (mission.missionType)
        {
            case MissionStateController.MissionType.Delivery:
                if (mission.deliveryMode ==
                        MissionStateController.DeliveryMode.MultipleDestinations &&
                    mission.deliveryTargets != null &&
                    index >= 0 &&
                    index < mission.deliveryTargets.Count)
                {
                    return mission.deliveryTargets[index];
                }

                return mission.endLocation;

            case MissionStateController.MissionType.SearchFind:
                if (mission.searchTargets != null &&
                    index >= 0 &&
                    index < mission.searchTargets.Count)
                {
                    return mission.searchTargets[index];
                }

                break;

            case MissionStateController.MissionType.Scan:
                if (mission.scanTargets != null &&
                    index >= 0 &&
                    index < mission.scanTargets.Count)
                {
                    return mission.scanTargets[index];
                }

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

        RefreshHUDState();

        if (actionButton != null) actionButton.SetActive(false);
    }

    private void HandleDeliveryTimerUpdated(
        float remainingTime,
        float totalTime)
    {
        if (isMissionCompleteDisplayActive) return;
        if (stateController == null) return;
        if (stateController.selectedMissionIndex == -1) return;
        if (stateController.selectedMissionIndex >= stateController.missions.Count) return;

        MissionStateController.Mission mission =
            stateController.missions[stateController.selectedMissionIndex];

        bool isTimedDelivery =
            mission.missionType ==
                MissionStateController.MissionType.Delivery &&
            mission.useDeliveryTimer &&
            totalTime > 0f &&
            stateController.isDeliveryTimerRunning;

        if (!isTimedDelivery)
        {
            isDeliveryWarningActive = false;
            RefreshHUDState();
            return;
        }

        int secondsRemaining =
            Mathf.Max(0, Mathf.CeilToInt(remainingTime));

        if (secondsRemaining <= 0)
        {
            isDeliveryWarningActive = true;
            RefreshHUDState();
            return;
        }

        if (secondsRemaining > deliveryWarningTime)
        {
            isDeliveryWarningActive = false;
            RefreshHUDState();
            return;
        }

        // 10 = red, 9 = blue, 8 = red, 7 = blue...
        isDeliveryWarningActive = secondsRemaining % 2 == 0;

        RefreshHUDState();
    }

    private void HandleScanProgressUpdated(float progressPercent)
    {
        if (isMissionCompleteDisplayActive) return;

        if (progressPercent <= 0f)
        {
            if (statusText != null &&
                statusText.text.StartsWith("Scanning"))
            {
                statusText.text = "";
            }

            if (extensionActionText != null &&
                extensionActionText.text.StartsWith("Bezig met scannen"))
            {
                extensionActionText.text = "";
            }

            return;
        }

        int roundedProgress =
            Mathf.RoundToInt(progressPercent);

        if (statusText != null)
            statusText.text = $"Scanning... {roundedProgress}%";

        if (extensionActionText != null)
            extensionActionText.text =
                $"Bezig met scannen... {roundedProgress}%";
    }

    private void HandleMissionStarted(int index)
    {
        StopAllCoroutines();

        isMissionCompleteDisplayActive = false;
        isDeliveryWarningActive = false;

        if (panelAnimator != null)
        {
            panelAnimator.SetBool("isOpen", false);
            panelAnimator.enabled = false;
        }

        if (rightExtensionAnimator != null)
            rightExtensionAnimator.enabled = false;

        if (leftExtensionAnimator != null)
            leftExtensionAnimator.enabled = false;

        if (actionButton != null)
            actionButton.SetActive(false);

        UpdateMissionUI();
        RefreshHUDState();
    }

    private void HandleMissionCompleted(int index)
    {
        isMissionCompleteDisplayActive = true;
        isDeliveryWarningActive = false;

        if (PopupManager.Instance != null)
            PopupManager.Instance.ClosePopup();

        if (panelAnimator != null)
            panelAnimator.enabled = true;

        StopAllCoroutines();

        RefreshHUDState();
        StartCoroutine(ShowMissionCompletePanel());
    }

    private void HandleMissionFailed(int index)
    {
        StopAllCoroutines();

        isMissionCompleteDisplayActive = false;
        isDeliveryWarningActive = true;

        if (PopupManager.Instance != null)
            PopupManager.Instance.ClosePopup();

        if (missionTitleText != null)
            missionTitleText.text = "MISSIE MISLUKT";

        if (missionTaskText != null)
            missionTaskText.text = "TIJD VERSTREKEN";

        if (missionDescriptionText != null)
            missionDescriptionText.text =
                "De missie wordt opnieuw gestart.";

        if (statusText != null)
            statusText.text =
                "De bezorgtijd is verstreken.";

        RefreshHUDState();
    }

    private void HandleStepCompleted()
    {
        if (PopupManager.Instance != null)
            PopupManager.Instance.ClosePopup();

        UpdateMissionUI();
        RefreshHUDState();

        if (statusText != null)
            statusText.text = "";
    }

    private void HandleMissionReset()
    {
        StopAllCoroutines();

        if (PopupManager.Instance != null)
            PopupManager.Instance.ClosePopup();

        if (introSequence != null)
            introSequence.ResetIntroSequence();

        isMissionCompleteDisplayActive = false;
        isDeliveryWarningActive = false;

        ResetUI();
    }

    public IEnumerator ShowMissionCompletePanel()
    {
        if (panelAnimator != null)
            panelAnimator.SetBool("isOpen", false);

        if (missionTitleText != null)
            missionTitleText.text = "MISSIE VOLTOOID";

        if (missionTaskText != null)
            missionTaskText.text = "Goed gedaan!";

        if (missionDescriptionText != null)
            missionDescriptionText.text = "";

        if (statusText != null)
            statusText.text = "";

        yield return new WaitForSeconds(5f);

        isMissionCompleteDisplayActive = false;
        ResetUI();
    }

    private void RefreshHUDState()
    {
        if (isDeliveryWarningActive)
        {
            SetHUDState(HUDVisualState.Warning);
            return;
        }

        if (isMissionCompleteDisplayActive)
        {
            SetHUDState(HUDVisualState.Finished);
            return;
        }

        SetHUDState(HUDVisualState.Normal);
    }

    public void ResetUI()
    {
        isDeliveryWarningActive = false;

        if (panelAnimator != null)
        {
            panelAnimator.enabled = true;
            panelAnimator.SetBool("isOpen", false);
        }

        if (missionTitleText != null)
            missionTitleText.text = "";

        if (missionTaskText != null)
            missionTaskText.text = "";

        if (missionDescriptionText != null)
            missionDescriptionText.text = "";

        if (statusText != null)
            statusText.text = "";

        if (actionButton != null)
            actionButton.SetActive(false);

        if (extensionActionText != null)
            extensionActionText.text = "";

        if (extensionActionIcon != null)
            extensionActionIcon.sprite = null;

        if (rightExtensionAnimator != null)
            rightExtensionAnimator.enabled = false;

        if (leftExtensionAnimator != null)
            leftExtensionAnimator.enabled = false;

        SetHUDState(HUDVisualState.Normal);
    }

    private void SetHUDState(HUDVisualState state)
    {
        Sprite left = null;
        Sprite right = null;
        Sprite bottom = null;
        Sprite top = null;
        Sprite panel = null;
        Sprite radar = null;
        Sprite extensionRightSprite = null;
        Sprite extensionLeftSprite = null;

        switch (state)
        {
            case HUDVisualState.Finished:
                left = leftBarFinished;
                right = rightBarFinished;
                bottom = botBarFinished;
                top = topBarFinished;
                panel = panelFinished;
                radar = radarFinished;
                extensionRightSprite = extensionRightFinished;
                extensionLeftSprite = extensionLeftFinished;
                break;

            case HUDVisualState.Warning:
                left = leftBarWarning;
                right = rightBarWarning;
                bottom = botBarWarning;
                top = topBarWarning;
                panel = panelWarning;
                radar = radarWarning;
                extensionRightSprite = extensionRightWarning;
                extensionLeftSprite = extensionLeftWarning;
                break;

            default:
                left = leftBarNormal;
                right = rightBarNormal;
                bottom = botBarNormal;
                top = topBarNormal;
                panel = panelNormal;
                radar = radarNormal;
                extensionRightSprite = extensionRightNormal;
                extensionLeftSprite = extensionLeftNormal;
                break;
        }

        if (leftBar != null) leftBar.sprite = left;
        if (rightBar != null) rightBar.sprite = right;
        if (botBar != null) botBar.sprite = bottom;
        if (topBarR != null) topBarR.sprite = top;
        if (topBarL != null) topBarL.sprite = top;
        if (missionPanelTopImage != null) missionPanelTopImage.sprite = panel;
        if (radarBackground != null) radarBackground.sprite = radar;

        if (extensionRight != null)
        {
            extensionRight.gameObject.SetActive(true);
            extensionRight.sprite = extensionRightSprite;
        }

        if (extensionLeft != null)
        {
            extensionLeft.gameObject.SetActive(true);
            extensionLeft.sprite = extensionLeftSprite;
        }
    }
}