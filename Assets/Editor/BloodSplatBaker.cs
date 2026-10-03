using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bakes the procedural blood splats from <see cref="BloodSplatSource"/> into PNG
/// assets so the shapes are never generated on the gameplay thread.
///
/// The runtime fallback in BloodDecal produces identical pixels, so a missing or
/// stale bake only costs the fallback path, not a visual mismatch. Run this after
/// changing anything in <see cref="BloodSplatSource"/>.
/// </summary>
public static class BloodSplatBaker
{
    private const string OutputFolder = "Assets/Resources/Effects/BloodSplats";
    private const string TextureNamePrefix = "BloodSplat_";

    [MenuItem("Tools/Bake Blood Splat Textures")]
    public static void Bake()
    {
        EnsureFolder(OutputFolder);

        for (int i = 0; i < BloodSplatSource.SplatTextureVariants; i++)
        {
            string path = $"{OutputFolder}/{TextureNamePrefix}{i}.png";
            WriteSplat(path, BloodSplatSource.GenerateSplat(i));
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Object selection = AssetDatabase.LoadAssetAtPath<Texture2D>(
            $"{OutputFolder}/{TextureNamePrefix}0.png");

        if (selection != null)
        {
            Selection.activeObject = selection;
            EditorGUIUtility.PingObject(selection);
        }

        Debug.Log($"BloodSplatBaker: Baked {BloodSplatSource.SplatTextureVariants} splats to {OutputFolder}");
    }

    private static void WriteSplat(string assetPath, Color32[] pixels)
    {
        Texture2D tex = new Texture2D(
            BloodSplatSource.SplatTextureSize,
            BloodSplatSource.SplatTextureSize,
            TextureFormat.RGBA32,
            false);

        tex.SetPixels32(pixels);
        tex.Apply();

        string fullPath = Path.Combine(ProjectRoot(), assetPath.Replace("/", "\\"));
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.npotScale = TextureImporterNPOTScale.None;

        // Uncompressed RGBA32 keeps the baked pixels byte-identical to the
        // runtime generator, so the fallback never looks different.
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static string ProjectRoot()
    {
        return Directory.GetParent(Application.dataPath).FullName;
    }

    private static void EnsureFolder(string folderPath)
    {
        folderPath = folderPath.Replace("\\", "/");
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        int idx = folderPath.LastIndexOf('/');
        if (idx <= 0)
        {
            AssetDatabase.CreateFolder("Assets", folderPath);
            AssetDatabase.Refresh();
            return;
        }

        string parent = folderPath.Substring(0, idx);
        string leaf = folderPath.Substring(idx + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
        AssetDatabase.Refresh();
    }
}