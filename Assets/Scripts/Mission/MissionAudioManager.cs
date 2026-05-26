using UnityEngine;

public class MissionAudioManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private MissionStateController stateController;
    [SerializeField] private MissionController missionController;

    [Header("Audio Sources")]
    [Tooltip("Voor korte effecten zoals clicks, sweeps en panelen")]
    [SerializeField] private AudioSource sfxSource;
    [Tooltip("Voor continu herhalende geluiden zoals de motor")]
    [SerializeField] private AudioSource loopSource;

    [Header("Movement References")]
    [SerializeField] private HelicopterMovement helicopterMovement;

    [Header("Audio Clips - Ambient")]
    public AudioClip hoverSound;

    [Header("Audio Clips - UI & Expansion")]
    public AudioClip joystickClickSound;
    public AudioClip topPanelExpandSound;
    public AudioClip expansionSideSound;
    public AudioClip actionButtonClickSound;

    [Header("Audio Clips - Missions")]
    public AudioClip missionFinishedSound;

    [Range(0f, 1f)] public float joystickVolume = 0.5f;

    private bool wasExtensionOpen = false;
    private bool engineStarted = false;

    public void Initialize(MissionStateController stateCtrl, MissionController missionCtrl)
    {
        stateController = stateCtrl;
        missionController = missionCtrl;

        // Subscribe to the exact Action definitions from your MissionStateController
        stateController.OnMissionStarted += HandleMissionStarted;
        stateController.OnMissionCompleted += HandleMissionCompleted;
        stateController.OnProximityChanged += HandleProximityChanged;
        stateController.OnMissionReset += HandleMissionReset;
    }

    private void OnDestroy()
    {
        if (stateController == null) return;
        stateController.OnMissionStarted -= HandleMissionStarted;
        stateController.OnMissionCompleted -= HandleMissionCompleted;
        stateController.OnProximityChanged -= HandleProximityChanged;
        stateController.OnMissionReset -= HandleMissionReset;
    }

    private void Update()
    {
        bool isHeliActive = missionController != null &&
                            missionController.manager != null &&
                            missionController.manager.helicopter != null &&
                            missionController.manager.helicopter.activeInHierarchy;

        if (isHeliActive)
        {
            if (!engineStarted)
            {
                StartEngineLoop();
            }

            // Zoek dynamisch de HelicopterMovement component als we die nog niet hebben
            if (helicopterMovement == null)
            {
                helicopterMovement = missionController.manager.helicopter.GetComponent<HelicopterMovement>();
            }

            // Dynamische pitch/volume regeling op basis van beweging
            if (helicopterMovement != null && loopSource != null)
            {
                // Controleren of de joysticks worden bewogen
                bool isMoving = Mathf.Abs(helicopterMovement.leftJoystick.Horizontal) > 0.05f ||
                                Mathf.Abs(helicopterMovement.leftJoystick.Vertical) > 0.05f ||
                                Mathf.Abs(helicopterMovement.rightJoystick.Horizontal) > 0.05f;

                if (isMoving)
                {
                    // Heli vliegt: Maak het geluid iets voller of hoger (Pitch naar 1.15)
                    loopSource.pitch = Mathf.Lerp(loopSource.pitch, 1.15f, Time.deltaTime * 3f);
                }
                else
                {
                    // IDLE = HOVER: Geluid zakt terug naar normale rustige hover (Pitch naar 1.0)
                    loopSource.pitch = Mathf.Lerp(loopSource.pitch, 1.0f, Time.deltaTime * 3f);
                }
            }
        }
        else if (engineStarted)
        {
            StopEngineLoop();
        }
    }

    private void StartEngineLoop()
    {
        if (loopSource != null && hoverSound != null)
        {
            loopSource.clip = hoverSound;
            loopSource.loop = true;
            loopSource.Play();
            engineStarted = true;
        }
    }

    private void StopEngineLoop()
    {
        if (loopSource != null)
        {
            loopSource.Stop();
        }
        engineStarted = false;
    }

    // Listens to the starting of a new mission
    private void HandleMissionStarted(int index)
    {
        PlaySFX(topPanelExpandSound);
    }

    // Listens to the interaction range changes
    private void HandleProximityChanged(bool isInRange, string actionText, Sprite actionIcon, string statusLabel)
    {
        // Avoids that the sound plays every single frame
        if (isInRange && !wasExtensionOpen)
        {
            PlaySFX(expansionSideSound);
            wasExtensionOpen = true;
        }
        else if (!isInRange)
        {
            wasExtensionOpen = false;
        }
    }

    // Listens to finishing of mission
    private void HandleMissionCompleted(int index)
    {
        PlaySFX(missionFinishedSound);
    }

    private void HandleMissionReset()
    {
        wasExtensionOpen = false;
        StopEngineLoop();
    }

    // Gets triggered from MissionController.OnActionButtonPressed
    public void PlayClickSound()
    {
        PlaySFX(actionButtonClickSound);
    }

    public void PlayJoystickTouchSound()
    {
        if (sfxSource != null && joystickClickSound != null)
        {
            sfxSource.PlayOneShot(joystickClickSound, joystickVolume);
        }
    }

    private void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }
}