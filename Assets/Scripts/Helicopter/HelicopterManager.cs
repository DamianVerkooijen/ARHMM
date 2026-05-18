using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;

public class HelicopterManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ARTrackedImageManager imageManager;
    [SerializeField] public GameObject helicopter;
    [SerializeField] private TMP_Text statusText;

    private readonly string[] requiredMarkers = { "TopLeft", "TopRight", "BottomLeft", "BottomRight" };
    
    // Memory of where markers are in the room
    private Dictionary<string, Pose> savedMarkerPoses = new Dictionary<string, Pose>();
    private Dictionary<string, Vector3> markerOffsets = new Dictionary<string, Vector3>();
    
    public bool hasSpawned = false;
    private GameObject masterAnchor;

    [HideInInspector] public float minX, maxX, minZ, maxZ;

    private void OnEnable() => imageManager.trackablesChanged.AddListener(OnTrackablesChanged);
    private void OnDisable() => imageManager.trackablesChanged.RemoveListener(OnTrackablesChanged);

    private void Update()
    {
        // Only try to spawn if we have all 4 and haven't spawned yet
        if (!hasSpawned && savedMarkerPoses.Count == 4)
        {
            InitialCalibration();
        }

        if (hasSpawned) UpdateTrackingWithLiveMarkers();

        UpdateStatusUI();
    }

    private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        // Only register markers that are actively being tracked
        foreach (var img in eventArgs.added) RegisterMarker(img);
        foreach (var img in eventArgs.updated) RegisterMarker(img);
    }

    private void RegisterMarker(ARTrackedImage img)
    {
        // CRITICAL: Only save if the state is 'Tracking'
        // If it's 'Limited', the camera is guessing and the data is 'fucked'
        if (img.trackingState == TrackingState.Tracking)
        {
            string name = img.referenceImage.name;
            if (System.Array.Exists(requiredMarkers, m => m == name))
            {
                savedMarkerPoses[name] = new Pose(img.transform.position, img.transform.rotation);
            }
        }
        else
        {
            // Optional: If a marker goes to 'None', you could choose to remove it from memory
            // but for "Progressive Scan", we usually keep it once it's locked in.
        }
    }

    private void InitialCalibration()
    {
        Vector3 sumPos = Vector3.zero;
        foreach (var p in savedMarkerPoses.Values) sumPos += p.position;
        Vector3 centerPos = sumPos / 4f;

        if (masterAnchor != null) Destroy(masterAnchor);
        masterAnchor = new GameObject("Heli_World_Anchor");
        masterAnchor.transform.position = centerPos;

        // Align Rotation
        if (savedMarkerPoses.ContainsKey("BottomLeft") && savedMarkerPoses.ContainsKey("BottomRight"))
        {
            Vector3 tableRight = (savedMarkerPoses["BottomRight"].position - savedMarkerPoses["BottomLeft"].position).normalized;
            Vector3 tableForward = Vector3.Cross(tableRight, Vector3.up);
            masterAnchor.transform.rotation = Quaternion.LookRotation(tableForward, Vector3.up);
        }

        // Calculate Bounds
        minX = minZ = float.MaxValue;
        maxX = maxZ = float.MinValue;

        foreach (var kvp in savedMarkerPoses)
        {
            Vector3 localPos = masterAnchor.transform.InverseTransformPoint(kvp.Value.position);
            markerOffsets[kvp.Key] = -localPos;

            if (localPos.x < minX) minX = localPos.x;
            if (localPos.x > maxX) maxX = localPos.x;
            if (localPos.z < minZ) minZ = localPos.z;
            if (localPos.z > maxZ) maxZ = localPos.z;
        }

        helicopter.SetActive(true);
        helicopter.transform.SetParent(masterAnchor.transform);
        helicopter.transform.localPosition = Vector3.zero;
        helicopter.transform.localRotation = Quaternion.identity;

        hasSpawned = true;
    }

    private void UpdateTrackingWithLiveMarkers()
    {
        foreach (var img in imageManager.trackables)
        {
            // Alleen updaten als de marker ÉCHT goed in beeld is (TrackingState.Tracking)
            if (img.trackingState == TrackingState.Tracking && markerOffsets.ContainsKey(img.referenceImage.name))
            {
                Vector3 targetWorldPos = img.transform.position + masterAnchor.transform.TransformDirection(markerOffsets[img.referenceImage.name]);

                // GEBRUIK LERP: In plaats van direct teleporteren, vloeit het anker naar de nieuwe positie.
                // Dit stopt het "shaken" en verspringen van de heli en markers.
                masterAnchor.transform.position = Vector3.Lerp(masterAnchor.transform.position, targetWorldPos, Time.deltaTime * 2f);

                // Optioneel: doe hetzelfde voor rotatie als dat ook verspringt
                // masterAnchor.transform.rotation = Quaternion.Lerp(masterAnchor.transform.rotation, img.transform.rotation, Time.deltaTime * 1f);

                break;
            }
        }
    }

    private void UpdateStatusUI()
    {
        if (statusText == null) return;

        if (hasSpawned)
        {
            // Make the text invisible once we're done so the HUD takes over
            statusText.alpha = Mathf.MoveTowards(statusText.alpha, 0f, Time.deltaTime * 2f);
        }
        else
        {
            // Make sure the text is visible when scanning
            statusText.alpha = 1f;

            string missing = "";
            foreach (var m in requiredMarkers)
            {
                if (!savedMarkerPoses.ContainsKey(m)) missing += m + " ";
            }
            statusText.text = $"Scanned: {savedMarkerPoses.Count}/4\nMissing: {missing}";
        }
    }

    public void ResetHeli()
    {
        hasSpawned = false;
        if (masterAnchor != null) Destroy(masterAnchor);
        
        helicopter.transform.SetParent(null); // Detach before disabling
        helicopter.SetActive(false);
        
        savedMarkerPoses.Clear();
        markerOffsets.Clear();

        // RE-SCAN CHECK: Only grab what the camera sees AT THIS SECOND
        foreach (var img in imageManager.trackables)
        {
            if (img.trackingState == TrackingState.Tracking)
            {
                RegisterMarker(img);
            }
        }
    }

    public void SoftResetHeli()
    {
        if (hasSpawned && helicopter != null)
        {
            // Return the helicopter to the starting point of the calibration
            helicopter.transform.localPosition = Vector3.zero;
            helicopter.transform.localRotation = Quaternion.identity;

            // Make sure the statusText remains invisible (alpha 0)
            if (statusText != null) statusText.alpha = 0f;

            Debug.Log("Heli gereset naar startpositie. Kalibratie behouden.");
        }
    }

    public Vector3 GetWorldPositionFromGrid(float gridX, float gridY)
{
    if (!hasSpawned || masterAnchor == null) return Vector3.zero;

    // Map 0-10 grid to local min/max
    float localX = Mathf.Lerp(minX, maxX, gridX / 100f);
    float localZ = Mathf.Lerp(minZ, maxZ, gridY / 100f);

    return masterAnchor.transform.TransformPoint(new Vector3(localX, 0, localZ));
}
}