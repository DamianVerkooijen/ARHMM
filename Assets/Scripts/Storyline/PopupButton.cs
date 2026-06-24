using UnityEngine;

/// <summary>
/// Zet dit script op de gameplay-knop (MissionButton of ActionButton in je HUD).
/// Sleep de MissionController erin via de Inspector.
/// De knop roept dan OnActionButtonPressed() aan.
/// </summary>
public class PopupButton : MonoBehaviour
{
    [SerializeField] private MissionController missionController;

    private void Awake()
    {
        if (missionController == null)
            missionController = FindFirstObjectByType<MissionController>();
    }

    /// <summary>
    /// Koppel deze methode aan de OnClick() van je HUD-knop in de Inspector.
    /// </summary>
    public void OnButtonPressed()
    {
        if (missionController != null)
            missionController.OnActionButtonPressed();
    }
}