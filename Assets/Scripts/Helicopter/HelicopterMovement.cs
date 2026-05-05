using UnityEngine;

public class HelicopterMovement : MonoBehaviour
{
    [Header("Input Joysticks")]
    public VariableJoystick leftJoystick;  // Movement/Strafe
    public VariableJoystick rightJoystick; // Turn (Yaw)
    
    [Header("Flight Settings")]
    public float speed = 2f;
    public float turnSpeed = 90f;

    [Header("Tilt Settings")]
    public Transform modelTransform; // DRAG YOUR 3D MODEL CHILD HERE
    public float leanAmount = 20f;
    public float leanSpeed = 5f;

    private float fixedY;

    void Start()
    {
        // Capture the spawn height to keep it consistent
        fixedY = transform.position.y;
    }

    void Update()
    {
        // 1. Rotation (Yaw) - Right Stick Horizontal
        float turn = rightJoystick.Horizontal * turnSpeed * Time.deltaTime;
        transform.Rotate(0, turn, 0);

        // 2. Movement Logic (Left Stick)
        float moveX = leftJoystick.Horizontal;
        float moveZ = leftJoystick.Vertical;

        // Using your "Secret Sauce" forward/right logic
        Vector3 direction = (transform.forward * moveZ) + (transform.right * moveX);
        
        // Apply movement while maintaining fixed height
        Vector3 newPos = transform.position + (direction * speed * Time.deltaTime);
        newPos.y = fixedY; 
        transform.position = newPos;

        // 3. Ground Clamp Safety
        if (transform.localPosition.y < 0.1f) {
            transform.localPosition = new Vector3(transform.localPosition.x, 0.1f, transform.localPosition.z);
        }

        // 4. Tilt Animation Logic
        ApplyVisualTilt(moveX, moveZ);
    }

    private void ApplyVisualTilt(float xInput, float zInput)
    {
        if (modelTransform == null) return;

        // Pitch: Leaning forward/back (Z input)
        // Roll: Leaning left/right (X input)
        float targetPitch = zInput * leanAmount;
        float targetRoll = -xInput * leanAmount;

        // Create the target rotation relative to the parent
        Quaternion targetRot = Quaternion.Euler(targetPitch, 0, targetRoll);

        // Smoothly interpolate (Slerp) to the target tilt
        modelTransform.localRotation = Quaternion.Slerp(
            modelTransform.localRotation, 
            targetRot, 
            Time.deltaTime * leanSpeed
        );
    }
}