using UnityEngine;

public class RadarScanner : MonoBehaviour
{
    public float rotationSpeed = 100f; // How fast it spins

    void Update()
    {
        // Rotates around the Z axis (flat side of the radar)
        transform.localEulerAngles += new Vector3(0, 0, -rotationSpeed * Time.deltaTime);
    }
}