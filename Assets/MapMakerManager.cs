using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;

public class MapController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ARTrackedImageManager imageManager;
    [SerializeField] private GameObject mapPrefab;
    [SerializeField] private TMP_Text statusText;

    [Header("Settings")]
    [Range(0.01f, 1f)]
    [SerializeField] private float smoothSpeed = 0.1f;

    private Dictionary<string, Vector3> markerPositions = new Dictionary<string, Vector3>();
    private GameObject spawnedMap;
    private string[] requiredMarkers = { "TopLeft", "TopRight", "BottomLeft", "BottomRight" };

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
        foreach (var image in eventArgs.added) UpdateMarkerData(image);
        foreach (var image in eventArgs.updated) UpdateMarkerData(image);

        UpdateStatusUI();
    }

    private void UpdateMarkerData(ARTrackedImage image)
    {
        if (image.trackingState == TrackingState.Tracking)
        {
            markerPositions[image.referenceImage.name] = image.transform.position;

            if (markerPositions.Count == 4)
            {
                HandleMapPlacement();
            }
        }
    }

    private void UpdateStatusUI()
    {
        if (statusText == null) return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>Marker Status:</b>");

        foreach (string markerName in requiredMarkers)
        {
            bool found = markerPositions.ContainsKey(markerName);
            string color = found ? "#00FF00" : "#FF0000";
            sb.AppendLine($"<color={color}>{markerName}: {(found ? "OK" : "Searching...")}</color>");
        }

        if (markerPositions.Count == 4) sb.AppendLine("\n<color=#00FFFF>Map Anchored!</color>");

        statusText.text = sb.ToString();
    }

    private void HandleMapPlacement()
    {
        Vector3 center = Vector3.zero;
        foreach (var pos in markerPositions.Values) center += pos;
        center /= 4;

        float currentDistance = Vector3.Distance(markerPositions["TopLeft"], markerPositions["BottomRight"]);
        Vector3 targetScale = new Vector3(currentDistance, currentDistance, currentDistance);

        if (spawnedMap == null)
        {
            spawnedMap = Instantiate(mapPrefab, center, Quaternion.identity);
        }
        else
        {
            spawnedMap.transform.position = Vector3.Lerp(spawnedMap.transform.position, center, smoothSpeed);
            spawnedMap.transform.localScale = Vector3.Lerp(spawnedMap.transform.localScale, targetScale, smoothSpeed);

            Vector3 forwardDir = (markerPositions["TopLeft"] - markerPositions["BottomLeft"]).normalized;
            if (forwardDir != Vector3.zero)
            {
                spawnedMap.transform.forward = Vector3.Lerp(spawnedMap.transform.forward, forwardDir, smoothSpeed);
            }
        }
    }
}