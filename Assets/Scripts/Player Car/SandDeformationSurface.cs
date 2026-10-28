using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Bounded, scene-local heightfield meshes replace the visible sand under tyre paths.
/// TerrainData, TerrainCollider, navigation and shared material assets are never edited.
/// </summary>
[DisallowMultipleComponent, RequireComponent(typeof(Terrain))]
public sealed class SandDeformationSurface : MonoBehaviour
{
    public const int MaximumPatches = 32;
    public const float PatchSize = 8f;
    public const int PatchSegments = 80;
    public const float Lifetime = 90f;
    private const float Step = PatchSize / PatchSegments;
    private const int Samples = PatchSegments + 3; // One ghost sample on each edge for seamless normals.
    private const int CoverageColumns = 8;
    private const int CoverageRows = MaximumPatches / CoverageColumns;
    private const string SandShader = "Death Derby/Training Sand";
    private static readonly int MaskId = Shader.PropertyToID("_SandPatchMask");
    private static readonly int CoverageId = Shader.PropertyToID("_SandPatchCoverage");
    private static readonly int WorldId = Shader.PropertyToID("_SandPatchWorld");
    private static readonly int EnabledId = Shader.PropertyToID("_SandPatchEnabled");
    private static readonly int SurfaceId = Shader.PropertyToID("_SandPatchSurface");
    private static readonly ProfilerMarker CreateMarker = new ProfilerMarker("SandDeformation.CreatePatch");
    private static readonly ProfilerMarker StampMarker = new ProfilerMarker("SandDeformation.Stamp");
    private static readonly ProfilerMarker UploadMarker = new ProfilerMarker("SandDeformation.UploadChangedArea");
    private static readonly ProfilerMarker FadeMarker = new ProfilerMarker("SandDeformation.FadePatch");

    private readonly Dictionary<int, Patch> patches = new Dictionary<int, Patch>(MaximumPatches);
    private readonly List<int> expired = new List<int>(MaximumPatches);
    private readonly List<Patch> active = new List<Patch>(MaximumPatches);
    private readonly Stack<Patch> pool = new Stack<Patch>(MaximumPatches);
    private Terrain terrain;
    private Material originalMaterial, groundMaterial, patchMaterial;
    private Texture2D mask;
    private byte[] maskPixels;
    private Texture2D coverage;
    private byte[] coveragePixels;
    private float[,] heights;
    private Vector3 origin, size;
    private int columns, rows, creationFrame = -1, creations;
    private int fadeCursor;
    private bool maskDirty;
    private bool coverageDirty;

    public int ActivePatchCount => patches.Count;

    public static SandDeformationSurface GetOrCreate(TerrainCollider collider)
    {
        if (collider == null) return null;
        var surface = collider.GetComponent<SandDeformationSurface>();
        if (surface != null) return surface.isActiveAndEnabled && surface.Initialize() ? surface : null;
        var terrain = collider.GetComponent<Terrain>();
        if (terrain == null || !Supports(terrain.materialTemplate)) return null;
        surface = terrain.gameObject.AddComponent<SandDeformationSurface>();
        surface.hideFlags = HideFlags.DontSave;
        if (surface.Initialize()) return surface;
        CoreUtils.Destroy(surface);
        return null;
    }

    private static bool Supports(Material material)
    {
        if (material == null || material.shader == null) return false;
        if (material.shader.name == SandShader) return true;
        // Desert's existing untextured URP/Lit sand can be reproduced without
        // recolouring it. Textured/metallic/transparent terrains retain their old tracks.
        return material.shader.name == "Universal Render Pipeline/Lit"
            && material.GetTexture("_BaseMap") == null && material.GetTexture("_BumpMap") == null
            && material.GetFloat("_Metallic") == 0 && material.GetFloat("_Surface") == 0
            && material.GetFloat("_WorkflowMode") == 1 && material.GetFloat("_ReceiveShadows") != 0
            && material.GetColor("_EmissionColor").maxColorComponent == 0;
    }

