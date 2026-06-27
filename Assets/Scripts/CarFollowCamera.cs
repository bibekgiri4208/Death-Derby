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

    [Header("Boost Camera Effect")]
    public float boostPullBackDistance = 2.5f;
    public float boostHeightIncrease = 0.3f;
    public float boostCameraSmoothness = 8f;

    [Header("Boost Shake")]
    public float boostShakeStrength = 0.08f;
    public float boostShakeSpeed = 35f;

    private float yaw;
    private float pitch = 15f;

    private Vector3 currentOffset;
    private float shakeTimer;

    private void Start()
    {
        LockCursor();

        currentOffset = offset;

        if (target != null)
        {
            yaw = target.eulerAngles.y;
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            LockCursor();
        }
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        HandleMouseLook();
        FollowTarget();
    }

    private void HandleMouseLook()
    {
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

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void FollowTarget()
    {
        bool isBoosting = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

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

    private Vector3 GetBoostShake(Quaternion cameraRotation)
    {
        shakeTimer += Time.deltaTime * boostShakeSpeed;

        float shakeX = Mathf.Sin(shakeTimer) * boostShakeStrength;
        float shakeY = Mathf.Cos(shakeTimer * 1.4f) * boostShakeStrength;

        Vector3 localShake = new Vector3(shakeX, shakeY, 0f);

        return cameraRotation * localShake;
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