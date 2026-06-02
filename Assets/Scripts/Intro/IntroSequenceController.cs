using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class IntroSequenceController : MonoBehaviour
{
    [Header("Joysticks")]
    public GameObject variableJoystickL;
    public GameObject variableJoystickR;

    [Header("Intro UI Elements")]
    [Tooltip("Het volledige paneel/canvas van de intro (de box met tekst en knop)")]
    public GameObject introPanel;
    [Tooltip("De Button waar de speler op klikt om te starten")]
    public Button startButton;

    [Header("Settings")]
    [Tooltip("Fade-out duur zodra je op start klikt")]
    public float fadeDuration = 0.35f;

    private CanvasGroup introCanvasGroup;

    private void Awake()
    {
        // Garandeer dat er een CanvasGroup op het introPanel zit voor een nette fade-out
        if (introPanel != null)
        {
            introCanvasGroup = introPanel.GetComponent<CanvasGroup>();
            if (introCanvasGroup == null) introCanvasGroup = introPanel.AddComponent<CanvasGroup>();
        }

        // Zorg dat de knop luistert naar de klik
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartButtonPressed);
        }

        // Zet de startposities direct in Awake keihard goed
        if (variableJoystickL != null) variableJoystickL.SetActive(false);
        if (variableJoystickR != null) variableJoystickR.SetActive(false);
        if (introPanel != null) introPanel.SetActive(true);
    }

    private void OnStartButtonPressed()
    {
        // Voorkom dubbel klikken
        if (startButton != null) startButton.interactable = false;

        StartCoroutine(StartGameSequence());
    }

    private IEnumerator StartGameSequence()
    {

        // 2. Activeer direct de rechter joystick voor de besturing
        if (variableJoystickR != null)
        {
            variableJoystickR.SetActive(true);
            CanvasGroup[] allJRCGs = variableJoystickR.GetComponentsInChildren<CanvasGroup>(true);
            foreach (var cg in allJRCGs) cg.alpha = 1f;
        }

        if (variableJoystickL != null)
        {
            variableJoystickL.SetActive(true);
            CanvasGroup[] allJLCGs = variableJoystickL.GetComponentsInChildren<CanvasGroup>(true);
            foreach (var cg in allJLCGs) cg.alpha = 1f;
        }

        // 3. Fade het intro paneel netjes uit
        if (introCanvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                introCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                yield return null;
            }
            introCanvasGroup.alpha = 0f;
        }

        // 4. Zet het paneel uit en ruim de controller op
        if (introPanel != null) introPanel.SetActive(false);

        // Schakel dit intro-script uit, we zijn klaar!
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnStartButtonPressed);
        }
    }

    public void ResetIntroSequence()
    {
        // 1. Ensure this controller GameObject is active again so it can run code
        gameObject.SetActive(true);

        // 2. Stop any lingering fade coroutines safely
        StopAllCoroutines();

        // 3. Reset the alpha and button interactability back to pristine states
        if (introCanvasGroup != null) introCanvasGroup.alpha = 1f;
        if (startButton != null) startButton.interactable = true;

        // 4. Reset the panel visibility and joystick setups
        InitialSetupState();
    }

    /// <summary>
    /// Shared state logic to guarantee identical layout on Awake and on Soft Reset.
    /// </summary>
    private void InitialSetupState()
    {
        if (variableJoystickL != null) variableJoystickL.SetActive(false);
        if (variableJoystickR != null) variableJoystickR.SetActive(false);
        if (introPanel != null) introPanel.SetActive(true);
    }
}