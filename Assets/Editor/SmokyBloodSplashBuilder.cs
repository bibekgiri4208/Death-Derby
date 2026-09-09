using UnityEditor;
using UnityEngine;

public static class SmokyBloodSplashBuilder
{
    private const string OutputFolder = "Assets/Resources/Effects/SmokyBloodSplash";
    private const string MaterialsFolder = OutputFolder + "/Materials";
    private const string PrefabPath = OutputFolder + "/SmokyBloodSplash.prefab";
    private const string PuffMatPath = MaterialsFolder + "/BloodPuff.mat";
    private const string DropletMatPath = MaterialsFolder + "/BloodDroplet.mat";

    private const string SphereMeshPath = "Assets/Effects/Blood/Meshes/Sphere.fbx";
    private const string BaseMaterialPath = "Assets/Effects/Blood/Materials/Sphere_Material.mat";

    [MenuItem("Tools/Build Smoky Blood Splash Prefab")]
    public static void Build()
    {
        EnsureFolder(OutputFolder);
        EnsureFolder(MaterialsFolder);

        Mesh sphereMesh = AssetDatabase.LoadAssetAtPath<Mesh>(SphereMeshPath);
        if (sphereMesh == null)
        {
            Debug.LogError("SmokyBloodSplashBuilder: Sphere mesh not found at " + SphereMeshPath);
            return;
        }

        Material baseMat = AssetDatabase.LoadAssetAtPath<Material>(BaseMaterialPath);
        if (baseMat == null)
        {
            Debug.LogError("SmokyBloodSplashBuilder: Base material not found at " + BaseMaterialPath);
            return;
        }

        Material puffMat = GetOrCreateMaterial(PuffMatPath, baseMat);
        Material dropletMat = GetOrCreateMaterial(DropletMatPath, baseMat);
        puffMat.SetColor("_Color", new Color(0.9f, 0.08f, 0.09f, 1f));
        dropletMat.SetColor("_Color", new Color(1f, 0.05f, 0.05f, 1f));
        EditorUtility.SetDirty(puffMat);
        EditorUtility.SetDirty(dropletMat);

        GameObject root = new GameObject("SmokyBloodSplash");
        ConfigureBloodPuff(root.AddComponent<ParticleSystem>(), puffMat, sphereMesh);

        GameObject droplets = new GameObject("BloodSplashDroplets");
        droplets.transform.SetParent(root.transform, false);
        ConfigureDroplets(droplets.AddComponent<ParticleSystem>(), dropletMat, sphereMesh);

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

    static void ConfigureBloodPuff(ParticleSystem ps, Material mat, Mesh mesh)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = true;
        main.loop = false;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.3f);
        main.startColor = Color.white;
        main.gravityModifier = -0.15f;
        main.maxParticles = 120;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 26, 32) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;
        shape.randomDirectionAmount = 1f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve grow = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.35f, 1.15f),
            new Keyframe(1f, 0.4f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, grow);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = mesh;
        renderer.sharedMaterial = mat;
        renderer.maxParticleSize = 0.8f;
        renderer.sortMode = ParticleSystemSortMode.None;
    }

    static void ConfigureDroplets(ParticleSystem ps, Material mat, Mesh mesh)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = true;
        main.loop = false;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f);
        main.startColor = Color.white;
        main.gravityModifier = 1.8f;
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.None;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 26, 32) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;
        shape.randomDirectionAmount = 0.6f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve shrink = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0.5f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, shrink);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = mesh;
        renderer.sharedMaterial = mat;
        renderer.maxParticleSize = 0.8f;
        renderer.sortMode = ParticleSystemSortMode.None;
    }

    static Material GetOrCreateMaterial(string path, Material baseMat)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(baseMat);
            mat.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = baseMat.shader;
        }
        return mat;
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
}