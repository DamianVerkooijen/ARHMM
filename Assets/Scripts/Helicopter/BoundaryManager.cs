using UnityEngine;

public class HelicopterBoundary : MonoBehaviour
{
    private HelicopterManager manager;

    void Start()
    {
        manager = Object.FindFirstObjectByType<HelicopterManager>();
    }

    void LateUpdate()
    {
        if (manager == null || !gameObject.activeInHierarchy) return;

        Vector3 pos = transform.localPosition; // MUST be localPosition

        pos.x = Mathf.Clamp(pos.x, manager.minX, manager.maxX);
        pos.z = Mathf.Clamp(pos.z, manager.minZ, manager.maxZ);

        transform.localPosition = pos;
    }
}