using UnityEngine;

public class RadarScanner : MonoBehaviour
{
    public float rotationSpeed = 100f; // Hoe snel hij draait

    void Update()
    {
        // Roteert om de Z-as (platte kant van de radar)
        transform.localEulerAngles += new Vector3(0, 0, -rotationSpeed * Time.deltaTime);
    }
}