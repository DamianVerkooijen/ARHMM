using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

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
    private bool subscribed = false;
    private string originalDescription = "";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (actionButton != null)
        {
            buttonText = actionButton.GetComponentInChildren<TMP_Text>();
        }

        if (visualPanel != null) visualPanel.SetActive(false);
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void TrySubscribe()
    {
        if (subscribed) return;

        if (stateController == null)
            stateController = FindFirstObjectByType<MissionStateController>();

        if (stateController != null)
        {
            stateController.OnMissionCompleted += HandleMissionCompleted;
            stateController.OnMissionReset += HandleMissionReset;
            stateController.OnScanProgressUpdated += HandleScanProgressUpdate;
            subscribed = true;
        }
        else
        {
            StartCoroutine(RetrySubscribe());
        }
    }

    private IEnumerator RetrySubscribe()
    {
        while (!subscribed)
        {
            yield return null;
            TrySubscribe();
        }
    }

    private void OnDestroy()
    {
        if (stateController != null)
        {
            stateController.OnMissionCompleted -= HandleMissionCompleted;
            stateController.OnMissionReset -= HandleMissionReset;
            stateController.OnScanProgressUpdated -= HandleScanProgressUpdate;
        }
    }

    private void HandleScanProgressUpdate(float progressPercent)
    {
        if (visualPanel != null && visualPanel.activeSelf && stateController != null && stateController.isScanning)
        {
            if (textDescription != null)
                textDescription.text = $"{originalDescription}\n\n🛰️ Progressie: {progressPercent}%";

            if (buttonText != null)
                buttonText.text = $"Scannen... {progressPercent}%";

            if (actionButton != null) actionButton.interactable = false;
        }
    }

    private void HandleMissionCompleted(int completedIndex)
    {
        ClosePopup();
        StartCoroutine(ShowCompletionThenNext(completedIndex));
    }

    private IEnumerator ShowCompletionThenNext(int completedIndex)
    {
        if (visualPanel != null) visualPanel.SetActive(false);
        yield return new WaitForSeconds(missionCompleteDelay);

        // Show completion popup for the completed mission
        if (stateController != null && completedIndex >= 0 && completedIndex < stateController.missions.Count)
        {
            var completedMission = stateController.missions[completedIndex];
            if (completedMission.missionCompletionPopup != null)
            {
                ShowPopup(
                    completedMission.missionCompletionPopup.title,
                    completedMission.missionCompletionPopup.description,
                    completedMission.missionCompletionPopup.icon,
                    completedMission.missionCompletionPopup.actionButtonText,
                    () => StartCoroutine(ShowNextMissionAfterDelay())
                );
                yield break;
            }
        }

        // Fallback if no completion popup defined
        ShowNextMissionOrGameEnd();
    }

    private IEnumerator ShowNextMissionAfterDelay()
    {
        yield return new WaitForSeconds(missionCompleteDelay);
        ShowNextMissionOrGameEnd();
    }

    private void ShowNextMissionOrGameEnd()
    {
        int nextIndex = -1;
        if (stateController != null)
        {
            for (int i = 0; i < stateController.missions.Count; i++)
            {
                if (!stateController.missions[i].isCompleted) { nextIndex = i; break; }
            }
        }

        if (nextIndex == -1)
        {
            ShowPopup("🎉 ALLE MISSIES VOLTOOID!",
                      "Geweldig werk! Je hebt de EVIL AI verslagen en ASML beschermd!",
                      null, "Afsluiten!");
        }
        else
        {
            var next = stateController.missions[nextIndex];
            if (next.missionIntroPopup != null)
            {
                ShowPopup(
                    next.missionIntroPopup.title,
                    next.missionIntroPopup.description,
                    next.missionIntroPopup.icon,
                    next.missionIntroPopup.actionButtonText,
                    () => stateController.StartMission(nextIndex)
                );
            }
            else
            {
                var first = GetFirstTarget(next);
                int captured = nextIndex;

                ShowPopup(
                    $"✅ Missie voltooid! Volgende: {next.missionName}",
                    first != null ? first.description : next.missionName,
                    first != null ? first.targetIcon : null,
                    "Start volgende missie!",
                    () => stateController.StartMission(captured)
                );
            }
        }
    }

    private void HandleMissionReset()
    {
        StopAllCoroutines();
        ClosePopup();
        subscribed = false;
        TrySubscribe();
    }

    public void ShowPopup(string title, string description, Sprite iconSprite,
                          string buttonLabel, Action onConfirmCallback = null)
    {
        pendingCallback = onConfirmCallback;
        originalDescription = description;

        if (textTitle != null) textTitle.text = title;
        if (textDescription != null) textDescription.text = description;
        if (buttonText != null) buttonText.text = buttonLabel;

        if (actionButton != null) actionButton.interactable = true;

        if (missionIcon != null)
        {
            bool has = iconSprite != null;
            missionIcon.gameObject.SetActive(has);
            if (has) missionIcon.sprite = iconSprite;
        }

        if (visualPanel != null) visualPanel.SetActive(true);
    }

    public void ClosePopup()
    {
        if (visualPanel != null) visualPanel.SetActive(false);
        pendingCallback = null;
    }

    // VERANDERD NAAR PUBLIC: Nu bereikbaar via de Unity Inspector koppeling!
    public void OnActionButtonClick()
    {
        Action cb = pendingCallback;

        if (stateController == null || !stateController.isScanning)
        {
            if (visualPanel != null) visualPanel.SetActive(false);
            pendingCallback = null;
        }

        cb?.Invoke();
    }

    private MissionStateController.MissionTarget GetFirstTarget(MissionStateController.Mission m)
    {
        switch (m.missionType)
        {
            case MissionStateController.MissionType.Delivery: return m.startLocation;
            case MissionStateController.MissionType.SearchFind: return (m.searchTargets != null && m.searchTargets.Count > 0) ? m.searchTargets[0] : null;
            case MissionStateController.MissionType.Scan: return (m.scanTargets != null && m.scanTargets.Count > 0) ? m.scanTargets[0] : null;
        }
        return null;
    }
}