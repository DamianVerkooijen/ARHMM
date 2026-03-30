using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems; // Required for TrackingState
using TMPro; // Using TextMeshPro as established

public class HelicopterManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ARTrackedImageManager imageManager;
    [SerializeField] private GameObject helicopter; // The one already in your Hierarchy
    [SerializeField] private TMP_Text statusText;

    [Header("Settings")]
    private string targetMarker = "BottomLeft";
    private bool hasSpawned = false;

    private void OnEnable()
    {
        if (imageManager != null)
            imageManager.trackablesChanged.AddListener(OnTrackablesChanged);
    }

    private void OnDisable()
    {
        if (imageManager != null)
            imageManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
    }

    private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
    foreach (var image in eventArgs.added)
        UpdateHelicopterState(image);

    foreach (var image in eventArgs.updated)
        UpdateHelicopterState(image);

    // FIX: Add '.Value' to the removed loop
    foreach (var image in eventArgs.removed)
        UpdateHelicopterState(image.Value); 
    }

    private void UpdateHelicopterState(ARTrackedImage image)
    {
        if (image == null || image.referenceImage == null) return;

        if (image.referenceImage.name != targetMarker)
            return;

        // Only move the helicopter if we haven't 'locked' it to the marker yet
        if (image.trackingState == TrackingState.Tracking && !hasSpawned)
        {
            // 1. Activate
            helicopter.SetActive(true);

            // 2. Force Global Position (World Space)
            // This ignores any parenting and puts it exactly where the marker is in the room
            helicopter.transform.SetPositionAndRotation(image.transform.position, image.transform.rotation);

            // 3. Lock it
            hasSpawned = true;

            UpdateStatusUI("<color=#00FF00>Heli Anchored to World!</color>");
        }
    }

    private void UpdateStatusUI(string message)
    {
        if (statusText != null)
            statusText.text = $"<b>Heli Status:</b> {message}";
    }

    public void ResetHeli()
    {
    hasSpawned = false;
    helicopter.SetActive(false);
    UpdateStatusUI("System Reset. Looking for marker...");
    }
}