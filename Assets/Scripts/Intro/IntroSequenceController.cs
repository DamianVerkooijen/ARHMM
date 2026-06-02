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
        if (variableJoystickL != null) variableJoystickL.SetActive(true);
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

        // 2. Activeer direct de rechter joystick voor de besturing
        if (variableJoystickR != null)
        {
            variableJoystickR.SetActive(true);
            CanvasGroup[] allJRCGs = variableJoystickR.GetComponentsInChildren<CanvasGroup>(true);
            foreach (var cg in allJRCGs) cg.alpha = 1f;
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
}