using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class IntroSequenceController : MonoBehaviour
{
    [Header("Joysticks")]
    public GameObject variableJoystickL;
    public GameObject variableJoystickR;

    [Header("Intro UI Elements")]
    [Tooltip("Het MissionPanel / introductie-paneel dat je wilt wegfaden")]
    public GameObject introPanel;

    [Tooltip("De Start-knop in het intro paneel")]
    public Button startButton;

    [Header("Settings")]
    public float fadeDuration = 0.35f;
    public float freeFlightDuration = 5f;

    private CanvasGroup introCanvasGroup;

    private void Awake()
    {
        if (introPanel != null)
        {
            introCanvasGroup = introPanel.GetComponent<CanvasGroup>();

            if (introCanvasGroup == null)
                introCanvasGroup = introPanel.AddComponent<CanvasGroup>();
        }

        if (startButton != null) startButton.onClick.AddListener(OnStartButtonPressed);

        InitialSetupState();
    }

    private void InitialSetupState()
    {
        if (variableJoystickL != null) variableJoystickL.SetActive(false);
        if (variableJoystickR != null) variableJoystickR.SetActive(false);
        if (introPanel != null) introPanel.SetActive(true);

        if (introCanvasGroup != null)
        {
            introCanvasGroup.alpha = 1f;
            introCanvasGroup.interactable = true;
            introCanvasGroup.blocksRaycasts = true;
        }

        if (startButton != null) startButton.interactable = true;
    }

    public void ResetIntroSequence()
    {
        enabled = true;
        StopAllCoroutines();
        InitialSetupState();
    }

    private void OnStartButtonPressed()
    {
        if (startButton != null) startButton.interactable = false;

        StartCoroutine(FadeOutAndStartGame());
    }

    private IEnumerator FadeOutAndStartGame()
    {
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            if (introCanvasGroup != null)
                introCanvasGroup.alpha = Mathf.Clamp01(1f - elapsed / fadeDuration);

            yield return null;
        }

        if (introCanvasGroup != null)
        {
            introCanvasGroup.alpha = 0f;
            introCanvasGroup.interactable = false;
            introCanvasGroup.blocksRaycasts = false;
        }

        if (variableJoystickL != null) variableJoystickL.SetActive(true);
        if (variableJoystickR != null) variableJoystickR.SetActive(true);

        Debug.Log($"[INTRO] Joysticks aan. {freeFlightDuration} seconden vrij vliegen...");

        yield return new WaitForSeconds(freeFlightDuration);

        Debug.Log("[INTRO] Waarschuwingspopup triggeren.");

        if (PopupManager.Instance == null)
        {
            Debug.LogError("[INTRO] PopupManager.Instance is null! Staat er een PopupManager in de scene?");

            FinishIntro();
            yield break;
        }

        MissionStateController stateController =
            FindFirstObjectByType<MissionStateController>();

        if (stateController == null || stateController.missions == null || stateController.missions.Count == 0)
        {
            Debug.LogError("[INTRO] Geen MissionStateController of missies gevonden!");

            FinishIntro();
            yield break;
        }

        var firstMission = stateController.missions[0];
        var introPopup = firstMission.missionIntroPopup;

        string title = "⚠️ WAARSCHUWING!";
        string description = "De EVIL AI stuurt zijn handlangers op pad naar ASML om de chips te bemachtigen! " +
                             "Laat dit niet gebeuren! We hebben deze chips hard nodig om de EVIL AI uit te schakelen!";
        Sprite icon = firstMission.startLocation?.targetIcon;
        string actionLabel = "Start Missie!";

        if (introPopup != null)
        {
            if (!string.IsNullOrEmpty(introPopup.title)) title = introPopup.title;
            if (!string.IsNullOrEmpty(introPopup.description)) description = introPopup.description;
            if (introPopup.icon != null) icon = introPopup.icon;
            if (!string.IsNullOrEmpty(introPopup.actionButtonText)) actionLabel = introPopup.actionButtonText;
        }

        PopupManager.Instance.ShowPopup(
            title,
            description,
            icon,
            actionLabel,
            () => stateController.StartMission(0)
        );

        FinishIntro();
    }

    private MissionStateController.MissionTarget GetFirstTarget(
        MissionStateController.Mission mission)
    {
        if (mission == null) return null;

        switch (mission.missionType)
        {
            case MissionStateController.MissionType.Delivery:
                if (mission.deliveryMode == MissionStateController.DeliveryMode.MultipleDestinations &&
                    mission.deliveryTargets != null &&
                    mission.deliveryTargets.Count > 0)
                {
                    return mission.deliveryTargets[0];
                }

                return mission.endLocation;

            case MissionStateController.MissionType.SearchFind:
                if (mission.searchTargets != null && mission.searchTargets.Count > 0)
                    return mission.searchTargets[0];

                break;

            case MissionStateController.MissionType.Scan:
                if (mission.scanTargets != null && mission.scanTargets.Count > 0)
                    return mission.scanTargets[0];

                break;
        }

        return null;
    }

    private void FinishIntro()
    {
        if (introPanel != null) introPanel.SetActive(false);

        enabled = false;
    }

    private void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartButtonPressed);
    }
}