using UnityEngine;

public class EnemyCarAI : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform playerTarget; // Drag your Player GameObject here

    [Header("Movement Settings")]
    public float speed = 15f;
    public float rotationSpeed = 5f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // If you didn't assign a player in the inspector, try to find it by tag
        if (playerTarget == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTarget = player.transform;
            }
        }
    }

    void FixedUpdate()
    {
        if (playerTarget == null) return;

        HandleMovement();
    }

    void HandleMovement()
    {
        // 1. Calculate the direction to the player
        Vector3 direction = (playerTarget.position - transform.position).normalized;

        // Keep the enemy on the same flat ground plane (Y-axis)
        direction.y = 0;

        // 2. Smoothly rotate towards the player
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }

        // 3. Move forward in the direction the enemy car is currently facing
        Vector3 moveVelocity = transform.forward * speed;

        // Apply velocity but preserve current vertical gravity fall
        rb.linearVelocity = new Vector3(moveVelocity.x, rb.linearVelocity.y, moveVelocity.z);
    }
}