    private bool Initialize()
    {
        if (groundMaterial != null) return true;
        terrain = GetComponent<Terrain>();
        if (terrain == null || terrain.terrainData == null || !Supports(terrain.materialTemplate)
            || terrain.transform.lossyScale != Vector3.one || terrain.transform.rotation != Quaternion.identity) return false;
        originalMaterial = terrain.materialTemplate;
        Material template = originalMaterial.shader.name == SandShader ? originalMaterial
            : Resources.Load<Material>("Effects/DeformableSand");
        if (template == null) return false;
        size = terrain.terrainData.size;
        origin = terrain.transform.position;
        columns = Mathf.CeilToInt(size.x / PatchSize);
        rows = Mathf.CeilToInt(size.z / PatchSize);
        if (columns < 1 || rows < 1 || columns > 512 || rows > 512) return false;
        heights = terrain.terrainData.GetHeights(0, 0, terrain.terrainData.heightmapResolution, terrain.terrainData.heightmapResolution);
        groundMaterial = new Material(template) { name = originalMaterial.name + " (Runtime Interactive Sand)", hideFlags = HideFlags.DontSave };
        if (template != originalMaterial)
        {
            Color colour = originalMaterial.GetColor("_BaseColor");
            groundMaterial.SetColor("_BaseColor", colour);
            groundMaterial.SetColor("_LowColor", colour);
            groundMaterial.SetFloat("_Smoothness", originalMaterial.GetFloat("_Smoothness"));
        }
        maskPixels = new byte[columns * rows];
        mask = new Texture2D(columns, rows, TextureFormat.R8, false, true) {
            name = "Runtime Sand Patch Mask", filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave
        };
        mask.SetPixelData(maskPixels, 0);
        mask.Apply(false, false);
        // Fine coverage is allocated only for the bounded mesh cache, not for
        // every ten-centimetre cell of the entire terrain. The coarse mask stores
        // slot + 1 (zero means no patch), so neighbouring slots need not be adjacent.
        coveragePixels = new byte[CoverageColumns * CoverageRows * PatchSegments * PatchSegments];
        coverage = new Texture2D(CoverageColumns * PatchSegments, CoverageRows * PatchSegments, TextureFormat.R8, false, true) {
            name = "Runtime Sand Deformation Coverage", filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave
        };
        coverage.SetPixelData(coveragePixels, 0);
        coverage.Apply(false, false);
        groundMaterial.SetTexture(MaskId, mask);
        groundMaterial.SetTexture(CoverageId, coverage);
        groundMaterial.SetVector(WorldId, new Vector4(origin.x, origin.z, 1f / (columns * PatchSize), 1f / (rows * PatchSize)));
        groundMaterial.SetFloat(EnabledId, 1);
        groundMaterial.SetFloat(SurfaceId, 0);
        patchMaterial = new Material(groundMaterial) { name = "Displaced Sand Surface", hideFlags = HideFlags.DontSave };
        patchMaterial.SetFloat(SurfaceId, 1);
        terrain.materialTemplate = groundMaterial;
        return true;
    }

