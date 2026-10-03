using System.Collections.Generic;
using UnityEngine;

public class BloodDecal : MonoBehaviour
{
    public static int MaxDecals = 60;
    public static float Lifetime = 25f;

    private static readonly List<BloodDecal> ActiveDecals = new List<BloodDecal>();
    private static readonly Stack<BloodDecal> InactiveDecals = new Stack<BloodDecal>();
    private static readonly List<Texture2D> generatedTextures = new List<Texture2D>();
    private static Sprite[] cachedSprites;
    private static Transform poolRoot;
    private static RaycastHit[] surfaceHits = new RaycastHit[16];

    private SpriteRenderer spriteRenderer;
    private Color tint;
    private float spawnedTime;

    public static void Spawn(Vector3 deathPosition)
    {
        if (MaxDecals <= 0) return;
        EnsureAssets();
        EnsurePoolRoot();

        if (TryGetSurface(deathPosition, out Vector3 point, out Vector3 normal))
        {
            float mainSize = Random.Range(1f, 1.4f);
            CreateDecal(point, normal, mainSize);

            int extra = Random.Range(0, 3);
            Vector3 right = Vector3.Cross(normal, Vector3.up).normalized;
            if (right.sqrMagnitude < 0.0001f)
            {
                right = Vector3.Cross(normal, Vector3.right).normalized;
            }
            Vector3 forwardDir = Vector3.Cross(right, normal).normalized;

            for (int i = 0; i < extra; i++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float dist = Random.Range(0.4f, 1.3f);
                Vector3 offset = (Mathf.Cos(angle) * right + Mathf.Sin(angle) * forwardDir) * dist;
                CreateDecal(point + offset, normal, Random.Range(0.3f, 0.55f));
            }
        }
        else
        {
            CreateDecal(deathPosition, Vector3.up, Random.Range(1f, 1.4f));
        }
    }

    private static void CreateDecal(Vector3 point, Vector3 normal, float size)
    {
        // Recycle immediately so bursts of kills cannot exceed the limit in one frame.
        while (ActiveDecals.Count >= MaxDecals)
            ActiveDecals[0].Recycle();

        BloodDecal decal = null;
        while (InactiveDecals.Count > 0 && decal == null)
            decal = InactiveDecals.Pop();
        if (decal == null)
            decal = CreatePooledDecal();

        GameObject go = decal.gameObject;
        go.transform.position = point + normal * 0.02f;

        Vector3 forward = normal;
        Vector3 upHint = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) < 0.999f ? Vector3.up : Vector3.forward;
        Quaternion rot = Quaternion.LookRotation(forward, upHint);
        rot *= Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);
        go.transform.rotation = rot;

        go.transform.localScale = new Vector3(size, size, 1f);

        decal.spriteRenderer.sprite = cachedSprites[Random.Range(0, cachedSprites.Length)];
        decal.Init();
        ActiveDecals.Add(decal);
        go.SetActive(true);
    }

    public static void Prewarm()
    {
        EnsureAssets();
        EnsurePoolRoot();
        while (ActiveDecals.Count + InactiveDecals.Count < Mathf.Max(0, MaxDecals))
            InactiveDecals.Push(CreatePooledDecal());
    }

    private static void EnsurePoolRoot()
    {
        if (poolRoot != null) return;
        ActiveDecals.Clear();
        InactiveDecals.Clear();
        poolRoot = new GameObject("Blood Decal Pool").transform;
    }

    private static BloodDecal CreatePooledDecal()
    {
        EnsurePoolRoot();
        GameObject go = new GameObject("BloodDecal", typeof(SpriteRenderer));
        go.transform.SetParent(poolRoot, false);
        BloodDecal decal = go.AddComponent<BloodDecal>();
        decal.spriteRenderer = go.GetComponent<SpriteRenderer>();
        go.SetActive(false);
        return decal;
    }

    private void Init()
    {
        spawnedTime = Time.time;

        float jitter = Random.Range(0.85f, 1.15f);
        tint = new Color(0.42f * jitter, 0.04f * jitter, 0.02f * jitter, 1f);
        ApplyTint(1f);
    }

    private void Update()
    {
        float t = (Time.time - spawnedTime) / Lifetime;
        if (t >= 1f)
        {
            Recycle();
            return;
        }

        float fadeStart = 0.6f;
        float alpha = t <= fadeStart ? 1f : 1f - Mathf.InverseLerp(fadeStart, 1f, t);
        ApplyTint(alpha);
    }

    private void ApplyTint(float alpha)
    {
        if (spriteRenderer == null) return;
        spriteRenderer.color = new Color(tint.r, tint.g, tint.b, alpha);
    }

    private void OnDestroy()
    {
        ActiveDecals.Remove(this);
    }

    private void Recycle()
    {
        ActiveDecals.Remove(this);
        gameObject.SetActive(false);
        if (InactiveDecals.Count < Mathf.Max(0, MaxDecals))
            InactiveDecals.Push(this);
        else
            Destroy(gameObject);
    }

    private static bool TryGetSurface(Vector3 pos, out Vector3 point, out Vector3 normal)
    {
        const float maxDist = 8f;
        Vector3 origin = pos + Vector3.up * 0.15f;

        int hitCount;
        do
        {
            hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, surfaceHits, maxDist,
                ~0, QueryTriggerInteraction.Ignore);
            if (hitCount < surfaceHits.Length) break;
            System.Array.Resize(ref surfaceHits, surfaceHits.Length * 2);
        } while (true);

        int nearest = -1;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            Collider c = surfaceHits[i].collider;
            if (c == null || c.transform.root.CompareTag("Player")) continue;
            if (surfaceHits[i].distance < nearestDistance)
            {
                nearest = i;
                nearestDistance = surfaceHits[i].distance;
            }
        }
        if (nearest >= 0)
        {
            point = surfaceHits[nearest].point;
            normal = surfaceHits[nearest].normal;
            return true;
        }

        point = pos;
        normal = Vector3.up;
        return false;
    }

    private static void EnsureAssets()
    {
        if (cachedSprites != null && cachedSprites[0] != null) return;

        int size = BloodSplatSource.SplatTextureSize;
        cachedSprites = new Sprite[BloodSplatSource.SplatTextureVariants];
        for (int i = 0; i < cachedSprites.Length; i++)
        {
            Texture2D texture = Resources.Load<Texture2D>("Effects/BloodSplats/BloodSplat_" + i);
            if (texture == null)
            {
                // Prewarm before zombies appear, keeping the fallback off the kill path.
                texture = BloodSplatSource.GenerateSplatTexture(i);
                generatedTextures.Add(texture);
            }
            cachedSprites[i] = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        if (cachedSprites != null)
        {
            foreach (Sprite sprite in cachedSprites)
            {
                if (sprite != null) Destroy(sprite);
            }
        }
        foreach (Texture2D texture in generatedTextures)
        {
            if (texture != null) Destroy(texture);
        }
        generatedTextures.Clear();
        cachedSprites = null;
        poolRoot = null;
        ActiveDecals.Clear();
        InactiveDecals.Clear();
    }
}
