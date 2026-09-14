using System.Collections.Generic;
using UnityEngine;

public class BloodDecal : MonoBehaviour
{
    public static int MaxDecals = 60;
    public static float Lifetime = 25f;

    private const int SplatTextureSize = 256;
    private const int SplatTextureVariants = 6;

    private static readonly List<BloodDecal> ActiveDecals = new List<BloodDecal>();
    private static List<Texture2D> cachedTextures;
    private static bool assetsReady;

    private SpriteRenderer spriteRenderer;
    private Sprite sprite;
    private Color tint;
    private float spawnedTime;

    public static void Spawn(Vector3 deathPosition)
    {
        EnsureAssets();

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
        if (!assetsReady || cachedTextures == null || cachedTextures.Count == 0) return;

        GameObject go = new GameObject("BloodDecal");
        go.transform.position = point + normal * 0.02f;

        Vector3 forward = normal;
        Vector3 upHint = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) < 0.999f ? Vector3.up : Vector3.forward;
        Quaternion rot = Quaternion.LookRotation(forward, upHint);
        rot *= Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);
        go.transform.rotation = rot;

        go.transform.localScale = new Vector3(size, size, 1f);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = Sprite.Create(
            PickSplatTexture(),
            new Rect(0, 0, SplatTextureSize, SplatTextureSize),
            new Vector2(0.5f, 0.5f),
            SplatTextureSize);

        BloodDecal decal = go.AddComponent<BloodDecal>();
        decal.Init(renderer);
        ActiveDecals.Add(decal);

        if (ActiveDecals.Count > MaxDecals)
        {
            BloodDecal oldest = ActiveDecals[0];
            if (oldest != decal && oldest != null)
            {
                Destroy(oldest.gameObject);
            }
        }
    }

    private void Init(SpriteRenderer renderer)
    {
        spriteRenderer = renderer;
        sprite = renderer.sprite;
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
            Destroy(gameObject);
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
        if (sprite != null) Destroy(sprite);
    }

    private static bool TryGetSurface(Vector3 pos, out Vector3 point, out Vector3 normal)
    {
        const float maxDist = 8f;
        Vector3 origin = pos + Vector3.up * 0.15f;

        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, maxDist, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i].collider;
            if (c == null || c.transform.root.CompareTag("Player")) continue;

            point = hits[i].point;
            normal = hits[i].normal;
            return true;
        }

        point = pos;
        normal = Vector3.up;
        return false;
    }

    private static Texture2D PickSplatTexture()
    {
        return cachedTextures[Random.Range(0, cachedTextures.Count)];
    }

    private static void EnsureAssets()
    {
        if (assetsReady) return;

        cachedTextures = new List<Texture2D>(SplatTextureVariants);
        for (int i = 0; i < SplatTextureVariants; i++)
        {
            cachedTextures.Add(GenerateSplatTexture(i));
        }

        assetsReady = true;
    }

    private static float Smoothstep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    private static Texture2D GenerateSplatTexture(int index)
    {
        const int size = SplatTextureSize;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        System.Random rng = new System.Random(1051 + index * 997);

        float p1 = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        float p2 = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        float p3 = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        float p4 = (float)(rng.NextDouble() * Mathf.PI * 2.0);

        int dropletCount = 6 + rng.Next(5);
        float[] dropletAngle = new float[dropletCount];
        float[] dropletDist = new float[dropletCount];
        float[] dropletRadius = new float[dropletCount];

        for (int i = 0; i < dropletCount; i++)
        {
            dropletAngle[i] = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            dropletDist[i] = 0.35f + (float)(rng.NextDouble() * 0.28f);
            dropletRadius[i] = 0.025f + (float)(rng.NextDouble() * 0.06f);
        }

        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float v = (y + 0.5f) / size;
                float dx = u - 0.5f;
                float dy = v - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);

                float boundary = 0.38f
                    + 0.085f * Mathf.Sin(2f * ang + p1)
                    + 0.055f * Mathf.Sin(3f * ang + p2)
                    + 0.04f * Mathf.Sin(5f * ang + p3)
                    + 0.02f * Mathf.Sin(7f * ang + p4);

                float baseCoverage = 1f - Smoothstep(boundary * 0.82f, boundary, r);

                float drop = 0f;
                for (int i = 0; i < dropletCount; i++)
                {
                    float cosA = Mathf.Cos(dropletAngle[i]);
                    float sinA = Mathf.Sin(dropletAngle[i]);
                    float offX = dx - cosA * dropletDist[i];
                    float offY = dy - sinA * dropletDist[i];
                    float dd = Mathf.Sqrt(offX * offX + offY * offY);
                    float hit = 1f - Smoothstep(dropletRadius[i] * 0.45f, dropletRadius[i], dd);
                    drop = Mathf.Max(drop, hit);
                }

                float coverage = Mathf.Clamp01(baseCoverage + drop * 0.95f);
                byte a = (byte)(Mathf.Clamp01(coverage * 0.95f) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}