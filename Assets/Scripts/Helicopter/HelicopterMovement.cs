using UnityEngine;

public class HelicopterMovement : MonoBehaviour
{
    public VariableJoystick leftJoystick;  // Movement/Strafe
    public VariableJoystick rightJoystick; // Turn/Height
    
    public float speed = 2f;
    public float turnSpeed = 90f;

    void Update()
    {
        // 1. Rotation (Yaw) - Right Stick Horizontal
        // We use -rightJoystick to keep that 'reversed' feel you liked
        float turn = rightJoystick.Horizontal * turnSpeed * Time.deltaTime;
        transform.Rotate(0, turn, 0);

        // 2. Heading-Based Movement - Left Stick
        // Unity's 'transform.forward' is the secret sauce here
        Vector3 direction = (transform.forward * leftJoystick.Vertical) + (transform.right * leftJoystick.Horizontal);
        transform.position += direction * speed * Time.deltaTime;

        // 4. Ground Clamp (Don't let it go through the table)
        if (transform.localPosition.y < 0.1f) {
            transform.localPosition = new Vector3(transform.localPosition.x, 0.1f, transform.localPosition.z);
        }
    }
}