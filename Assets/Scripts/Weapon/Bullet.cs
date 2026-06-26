using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 80f;
    public float lifetime = 3f;
    public int damage = 10;
    public float castRadius = 0.12f;

    private Rigidbody rb;
    private Collider bulletCollider;
    private Collider[] ignoredColliders;

    private Vector3 lastPosition;
    private bool launched;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bulletCollider = GetComponent<Collider>();
    }

    public void Launch(Vector3 direction, Collider[] ownerColliders)
    {
        ignoredColliders = ownerColliders;

        direction.Normalize();

        if (bulletCollider != null && ignoredColliders != null)
        {
            foreach (Collider ownerCollider in ignoredColliders)
            {
                if (ownerCollider != null)
                {
                    Physics.IgnoreCollision(bulletCollider, ownerCollider, true);
                }
            }
        }

        transform.forward = direction;
        lastPosition = transform.position;
        launched = true;

        rb.linearVelocity = direction * speed;

        Destroy(gameObject, lifetime);
    }

    private void FixedUpdate()
    {
        if (!launched)
            return;

        Vector3 travel = transform.position - lastPosition;
        float distance = travel.magnitude;

        if (distance > 0.001f)
        {
            RaycastHit[] hits = Physics.SphereCastAll(
                lastPosition,
                castRadius,
                travel.normalized,
                distance
            );

            foreach (RaycastHit hit in hits)
            {
                if (ShouldIgnore(hit.collider))
                    continue;

                Hit(hit.collider.gameObject);
                return;
            }
        }

        lastPosition = transform.position;
    }

    private bool ShouldIgnore(Collider other)
    {
        if (other == null)
            return true;

        if (bulletCollider != null && other == bulletCollider)
            return true;

        if (ignoredColliders == null)
            return false;

        foreach (Collider ignored in ignoredColliders)
        {
            if (other == ignored)
                return true;
        }

        return false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (ShouldIgnore(collision.collider))
            return;

        Hit(collision.gameObject);
    }

    private void Hit(GameObject hitObject)
    {
        Health health = hitObject.GetComponent<Health>();

        if (health != null)
        {
            health.TakeDamage(damage);
        }

        Destroy(gameObject);
    }
}