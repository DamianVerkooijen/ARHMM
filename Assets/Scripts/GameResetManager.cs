using UnityEngine;
using UnityEngine.SceneManagement;

public class GameResetManager : MonoBehaviour
{
    [Header("Manager References")]
    [SerializeField] private ARTrackingManager arTrackingManager;
    [SerializeField] private HelicopterManager helicopterManager;
    [SerializeField] private MarkerManager markerManager;
    [SerializeField] private MissionStateController stateController;

    /// <summary>
    /// Hard reset: reloads the complete scene.
    /// AR tracking, UI and mission progress are restarted.
    /// </summary>
    public void FullAppReset()
    {
        Debug.Log("Hard Reset triggered: Reloading scene...");

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().name
        );
    }

    /// <summary>
    /// Soft reset: keeps AR calibration but resets gameplay progress.
    /// The introduction screen is shown again.
    /// </summary>
    public void GameResetOnly()
    {
        if (arTrackingManager != null && !arTrackingManager.IsCalibrated)
        {
            Debug.LogWarning(
                "Cannot reset game progress: AR is not calibrated yet!"
            );

            return;
        }

        if (helicopterManager != null)
            helicopterManager.ResetHelicopterPosition();

        if (markerManager != null)
            markerManager.ClearAllActiveMarkers();

        if (stateController != null)
            stateController.ResetAllMissionsToStart();

        Debug.Log(
            "Game Progress Reset executed. AR calibration preserved."
        );
    }
}