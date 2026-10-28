using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Deterministic geometry/lifecycle checks in a disposable additive scene.</summary>
public static class SandDeformationValidation
{
    [MenuItem("Death Derby/Tests/Validate Interactive Sand")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run the sand checks outside Play mode.");
        Scene previous = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(previous.path))
            throw new InvalidOperationException("Save the current scene before running the additive sand checks.");
        bool asynchronousCompilation = ShaderUtil.allowAsyncCompilation;
        Scene test = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        TerrainData data = null;
        Material material = null;
        try
        {
            SceneManager.SetActiveScene(test);
            var shader = Shader.Find("Death Derby/Training Sand");
            Require(shader != null, "Sand shader missing.");
            material = new Material(shader);
            data = new TerrainData { heightmapResolution = 129, size = new Vector3(96, 8, 96) };
            var heights = new float[129, 129];
            for (int z = 0; z < 129; z++)
            for (int x = 0; x < 129; x++) heights[z, x] = 0.25f;
            data.SetHeights(0, 0, heights);
            heights = data.GetHeights(0, 0, 129, 129);
            float baseY = heights[0, 0] * data.size.y;
            var host = Terrain.CreateTerrainGameObject(data);
            var terrain = host.GetComponent<Terrain>();
            var collider = host.GetComponent<TerrainCollider>();
            terrain.materialTemplate = material;
            terrain.reflectionProbeUsage = ReflectionProbeUsage.Simple;
            // Terrain and recycled meshes must share the same world-aligned bake;
            // per-tile GI coordinates would bring back visible replacement squares.
            material.SetTexture("_SandBakedGI", Texture2D.whiteTexture);
            material.SetTexture("_SandBakedDirection", Texture2D.grayTexture);
            material.SetVector("_SandBakedWorld", new Vector4(0, 0, 1f / 96, 1f / 96));
            material.SetVector("_SandBakedST", new Vector4(0.8f, 0.8f, 0.1f, 0.1f));
            material.SetFloat("_SandBakedEnabled", 1);
            var surface = SandDeformationSurface.GetOrCreate(collider);
            Require(surface != null, "Supported terrain did not get a deformation surface.");
            Require(terrain.materialTemplate != material, "Runtime material must be a private copy.");
            Require(material.GetFloat("_SandPatchEnabled") == 0, "Shared material was changed.");

            surface.DeformSegment(new Vector3(8, 2, 4), new Vector3(8, 2, 7), Vector3.right, 0.4f, 0.045f, 0.035f, 0);
            Flush(surface);
            Require(surface.ActivePatchCount == 2, "A rut crossing the tile seam must create both patches.");
            MeshFilter left = FindPatch(terrain, 0, 0), right = FindPatch(terrain, 8, 0);
            Require(left != null && right != null, "Expected adjoining heightfields.");
            float minimum = 0, maximum = 0;
            foreach (var filter in terrain.GetComponentsInChildren<MeshFilter>())
            {
                foreach (Vector3 point in filter.sharedMesh.vertices)
                {
                    minimum = Mathf.Min(minimum, point.y - baseY);
                    maximum = Mathf.Max(maximum, point.y - baseY);
                }
                foreach (Vector3 n in filter.sharedMesh.normals)
                    Require(!float.IsNaN(n.x) && Mathf.Abs(n.magnitude - 1) < 0.001f, "Invalid surface normal.");
                Require(filter.GetComponent<Collider>() == null, "Visual patches must not create colliders.");
                var renderer = filter.GetComponent<MeshRenderer>();
                Require(renderer.lightProbeUsage == LightProbeUsage.Off, "Replacement sand tiles must use the terrain's global ambient probe, not per-object light probes.");
                Require(renderer.reflectionProbeUsage == terrain.reflectionProbeUsage, "Replacement sand tile reflection settings must match the terrain.");
                Require(renderer.motionVectorGenerationMode == MotionVectorGenerationMode.Camera,
                    "Static ground tiles must use camera motion vectors, not pooled-object motion.");
                RequireBakedLighting(material, renderer.sharedMaterial);
            }
            Require(minimum < -0.04f && maximum > 0.025f, "Real geometry must include both a groove and raised shoulders.");
            // Contiguous wheel updates must not refill the preceding groove with end-cap piles.
            surface.DeformSegment(new Vector3(8, 2, 7), new Vector3(8, 2, 7.5f), Vector3.right, 0.4f, 0.045f, 0.035f, 0);
            Flush(surface);
            RequireContinuousRut(left.sharedMesh, baseY);
            Vector3[] lv = left.sharedMesh.vertices, rv = right.sharedMesh.vertices;
            Vector3[] ln = left.sharedMesh.normals, rn = right.sharedMesh.normals;
            int edge = SandDeformationSurface.PatchSegments;
            for (int z = 0; z <= edge; z++)
            {
                int li = edge + z * (edge + 1), ri = z * (edge + 1);
                Require(Mathf.Abs(lv[li].y - rv[ri].y) < 0.0001f, "Height crack at patch boundary.");
                Require(Vector3.Distance(ln[li], rn[ri]) < 0.001f, "Normal discontinuity at patch boundary.");
            }
            VerifyPartialUpdates(surface);
            VerifyLocalizedCoverage(surface, terrain, baseY);
            Require(Covered(terrain, 7.95f, 5) && Covered(terrain, 8.05f, 5), "Localized coverage must cross adjacent atlas slots without a seam.");
            Require(!Covered(terrain, 2, 2) && !Covered(terrain, 12, 2), "Untouched tile interiors must remain original terrain.");
            int count = surface.ActivePatchCount;
            surface.DeformSegment(Vector3.zero, new Vector3(80, 2, 80), Vector3.right, 0.4f, 0.05f, 0.04f, 0);
            surface.DeformSegment(new Vector3(float.NaN, 0, 0), Vector3.one, Vector3.right, 0.4f, 0.05f, 0.04f, 0);
            Require(surface.ActivePatchCount == count, "Invalid contacts or teleports allocated patches.");

            for (int i = 0; i < 100; i++)
                surface.DeformSegment(new Vector3(8, 2, 4), new Vector3(8, 2, 7), Vector3.right, 0.4f, 1, 1, 1);
            Flush(surface);
            foreach (var filter in terrain.GetComponentsInChildren<MeshFilter>())
            foreach (Vector3 point in filter.sharedMesh.vertices)
                Require(point.y >= baseY - 0.0801f && point.y <= baseY + 0.0601f,
                    "Repeated wheelspin exceeded the displacement limits: y=" + point.y + ", base=" + baseY);

            var frame = typeof(SandDeformationSurface).GetField("creationFrame", BindingFlags.NonPublic | BindingFlags.Instance);
            for (int z = 1; z < 11; z++)
            for (int x = 1; x < 11; x++)
            {
                frame.SetValue(surface, -1); // Simulate separate creation frames without waiting in the editor.
                Vector3 point = new Vector3(x * 8 + 4, 2, z * 8 + 4);
                surface.DeformSegment(point, point, Vector3.right, 0.4f, 0.04f, 0.03f, 0);
            }
            Require(surface.ActivePatchCount == SandDeformationSurface.MaximumPatches, "Patch cache did not respect its fixed capacity.");
            VerifyPoolAndFadeBudget(surface, terrain, baseY, frame);
            VerifyCoverageExpiry(surface, terrain, frame);
            Require(terrain.terrainData == data && collider.terrainData == data, "Terrain/collider data was replaced.");
            float[,] after = data.GetHeights(0, 0, 129, 129);
            for (int z = 0; z < 129; z++)
            for (int x = 0; x < 129; x++) Require(after[z, x] == heights[z, x], "Source terrain height was modified.");

            ShaderUtil.allowAsyncCompilation = false;
            foreach (string[] keywords in new[] { new string[0], new[] { "_MAIN_LIGHT_SHADOWS_CASCADE", "_SHADOWS_SOFT_HIGH" } })
            {
                material.shaderKeywords = keywords;
                for (int pass = 0; pass < material.passCount; pass++) ShaderUtil.CompilePass(material, pass, true);
            }
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                Require(message.severity.ToString() != "Error", message.message);

            Invoke(surface, "OnDisable");
            Require(terrain.materialTemplate == material, "Original terrain material was not restored.");
            Require(surface.ActivePatchCount == 0 && terrain.GetComponentsInChildren<MeshFilter>().Length == 0, "Runtime mesh cleanup failed.");
            Object.DestroyImmediate(surface);
            var flat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                flat.SetColor("_BaseColor", new Color(0.8f, 0.4f, 0.12f));
                flat.SetFloat("_Metallic", 0);
                terrain.materialTemplate = flat;
                surface = SandDeformationSurface.GetOrCreate(collider);
                Require(surface != null, "Desert's untextured URP/Lit sand was not supported.");
                Require(terrain.materialTemplate.GetColor("_BaseColor") == flat.GetColor("_BaseColor"), "Flat sand colour changed.");
                Require(terrain.materialTemplate.GetFloat("_RippleHeight") == 0, "Flat sand gained unwanted surface texture.");
                Invoke(surface, "OnDisable");
                Object.DestroyImmediate(surface);
                flat.SetTexture("_BaseMap", Texture2D.whiteTexture);
                Require(SandDeformationSurface.GetOrCreate(collider) == null, "Textured terrains must not be silently converted.");
            }
            finally { Object.DestroyImmediate(flat); terrain.materialTemplate = material; }
            Debug.Log("SAND_DEFORMATION_PASS: localized coverage, untouched terrain, atlas seams, mask reuse/fade/expiry, grooves, raised berms, seam heights/normals, partial-upload equivalence, pooled mesh reuse, single-patch fade budget, stationary wheelspin, teleport/NaN guards, cache limit, original collision/data, cleanup, flat-sand compatibility and shader passes checked.");
        }
        finally
        {
            foreach (var root in test.GetRootGameObjects())
            foreach (var surface in root.GetComponentsInChildren<SandDeformationSurface>()) Invoke(surface, "OnDisable");
            EditorSceneManager.CloseScene(test, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            ShaderUtil.allowAsyncCompilation = asynchronousCompilation;
            if (material != null) Object.DestroyImmediate(material);
            if (data != null) Object.DestroyImmediate(data);
        }
    }

    private static void Flush(SandDeformationSurface surface) => Invoke(surface, "LateUpdate");
    private static void Invoke(SandDeformationSurface surface, string method) =>
        typeof(SandDeformationSurface).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(surface, null);
    private static MeshFilter FindPatch(Terrain terrain, float x, float z)
    {
        foreach (var filter in terrain.GetComponentsInChildren<MeshFilter>())
            if (Mathf.Abs(filter.transform.localPosition.x - x) < 0.01f && Mathf.Abs(filter.transform.localPosition.z - z) < 0.01f) return filter;
        return null;
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void RequireContinuousRut(Mesh mesh, float baseY)
    {
        int index = SandDeformationSurface.PatchSegments + 67 * (SandDeformationSurface.PatchSegments + 1);
        Require(mesh.vertices[index].y < baseY - 0.035f, "Contiguous wheel updates filled the preceding rut.");
    }

    private static System.Collections.IDictionary Patches(SandDeformationSurface surface) =>
        (System.Collections.IDictionary)typeof(SandDeformationSurface).GetField("patches", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(surface);

    private static void VerifyPartialUpdates(SandDeformationSurface surface)
    {
        foreach (System.Collections.DictionaryEntry entry in Patches(surface))
        {
            object patch = entry.Value;
            var type = patch.GetType();
            var mesh = (Mesh)type.GetField("mesh", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(patch);
            var beforeVertices = mesh.vertices;
            var beforeNormals = mesh.normals;
            var coverage = (byte[])((byte[])typeof(SandDeformationSurface).GetField("coveragePixels", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(surface)).Clone();
            type.GetMethod("UploadFull").Invoke(patch, new object[] { Time.time });
            var full = (byte[])typeof(SandDeformationSurface).GetField("coveragePixels", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(surface);
            for (int i = 0; i < full.Length; i++) Require(coverage[i] == full[i], "Partial coverage update missed a changed cell.");
            var afterVertices = mesh.vertices;
            var afterNormals = mesh.normals;
            for (int i = 0; i < beforeVertices.Length; i++)
            {
                Require(Vector3.Distance(beforeVertices[i], afterVertices[i]) < 0.000001f, "Partial upload missed a changed vertex.");
                Require(Vector3.Distance(beforeNormals[i], afterNormals[i]) < 0.00001f, "Partial upload missed a changed normal.");
                Require(mesh.bounds.Contains(afterVertices[i]), "Cached mesh bounds exclude a deformed vertex.");
            }
        }
    }

    private static void VerifyPoolAndFadeBudget(SandDeformationSurface surface, Terrain terrain, float baseY, FieldInfo frame)
    {
        var original = FindPatch(terrain, 0, 0).sharedMesh;
        typeof(SandDeformationSurface).GetMethod("Remove", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(surface, new object[] { 0 });
        frame.SetValue(surface, -1);
        var point = new Vector3(84, 2, 84);
        surface.DeformSegment(point, point, Vector3.right, 0.4f, 0.04f, 0.03f, 0);
        Flush(surface);
        var reused = FindPatch(terrain, 80, 80);
        Require(reused != null && reused.sharedMesh == original, "Retired patch allocated a new mesh instead of reusing its buffers.");
        RequireBakedLighting(terrain.materialTemplate, reused.GetComponent<MeshRenderer>().sharedMaterial);
        Require(reused.GetComponent<MeshRenderer>().lightProbeUsage == LightProbeUsage.Off
            && reused.GetComponent<MeshRenderer>().reflectionProbeUsage == terrain.reflectionProbeUsage
            && reused.GetComponent<MeshRenderer>().motionVectorGenerationMode == MotionVectorGenerationMode.Camera,
            "Recycled tiles must retain terrain-matching lighting settings.");
        foreach (var vertex in reused.sharedMesh.vertices)
        {
            if (new Vector2(vertex.x - 4, vertex.z - 4).sqrMagnitude > 0.53f * 0.53f)
                Require(Mathf.Abs(vertex.y - baseY) < 0.0001f, "Recycled patch retained old ruts.");
        }
        float now = Time.time;
        foreach (System.Collections.DictionaryEntry entry in Patches(surface))
        {
            var type = entry.Value.GetType();
            type.GetField("firstTouch", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(entry.Value, now - 72);
            type.GetField("nextRefresh", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(entry.Value, -1f);
        }
        Flush(surface);
        int fades = 0;
        foreach (System.Collections.DictionaryEntry entry in Patches(surface))
        {
            float refresh = (float)entry.Value.GetType().GetField("nextRefresh", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(entry.Value);
            if (refresh > now) fades++;
        }
        Require(fades == 1, "Fade work must rebuild no more than one complete patch per update.");
        Require(!Covered(terrain, 7.95f, 5), "Retired tile still clips its old terrain location.");
        VerifyLocalizedCoverage(surface, terrain, baseY);
    }

    private static void RequireBakedLighting(Material original, Material replacement)
    {
        Require(replacement.GetFloat("_SandBakedEnabled") == original.GetFloat("_SandBakedEnabled")
            && replacement.GetTexture("_SandBakedGI") == original.GetTexture("_SandBakedGI")
            && replacement.GetTexture("_SandBakedDirection") == original.GetTexture("_SandBakedDirection")
            && replacement.GetVector("_SandBakedWorld") == original.GetVector("_SandBakedWorld")
            && replacement.GetVector("_SandBakedST") == original.GetVector("_SandBakedST"),
            "Replacement sand did not inherit the terrain's world-aligned baked indirect light.");
    }

    private static bool Covered(Terrain terrain, float x, float z)
    {
        var mask = (Texture2D)terrain.materialTemplate.GetTexture("_SandPatchMask");
        var coverage = (Texture2D)terrain.materialTemplate.GetTexture("_SandPatchCoverage");
        int tx = Mathf.FloorToInt(x / 8), tz = Mathf.FloorToInt(z / 8);
        int slot = Mathf.RoundToInt(mask.GetPixel(tx, tz).r * 255) - 1;
        if (slot < 0) return false;
        int ix = Mathf.Clamp(Mathf.FloorToInt((x - tx * 8) * 10), 0, 79);
        int iz = Mathf.Clamp(Mathf.FloorToInt((z - tz * 8) * 10), 0, 79);
        return coverage.GetPixel(slot % 8 * 80 + ix, slot / 8 * 80 + iz).r > 0.5f;
    }

    private static void VerifyLocalizedCoverage(SandDeformationSurface surface, Terrain terrain, float baseY)
    {
        Flush(surface);
        var texture = (Texture2D)terrain.materialTemplate.GetTexture("_SandPatchCoverage");
        Require(texture.width == 640 && texture.height == 320, "Fine mask must be bounded by the 32-slot mesh cache, not terrain size.");
        foreach (var filter in terrain.GetComponentsInChildren<MeshFilter>())
        {
            var vertices = filter.sharedMesh.vertices;
            var normals = filter.sharedMesh.normals;
            for (int z = 0; z < 80; z++)
            for (int x = 0; x < 80; x++)
            {
                bool changed = false;
                for (int vz = Mathf.Max(0, z - 1); vz <= Mathf.Min(80, z + 2); vz++)
                for (int vx = Mathf.Max(0, x - 1); vx <= Mathf.Min(80, x + 2); vx++)
                {
                    int vertex = vx + vz * 81;
                    changed |= vertices[vertex].y != baseY || normals[vertex] != Vector3.up;
                }
                Require(Covered(terrain, filter.transform.localPosition.x + (x + 0.5f) * 0.1f,
                    filter.transform.localPosition.z + (z + 0.5f) * 0.1f) == changed,
                    "Fine coverage disagrees with the displayed groove/berm geometry or retained stale tracks.");
            }
        }
    }

    private static void VerifyCoverageExpiry(SandDeformationSurface surface, Terrain terrain, FieldInfo frame)
    {
        var remove = typeof(SandDeformationSurface).GetMethod("Remove", BindingFlags.Instance | BindingFlags.NonPublic);
        var keys = new System.Collections.Generic.List<int>();
        foreach (System.Collections.DictionaryEntry entry in Patches(surface)) keys.Add((int)entry.Key);
        foreach (int key in keys) remove.Invoke(surface, new object[] { key });
        frame.SetValue(surface, -1);
        var point = new Vector3(20, 2, 4);
        surface.DeformSegment(point, point, Vector3.right, 0.4f, 0, 0, 0);
        Flush(surface);
        var coverage = (Texture2D)terrain.materialTemplate.GetTexture("_SandPatchCoverage");
        foreach (byte value in coverage.GetRawTextureData<byte>()) Require(value == 0, "An undeformed/recycled patch must have no replacement coverage.");
        surface.DeformSegment(point, point, Vector3.right, 0.4f, 0.04f, 0.03f, 0);
        Flush(surface);
        Require(Covered(terrain, 20.05f, 4.05f), "Stationary deformation did not enable localized coverage.");
        foreach (System.Collections.DictionaryEntry entry in Patches(surface))
            entry.Value.GetType().GetMethod("UploadFull").Invoke(entry.Value, new object[] { Time.time + SandDeformationSurface.Lifetime + 1 });
        Flush(surface);
        foreach (byte value in coverage.GetRawTextureData<byte>()) Require(value == 0, "Fully faded geometry retained coverage.");
        foreach (System.Collections.DictionaryEntry entry in Patches(surface))
            entry.Value.GetType().GetField("lastTouch").SetValue(entry.Value, Time.time - SandDeformationSurface.Lifetime - 1);
        Flush(surface);
        Require(surface.ActivePatchCount == 0 && !Covered(terrain, 20.05f, 4.05f), "Expired deformation did not restore original terrain coverage.");
    }
}
