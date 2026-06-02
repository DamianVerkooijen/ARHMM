using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;

public class ARTrackingManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ARTrackedImageManager imageManager;
    [SerializeField] private ARSession arSession; 
    [SerializeField] private TMP_Text statusText;

    private readonly string[] requiredMarkers = { "TopLeft", "TopRight", "BottomLeft", "BottomRight" };
    private Dictionary<string, Pose> savedMarkerPoses = new Dictionary<string, Pose>();
    
    public Dictionary<string, Vector3> MarkerOffsets { get; private set; } = new Dictionary<string, Vector3>();
    public GameObject MasterAnchor { get; private set; }
    public bool IsCalibrated { get; private set; } = false;

    [HideInInspector] public float minX, maxX, minZ, maxZ;

    // Events to alert the HelicopterManager when initialization changes
    public event Action OnSetupComplete;
    public event Action OnSetupReset;

    public ARTrackedImageManager ImageManager => imageManager;

    private void OnEnable() => imageManager.trackablesChanged.AddListener(OnTrackablesChanged);
    private void OnDisable() => imageManager.trackablesChanged.RemoveListener(OnTrackablesChanged);

    private void Update()
    {
        if (!IsCalibrated && savedMarkerPoses.Count == 4)
        {
            InitialCalibration();
        }

        UpdateStatusUI();
    }

    private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        if (IsCalibrated) return;
        foreach (var img in eventArgs.added) RegisterMarker(img);
        foreach (var img in eventArgs.updated) RegisterMarker(img);
    }

    private void RegisterMarker(ARTrackedImage img)
    {
        if (img.trackingState == TrackingState.Tracking)
        {
            string name = img.referenceImage.name;
            if (Array.Exists(requiredMarkers, m => m == name))
            {
                savedMarkerPoses[name] = new Pose(img.transform.position, img.transform.rotation);
            }
        }
    }

    private void InitialCalibration()
    {
        Vector3 sumPos = Vector3.zero;
        foreach (var p in savedMarkerPoses.Values) sumPos += p.position;
        Vector3 centerPos = sumPos / 4f;

        if (MasterAnchor != null) Destroy(MasterAnchor);
        MasterAnchor = new GameObject("Heli_World_Anchor");
        MasterAnchor.transform.position = centerPos;

        if (savedMarkerPoses.ContainsKey("BottomLeft") && savedMarkerPoses.ContainsKey("BottomRight"))
        {
            Vector3 tableRight = (savedMarkerPoses["BottomRight"].position - savedMarkerPoses["BottomLeft"].position).normalized;
            Vector3 tableForward = Vector3.Cross(tableRight, Vector3.up);
            MasterAnchor.transform.rotation = Quaternion.LookRotation(tableForward, Vector3.up);
        }

        minX = minZ = float.MaxValue;
        maxX = maxZ = float.MinValue;

        foreach (var kvp in savedMarkerPoses)
        {
            Vector3 localPos = MasterAnchor.transform.InverseTransformPoint(kvp.Value.position);
            MarkerOffsets[kvp.Key] = -localPos;

            if (localPos.x < minX) minX = localPos.x;
            if (localPos.x > maxX) maxX = localPos.x;
            if (localPos.z < minZ) minZ = localPos.z;
            if (localPos.z > maxZ) maxZ = localPos.z;
        }

        IsCalibrated = true;
        OnSetupComplete?.Invoke();
    }

    private void UpdateStatusUI()
    {
        if (statusText == null) return;

        if (IsCalibrated)
        {
            // Fades out cleanly when playing
            statusText.alpha = Mathf.MoveTowards(statusText.alpha, 0f, Time.deltaTime * 2f);
        }
        else
        {
            statusText.alpha = 1f;
            string missing = "";
            foreach (var m in requiredMarkers)
            {
                if (!savedMarkerPoses.ContainsKey(m)) missing += m + " ";
            }
            statusText.text = $"<color=green>Scanned: {savedMarkerPoses.Count}/4\nMissing: {missing}</color>";
        }
    }

    public void ResetSetup()
    {
        IsCalibrated = false;
        
        if (MasterAnchor != null) Destroy(MasterAnchor);

        savedMarkerPoses.Clear();
        MarkerOffsets.Clear();

        // === FIX 1: WIPE THE AR ENGINE SUBSYSTEM MEMORY ===
        // This clears out cached images and forces old trackables to vanish.
        if (arSession != null)
        {
            arSession.Reset();
        }

        // === FIX 2: RE-SHOW DEBUG UI IMMEDIATELY ===
        if (statusText != null)
        {
            statusText.alpha = 1f; 
            statusText.text = "<color=green>Scanned: 0/4\nMissing: TopLeft TopRight BottomLeft BottomRight </color>";
        }

        // Alert HelicopterManager to turn off the helicopter/boundaries
        OnSetupReset?.Invoke();
    }

    public Vector3 GetWorldPositionFromGrid(float gridX, float gridY)
    {
        if (!IsCalibrated || MasterAnchor == null) return Vector3.zero;

        float localX = Mathf.Lerp(minX, maxX, gridX / 100f);
        float localZ = Mathf.Lerp(minZ, maxZ, gridY / 100f);

        return MasterAnchor.transform.TransformPoint(new Vector3(localX, 0, localZ));
    }

    public void ForceHideUI()
    {
        if (statusText != null) statusText.alpha = 0f;
    }
}