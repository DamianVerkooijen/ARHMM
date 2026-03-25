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
    [SerializeField] private GameObject helicopterPrefab;
    [SerializeField] private TMP_Text statusText;

    [Header("Settings")]
    [Range(0.01f, 1f)]
    [SerializeField] private float smoothSpeed = 0.1f;

    [SerializeField] private Vector3 helicopterLocalOffset = new Vector3(0f, 0.1f, 0f);

    private readonly Dictionary<string, ARTrackedImage> trackedMarkers = new Dictionary<string, ARTrackedImage>();

    private GameObject spawnedMap;
    private GameObject spawnedHelicopter;
    private GameObject mapAnchorObject;

    private readonly string[] requiredMarkers = { "TopLeft", "TopRight", "BottomLeft", "BottomRight" };

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
            UpdateMarkerData(image);

        foreach (var image in eventArgs.updated)
            UpdateMarkerData(image);

        foreach (var image in eventArgs.removed)
            UpdateMarkerData(image.Value);

        UpdateStatusUI();

        if (HasAllMarkers())
        {
            HandleMapPlacement();
        }
    }

    private void UpdateMarkerData(ARTrackedImage image)
    {
        string markerName = image.referenceImage.name;

        if (!System.Array.Exists(requiredMarkers, m => m == markerName))
            return;

        if (image.trackingState == TrackingState.Tracking)
        {
            trackedMarkers[markerName] = image;
        }
    }

    private void RemoveMarkerData(ARTrackedImage image)
    {
        string markerName = image.referenceImage.name;

        if (trackedMarkers.ContainsKey(markerName))
        {
            trackedMarkers.Remove(markerName);
        }
    }

    private bool HasAllMarkers()
    {
        foreach (string marker in requiredMarkers)
        {
            if (!trackedMarkers.ContainsKey(marker))
                return false;
        }

        return true;
    }

    private void UpdateStatusUI()
    {
        if (statusText == null) return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>Marker Status:</b>");

        foreach (string markerName in requiredMarkers)
        {
            bool found = trackedMarkers.ContainsKey(markerName);
            string color = found ? "#00FF00" : "#FF0000";
            sb.AppendLine($"<color={color}>{markerName}: {(found ? "OK" : "Searching...")}</color>");
        }

        if (HasAllMarkers())
            sb.AppendLine("\n<color=#00FFFF>Map Anchored!</color>");

        statusText.text = sb.ToString();
    }

    private void HandleMapPlacement()
    {
        Vector3 center = GetMapCenter();
        Vector3 targetScale = GetMapScale();
        Quaternion targetRotation = GetMapRotation();

        if (mapAnchorObject == null)
        {
            mapAnchorObject = new GameObject("MapAnchor");
            mapAnchorObject.transform.position = center;
            mapAnchorObject.transform.rotation = targetRotation;
        }
        else
        {
            mapAnchorObject.transform.position = Vector3.Lerp(
                mapAnchorObject.transform.position,
                center,
                smoothSpeed
            );

            mapAnchorObject.transform.rotation = Quaternion.Slerp(
                mapAnchorObject.transform.rotation,
                targetRotation,
                smoothSpeed
            );
        }

        if (spawnedMap == null)
        {
            spawnedMap = Instantiate(mapPrefab, mapAnchorObject.transform);
            spawnedMap.transform.localPosition = Vector3.zero;
            spawnedMap.transform.localRotation = Quaternion.identity;
            spawnedMap.transform.localScale = targetScale;

            SpawnHelicopter();
        }
        else
        {
            spawnedMap.transform.localScale = Vector3.Lerp(
                spawnedMap.transform.localScale,
                targetScale,
                smoothSpeed
            );
        }
    }

    private Vector3 GetMapCenter()
    {
        Vector3 topLeft = trackedMarkers["TopLeft"].transform.position;
        Vector3 topRight = trackedMarkers["TopRight"].transform.position;
        Vector3 bottomLeft = trackedMarkers["BottomLeft"].transform.position;
        Vector3 bottomRight = trackedMarkers["BottomRight"].transform.position;

        return (topLeft + topRight + bottomLeft + bottomRight) / 4f;
    }

    private Vector3 GetMapScale()
    {
        float width = Vector3.Distance(
            trackedMarkers["TopLeft"].transform.position,
            trackedMarkers["TopRight"].transform.position
        );

        float height = Vector3.Distance(
            trackedMarkers["TopLeft"].transform.position,
            trackedMarkers["BottomLeft"].transform.position
        );

        return new Vector3(width, 1f, height);
    }

    private Quaternion GetMapRotation()
    {
        Vector3 right = (
            trackedMarkers["TopRight"].transform.position -
            trackedMarkers["TopLeft"].transform.position
        ).normalized;

        Vector3 forward = (
            trackedMarkers["TopLeft"].transform.position -
            trackedMarkers["BottomLeft"].transform.position
        ).normalized;

        if (right == Vector3.zero || forward == Vector3.zero)
            return Quaternion.identity;

        return Quaternion.LookRotation(forward, Vector3.up);
    }

    private void SpawnHelicopter()
    {
        if (helicopterPrefab == null || spawnedMap == null || spawnedHelicopter != null)
            return;

        spawnedHelicopter = Instantiate(helicopterPrefab, spawnedMap.transform);
        spawnedHelicopter.transform.localPosition = helicopterLocalOffset;
        spawnedHelicopter.transform.localRotation = Quaternion.identity;
    }
}