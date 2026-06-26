using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 45f;
    public float lifetime = 4f;
    public int damage = 10;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Launch(Vector3 direction, Collider[] ignoredColliders)
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        Collider bulletCollider = GetComponent<Collider>();

        if (bulletCollider != null && ignoredColliders != null)
        {
            foreach (Collider ownerCollider in ignoredColliders)
            {
                if (ownerCollider != null)
                {
                    Physics.IgnoreCollision(bulletCollider, ownerCollider);
                }
            }
        }

        rb.linearVelocity = direction.normalized * speed;

        Destroy(gameObject, lifetime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Bullet hit: " + collision.gameObject.name);

        Health health = collision.gameObject.GetComponent<Health>();

        if (health != null)
        {
            health.TakeDamage(damage);
        }

        Destroy(gameObject);
    }
}