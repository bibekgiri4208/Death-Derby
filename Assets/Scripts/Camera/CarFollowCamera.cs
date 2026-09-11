using UnityEngine;
using UnityEngine.InputSystem;

public class CarFollowCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Camera Position")]
    public Vector3 offset = new Vector3(0f, 3f, -7f);
    public float followSmoothness = 12f;
    public float lookHeight = 1.5f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 0.15f;
    public float minPitch = -10f;
    public float maxPitch = 45f;
    [Tooltip("Camera stops this many degrees before it would look straight down/up (the view-switch zone).")]
    public float pitchSafeGap = 15f;

    [Header("Boost Camera Effect")]
    public float boostPullBackDistance = 2.5f;
    public float boostHeightIncrease = 0.3f;
    public float boostCameraSmoothness = 8f;

    [Header("Boost Shake")]
    public float boostShakeStrength = 0.08f;
    public float boostShakeSpeed = 35f;

    [Header("Fixed Cam")]
    public float fixedDistance = 8f;
    public float fixedHeight = 3.5f;
    public float fixedLookHeight = 1.5f;
    public float fixedFollowSpeed = 5f;
    public float fixedRotationSpeed = 6f;
    [Tooltip("Maximum roll angle (degrees) applied during drifts.")]
    public float fixedDriftTiltAngle = 8f;
    [Tooltip("Multiplier on the car's yaw angular velocity to produce the target tilt.")]
    public float fixedDriftTiltMultiplier = 40f;
    public float fixedTiltSmoothing = 4f;

    private float yaw;
    private float pitch = 15f;
    private Vector3 currentOffset;
    private float shakeTimer;
    private bool isFixedCam;
    private float currentTilt;
    private CarController cachedCarController;

    private void Start()
    {
        LockCursor();

        currentOffset = offset;

        if (target != null)
        {
            yaw = target.eulerAngles.y;
            cachedCarController = target.GetComponent<CarController>();
        }

        // New serialized fields default to 0 when added to an existing scene
        if (fixedDistance < 0.1f) fixedDistance = 8f;
        if (fixedHeight < 0.1f) fixedHeight = 3.5f;
        if (fixedLookHeight < 0.1f) fixedLookHeight = 1.5f;
        if (fixedFollowSpeed < 0.1f) fixedFollowSpeed = 5f;
        if (fixedRotationSpeed < 0.1f) fixedRotationSpeed = 6f;
        if (fixedDriftTiltAngle < 0.1f) fixedDriftTiltAngle = 8f;
        if (fixedDriftTiltMultiplier < 0.1f) fixedDriftTiltMultiplier = 40f;
        if (fixedTiltSmoothing < 0.1f) fixedTiltSmoothing = 4f;
    }

    private bool isUIMode;

    private void Update()
    {
        isUIMode = Keyboard.current != null &&
                   Keyboard.current.leftAltKey.isPressed;

        if (isUIMode)
        {
            UnlockCursor();
        }
        else
        {
            LockCursor();
        }

        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
            isFixedCam = !isFixedCam;

        if (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame)
            isFixedCam = !isFixedCam;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        if (isFixedCam)
        {
            FixedCamFollow();
        }
        else
        {
            HandleMouseLook();
            FreeCamFollow();
        }
    }

    private void HandleMouseLook()
    {
        if (Keyboard.current != null &&
            Keyboard.current.leftAltKey.isPressed)
        {
            return;
        }

        Vector2 lookInput = Vector2.zero;

        if (Mouse.current != null)
        {
            lookInput += Mouse.current.delta.ReadValue() * mouseSensitivity;
        }

        if (Gamepad.current != null)
        {
            Vector2 rightStick = Gamepad.current.rightStick.ReadValue();

            lookInput.x += rightStick.x * 120f * Time.deltaTime;
            lookInput.y += rightStick.y * 120f * Time.deltaTime;
        }

        yaw += lookInput.x;
        pitch -= lookInput.y;

        float verticalPitch = Mathf.Atan2(-currentOffset.z, currentOffset.y) * Mathf.Rad2Deg;
        float maxSafePitch = verticalPitch - pitchSafeGap;

        pitch = Mathf.Clamp(pitch, minPitch, Mathf.Min(maxPitch, maxSafePitch));
    }

    private void FreeCamFollow()
    {
        bool isBoosting = IsBoostActive();

        Vector3 targetOffset = offset;

        if (isBoosting)
        {
            targetOffset = new Vector3(
                offset.x,
                offset.y + boostHeightIncrease,
                offset.z - boostPullBackDistance
            );
        }

        currentOffset = Vector3.Lerp(
            currentOffset,
            targetOffset,
            boostCameraSmoothness * Time.deltaTime
        );

        Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 desiredPosition = target.position + cameraRotation * currentOffset;

        if (isBoosting)
        {
            desiredPosition += GetBoostShake(cameraRotation);
        }

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            followSmoothness * Time.deltaTime
        );

        Vector3 lookPoint = target.position + Vector3.up * lookHeight;
        transform.rotation = Quaternion.LookRotation(lookPoint - transform.position);
    }

    private void FixedCamFollow()
    {
        Rigidbody rb = cachedCarController != null ? cachedCarController.CarRigidbody : null;
        bool isBoosting = IsBoostActive();

        Vector3 desiredPos = target.position
                            - target.forward * fixedDistance
                            + Vector3.up * fixedHeight;

        if (isBoosting)
        {
            desiredPos -= target.forward * boostPullBackDistance;
            desiredPos += Vector3.up * boostHeightIncrease;
        }

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPos,
            fixedFollowSpeed * Time.deltaTime
        );

        Vector3 lookPoint = target.position + Vector3.up * fixedLookHeight;
        Quaternion desiredRot = Quaternion.LookRotation(lookPoint - transform.position);

        // --- slight drift tilt ---
        float targetTilt = 0f;

        if (cachedCarController != null && cachedCarController.IsDrifting && rb != null)
        {
            float yawAngVel = Vector3.Dot(rb.angularVelocity, target.up);
            targetTilt = Mathf.Clamp(
                -yawAngVel * fixedDriftTiltMultiplier,
                -fixedDriftTiltAngle,
                fixedDriftTiltAngle
            );
        }

        currentTilt = Mathf.Lerp(currentTilt, targetTilt, fixedTiltSmoothing * Time.deltaTime);
        desiredRot *= Quaternion.Euler(0f, 0f, currentTilt);

        // --- boost shake ---
        if (isBoosting)
        {
            shakeTimer += Time.deltaTime * boostShakeSpeed;
            float sx = Mathf.Sin(shakeTimer) * boostShakeStrength * Mathf.Rad2Deg;
            float sy = Mathf.Cos(shakeTimer * 1.4f) * boostShakeStrength * Mathf.Rad2Deg;
            desiredRot *= Quaternion.Euler(sx, sy, 0f);
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRot,
            fixedRotationSpeed * Time.deltaTime
        );
    }

    private Vector3 GetBoostShake(Quaternion cameraRotation)
    {
        shakeTimer += Time.deltaTime * boostShakeSpeed;

        float shakeX = Mathf.Sin(shakeTimer) * boostShakeStrength;
        float shakeY = Mathf.Cos(shakeTimer * 1.4f) * boostShakeStrength;

        Vector3 localShake = new Vector3(shakeX, shakeY, 0f);

        return cameraRotation * localShake;
    }

    private bool IsBoostActive()
    {
        if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
            return true;

        if (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed)
            return true;

        return false;
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}