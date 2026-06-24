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

    private CanvasGroup introCanvasGroup;

    private void Awake()
    {
        if (introPanel != null)
        {
            introCanvasGroup = introPanel.GetComponent<CanvasGroup>();
            if (introCanvasGroup == null)
                introCanvasGroup = introPanel.AddComponent<CanvasGroup>();
        }

        if (startButton != null)
            startButton.onClick.AddListener(OnStartButtonPressed);

<<<<<<< Updated upstream
        // Zet de startposities direct in Awake keihard goed
        if (variableJoystickL != null) variableJoystickL.SetActive(true);
=======
        InitialSetupState();
    }

    private void InitialSetupState()
    {
        // Joysticks uit bij start
        if (variableJoystickL != null) variableJoystickL.SetActive(false);
>>>>>>> Stashed changes
        if (variableJoystickR != null) variableJoystickR.SetActive(false);

        // Intro paneel aan
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
        this.enabled = true;
        StopAllCoroutines();
        InitialSetupState();
    }

    private void OnStartButtonPressed()
    {
        StartCoroutine(FadeOutAndStartGame());
    }

    private IEnumerator FadeOutAndStartGame()
    {
<<<<<<< Updated upstream
        // 1. Verhuis de linker joystick nu definitief naar de hoofd-UI zodat hij ALTIJD blijft staan
        if (variableJoystickL != null && introPanel != null)
        {
            if (variableJoystickL.transform.IsChildOf(introPanel.transform))
            {
                variableJoystickL.transform.SetParent(introPanel.transform.parent, true);
            }

            // Dwing alle alphas op de linker joystick naar 1 (voorkomt onzichtbaarheid)
            CanvasGroup[] allJLCGs = variableJoystickL.GetComponentsInChildren<CanvasGroup>(true);
            foreach (var cg in allJLCGs) cg.alpha = 1f;
        }
=======
        if (startButton != null) startButton.interactable = false;
>>>>>>> Stashed changes

        // 1. Fade het intro paneel uit
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            if (introCanvasGroup != null)
                introCanvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / fadeDuration));
            yield return null;
        }

<<<<<<< Updated upstream
        // 3. Fade het intro paneel netjes uit
=======
        // FIX: Instead of killing the whole GameObject here, turn off interaction & visibility
>>>>>>> Stashed changes
        if (introCanvasGroup != null)
        {
            introCanvasGroup.alpha = 0f;
            introCanvasGroup.interactable = false;
            introCanvasGroup.blocksRaycasts = false;
        }

        // 3. Joysticks aan — speler kan nu al vliegen
        if (variableJoystickL != null) variableJoystickL.SetActive(true);
        if (variableJoystickR != null) variableJoystickR.SetActive(true);
        Debug.Log("[INTRO] Joysticks aan. 5 seconden vrij vliegen...");

        // 4. 5 seconden vrij vliegen (This will now successfully run!)
        yield return new WaitForSeconds(5f);

        // 5. Waarschuwingspopup via PopupManager
        Debug.Log("[INTRO] Popup triggeren.");
        if (PopupManager.Instance == null)
        {
            Debug.LogError("[INTRO] PopupManager.Instance is null! Staat er een PopupManager in de scene?");
            // Clean up panel before breaking
            if (introPanel != null) introPanel.SetActive(false);
            this.enabled = false;
            yield break;
        }

        MissionStateController stateController = FindFirstObjectByType<MissionStateController>();
        if (stateController == null || stateController.missions.Count == 0)
        {
            Debug.LogError("[INTRO] Geen MissionStateController of missies gevonden!");
            if (introPanel != null) introPanel.SetActive(false);
            this.enabled = false;
            yield break;
        }

        var firstMission = stateController.missions[0];
        Sprite icon = firstMission.startLocation?.targetIcon;

        PopupManager.Instance.ShowPopup(
            "⚠️ WAARSCHUWING!",
            "De EVIL AI stuurt zijn handlangers op pad naar ASML om de chips te bemachtigen! " +
            "Laat dit niet gebeuren! We hebben deze chips hard nodig om de EVIL AI uit te schakelen!",
            icon,
            "Start Missie!",
            () => stateController.StartMission(0)
        );

        // FINALLY safe to completely turn off the GameObject now that our work is done
        if (introPanel != null) introPanel.SetActive(false);
        this.enabled = false;
    }

    private void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(OnStartButtonPressed);
<<<<<<< Updated upstream
        }
=======
>>>>>>> Stashed changes
    }
}