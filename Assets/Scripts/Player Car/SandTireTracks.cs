using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Scene-local, bounded tyre ribbons. All cars share one mesh; fading happens on the GPU.</summary>
public sealed class SandTireTracks : MonoBehaviour
{
    private const int Capacity = 8192;
    private const float Lifetime = 75f;
    private static SandTireTracks instance;

    private readonly Vector3[] vertices = new Vector3[Capacity * 4];
    private readonly Color32[] colors = new Color32[Capacity * 4];
    private readonly Vector2[] uvs = new Vector2[Capacity * 4];
    private readonly Vector2[] timesAndSlip = new Vector2[Capacity * 4];
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock properties;
    private int cursor;
    private bool dirty;
    private bool hasBounds;
    private Bounds trackBounds;

    public static SandTireTracks GetOrCreate()
    {
        if (instance != null) return instance;
        var material = Resources.Load<Material>("Effects/SandTireTracks");
        if (material == null) return null;

        var host = new GameObject("Sand Tyre Tracks", typeof(MeshFilter), typeof(MeshRenderer));
        instance = host.AddComponent<SandTireTracks>();
        instance.Initialize(material);
        return instance;
    }

    private void Initialize(Material material)
    {
        mesh = new Mesh { name = "Sand Tyre Ribbon Buffer" };
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        mesh.colors32 = colors;
        mesh.uv = uvs;
        mesh.uv2 = timesAndSlip;
        var indices = new int[Capacity * 6];
        for (int i = 0; i < Capacity; i++)
        {
            int v = i * 4;
            int t = i * 6;
            indices[t] = v;
            indices[t + 1] = v + 2;
            indices[t + 2] = v + 1;
            indices[t + 3] = v + 2;
            indices[t + 4] = v + 3;
            indices[t + 5] = v + 1;
        }
        mesh.triangles = indices;
        GetComponent<MeshFilter>().sharedMesh = mesh;
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        properties = new MaterialPropertyBlock();
    }

    public void AddSegment(Vector3 from, Vector3 to, Vector3 fromNormal, Vector3 toNormal,
        Vector3 fromRight, Vector3 toRight, float width, float distance, float length, float opacity, float slip)
    {
        int v = cursor * 4;
        cursor = (cursor + 1) % Capacity;
        Vector3 a = from + fromNormal * 0.012f;
        Vector3 b = to + toNormal * 0.012f;
        float halfWidth = width * 0.5f;
        vertices[v] = a - fromRight * halfWidth;
        vertices[v + 1] = a + fromRight * halfWidth;
        vertices[v + 2] = b - toRight * halfWidth;
        vertices[v + 3] = b + toRight * halfWidth;
        uvs[v] = new Vector2(0, distance);
        uvs[v + 1] = new Vector2(1, distance);
        uvs[v + 2] = new Vector2(0, distance + length);
        uvs[v + 3] = new Vector2(1, distance + length);
        Color32 color = new Color(1, 1, 1, Mathf.Clamp01(opacity));
        Vector2 timing = new Vector2(Time.time, Mathf.Clamp01(slip));
        for (int i = 0; i < 4; i++)
        {
            colors[v + i] = color;
            timesAndSlip[v + i] = timing;
            if (!hasBounds)
            {
                trackBounds = new Bounds(vertices[v + i], Vector3.one);
                hasBounds = true;
            }
            else trackBounds.Encapsulate(vertices[v + i]);
        }
        dirty = true;
    }

    private void LateUpdate()
    {
        if (mesh == null) return;
        if (dirty)
        {
            // Reuse the buffers, and upload at most once per rendered frame, not once per wheel.
            mesh.vertices = vertices;
            mesh.colors32 = colors;
            mesh.uv = uvs;
            mesh.uv2 = timesAndSlip;
            mesh.bounds = trackBounds;
            dirty = false;
        }
        properties.SetFloat("_TrackTime", Time.time);
        properties.SetFloat("_TrackLifetime", Lifetime);
        meshRenderer.SetPropertyBlock(properties);
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (instance == this) instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;
}
