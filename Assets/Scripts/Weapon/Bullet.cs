using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 80f;
    public float lifetime = 3f; // Fallback timer
    public int damage = 10;
    public float castRadius = 0.12f;

    [Header("Effects")]
    public GameObject sparkPrefab;

    private Rigidbody rb;
    private Collider bulletCollider;
    private Collider[] ignoredColliders;

    private Vector3 startPosition; // Stores where the bullet was fired from
    private Vector3 lastPosition;
    private float maxRange = 100f;  // Set dynamically by the gun
    private bool launched;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bulletCollider = GetComponent<Collider>();
    }

    // Added maxRange parameter to the Launch method
    public void Launch(Vector3 direction, Collider[] ownerColliders, float range)
    {
        ignoredColliders = ownerColliders;
        maxRange = range;

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
        startPosition = transform.position; // Record spawn point
        lastPosition = transform.position;
        launched = true;

        rb.linearVelocity = direction * speed;

        Destroy(gameObject, lifetime); // Keep as a safety fallback
    }

    private void FixedUpdate()
    {
        if (!launched)
            return;

        // --- RANGE CHECK ---
        // Calculate total distance traveled from the muzzle point
        float distanceTraveled = Vector3.Distance(startPosition, transform.position);
        if (distanceTraveled >= maxRange)
        {
            Destroy(gameObject);
            return; // Stop processing further physics
        }
        // -------------------

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

                Vector3 hitPoint = hit.point;
                Vector3 hitNormal = hit.normal;
                Hit(hit.collider.gameObject, hitPoint, hitNormal);
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

        Vector3 hitPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
        Vector3 hitNormal = collision.contacts.Length > 0 ? collision.contacts[0].normal : -transform.forward;

        Hit(collision.gameObject, hitPoint, hitNormal);
    }

    private void Hit(GameObject hitObject, Vector3 point, Vector3 normal)
    {
        Health health = hitObject.GetComponent<Health>();

        if (health != null)
        {
            health.TakeDamage(damage);
        }

        if (sparkPrefab != null)
        {
            GameObject sparks = Instantiate(sparkPrefab, point, Quaternion.LookRotation(normal));
            Destroy(sparks, 1f);
        }

        Destroy(gameObject);
    }
}