using UnityEngine;

public class HelicopterMovement : MonoBehaviour
{
    [Header("Controls")]
    public VariableJoystick leftJoystick;  // Movement/Strafe
    public VariableJoystick rightJoystick; // Turn
    
    [Header("Flight Settings")]
    public float maxSpeed = 0.1f;       
    public float turnSpeed = 90f;        
    
    [Range(0f, 1f)]
    public float turnSpeedReduction = 0.8f; 

    [Header("Momentum Settings")]
    public float acceleration = 0.15f;
    public float deceleration = 0.3f;

    private float currentSpeed = 0f;

    // === NEW SAFEGUARD FOR RESETS ===
    private void OnEnable()
    {
        // Reset internal momentum tracking entirely upon spawning
        currentSpeed = 0f;

        // If the joysticks are stuck processing an old touch, this forces them to release
        if (leftJoystick != null) leftJoystick.OnPointerUp(null);
        if (rightJoystick != null) rightJoystick.OnPointerUp(null);
    }

    void Update()
    {
        // Safety check: Don't execute if the reset manager has temporarily unlinked joysticks
        if (leftJoystick == null || rightJoystick == null) return;

        // 1. Rotation (Yaw)
        float turnInput = rightJoystick.Horizontal;
        float turn = turnInput * turnSpeed * Time.deltaTime;
        transform.Rotate(0, turn, 0);

        // 2. Check Input Magnitude
        Vector3 inputDirection = (transform.forward * leftJoystick.Vertical) + (transform.right * leftJoystick.Horizontal);
        float inputMagnitude = Mathf.Clamp01(inputDirection.magnitude);

        // 3. Determine Target Speed
        float targetSpeed = inputMagnitude * maxSpeed;
        float currentSpeedPenalty = Mathf.Abs(turnInput) * turnSpeedReduction;
        float speedMultiplier = 1f - currentSpeedPenalty;
        targetSpeed *= speedMultiplier;

        // 4. Momentum Easing
        if (targetSpeed > currentSpeed)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
        }
        else
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, deceleration * Time.deltaTime);
        }

        // 5. Heading-Based Movement
        if (inputDirection.sqrMagnitude > 0.001f)
        {
            transform.position += inputDirection.normalized * currentSpeed * Time.deltaTime;
        }
        else if (currentSpeed > 0f)
        {
            transform.position += transform.forward * currentSpeed * Time.deltaTime;
        }

        // 6. Ground Clamp
        if (transform.localPosition.y < 0.1f) {
            transform.localPosition = new Vector3(transform.localPosition.x, 0.1f, transform.localPosition.z);
        }
    }
}