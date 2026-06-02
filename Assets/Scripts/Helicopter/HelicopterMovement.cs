using UnityEngine;

public class HelicopterMovement : MonoBehaviour
{
    [Header("Controls")]
    public VariableJoystick leftJoystick;  // Movement/Strafe
    public VariableJoystick rightJoystick; // Turn
    
    [Header("Flight Settings")]
    public float maxSpeed = 0.1f;       // Maximum possible speed 
    public float turnSpeed = 90f;        // Turn speed of right joystick
    
    [Range(0f, 1f)]
    [Tooltip("How much the max potential speed drops while turning. 1 = stops completely during full turns.")]
    public float turnSpeedReduction = 0.8f; 

    [Header("Momentum Settings")]
    [Tooltip("How fast the helicopter speeds up when flying forward.")]
    public float acceleration = 0.15f;
    [Tooltip("How fast the helicopter slows down when you let go of the joystick.")]
    public float deceleration = 0.3f;

    [Header("Tilt Settings")]
    public Transform modelTransform; // Visual model to tilt
    public float leanAmount = 0.1f; // Tilt intensity
    public float leanSpeed = 0.01f; // How fast it tilts

    // Internal tracker for current forward/strafe speed momentum
    private float currentSpeed = 0f;

    void Update()
    {
        // 1. Rotation (Yaw)
        float turnInput = rightJoystick.Horizontal;
        float turn = turnInput * turnSpeed * Time.deltaTime;
        transform.Rotate(0, turn, 0);

        // 2. Check Input Magnitude (Are we trying to move at all?)
        Vector3 inputDirection = (transform.forward * leftJoystick.Vertical) + (transform.right * leftJoystick.Horizontal);
        float inputMagnitude = Mathf.Clamp01(inputDirection.magnitude);

        // 3. Determine Target Speed
        // Start with the ideal speed based on how far the movement joystick is pushed
        float targetSpeed = inputMagnitude * maxSpeed;

        // Apply the heavy turn penalty to the target speed if we are actively turning
        float currentSpeedPenalty = Mathf.Abs(turnInput) * turnSpeedReduction;
        float speedMultiplier = 1f - currentSpeedPenalty;
        targetSpeed *= speedMultiplier;

        // 4. Gradually Ease Current Speed Toward Target Speed (Momentum)
        if (targetSpeed > currentSpeed)
        {
            // Gaining speed (Accelerating)
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
        }
        else
        {
            // Losing speed due to turning or letting go of the stick (Decelerating)
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, deceleration * Time.deltaTime);
        }

        // 5. Heading-Based Movement (Using our smoothly blended speed)
        if (inputDirection.sqrMagnitude > 0.001f)
        {
            // Normalize direction to prevent diagonal speed boosts, then apply momentum
            transform.position += inputDirection.normalized * currentSpeed * Time.deltaTime;
        }
        else if (currentSpeed > 0f)
        {
            // Coasting/Drifting slightly forward due to momentum even if the stick was released
            transform.position += transform.forward * currentSpeed * Time.deltaTime;
        }

        // 6. Ground Clamp
        if (transform.localPosition.y < 0.1f) {
            transform.localPosition = new Vector3(transform.localPosition.x, 0.1f, transform.localPosition.z);
        }
    }
}