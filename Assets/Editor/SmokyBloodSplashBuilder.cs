using System.IO;
using UnityEditor;
using UnityEngine;

public static class SmokyBloodSplashBuilder
{
    private const string OutputFolder = "Assets/Resources/Effects/SmokyBloodSplash";
    private const string MaterialsFolder = OutputFolder + "/Materials";
    private const string TexturesFolder = OutputFolder + "/Textures";
    private const string PrefabPath = OutputFolder + "/SmokyBloodSplash.prefab";
    private const string TexturePath = TexturesFolder + "/SmokePuff.png";
    private const string BloodTexturePath = TexturesFolder + "/BloodDrop.png";
    private const string SmokeMatPath = MaterialsFolder + "/Smoke.mat";
    private const string DropletMatPath = MaterialsFolder + "/Droplet.mat";

    [MenuItem("Tools/Build Smoky Blood Splash Prefab")]
    public static void Build()
    {
        EnsureFolder(OutputFolder);
        EnsureFolder(MaterialsFolder);
        EnsureFolder(TexturesFolder);

        Texture2D puffTexture = CreateAndImportPuffTexture();
        Texture2D bloodTexture = CreateAndImportBloodDropTexture();
        Shader shader = PickParticleShader();
        if (shader == null)
        {
            Debug.LogError("SmokyBloodSplashBuilder: No particle shader found.");
            return;
        }

        Material smokeMat = GetOrCreateMaterial(SmokeMatPath, shader);
        Material dropletMat = GetOrCreateMaterial(DropletMatPath, shader);
        AssignTextureAndColor(smokeMat, puffTexture);
        AssignTextureAndColor(dropletMat, bloodTexture);
        EditorUtility.SetDirty(smokeMat);
        EditorUtility.SetDirty(dropletMat);

        GameObject root = new GameObject("SmokyBloodSplash");
        ConfigureSmokeCloud(root.AddComponent<ParticleSystem>(), smokeMat);

        GameObject droplets = new GameObject("BloodDroplets");
        droplets.transform.SetParent(root.transform, false);
        ConfigureDroplets(droplets.AddComponent<ParticleSystem>(), dropletMat);

        GameObject wisps = new GameObject("SmokeWisps");
        wisps.transform.SetParent(root.transform, false);
        ConfigureWisps(wisps.AddComponent<ParticleSystem>(), smokeMat);

        GameObject flash = new GameObject("PopFlash");
        flash.transform.SetParent(root.transform, false);
        ConfigureFlash(flash.AddComponent<ParticleSystem>(), smokeMat);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null)
        {
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }

