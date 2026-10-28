using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Rebakes Training's actual sunlight bounce and static reflections. Unity's
/// terrain baker ignores layerless custom terrains, so a bake-only mesh contributes
/// the sand and supplies one world-aligned indirect map to terrain and tyre ruts.
/// No TerrainData, colliders, gameplay components or shared model imports are edited.
/// </summary>
public static class TrainingLightingBake
{
    private const string ScenePath = "Assets/Scenes/Training.unity";
    private const string GroundName = "Sand GI Bake Proxy (Editor Bake Only)";
    private static Terrain terrain;
    private static MeshRenderer ground;
    private static bool terrainWasEnabled;
    private static float bakedWasEnabled;
    private static bool baking;

    [MenuItem("Death Derby/Lighting/Rebake Training Lighting")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Stop Play mode and any existing bake first.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || scene.isDirty)
            throw new InvalidOperationException("Open Training and save your scene edits before rebaking its lighting.");
        terrain = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Terrain>()).Single();
        ground = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true))
            .Single(renderer => renderer.name == GroundName);
        if (Lightmapping.lightingSettings == null || !Lightmapping.lightingSettings.bakedGI
            || ground.sharedMaterial != terrain.materialTemplate)
            throw new InvalidOperationException("Training's baked lighting setup or sand material is missing.");
        RefreshGroundMesh();
        terrainWasEnabled = terrain.enabled;
        bakedWasEnabled = ground.sharedMaterial.GetFloat("_SandBakedEnabled");
        ground.sharedMaterial.SetFloat("_SandBakedEnabled", 0);
        ground.enabled = true;
        // Avoid two coplanar ground surfaces in reflection captures. TerrainCollider
        // remains untouched; this operation is never allowed during gameplay.
        terrain.enabled = false;
        baking = true;
        Lightmapping.bakeCompleted += Complete;
        Lightmapping.bakeCancelled += Cancelled;
        AssemblyReloadEvents.beforeAssemblyReload += CancelBeforeReload;
        try
        {
            if (!Lightmapping.BakeAsync()) throw new InvalidOperationException("Unity could not start the lighting bake.");
        }
        catch
        {
            Restore(false);
            throw;
        }
    }

    private static void RefreshGroundMesh()
    {
        // Keep future rebakes correct after terrain sculpting, without editing its data.
        Mesh mesh = ground.GetComponent<MeshFilter>().sharedMesh;
        Vector2[] uv = mesh.uv;
        Vector3[] vertices = mesh.vertices, normals = mesh.normals;
        Vector3 size = terrain.terrainData.size;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = new Vector3(uv[i].x * size.x, terrain.terrainData.GetInterpolatedHeight(uv[i].x, uv[i].y), uv[i].y * size.z);
            normals[i] = terrain.terrainData.GetInterpolatedNormal(uv[i].x, uv[i].y);
        }
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.RecalculateBounds();
        ground.transform.position = terrain.transform.position;
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssetIfDirty(mesh);
    }

    private static void Complete()
    {
        bool success = false;
        try
        {
            if (Lightmapping.lightingDataAsset == null || ground.lightmapIndex < 0
                || ground.lightmapIndex >= LightmapSettings.lightmaps.Length
                || LightmapSettings.lightProbes == null || LightmapSettings.lightProbes.count == 0)
                throw new InvalidOperationException("Bake did not produce the sand lightmap and car light probes.");
            var map = LightmapSettings.lightmaps[ground.lightmapIndex];
            if (map.lightmapColor == null || map.lightmapDir == null)
                throw new InvalidOperationException("Training requires directional baked lightmaps.");
            var captures = terrain.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ReflectionProbe>()).ToArray();
            if (captures.Length == 0 || captures.Any(probe => probe.bakedTexture == null))
                throw new InvalidOperationException("Training reflection captures were not generated.");
            Material material = ground.sharedMaterial;
            Vector3 origin = terrain.transform.position, size = terrain.terrainData.size;
            material.SetTexture("_SandBakedGI", map.lightmapColor);
            material.SetTexture("_SandBakedDirection", map.lightmapDir);
            material.SetVector("_SandBakedST", ground.lightmapScaleOffset);
            material.SetVector("_SandBakedWorld", new Vector4(origin.x, origin.z, 1f / size.x, 1f / size.z));
            material.SetFloat("_SandBakedEnabled", 1);
            EditorUtility.SetDirty(material);
            success = true;
        }
        finally
        {
            Restore(success);
        }
        if (!success) return;
        Scene scene = terrain.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssetIfDirty(ground.sharedMaterial);
        Debug.Log("Training sunlight bounce, light probes and reflection captures baked. The bake-only ground renderer is disabled.");
    }

    private static void Restore(bool success)
    {
        Lightmapping.bakeCompleted -= Complete;
        Lightmapping.bakeCancelled -= Cancelled;
        AssemblyReloadEvents.beforeAssemblyReload -= CancelBeforeReload;
        if (ground != null)
        {
            ground.enabled = false;
            if (!success && ground.sharedMaterial != null) ground.sharedMaterial.SetFloat("_SandBakedEnabled", bakedWasEnabled);
        }
        if (terrain != null) terrain.enabled = terrainWasEnabled;
        baking = false;
    }

    private static void Cancelled() => Restore(false);

    private static void CancelBeforeReload()
    {
        if (!baking) return;
        Lightmapping.Cancel();
        Restore(false);
    }
}
