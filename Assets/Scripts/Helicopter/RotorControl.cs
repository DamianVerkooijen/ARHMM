using UnityEngine;

public class RotorCOntrol : MonoBehaviour
{
    [Header("Rotor Transforms")]
    public Transform mainRotor;
    public Transform tailRotor;

    [Header("Speed Settings")]
    public float mainRotorSpeed = 1500f;
    public float tailRotorSpeed = 1500f;

    [Header("Axis Configuration")]
    public Vector3 mainRotorAxis = Vector3.up;      // Usually (0, 1, 0)
    public Vector3 tailRotorAxis = Vector3.right;   // Usually (1, 0, 0) or (0, 0, 1)

    void Update()
    {
        // Spin the main rotor
        if (mainRotor != null)
        {
            mainRotor.Rotate(mainRotorAxis * mainRotorSpeed * Time.deltaTime, Space.Self);
        }

        // Spin the tail rotor
        if (tailRotor != null)
        {
            tailRotor.Rotate(tailRotorAxis * tailRotorSpeed * Time.deltaTime, Space.Self);
        }
    }
}