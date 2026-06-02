using System.Collections.Generic;
using UnityEngine;

public class MarkerManager : MonoBehaviour
{
    [Header("Physical Object Prefabs")]
    public GameObject markerPrefab;
    public GameObject padPrefab;

    private List<GameObject> spawnedMarkers = new List<GameObject>();
    private GameObject activePad;

    private MissionStateController stateController;
    private HelicopterManager manager;

    public void Initialize(MissionStateController controller, HelicopterManager heliManager)
    {
        stateController = controller;
        manager = heliManager;

        if (padPrefab != null)
        {
            activePad = Instantiate(padPrefab);
            activePad.SetActive(false);
        }

        stateController.OnMissionStarted += HandleMissionStarted;
        stateController.OnMissionCompleted += HandleMissionCompleted;
        stateController.OnStepCompleted += HandleStepCompleted;
        stateController.OnMissionReset += HandleMissionReset;
    }

    private void OnDestroy()
    {
        if (stateController == null) return;
        stateController.OnMissionStarted -= HandleMissionStarted;
        stateController.OnMissionCompleted -= HandleMissionCompleted;
        stateController.OnStepCompleted -= HandleStepCompleted;
        stateController.OnMissionReset -= HandleMissionReset;
    }

    public void EvaluateMarkerVisualPlacement()
    {
        if (stateController == null || manager == null) return;

        if (stateController.selectedMissionIndex != -1 && activePad != null)
        {
            activePad.SetActive(true);
            activePad.transform.position = stateController.GetCurrentTargetWorldPos();
        }
        else if (activePad != null)
        {
            activePad.SetActive(false);
        }
    }

    public void SpawnWorldMarkers(List<MissionStateController.Mission> currentMissions)
    {
        foreach (var marker in spawnedMarkers) if (marker != null) Destroy(marker);
        spawnedMarkers.Clear();

        for (int i = 0; i < currentMissions.Count; i++)
        {
            if (currentMissions[i].isCompleted) continue;
            Vector2 gridPos = stateController.GetFirstTargetPosition(currentMissions[i]);
            Vector3 worldPos = manager.GetWorldPositionFromGrid(gridPos.x, gridPos.y);
            
            Vector3 markerPos = worldPos;
            markerPos.y += 0.01f;

            GameObject marker = Instantiate(markerPrefab, markerPos, Quaternion.identity, transform);
            spawnedMarkers.Add(marker);
        }
    }

    private void HandleMissionStarted(int index)
    {

        for (int i = 0; i < spawnedMarkers.Count; i++)
        {
            if (spawnedMarkers[i] != null)
            {
                spawnedMarkers[i].SetActive(i == index);
            }
        }
    }

    private void HandleMissionCompleted(int index)
    {
        if (activePad != null) activePad.SetActive(false);
        SpawnWorldMarkers(stateController.missions);
    }

    private void HandleStepCompleted()
    {
        if (activePad != null) activePad.transform.position = stateController.GetCurrentTargetWorldPos();
    }

    private void HandleMissionReset()
    {
        if (activePad != null) activePad.SetActive(false);
        SpawnWorldMarkers(stateController.missions);
    }
}