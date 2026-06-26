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

    private float yaw;
    private float pitch = 15f;

    private void Start()
    {
        LockCursor();

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
        if (Mouse.current == null)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void FollowTarget()
    {
        Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 desiredPosition = target.position + cameraRotation * offset;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            followSmoothness * Time.deltaTime
        );

        Vector3 lookPoint = target.position + Vector3.up * lookHeight;
        transform.rotation = Quaternion.LookRotation(lookPoint - transform.position);
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