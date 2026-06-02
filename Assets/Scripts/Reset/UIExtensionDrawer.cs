using UnityEngine;
using UnityEngine.InputSystem;

public class UIExtensionDrawer : MonoBehaviour
{
    [Header("Animator Instellingen")]
    [Tooltip("De animator die op deze extensie-lade zit")]
    public Animator extensionAnimator;
    [Tooltip("De parameter naam in de animator (standaard 'IsInRange')")]
    public string animParamName = "IsInRange";

    [Header("Swipe Gevoeligheid")]
    [Tooltip("Hoeveel pixels moet er minimaal horizontaal geswiped worden om te reageren?")]
    public float swipeThreshold = 80f;
    [Tooltip("Moet de swipe vanaf de uiterste linkerrand van het scherm beginnen?")]
    public bool requireLeftEdgeStart = true;
    [Tooltip("Breedte van de actieve zone aan de linkerkant van het scherm (in pixels)")]
    public float edgeZoneWidth = 150f;

    private Vector2 touchStartPos;
    private bool isValidSwipeStart = false;
    private bool isDrawerOpen = false;
    private bool isTrackingSwipe = false;

    void Start()
    {
        if (extensionAnimator == null)
            extensionAnimator = GetComponent<Animator>();
    }

    void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        // --- 1. TOUCH INPUT (Nieuwe Input System voor Mobiel/Tablet) ---
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            if (touch.isInProgress)
            {
                Vector2 currentPos = touch.position.ReadValue();

                if (touch.press.wasPressedThisFrame)
                {
                    StartSwipe(currentPos);
                }
            }
            else if (touch.press.wasReleasedThisFrame && isTrackingSwipe)
            {
                EndSwipe(touch.position.ReadValue());
            }

            // Als er touch-input is, skippen we de muis-input om dubbele registratie te voorkomen
            if (Touchscreen.current.touches.Count > 0) return;
        }

        // --- 2. POINTER INPUT (Nieuwe Input System voor Muis/Editor) ---
        if (Pointer.current != null)
        {
            if (Pointer.current.press.wasPressedThisFrame)
            {
                StartSwipe(Pointer.current.position.ReadValue());
            }
            else if (Pointer.current.press.wasReleasedThisFrame && isTrackingSwipe)
            {
                EndSwipe(Pointer.current.position.ReadValue());
            }
        }
    }

    private void StartSwipe(Vector2 screenPos)
    {
        touchStartPos = screenPos;

        if (requireLeftEdgeStart)
        {
            // Als de lade dicht is, moet de swipe links beginnen.
            // Als de lade open is, mag je overal naar links swipen om hem dicht te doen.
            isValidSwipeStart = isDrawerOpen || (screenPos.x <= edgeZoneWidth);
        }
        else
        {
            isValidSwipeStart = true;
        }

        isTrackingSwipe = isValidSwipeStart;
    }

    private void EndSwipe(Vector2 screenPos)
    {
        isTrackingSwipe = false;
        if (!isValidSwipeStart) return;

        float horizontalDelta = screenPos.x - touchStartPos.x;

        // Van LINKS naar RECHTS swipen -> Open de lade
        if (!isDrawerOpen && horizontalDelta > swipeThreshold)
        {
            SetDrawerState(true);
        }
        // Van RECHTS naar LINKS swipen -> Sluit de lade
        else if (isDrawerOpen && horizontalDelta < -swipeThreshold)
        {
            SetDrawerState(false);
        }

        isValidSwipeStart = false;
    }

    public void SetDrawerState(bool open)
    {
        isDrawerOpen = open;
        if (extensionAnimator != null)
        {
            extensionAnimator.SetBool(animParamName, open);
        }
    }
}