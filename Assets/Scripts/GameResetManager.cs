using UnityEngine;
using UnityEngine.SceneManagement; // <-- REQUIRED FOR RELOADING

public class GameResetManager : MonoBehaviour
{
    [Header("Manager References (Soft Reset Only)")]
    [SerializeField] private ARTrackingManager arTrackingManager;
    [SerializeField] private HelicopterManager helicopterManager;
    [SerializeField] private MarkerManager markerManager;
    [SerializeField] private MissionStateController stateController;

    /// <summary>
    /// BUTTON 1: Hard Reset. Reloads the entire scene from scratch.
    /// Perfectly mimics opening the app for the very first time.
    /// </summary>
    public void FullAppReset()
    {
        Debug.Log("Hard Reset triggered: Reloading scene...");
        
        // This single line wipes AR memory, UI states, and resets everything perfectly.
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// BUTTON 2: Soft Reset. Keeps AR tracking perfectly intact. Just restarts the gameplay loop.
    /// </summary>
    public void GameResetOnly()
    {
        if (arTrackingManager != null && !arTrackingManager.IsCalibrated)
        {
            Debug.LogWarning("Cannot reset game progress: AR is not calibrated yet!");
            return;
        }

        // 1. Snap the helicopter back to the exact center of your anchor and clear physics
        if (helicopterManager != null)
        {
            helicopterManager.ResetHelicopterPosition();
        }

        // 2. Clear out old rings/waypoints safely
        if (markerManager != null)
        {
            markerManager.ClearAllActiveMarkers();
        }

        // 3. Reset the mission logs and re-instantiate the first mission
        if (stateController != null)
        {
            stateController.ResetAllMissionsToStart();
            
            if (markerManager != null)
            {
                markerManager.SpawnWorldMarkers(stateController.missions);
                markerManager.EvaluateMarkerVisualPlacement();
            }
        }

        Debug.Log("Game Progress Reset executed. AR calibration preserved.");
    }
}