    public void DeformSegment(Vector3 from, Vector3 to, Vector3 right, float width, float depth, float pileHeight, float sidewaysSlip)
    {
        if (!Initialize() || !Finite(from) || !Finite(to) || !Finite(right)
            || !Finite(width) || !Finite(depth) || !Finite(pileHeight) || !Finite(sidewaysSlip)
            || (to - from).sqrMagnitude > 3.5f * 3.5f) return;
        Vector2 a = new Vector2(from.x - origin.x, from.z - origin.z);
        Vector2 b = new Vector2(to.x - origin.x, to.z - origin.z);
        if (b.x < 0 || b.y < 0 || b.x > size.x || b.y > size.z) return;
        Vector2 across = new Vector2(right.x, right.z).normalized;
        float halfWidth = Mathf.Clamp(width, 0.15f, 0.85f) * 0.5f;
        float radius = halfWidth * 2.6f;
        depth = Mathf.Clamp(depth, 0, 0.08f);
        pileHeight = Mathf.Clamp(pileHeight, 0, 0.06f);
        sidewaysSlip = Mathf.Clamp(sidewaysSlip, -1, 1);
        float now = Time.time;
        int minX = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x, b.x) - radius - Step) / PatchSize), 0, columns - 1);
        int maxX = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(a.x, b.x) + radius + Step) / PatchSize), 0, columns - 1);
        int minZ = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.y, b.y) - radius - Step) / PatchSize), 0, rows - 1);
        int maxZ = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(a.y, b.y) + radius + Step) / PatchSize), 0, rows - 1);
        for (int z = minZ; z <= maxZ; z++)
        for (int x = minX; x <= maxX; x++)
        {
            Patch patch = Acquire(x, z, b, now);
            if (patch != null)
            {
                using (StampMarker.Auto()) patch.Stamp(a, b, across, halfWidth, depth, pileHeight, sidewaysSlip, now);
            }
        }
    }

    private Patch Acquire(int x, int z, Vector2 contact, float now)
    {
        int key = x + z * columns;
        if (patches.TryGetValue(key, out var patch)) return patch;
        if (creationFrame != Time.frameCount) { creationFrame = Time.frameCount; creations = 0; }
        // Mesh allocation/sampling is bounded as well as total retained geometry.
        if (creations >= 2) return null;
        if (patches.Count >= MaximumPatches)
        {
            int oldest = -1;
            float oldestTime = now - 3;
            Camera camera = Camera.main;
            foreach (var entry in patches)
            {
                Vector3 centre = entry.Value.host.transform.position + new Vector3(PatchSize * 0.5f, 0, PatchSize * 0.5f);
                Vector2 delta = new Vector2(centre.x - origin.x, centre.z - origin.z) - contact;
                if (entry.Value.lastTouch >= oldestTime || delta.sqrMagnitude < 24 * 24
                    || (camera != null && (centre - camera.transform.position).sqrMagnitude < 40 * 40)) continue;
                oldest = entry.Key;
                oldestTime = entry.Value.lastTouch;
            }
            if (oldest < 0) return null;
            Remove(oldest);
        }
        using (CreateMarker.Auto())
        {
            if (pool.Count > 0)
            {
                patch = pool.Pop();
                patch.Reset(this, x, z, now);
            }
            else patch = new Patch(this, x, z, now, patches.Count);
        }
        patches.Add(key, patch);
        active.Add(patch);
        maskPixels[key] = (byte)(patch.slot + 1);
        maskDirty = true;
        creations++;
        return patch;
    }

    private float BaseHeight(float x, float z)
    {
        int resolution = heights.GetLength(0);
        float u = Mathf.Clamp01(x / size.x) * (resolution - 1);
        float v = Mathf.Clamp01(z / size.z) * (resolution - 1);
        int ix = Mathf.Min((int)u, resolution - 2), iz = Mathf.Min((int)v, resolution - 2);
        return Mathf.Lerp(Mathf.Lerp(heights[iz, ix], heights[iz, ix + 1], u - ix),
            Mathf.Lerp(heights[iz + 1, ix], heights[iz + 1, ix + 1], u - ix), v - iz) * size.y;
    }

    private bool TryFlatHeight(float x, float z, out float height)
    {
        int resolution = heights.GetLength(0);
        // Include the source samples used by the boundary normals as well.
        int minX = Mathf.Max(0, Mathf.FloorToInt(x / size.x * (resolution - 1)) - 2);
        int minZ = Mathf.Max(0, Mathf.FloorToInt(z / size.z * (resolution - 1)) - 2);
        int maxX = Mathf.Min(resolution - 1, Mathf.CeilToInt((x + PatchSize) / size.x * (resolution - 1)) + 2);
        int maxZ = Mathf.Min(resolution - 1, Mathf.CeilToInt((z + PatchSize) / size.z * (resolution - 1)) + 2);
        float first = heights[minZ, minX];
        height = first * size.y;
        for (int iz = minZ; iz <= maxZ; iz++)
        for (int ix = minX; ix <= maxX; ix++)
            if (heights[iz, ix] != first) return false;
        return true;
    }

    private void CopySample(float x, float z, out float displacement, out float time)
    {
        displacement = 0; time = 0;
        int cx = Mathf.FloorToInt(x / PatchSize), cz = Mathf.FloorToInt(z / PatchSize);
        for (int dz = -1; dz <= 1; dz++)
        for (int dx = -1; dx <= 1; dx++)
        {
            int tx = cx + dx, tz = cz + dz;
            if (tx < 0 || tz < 0 || tx >= columns || tz >= rows
                || !patches.TryGetValue(tx + tz * columns, out var patch)) continue;
            int ix = Mathf.RoundToInt((x - patch.x) / Step) + 1;
            int iz = Mathf.RoundToInt((z - patch.z) / Step) + 1;
            if (ix < 0 || iz < 0 || ix >= Samples || iz >= Samples) continue;
            int index = ix + iz * Samples;
            if (patch.times[index] > time || (patch.times[index] == time && Mathf.Abs(patch.offsets[index]) > Mathf.Abs(displacement)))
            {
                displacement = patch.offsets[index];
                time = patch.times[index];
            }
        }
    }

    private void LateUpdate()
    {
        if (groundMaterial == null) return;
        float now = Time.time;
        // A whole-cache fade used to rebuild 210,000 vertices in the same frame.
        // Choose at most one full fade; tyre contacts still update every frame.
        Patch fade = null;
        for (int attempt = 0; attempt < active.Count; attempt++)
        {
            fadeCursor %= active.Count;
            Patch candidate = active[fadeCursor++];
            if (now - candidate.lastTouch <= Lifetime && candidate.ShouldFade(now)) { fade = candidate; break; }
        }
        foreach (var entry in patches)
        {
            if (now - entry.Value.lastTouch > Lifetime) expired.Add(entry.Key);
            else if (entry.Value == fade)
            {
                using (FadeMarker.Auto()) entry.Value.UploadFull(now);
            }
            else if (entry.Value.IsDirty)
            {
                using (UploadMarker.Auto()) entry.Value.Upload(now);
            }
        }
        foreach (int key in expired) Remove(key);
        expired.Clear();
        if (coverageDirty)
        {
            coverage.SetPixelData(coveragePixels, 0);
            coverage.Apply(false, false);
            coverageDirty = false;
        }
        if (maskDirty)
        {
            mask.SetPixelData(maskPixels, 0);
            mask.Apply(false, false);
            maskDirty = false;
        }
    }

    private void Remove(int key)
    {
        Patch patch = patches[key];
        patch.ClearCoverage();
        patch.host.SetActive(false);
        active.Remove(patch);
        pool.Push(patch);
        patches.Remove(key);
        maskPixels[key] = 0;
        maskDirty = true;
    }

    private void OnDisable()
    {
        if (terrain != null && terrain.materialTemplate == groundMaterial) terrain.materialTemplate = originalMaterial;
        foreach (var patch in patches.Values) patch.Dispose();
        foreach (var patch in pool) patch.Dispose();
        patches.Clear();
        expired.Clear();
        active.Clear();
        pool.Clear();
        fadeCursor = 0;
        CoreUtils.Destroy(patchMaterial);
        CoreUtils.Destroy(groundMaterial);
        CoreUtils.Destroy(mask);
        CoreUtils.Destroy(coverage);
        patchMaterial = groundMaterial = null;
        mask = null; heights = null; maskPixels = null;
        coverage = null; coveragePixels = null;
        maskDirty = false;
        coverageDirty = false;
        creationFrame = -1;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);

    private sealed class Patch
    {
        private const MeshUpdateFlags UploadFlags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;
        private static readonly VertexAttributeDescriptor[] VertexLayout = {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 2)
        };
        private static readonly ushort[] Indices = CreateIndices();
        public readonly GameObject host;
        public readonly int slot;
        private readonly SandDeformationSurface owner;
        public float x, z;
        public readonly float[] offsets = new float[Samples * Samples];
        public readonly float[] times = new float[Samples * Samples];
        public float lastTouch;
        private readonly Mesh mesh;
        private readonly Vector3[] vertices = new Vector3[(PatchSegments + 1) * (PatchSegments + 1)];
        private readonly Vector3[] normals = new Vector3[(PatchSegments + 1) * (PatchSegments + 1)];
        private readonly float[] baseHeights = new float[(PatchSegments + 1) * (PatchSegments + 1)];
        private readonly Vector3[] baseNormals = new Vector3[(PatchSegments + 1) * (PatchSegments + 1)];
        private readonly Vector2[] uv = new Vector2[(PatchSegments + 1) * (PatchSegments + 1)];
        private float firstTouch;
        private float nextRefresh;
        private int minDirtyX, minDirtyZ, maxDirtyX = -1, maxDirtyZ = -1;

        public bool IsDirty => maxDirtyX >= minDirtyX && maxDirtyZ >= minDirtyZ;
        public bool ShouldFade(float now) => now >= nextRefresh && now - firstTouch >= Lifetime * 0.65f;

        public Patch(SandDeformationSurface owner, int tileX, int tileZ, float now, int slot)
        {
            this.owner = owner;
            this.slot = slot;
            mesh = new Mesh { name = "Displaced Sand Heightfield", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            mesh.SetVertexBufferParams(vertices.Length, VertexLayout);
            mesh.SetIndexBufferParams(Indices.Length, IndexFormat.UInt16);
            mesh.SetIndexBufferData(Indices, 0, 0, Indices.Length, UploadFlags);
            mesh.subMeshCount = 1;
            host = new GameObject("Interactive Sand Patch", typeof(MeshFilter), typeof(MeshRenderer)) {
                layer = owner.gameObject.layer, hideFlags = HideFlags.DontSave
            };
            host.transform.SetParent(owner.transform, false);
            host.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = host.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = owner.patchMaterial;
            renderer.shadowCastingMode = owner.terrain.shadowCastingMode;
            renderer.receiveShadows = true;
            // Terrain uses the global ambient probe. Per-object probes on these
            // replacement tiles create visibly brighter/darker 8-metre squares.
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = owner.terrain.reflectionProbeUsage;
            // Tiles are static ground, including after pool reuse. Treating them
            // as moving objects gives whole rectangles a different motion blur.
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
            // Baked occlusion still describes the undeformed terrain surface.
            renderer.allowOcclusionWhenDynamic = false;
            renderer.renderingLayerMask = owner.terrain.renderingLayerMask;
            renderer.lightmapIndex = owner.terrain.lightmapIndex;
            renderer.lightmapScaleOffset = owner.terrain.lightmapScaleOffset;
            Reset(owner, tileX, tileZ, now);
        }

        public void Reset(SandDeformationSurface owner, int tileX, int tileZ, float now)
        {
            ClearCoverage();
            x = tileX * PatchSize; z = tileZ * PatchSize;
            lastTouch = firstTouch = now;
            System.Array.Clear(offsets, 0, offsets.Length);
            System.Array.Clear(times, 0, times.Length);
            bool copiedDeformation = false;
            for (int iz = 0; iz < Samples; iz++)
            for (int ix = 0; ix < Samples; ix++)
            {
                // Neighbouring patches overlap only at their boundary/ghost samples.
                // Interior lookups used to perform ~60,000 unnecessary dictionary probes.
                if (ix > 2 && ix < Samples - 3 && iz > 2 && iz < Samples - 3) continue;
                owner.CopySample(x + (ix - 1) * Step, z + (iz - 1) * Step, out offsets[ix + iz * Samples], out times[ix + iz * Samples]);
                if (Mathf.Abs(offsets[ix + iz * Samples]) > 0.00001f)
                {
                    firstTouch = Mathf.Min(firstTouch, times[ix + iz * Samples]);
                    copiedDeformation = true;
                }
            }
            float dx = owner.size.x / (owner.heights.GetLength(0) - 1);
            float dz = owner.size.z / (owner.heights.GetLength(0) - 1);
            bool flat = owner.TryFlatHeight(x, z, out float flatHeight);
            float minHeight = float.MaxValue, maxHeight = float.MinValue;
            for (int iz = 0; iz <= PatchSegments; iz++)
            for (int ix = 0; ix <= PatchSegments; ix++)
            {
                int index = ix + iz * (PatchSegments + 1);
                float px = Mathf.Min(x + ix * Step, owner.size.x), pz = Mathf.Min(z + iz * Step, owner.size.z);
                baseHeights[index] = flat ? flatHeight : owner.BaseHeight(px, pz);
                baseNormals[index] = flat ? Vector3.up : new Vector3(
                    (owner.BaseHeight(px - dx, pz) - owner.BaseHeight(px + dx, pz)) / (2 * dx), 1,
                    (owner.BaseHeight(px, pz - dz) - owner.BaseHeight(px, pz + dz)) / (2 * dz)).normalized;
                vertices[index] = new Vector3(px - x, baseHeights[index], pz - z);
                normals[index] = baseNormals[index];
                uv[index] = new Vector2(px / owner.size.x, pz / owner.size.z);
                minHeight = Mathf.Min(minHeight, baseHeights[index]);
                maxHeight = Mathf.Max(maxHeight, baseHeights[index]);
            }
            if (copiedDeformation) UpdateVertices(now, 0, 0, PatchSegments, PatchSegments);
            if (copiedDeformation) UpdateCoverage(0, 0, PatchSegments, PatchSegments);
            // Bounds include every possible rut/berm, so no full vertex scan is needed
            // when a tyre changes a few centimetres of the surface.
            mesh.bounds = new Bounds(new Vector3(PatchSize * 0.5f, (minHeight + maxHeight) * 0.5f, PatchSize * 0.5f),
                new Vector3(PatchSize, maxHeight - minHeight + 0.17f, PatchSize));
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, Indices.Length) {
                firstVertex = 0, vertexCount = vertices.Length, bounds = mesh.bounds
            }, UploadFlags);
            mesh.SetVertexBufferData(vertices, 0, 0, vertices.Length, 0, UploadFlags);
            mesh.SetVertexBufferData(normals, 0, 0, normals.Length, 1, UploadFlags);
            mesh.SetVertexBufferData(uv, 0, 0, uv.Length, 2, UploadFlags);
            host.transform.localPosition = new Vector3(x, 0, z);
            ClearDirty();
            nextRefresh = now + 0.1f;
            host.SetActive(true);
        }

        private static ushort[] CreateIndices()
        {
            var triangles = new ushort[PatchSegments * PatchSegments * 6];
            for (int iz = 0; iz < PatchSegments; iz++)
            for (int ix = 0; ix < PatchSegments; ix++)
            {
                int v = ix + iz * (PatchSegments + 1), t = (ix + iz * PatchSegments) * 6;
                triangles[t] = (ushort)v; triangles[t + 1] = (ushort)(v + PatchSegments + 1); triangles[t + 2] = (ushort)(v + 1);
                triangles[t + 3] = (ushort)(v + 1); triangles[t + 4] = (ushort)(v + PatchSegments + 1); triangles[t + 5] = (ushort)(v + PatchSegments + 2);
            }
            return triangles;
        }

        public void Stamp(Vector2 a, Vector2 b, Vector2 across, float halfWidth, float depth, float pileHeight, float slip, float now)
        {
            float radius = halfWidth * 2.6f;
            int minX = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x, b.x) - radius - x) / Step) + 1, 0, Samples - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x, b.x) + radius - x) / Step) + 1, 0, Samples - 1);
            int minZ = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.y, b.y) - radius - z) / Step) + 1, 0, Samples - 1);
            int maxZ = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.y, b.y) + radius - z) / Step) + 1, 0, Samples - 1);
            Vector2 path = b - a;
            float lengthSq = path.sqrMagnitude;
            for (int iz = minZ; iz <= maxZ; iz++)
            for (int ix = minX; ix <= maxX; ix++)
            {
                Vector2 point = new Vector2(x + (ix - 1) * Step, z + (iz - 1) * Step);
                float rawAlong = lengthSq > 0.000001f ? Vector2.Dot(point - a, path) / lengthSq : 0;
                float along = Mathf.Clamp01(rawAlong);
                Vector2 delta = point - (a + path * along);
                float radial = delta.magnitude / halfWidth;
                if (radial >= 2.6f) continue;
                int index = ix + iz * Samples;
                float current = Evaluate(index, now);
                float updated;
                if (radial < 1)
                {
                    float groove = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.45f, 1, radial));
                    updated = Mathf.Min(current, -depth * groove);
                }
                else
                {
                    // Do not deposit a semicircular mound behind the start of every
                    // physics-step segment: that would repeatedly fill the previous rut.
                    // A stationary spinning tyre may still push a ring of sand outward.
                    if (lengthSq > 0.000001f && (rawAlong < 0 || rawAlong > 1)) continue;
                    float shoulder = Mathf.Sin(Mathf.InverseLerp(1, 2.6f, radial) * Mathf.PI);
                    float pushedSide = 1 + Mathf.Sign(Vector2.Dot(delta, across)) * slip * 0.5f;
                    updated = Mathf.Max(current, Mathf.Min(0.06f, pileHeight * shoulder * shoulder * pushedSide));
                }
                if (Mathf.Abs(updated - current) > 0.000001f || now - times[index] >= Lifetime * 0.65f)
                {
                    // Expand by one vertex for normals that sample a changed neighbour.
                    minDirtyX = Mathf.Min(minDirtyX, Mathf.Max(0, ix - 2));
                    minDirtyZ = Mathf.Min(minDirtyZ, Mathf.Max(0, iz - 2));
                    maxDirtyX = Mathf.Max(maxDirtyX, Mathf.Min(PatchSegments, ix));
                    maxDirtyZ = Mathf.Max(maxDirtyZ, Mathf.Min(PatchSegments, iz));
                }
                offsets[index] = updated;
                times[index] = now;
                lastTouch = now;
            }
        }

        private float Evaluate(int index, float now)
        {
            float value = offsets[index];
            if (value == 0) return 0;
            float age = now - times[index];
            if (age <= Lifetime * 0.65f) return value;
            if (age >= Lifetime) return 0;
            float t = (age - Lifetime * 0.65f) / (Lifetime * 0.35f);
            return value * (1 - t * t * (3 - 2 * t));
        }

        public void Upload(float now)
        {
            if (!IsDirty) return;
            UpdateVertices(now, minDirtyX, minDirtyZ, maxDirtyX, maxDirtyZ);
            // Upload one contiguous span rather than making a native call per row.
            // Unchanged vertices between the rows keep their existing data.
            int first = minDirtyX + minDirtyZ * (PatchSegments + 1);
            int count = maxDirtyX + maxDirtyZ * (PatchSegments + 1) - first + 1;
            mesh.SetVertexBufferData(vertices, first, first, count, 0, UploadFlags);
            mesh.SetVertexBufferData(normals, first, first, count, 1, UploadFlags);
            UpdateCoverage(minDirtyX, minDirtyZ, maxDirtyX, maxDirtyZ);
            ClearDirty();
        }

        public void UploadFull(float now)
        {
            UpdateVertices(now, 0, 0, PatchSegments, PatchSegments);
            mesh.SetVertexBufferData(vertices, 0, 0, vertices.Length, 0, UploadFlags);
            mesh.SetVertexBufferData(normals, 0, 0, normals.Length, 1, UploadFlags);
            UpdateCoverage(0, 0, PatchSegments, PatchSegments);
            nextRefresh = now + 0.1f;
            ClearDirty();
        }

        private void ClearDirty()
        {
            minDirtyX = minDirtyZ = PatchSegments + 1;
            maxDirtyX = maxDirtyZ = -1;
        }

        public void ClearCoverage()
        {
            int stride = CoverageColumns * PatchSegments;
            int start = slot % CoverageColumns * PatchSegments + slot / CoverageColumns * PatchSegments * stride;
            for (int row = 0; row < PatchSegments; row++)
                System.Array.Clear(owner.coveragePixels, start + row * stride, PatchSegments);
            owner.coverageDirty = true;
        }

        private bool ChangedVertex(int ix, int iz)
        {
            int vertex = ix + iz * (PatchSegments + 1);
            return vertices[vertex].y != baseHeights[vertex] || normals[vertex] != baseNormals[vertex];
        }

        private void UpdateCoverage(int minX, int minZ, int maxX, int maxZ)
        {
            int stride = CoverageColumns * PatchSegments;
            int start = slot % CoverageColumns * PatchSegments + slot / CoverageColumns * PatchSegments * stride;
            // Include all triangles influenced by changed vertices/normals, plus
            // one flat cell for conservative MSAA overlap with the original terrain.
            for (int iz = Mathf.Max(0, minZ - 2); iz <= Mathf.Min(PatchSegments - 1, maxZ + 1); iz++)
            for (int ix = Mathf.Max(0, minX - 2); ix <= Mathf.Min(PatchSegments - 1, maxX + 1); ix++)
            {
                bool changed = false;
                for (int vz = Mathf.Max(0, iz - 1); vz <= Mathf.Min(PatchSegments, iz + 2) && !changed; vz++)
                for (int vx = Mathf.Max(0, ix - 1); vx <= Mathf.Min(PatchSegments, ix + 2); vx++)
                    if (ChangedVertex(vx, vz)) { changed = true; break; }
                int pixel = start + ix + iz * stride;
                byte value = changed ? (byte)255 : (byte)0;
                if (owner.coveragePixels[pixel] == value) continue;
                owner.coveragePixels[pixel] = value;
                owner.coverageDirty = true;
            }
        }

        private void UpdateVertices(float now, int minX, int minZ, int maxX, int maxZ)
        {
            for (int iz = minZ; iz <= maxZ; iz++)
            for (int ix = minX; ix <= maxX; ix++)
            {
                int vertex = ix + iz * (PatchSegments + 1), sample = ix + 1 + (iz + 1) * Samples;
                Vector3 position = vertices[vertex];
                position.y = baseHeights[vertex] + Evaluate(sample, now);
                vertices[vertex] = position;
                float dx = (Evaluate(sample + 1, now) - Evaluate(sample - 1, now)) / (2 * Step);
                float dz = (Evaluate(sample + Samples, now) - Evaluate(sample - Samples, now)) / (2 * Step);
                Vector3 n = baseNormals[vertex];
                normals[vertex] = dx == 0 && dz == 0 ? n : new Vector3(n.x - dx * n.y, n.y, n.z - dz * n.y).normalized;
            }
        }

        public void Dispose()
        {
            // No MeshCollider is created: wheel contact stays on the original terrain.
            CoreUtils.Destroy(host);
            CoreUtils.Destroy(mesh);
        }
    }
}