        Debug.Log("SmokyBloodSplashBuilder: Prefab built at " + PrefabPath);
    }

    static void ConfigureSmokeCloud(ParticleSystem ps, Material mat)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = true;
        main.loop = false;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.13f, 0.22f);
        main.startColor = new Color(0.7f, 0.1f, 0.13f, 1f);
        main.gravityModifier = 0.2f;
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14, 18) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.14f;
        shape.randomDirectionAmount = 0.3f;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.5f);
        noise.frequency = 1.2f;
        noise.scrollSpeed = new ParticleSystem.MinMaxCurve(1.2f);
        noise.damping = true;
        noise.octaveCount = 2;
        noise.octaveMultiplier = 0.5f;
        noise.octaveScale = 2f;
        noise.quality = ParticleSystemNoiseQuality.High;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve grow = new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.5f, 1f), new Keyframe(1f, 1.2f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, grow);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.7f, 0.1f, 0.13f), 0f),
                new GradientColorKey(new Color(0.5f, 0.09f, 0.11f), 0.5f),
                new GradientColorKey(new Color(0.3f, 0.05f, 0.08f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.75f, 0.06f),
                new GradientAlphaKey(0.6f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = grad;

        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-1.4f, 1.4f);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = mat;
        renderer.sortMode = ParticleSystemSortMode.None;
        renderer.maxParticleSize = 2f;
    }

    static void ConfigureDroplets(ParticleSystem ps, Material mat)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = true;
        main.loop = false;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.06f);
        main.startColor = new Color(0.82f, 0.06f, 0.06f, 1f);
        main.gravityModifier = 2.6f;
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.None;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12, 16) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f;
        shape.randomDirectionAmount = 0.5f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.82f, 0.06f, 0.06f), 0f),
                new GradientColorKey(new Color(0.55f, 0.05f, 0.06f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = grad;

        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.bounce = 0.15f;
        collision.lifetimeLoss = 0.9f;
        collision.dampen = 0f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = mat;
        renderer.sortMode = ParticleSystemSortMode.None;
    }

    static void ConfigureWisps(ParticleSystem ps, Material mat)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = true;
        main.loop = false;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
        main.startColor = new Color(0.52f, 0.08f, 0.11f, 1f);
        main.gravityModifier = -0.12f;
        main.maxParticles = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.None;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 7, 10) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;
        shape.randomDirectionAmount = 0.4f;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.6f);
        noise.frequency = 0.9f;
        noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.9f);
        noise.damping = true;
        noise.octaveCount = 3;
        noise.octaveMultiplier = 0.5f;
        noise.octaveScale = 2f;
        noise.quality = ParticleSystemNoiseQuality.High;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve grow = new AnimationCurve(new Keyframe(0f, 0.8f), new Keyframe(1f, 1.25f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, grow);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.52f, 0.08f, 0.11f), 0f),
                new GradientColorKey(new Color(0.35f, 0.06f, 0.08f), 0.5f),
                new GradientColorKey(new Color(0.2f, 0.04f, 0.06f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.6f, 0.08f),
                new GradientAlphaKey(0.45f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = grad;

        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.7f, 0.7f);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = mat;
        renderer.sortMode = ParticleSystemSortMode.None;
        renderer.maxParticleSize = 2f;
    }

    static void ConfigureFlash(ParticleSystem ps, Material mat)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = true;
        main.loop = false;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.18f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.26f);
        main.startColor = new Color(0.95f, 0.3f, 0.18f, 1f);
        main.gravityModifier = 0f;
        main.maxParticles = 24;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.None;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 6, 8) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve grow = new AnimationCurve(new Keyframe(0f, 0.8f), new Keyframe(1f, 1.25f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, grow);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.95f, 0.3f, 0.18f), 0f),
                new GradientColorKey(new Color(0.4f, 0.05f, 0.06f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.7f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = grad;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = mat;
        renderer.sortMode = ParticleSystemSortMode.None;
    }

    static Texture2D CreateAndImportPuffTexture()
    {
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float radius = Mathf.Sqrt(u * u + v * v);
                float baseFalloff = Mathf.Clamp01(1f - radius / 0.9f);
                float soft = Mathf.Pow(baseFalloff, 1.6f);
                float noise01 = Fbm(u * 3.5f, v * 3.5f);
                float mult = 0.8f + (noise01 - 0.5f) * 0.35f;
                float a = Mathf.Clamp01(soft * mult);
                pixels[y * size + x] = new Color(1f, 0.98f, 0.95f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        string fullPath = Path.Combine(ProjectRoot(), TexturePath.Replace("/", "\\"));
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
    }

    static Texture2D CreateAndImportBloodDropTexture()
    {
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float radius = Mathf.Sqrt(u * u + v * v);
                float a = 1f - Mathf.SmoothStep(0.42f, 0.62f, radius);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        string fullPath = Path.Combine(ProjectRoot(), BloodTexturePath.Replace("/", "\\"));
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(BloodTexturePath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(BloodTexturePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(BloodTexturePath);
    }

    static Material GetOrCreateMaterial(string path, Shader shader)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }
        return mat;
    }

    static void AssignTextureAndColor(Material mat, Texture2D texture)
    {
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
    }

    static Shader PickParticleShader()
    {
        string[] candidates =
        {
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Standard Unlit",
            "Legacy Shaders/Particles/Alpha Blended",
            "Sprites/Default"
        };

        foreach (string name in candidates)
        {
            Shader shader = Shader.Find(name);
            if (shader != null) return shader;
        }
        return null;
    }

    static void EnsureFolder(string folderPath)
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

    static string ProjectRoot()
    {
        return Directory.GetParent(Application.dataPath).FullName;
    }

    static float Hash(int x, int y)
    {
        int h = x * 374761393 + y * 668265263;
        h = (h ^ (h >> 13)) * 1274126177;
        h = h ^ (h >> 16);
        return (h & 0x7fffffff) / (float)0x7fffffff;
    }

    static float SmoothNoise(float x, float y)
    {
        int xi = Mathf.FloorToInt(x);
        int yi = Mathf.FloorToInt(y);
        float xf = x - xi;
        float yf = y - yi;
        float u = xf * xf * (3f - 2f * xf);
        float v = yf * yf * (3f - 2f * yf);

        float a = Hash(xi, yi);
        float b = Hash(xi + 1, yi);
        float c = Hash(xi, yi + 1);
        float d = Hash(xi + 1, yi + 1);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    static float Fbm(float x, float y)
    {
        float sum = 0f;
        float amp = 0.5f;
        float freq = 2f;
        for (int octave = 0; octave < 4; octave++)
        {
            sum += SmoothNoise(x * freq, y * freq) * amp;
            freq *= 2f;
            amp *= 0.5f;
        }
        return sum;
    }
}