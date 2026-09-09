using UnityEngine;

public class BloodSplash : MonoBehaviour
{
    private const string PrefabResourcePath = "Effects/SmokyBloodSplash/SmokyBloodSplash";
    private static GameObject cachedPrefab;

    public static BloodSplash Spawn(Vector3 position, Quaternion rotation)
    {
        GameObject prefab = GetPrefab();
        if (prefab != null)
        {
            GameObject spawned = Object.Instantiate(prefab, position, rotation);
            Object.Destroy(spawned, 2f);
            return null;
        }

        GameObject go = new GameObject("BloodSplash");
        go.transform.SetPositionAndRotation(position, rotation);
        BloodSplash splash = go.AddComponent<BloodSplash>();
        return splash;
    }

    private static GameObject GetPrefab()
    {
        if (cachedPrefab == null)
        {
            cachedPrefab = Resources.Load<GameObject>(PrefabResourcePath);
        }
        return cachedPrefab;
    }

    void Awake()
    {
        ParticleSystem ps = gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.1f;
        main.startLifetime = 0.4f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startColor = new Color(0.6f, 0.0f, 0.0f, 1f);
        main.maxParticles = 50;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.5f;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30, 50) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.1f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.7f, 0f, 0f), 0f), new GradientColorKey(new Color(0.3f, 0f, 0f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.bounce = 0.1f;
        collision.lifetimeLoss = 0.8f;

        var renderer = GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        renderer.material.color = new Color(0.65f, 0f, 0f);
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.maxParticleSize = 0.2f;

        ps.Play();

        Destroy(gameObject, 1.5f);
    }
}
