using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MissionController : MonoBehaviour
{
    [Header("Settings")]
    public HelicopterManager manager;
    public GameObject padPrefab; 
    public TMP_Text statusText;
    public GameObject actionButton; 
    public float interactionRange = 0.15f;
    public RadarMarker missionMarker;

    [Header("Mission List")]
    public List<Mission> missions = new List<Mission>();
    
    private int currentMissionIndex = 0;
    private bool missionActive = false;
    private GameObject activePad;

    void Start()
    {
        if (actionButton != null) actionButton.SetActive(false);
        
        if (padPrefab != null) {
            activePad = Instantiate(padPrefab);
            activePad.SetActive(false);
        }
    }

    void Update()
    {
        // 1. Safety Checks
        if (!manager.hasSpawned || missions.Count == 0) return;

        // 2. Check if we've finished all missions
        if (currentMissionIndex >= missions.Count)
        {
            statusText.text = "All Missions Complete!";
            if(activePad != null) activePad.SetActive(false);
            if(actionButton != null) actionButton.SetActive(false);
            return;
        }

        // 3. Determine current target based on state
        Vector2 targetGrid = missionActive ? missions[currentMissionIndex].endGridPos : missions[currentMissionIndex].startGridPos;
        
        // 4. Update Pad Position
        activePad.SetActive(true);
        activePad.transform.position = manager.GetWorldPositionFromGrid(targetGrid.x, targetGrid.y);

        // 5. Range Check (Flat Distance)
        float dist = GetFlatDistance(manager.helicopter.transform.position, activePad.transform.position);

        if (dist < interactionRange)
        {
            actionButton.SetActive(true);
            statusText.text = missionActive ? $"Arrived at Destination: {missions[currentMissionIndex].missionName}" : "Ready to Start Mission?";
        }
        else
        {
            actionButton.SetActive(false);
            statusText.text = missionActive ? $"Fly to Destination ({missions[currentMissionIndex].endGridPos})" : $"Fly to Start ({missions[currentMissionIndex].startGridPos})";
        }

        if (missionMarker != null)
        {
            Vector3 targetPos = manager.GetWorldPositionFromGrid(targetGrid.x, targetGrid.y);

            // We sturen nu ook de rotatie van de helikopter mee (eulerAngles.y)
            missionMarker.UpdatePosition(
                targetPos,
                manager.helicopter.transform.position,
                manager.helicopter.transform.eulerAngles.y
            );
        }
    }

    private float GetFlatDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }

    public void OnActionButtonPressed()
    {
        // If we click the button, hide it immediately so it doesn't stay visible incorrectly
        actionButton.SetActive(false);

        if (!missionActive)
        {
            // START MISSION
            missionActive = true;
            Debug.Log("MISSION STARTED: " + missions[currentMissionIndex].missionName);
        }
        else
        {
            // COMPLETE MISSION
            Debug.Log("MISSION COMPLETE: " + missions[currentMissionIndex].missionName);
            missionActive = false;
            currentMissionIndex++; // THIS advances to the next mission in the list
        }
    }
}