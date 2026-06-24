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
    [SerializeField] private float betweenPopupDelay = 0.3f;
    [SerializeField] private float missionCompleteDelay = 1f;

    private TMP_Text buttonText;
    private Action pendingCallback;
    private bool subscribed = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (actionButton != null)
        {
            actionButton.onClick.AddListener(OnActionButtonClick);
            buttonText = actionButton.GetComponentInChildren<TMP_Text>();
        }

        if (visualPanel != null) visualPanel.SetActive(false);
    }

    private void Start()
    {
        TrySubscribe();
    }

    /// <summary>
    /// Probeert te subscriben. Lukt het niet direct, dan via coroutine retry.
    /// </summary>
    private void TrySubscribe()
    {
        if (subscribed) return;

        if (stateController == null)
            stateController = FindFirstObjectByType<MissionStateController>();

        if (stateController != null)
        {
            stateController.OnMissionStarted += HandleMissionStarted;
            stateController.OnStepCompleted += HandleStepCompleted;
            stateController.OnMissionCompleted += HandleMissionCompleted;
            stateController.OnMissionReset += HandleMissionReset;
            subscribed = true;
            Debug.Log("[PopupManager] Gesubscribed op MissionStateController.");
        }
        else
        {
            Debug.LogWarning("[PopupManager] MissionStateController nog niet gevonden, retry volgende frame...");
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
            stateController.OnMissionStarted -= HandleMissionStarted;
            stateController.OnStepCompleted -= HandleStepCompleted;
            stateController.OnMissionCompleted -= HandleMissionCompleted;
            stateController.OnMissionReset -= HandleMissionReset;
        }
        if (actionButton != null)
            actionButton.onClick.RemoveListener(OnActionButtonClick);
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void HandleMissionStarted(int index)
    {
        StartCoroutine(ShowAfterDelay(() =>
        {
            if (stateController == null || index >= stateController.missions.Count) return;
            var mission = stateController.missions[index];
            var firstTarget = GetFirstTarget(mission);

            ShowPopup(
                $"MISSIE: {mission.missionName}",
                firstTarget != null ? firstTarget.description : mission.missionName,
                firstTarget != null ? firstTarget.targetIcon : null,
                "Begrepen!"
            );
        }));
    }

    private void HandleStepCompleted()
    {
        StartCoroutine(ShowAfterDelay(() =>
        {
            if (stateController == null || stateController.selectedMissionIndex == -1) return;
            var mission = stateController.missions[stateController.selectedMissionIndex];
            var nextTarget = GetCurrentTarget(mission);
            if (nextTarget == null) return;

            ShowPopup(
                $"{mission.missionName} — Volgende stap",
                nextTarget.description,
                nextTarget.targetIcon,
                "Volgende!"
            );
        }));
    }

    private void HandleMissionCompleted(int completedIndex)
    {
        StartCoroutine(ShowCompletionThenNext());
    }

    private IEnumerator ShowCompletionThenNext()
    {
        if (visualPanel != null) visualPanel.SetActive(false);
        yield return new WaitForSeconds(missionCompleteDelay);

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

    private void HandleMissionReset()
    {
        StopAllCoroutines();
        ClosePopup();
        // Na reset opnieuw subscriben als dat nodig is
        subscribed = false;
        TrySubscribe();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Sluit de huidige popup als die open is, wacht de delay, dan voer actie uit.
    /// </summary>
    private IEnumerator ShowAfterDelay(Action show)
    {
        if (visualPanel != null && visualPanel.activeSelf)
        {
            visualPanel.SetActive(false);
            yield return new WaitForSeconds(betweenPopupDelay);
        }
        show?.Invoke();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void ShowPopup(string title, string description, Sprite iconSprite,
                          string buttonLabel, Action onConfirmCallback = null)
    {
        pendingCallback = onConfirmCallback;

        if (textTitle != null) textTitle.text = title;
        if (textDescription != null) textDescription.text = description;
        if (buttonText != null) buttonText.text = buttonLabel;

        if (missionIcon != null)
        {
            bool has = iconSprite != null;
            missionIcon.gameObject.SetActive(has);
            if (has) missionIcon.sprite = iconSprite;
        }

        if (visualPanel != null) visualPanel.SetActive(true);
        Debug.Log($"[PopupManager] ShowPopup: '{title}'");
    }

    public void ClosePopup()
    {
        if (visualPanel != null) visualPanel.SetActive(false);
        pendingCallback = null;
    }

    // ── Button handler ────────────────────────────────────────────────────────

    private void OnActionButtonClick()
    {
        Debug.Log("[PopupManager] Popup knop geklikt.");
        if (visualPanel != null) visualPanel.SetActive(false);

        Action cb = pendingCallback;
        pendingCallback = null;
        cb?.Invoke();
    }

    // ── Target helpers ────────────────────────────────────────────────────────

    private MissionStateController.MissionTarget GetFirstTarget(MissionStateController.Mission m)
    {
        switch (m.missionType)
        {
            case MissionStateController.MissionType.Delivery:
                return m.startLocation;
            case MissionStateController.MissionType.SearchFind:
                return (m.searchTargets != null && m.searchTargets.Count > 0) ? m.searchTargets[0] : null;
            case MissionStateController.MissionType.Scan:
                return (m.scanTargets != null && m.scanTargets.Count > 0) ? m.scanTargets[0] : null;
        }
        return null;
    }

    private MissionStateController.MissionTarget GetCurrentTarget(MissionStateController.Mission m)
    {
        int idx = stateController.currentTargetIndex;
        switch (m.missionType)
        {
            case MissionStateController.MissionType.Delivery:
                return stateController.missionActive ? m.endLocation : m.startLocation;
            case MissionStateController.MissionType.SearchFind:
                return (m.searchTargets != null && idx < m.searchTargets.Count) ? m.searchTargets[idx] : null;
            case MissionStateController.MissionType.Scan:
                return (m.scanTargets != null && idx < m.scanTargets.Count) ? m.scanTargets[idx] : null;
        }
        return null;
    }
}