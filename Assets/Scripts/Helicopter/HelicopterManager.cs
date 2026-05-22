using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class HelicopterManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ARTrackingManager trackingManager;

    [Header("References")]
    [SerializeField] public GameObject helicopter;

    // CRITICAL: Keep these names identical so your HelicopterBoundary script compiles perfectly!
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
        // if (hasSpawned) UpdateTrackingWithLiveMarkers();
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
                Vector3 targetWorldPos = img.transform.position + trackingManager.MasterAnchor.transform.TransformDirection(trackingManager.MarkerOffsets[img.referenceImage.name]);

                // Maintain the smooth positional Lerp logic to stop environmental jitter
                trackingManager.MasterAnchor.transform.position = Vector3.Lerp(trackingManager.MasterAnchor.transform.position, targetWorldPos, Time.deltaTime * 2f);
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
        if (trackingManager == null) return Vector3.zero;
        return trackingManager.GetWorldPositionFromGrid(gridX, gridY);
    }
}