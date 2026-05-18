using UnityEngine;

public class HelicopterMovement : MonoBehaviour
{
    [Header("Controls")]
    public VariableJoystick leftJoystick;  // Movement/Strafe
    public VariableJoystick rightJoystick; // Turn
    
    [Header("Flight Settings")]
    public float speed = 0.25f;    // Movement speed 
    public float turnSpeed = 90f; // Turn speed of right joystick

    [Header("Tilt Settings")]
    public Transform modelTransform; // Visual model to tilt
    public float leanAmount = 0.1f; // Tilt intensity
    public float leanSpeed = 0.01f; // How fast it tilts

    void Update()
    {
        // Rotation (Yaw)
        float turn = rightJoystick.Horizontal * turnSpeed * Time.deltaTime;
        transform.Rotate(0, turn, 0);

        // Heading-Based Movement
        Vector3 direction = (transform.forward * leftJoystick.Vertical) + (transform.right * leftJoystick.Horizontal);
        transform.position += direction * speed * Time.deltaTime;

        // Ground Clamp
        if (transform.localPosition.y < 0.1f) {
            transform.localPosition = new Vector3(transform.localPosition.x, 0.1f, transform.localPosition.z);
        }

        // Visual Tilt Animation
        // This only rotates the visual child 
        //if (modelTransform != null)
        //{
        //    float targetPitch = leftJoystick.Vertical * leanAmount;
        //    float targetRoll = -leftJoystick.Horizontal * leanAmount;

        //    Quaternion targetRot = Quaternion.Euler(targetPitch, 0, targetRoll);

        //    modelTransform.localRotation = Quaternion.Slerp(
        //        modelTransform.localRotation, 
        //        targetRot, 
        //        Time.deltaTime * leanSpeed
        //    );
        //}
    }
}