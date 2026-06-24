using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopupManager : MonoBehaviour
{
    public static PopupManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject visualPanel;
    [SerializeField] private TMP_Text textTitle;
    [SerializeField] private TMP_Text textDescription;
    [SerializeField] private Image missionIcon;
    [SerializeField] private Button actionButton;

    [Header("Scene References")]
    [SerializeField] private MissionStateController stateController;

    [Header("Settings")]
    [SerializeField] private float missionCompleteDelay = 1f;

    private TMP_Text buttonText;
    private Action pendingCallback;
    private Coroutine subscribeCoroutine;
    private bool subscribed;
    private string originalDescription = "";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (actionButton != null) buttonText = actionButton.GetComponentInChildren<TMP_Text>();
        if (visualPanel != null) visualPanel.SetActive(false);
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void TrySubscribe()
    {
        if (subscribed) return;

        if (stateController == null) stateController = FindFirstObjectByType<MissionStateController>();

        if (stateController != null)
        {
            SubscribeToMissionEvents();
            return;
        }

        if (subscribeCoroutine == null) subscribeCoroutine = StartCoroutine(RetrySubscribe());
    }

    private IEnumerator RetrySubscribe()
    {
        while (stateController == null)
        {
            stateController = FindFirstObjectByType<MissionStateController>();
            yield return null;
        }

        subscribeCoroutine = null;
        SubscribeToMissionEvents();
    }

    private void SubscribeToMissionEvents()
    {
        if (stateController == null || subscribed) return;

        stateController.OnMissionStarted += HandleMissionStarted;
        stateController.OnMissionCompleted += HandleMissionCompleted;
        stateController.OnMissionFailed += HandleMissionFailed;
        stateController.OnMissionReset += HandleMissionReset;
        stateController.OnScanProgressUpdated += HandleScanProgressUpdate;

        subscribed = true;
    }

    private void OnDestroy()
    {
        if (subscribeCoroutine != null) StopCoroutine(subscribeCoroutine);

        if (stateController != null && subscribed)
        {
            stateController.OnMissionStarted -= HandleMissionStarted;
            stateController.OnMissionCompleted -= HandleMissionCompleted;
            stateController.OnMissionFailed -= HandleMissionFailed;
            stateController.OnMissionReset -= HandleMissionReset;
            stateController.OnScanProgressUpdated -= HandleScanProgressUpdate;
        }

        if (Instance == this) Instance = null;
    }

    private void HandleMissionStarted(int missionIndex)
    {
        StopAllCoroutines();
        ClosePopup();
    }

    private void HandleMissionCompleted(int completedIndex)
    {
        StopAllCoroutines();
        ClosePopup();
        StartCoroutine(ShowCompletionThenNext(completedIndex));
    }

    private IEnumerator ShowCompletionThenNext(int completedIndex)
    {
        yield return new WaitForSeconds(missionCompleteDelay);

        if (stateController == null || stateController.missions == null) yield break;

        int nextIndex = completedIndex + 1;
        bool allMissionsCompleted = nextIndex >= stateController.missions.Count;

        if (allMissionsCompleted)
        {
            ShowPopup(
                "🎉 ALLE MISSIES VOLTOOID!",
                "Geweldig werk! Je hebt de EVIL AI verslagen en ASML beschermd!",
                null,
                "Afsluiten!"
            );

            yield break;
        }

        MissionStateController.Mission nextMission = stateController.missions[nextIndex];
        MissionStateController.MissionTarget firstTarget = GetFirstTarget(nextMission);

        ShowPopup(
            $"✅ Missie voltooid! Volgende: {nextMission.missionName}",
            firstTarget != null ? firstTarget.description : nextMission.missionName,
            firstTarget != null ? firstTarget.targetIcon : null,
            "Doorgaan"
        );
    }

    private void HandleMissionFailed(int missionIndex)
    {
        StopAllCoroutines();
        ClosePopup();

        ShowPopup(
            "❌ MISSIE MISLUKT",
            "De bezorgtijd is verstreken. De missie wordt automatisch opnieuw gestart.",
            null,
            "Doorgaan"
        );
    }

    private void HandleMissionReset()
    {
        StopAllCoroutines();
        ClosePopup();
    }

    private void HandleScanProgressUpdate(float progressPercent)
    {
        if (visualPanel == null || !visualPanel.activeSelf) return;
        if (stateController == null || !stateController.isScanning) return;

        int roundedProgress = Mathf.RoundToInt(progressPercent);

        if (textDescription != null)
            textDescription.text = $"{originalDescription}\n\n🛰️ Progressie: {roundedProgress}%";

        if (buttonText != null)
            buttonText.text = $"Scannen... {roundedProgress}%";

        if (actionButton != null) actionButton.interactable = false;
    }

    public void ShowPopup(
        string title,
        string description,
        Sprite iconSprite,
        string buttonLabel,
        Action onConfirmCallback = null)
    {
        pendingCallback = onConfirmCallback;
        originalDescription = description ?? "";

        if (textTitle != null) textTitle.text = title;
        if (textDescription != null) textDescription.text = description;
        if (buttonText != null) buttonText.text = buttonLabel;
        if (actionButton != null) actionButton.interactable = true;

        if (missionIcon != null)
        {
            bool hasIcon = iconSprite != null;

            missionIcon.gameObject.SetActive(hasIcon);

            if (hasIcon) missionIcon.sprite = iconSprite;
        }

        if (visualPanel != null) visualPanel.SetActive(true);
    }

    public void ClosePopup()
    {
        if (visualPanel != null) visualPanel.SetActive(false);
        if (actionButton != null) actionButton.interactable = true;

        pendingCallback = null;
        originalDescription = "";
    }

    public void OnActionButtonClick()
    {
        if (actionButton != null && !actionButton.interactable) return;

        Action callback = pendingCallback;

        if (callback == null)
        {
            ClosePopup();
            return;
        }

        pendingCallback = null;
        callback.Invoke();

        if (stateController != null && stateController.isScanning)
        {
            if (visualPanel != null) visualPanel.SetActive(true);
            if (actionButton != null) actionButton.interactable = false;
            if (buttonText != null) buttonText.text = "Scannen... 0%";
            return;
        }

        ClosePopup();
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
}