using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;

public class HelicopterManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ARTrackedImageManager imageManager;
    [SerializeField] private GameObject helicopter;
    [SerializeField] private TMP_Text statusText;

    [Header("Settings")]
    private readonly string[] requiredMarkers = { "TopLeft", "TopRight", "BottomLeft", "BottomRight" };
    private Dictionary<string, ARTrackedImage> trackedMarkers = new Dictionary<string, ARTrackedImage>();
    
    // We store the offsets so we know where the center is relative to each marker
    private Dictionary<string, Vector3> markerOffsets = new Dictionary<string, Vector3>();
    private bool hasSpawned = false;
    private GameObject masterAnchor;

    private void OnEnable() => imageManager.trackablesChanged.AddListener(OnTrackablesChanged);
    private void OnDisable() => imageManager.trackablesChanged.RemoveListener(OnTrackablesChanged);

    private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        foreach (var img in eventArgs.added) UpdateMarker(img);
        foreach (var img in eventArgs.updated) UpdateMarker(img);
        foreach (var img in eventArgs.removed) 
        {
            if (trackedMarkers.ContainsKey(img.Value.referenceImage.name))
                trackedMarkers.Remove(img.Value.referenceImage.name);
        }

        // 1. INITIAL SPAWN: Needs all 4
        if (!hasSpawned && trackedMarkers.Count == 4)
        {
            InitialCalibration();
        }

        // 2. CONTINUOUS TRACKING: Only needs 1
        if (hasSpawned && trackedMarkers.Count > 0)
        {
            UpdateWorldAnchor();
        }

        UpdateStatusUI();
    }

    private void UpdateMarker(ARTrackedImage image)
    {
        string name = image.referenceImage.name;
        if (System.Array.Exists(requiredMarkers, m => m == name))
        {
            if (image.trackingState == TrackingState.Tracking)
                trackedMarkers[name] = image;
            else
                trackedMarkers.Remove(name);
        }
    }

    private void InitialCalibration()
    {
        // Calculate the initial center
        Vector3 sumPos = Vector3.zero;
        foreach (var m in trackedMarkers.Values) sumPos += m.transform.position;
        Vector3 centerPos = sumPos / 4f;

        // Create an invisible anchor object at the center
        masterAnchor = new GameObject("Heli_World_Anchor");
        masterAnchor.transform.position = centerPos;
        masterAnchor.transform.rotation = Quaternion.identity;

        // Save how far each marker is from the center (the "blueprint")
        foreach (var kvp in trackedMarkers)
        {
            markerOffsets[kvp.Key] = masterAnchor.transform.position - kvp.Value.transform.position;
        }

        // Place the helicopter at the anchor and make it a CHILD
        helicopter.SetActive(true);
        helicopter.transform.position = centerPos;
        helicopter.transform.SetParent(masterAnchor.transform);

        hasSpawned = true;
    }

    private void UpdateWorldAnchor()
    {
        // Find the first marker we can see
        foreach (var kvp in trackedMarkers)
        {
            if (markerOffsets.ContainsKey(kvp.Key))
            {
                // Move the Master Anchor to its correct spot relative to this single marker
                masterAnchor.transform.position = kvp.Value.transform.position + markerOffsets[kvp.Key];
                
                // Once we update from ONE marker, we stop (to avoid jitter from multiple markers)
                break;
            }
        }
    }

    private void UpdateStatusUI(string message = null)
    {
        if (statusText == null) return;
        if (!string.IsNullOrEmpty(message)) { statusText.text = message; return; }

        if (!hasSpawned)
            statusText.text = $"Calibration: {trackedMarkers.Count}/4 Markers Found";
        else
            statusText.text = $"Tracking Active (Visible: {trackedMarkers.Count})";
    }

    public void ResetHeli()
    {
        hasSpawned = false;
        if (masterAnchor != null) Destroy(masterAnchor);
        helicopter.SetActive(false);
        trackedMarkers.Clear();
        markerOffsets.Clear();
    }
}