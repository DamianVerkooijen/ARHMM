using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class HelicopterManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ARTrackingManager trackingManager;

    [Header("References")]
    public float cellSize = 0.1f;
    [SerializeField] public GameObject helicopter;

    // Keep these identical so your HelicopterBoundary script compiles perfectly!
    [HideInInspector] public float minX, maxX, minZ, maxZ;
    [HideInInspector] public bool hasSpawned = false;

    private void OnEnable()
    {
        if (trackingManager != null)
        {
            trackingManager.OnSetupComplete += HandleSetupComplete;
            trackingManager.OnSetupReset += HandleSetupReset;
        }
    }

    private void OnDisable()
    {
        if (trackingManager != null)
        {
            trackingManager.OnSetupComplete -= HandleSetupComplete;
            trackingManager.OnSetupReset -= HandleSetupReset;
        }
    }

    private void Update()
    {
        // FIX 1: UNCOMMENTED THIS. This must run frame-by-frame to fight AR Drift!
        if (hasSpawned) UpdateTrackingWithLiveMarkers();
    }

    private void HandleSetupComplete()
    {
        // Copy the boundary dimensions locally so HelicopterBoundary can read them instantly
        minX = trackingManager.minX;
        maxX = trackingManager.maxX;
        minZ = trackingManager.minZ;
        maxZ = trackingManager.maxZ;

        if (helicopter == null || trackingManager.MasterAnchor == null) return;

        helicopter.SetActive(true);
        helicopter.transform.SetParent(trackingManager.MasterAnchor.transform);
        helicopter.transform.localPosition = Vector3.zero;
        helicopter.transform.localRotation = Quaternion.identity;

        hasSpawned = true;
    }

    private void HandleSetupReset()
    {
        hasSpawned = false;
        if (helicopter == null) return;

        helicopter.transform.SetParent(null);
        helicopter.SetActive(false);
    }

    private void UpdateTrackingWithLiveMarkers()
    {
        if (trackingManager == null || trackingManager.MasterAnchor == null || trackingManager.ImageManager == null) return;

        foreach (var img in trackingManager.ImageManager.trackables)
        {
            if (img.trackingState == TrackingState.Tracking && trackingManager.MarkerOffsets.ContainsKey(img.referenceImage.name))
            {
                // Calculate where the MasterAnchor should be based on the live tracked image position and its initial offset
                Vector3 targetWorldPos = img.transform.position + trackingManager.MasterAnchor.transform.TransformDirection(trackingManager.MarkerOffsets[img.referenceImage.name]);

                // Smoothly Lerp the anchor position to eliminate tracking jitter
                trackingManager.MasterAnchor.transform.position = Vector3.Lerp(trackingManager.MasterAnchor.transform.position, targetWorldPos, Time.deltaTime * 2f);
                
                // Get the marker's rotation, but ONLY extract the Y-axis (left/right rotation)
                Vector3 markerEuler = img.transform.rotation.eulerAngles;
                Quaternion strictlyFlatRotation = Quaternion.Euler(0f, markerEuler.y, 0f);
            
                // Apply the flat rotation. Now the map can rotate to match the paper, but will never tilt up or down!
                trackingManager.MasterAnchor.transform.rotation = Quaternion.Lerp(trackingManager.MasterAnchor.transform.rotation, strictlyFlatRotation, Time.deltaTime * 2f);
                
                break;
            }
        }
    }

    public void ResetHeli()
    {
        if (trackingManager != null) trackingManager.ResetSetup();
    }

    public void SoftResetHeli()
    {
        if (hasSpawned && helicopter != null)
        {
            helicopter.transform.localPosition = Vector3.zero;
            helicopter.transform.localRotation = Quaternion.identity;

            if (trackingManager != null) trackingManager.ForceHideUI();

            Debug.Log("Heli gereset naar startpositie. Kalibratie behouden.");
        }
    }

    public Vector3 GetWorldPositionFromGrid(float gridX, float gridY)
{
    // 1. Convert the 0-100 grid coordinates into a percentage (0.0f to 1.0f)
    float percentX = gridX / 100f;
    float percentZ = gridY / 100f;

    // 2. Map that percentage exactly between your dynamic AR corners
    float preciseLocalX = Mathf.Lerp(minX, maxX, percentX);
    float preciseLocalZ = Mathf.Lerp(minZ, maxZ, percentZ);

    Vector3 localPosition = new Vector3(preciseLocalX, 0f, preciseLocalZ);

    // 3. Translate that local point into the correct AR world space
    if (trackingManager != null && trackingManager.MasterAnchor != null)
    {
        return trackingManager.MasterAnchor.transform.TransformPoint(localPosition);
    }

    // Fallback
    return localPosition;
}
}