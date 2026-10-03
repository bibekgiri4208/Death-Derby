using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 80f;
    public float lifetime = 3f;
    public int damage = 10;
    public float castRadius = 0.12f;

    [Header("Effects")]
    public GameObject sparkPrefab;

    private Rigidbody rb;
    private Collider[] ignoredColliders;
    private TrailRenderer[] trails;
    private RaycastHit[] hits = new RaycastHit[16];
    private CombatPool pool;
    private GameObject sourcePrefab;
    private Vector3 direction;
    private float distanceTraveled;
    private float expiresAt;
    private float maxRange = 100f;
    private bool launched;
    private int shooterPlayerIndex = -1; // Used to credit the kill to the right player

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        trails = GetComponentsInChildren<TrailRenderer>(true);

        // Sweeps below are the single collision path, including zombie trigger colliders.
        // No dynamic Rigidbody simulation, CCD, or bullet-to-bullet contact pairs are needed.
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
    }

    internal void SetPool(CombatPool owner, GameObject prefab)
    {
        pool = owner;
        sourcePrefab = prefab;
    }

    public void Launch(Vector3 direction, Collider[] ownerColliders, float range, int playerIndex = -1)
    {
        ignoredColliders = ownerColliders;
        maxRange = range;
        shooterPlayerIndex = playerIndex;

        this.direction = direction.normalized;
        transform.forward = direction;
        if (rb != null)
        {
            rb.position = transform.position;
            rb.rotation = transform.rotation;
        }
        foreach (TrailRenderer trail in trails)
            trail.Clear();
        distanceTraveled = 0f;
        expiresAt = Time.time + lifetime;
        launched = true;
    }

    private void FixedUpdate()
    {
        if (!launched)
            return;

        if (Time.time >= expiresAt || distanceTraveled >= maxRange)
        {
            Release();
            return;
        }

        Vector3 position = rb != null ? rb.position : transform.position;
        float distance = Mathf.Min(speed * Time.fixedDeltaTime, maxRange - distanceTraveled);

        if (distance > 0.001f)
        {
            int hitCount;
            // Grow only when saturated so dense crowds never truncate the nearest valid hit.
            do
            {
                hitCount = Physics.SphereCastNonAlloc(position, castRadius, direction, hits,
                    distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
                if (hitCount < hits.Length)
                    break;
                System.Array.Resize(ref hits, hits.Length * 2);
            } while (true);

            int nearest = -1;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                if (!ShouldIgnore(hits[i].collider) && hits[i].distance < nearestDistance)
                {
                    nearest = i;
                    nearestDistance = hits[i].distance;
                }
            }
            if (nearest >= 0)
            {
                RaycastHit hit = hits[nearest];
                Hit(hit.collider.gameObject, hit.point, hit.normal);
                return;
            }
        }

        distanceTraveled += distance;
        Vector3 nextPosition = position + direction * distance;
        if (rb != null)
            rb.MovePosition(nextPosition);
        else
            transform.position = nextPosition;
    }

    private bool ShouldIgnore(Collider other)
    {
        if (other == null)
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

    private void Hit(GameObject hitObject, Vector3 point, Vector3 normal)
    {
        if (!launched)
            return;
        launched = false;

        // Finds ANY component on the hit object (or its parent) that implements IDamageable
        IDamageable damageable = hitObject.GetComponentInParent<IDamageable>();

        if (damageable != null)
        {
            damageable.TakeDamage(damage, shooterPlayerIndex);
        }

        if (sparkPrefab != null)
        {
            Quaternion rotation = Quaternion.LookRotation(normal.sqrMagnitude > 0.001f ? normal : -direction);
            CombatPool.SpawnEffect(sparkPrefab, point, rotation, 1f);
        }

        Release();
    }

    private void Release()
    {
        launched = false;
        ignoredColliders = null;
        foreach (TrailRenderer trail in trails)
            trail.Clear();
        if (pool != null && sourcePrefab != null)
            pool.ReturnBullet(this, sourcePrefab);
        else
            Destroy(gameObject);
    }
}
