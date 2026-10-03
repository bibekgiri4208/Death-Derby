using UnityEngine;

/// <summary>
/// Procedural blood splat shapes used by <see cref="BloodDecal"/>.
///
/// The splats are deterministic: the same <c>index</c> always produces the same
/// image. That lets the editor baker (BloodSplatBaker) write them out as PNGs so
/// gameplay never has to pay for the generation, while <see cref="BloodDecal"/>
/// still keeps the runtime path as a fallback if the baked assets are missing.
///
/// Generation is only ever done off the gameplay critical path (editor bake or a
/// warm-up during loading), so the cost here matters far less than the pixel-loop
/// structure: the per-pixel inner loop below deliberately hoists the droplet
/// trig out of the loop and rejects droplets by bounding box before the sqrt,
/// because a droplet only covers a couple of percent of the texture.
/// </summary>
public static class BloodSplatSource
{
    public const int SplatTextureSize = 256;
    public const int SplatTextureVariants = 6;

    private const float SeedBase = 1051f;
    private const int SeedStride = 997;

    /// <summary>Builds one splat variant as raw RGBA pixels, white with a splat-shaped alpha.</summary>
    public static Color32[] GenerateSplat(int index)
    {
        int size = SplatTextureSize;
        System.Random rng = new System.Random((int)SeedBase + index * SeedStride);

        float p1 = NextAngle(rng);
        float p2 = NextAngle(rng);
        float p3 = NextAngle(rng);
        float p4 = NextAngle(rng);

        int dropletCount = 6 + rng.Next(5);
        var droplets = new Droplet[dropletCount];

        for (int i = 0; i < dropletCount; i++)
        {
            float angle = NextAngle(rng);
            float dist = 0.35f + (float)(rng.NextDouble() * 0.28f);
            float radius = 0.025f + (float)(rng.NextDouble() * 0.06f);

            // Trig is loop-invariant with respect to pixels, so resolve it here.
            float cosA = Mathf.Cos(angle);
            float sinA = Mathf.Sin(angle);

            droplets[i] = new Droplet
            {
                offsetX = cosA * dist,
                offsetY = sinA * dist,
                radius = radius,
                innerRadius = radius * 0.45f,
                minX = ((cosA * dist - radius) + 0.5f) * size - 1f,
                maxX = ((cosA * dist + radius) + 0.5f) * size + 1f,
                minY = ((sinA * dist - radius) + 0.5f) * size - 1f,
                maxY = ((sinA * dist + radius) + 0.5f) * size + 1f
            };
        }

        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            float v = (y + 0.5f) / size;
            float dy = v - 0.5f;

            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float dx = u - 0.5f;
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
                    Droplet d = droplets[i];

                    // Cheap integer reject: most droplets miss this pixel entirely.
                    if (x < d.minX || x > d.maxX || y < d.minY || y > d.maxY)
                        continue;

                    float offX = dx - d.offsetX;
                    float offY = dy - d.offsetY;
                    float dd = Mathf.Sqrt(offX * offX + offY * offY);
                    float hit = 1f - Smoothstep(d.innerRadius, d.radius, dd);
                    drop = Mathf.Max(drop, hit);
                }

                float coverage = Mathf.Clamp01(baseCoverage + drop * 0.95f);
                byte a = (byte)(Mathf.Clamp01(coverage * 0.95f) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        return pixels;
    }

    /// <summary>Builds one splat variant as a ready-to-use texture.</summary>
    public static Texture2D GenerateSplatTexture(int index)
    {
        Texture2D tex = new Texture2D(SplatTextureSize, SplatTextureSize, TextureFormat.RGBA32, false)
        {
            name = "BloodSplat_" + index,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        tex.SetPixels32(GenerateSplat(index));
        tex.Apply();
        return tex;
    }

    private static float NextAngle(System.Random rng)
    {
        return (float)(rng.NextDouble() * Mathf.PI * 2.0);
    }

    private static float Smoothstep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    private struct Droplet
    {
        public float offsetX;
        public float offsetY;
        public float radius;
        public float innerRadius;
        public float minX;
        public float maxX;
        public float minY;
        public float maxY;
    }
}