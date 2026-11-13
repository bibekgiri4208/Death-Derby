using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

/// <summary>Checks fresh wheel dust separately from its lingering wake, without Play mode or audio.</summary>
public static class WheelDustValidation
{
    [MenuItem("Death Derby/Tests/Validate Wheel Dust")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run the dust checks outside Play mode.");
        Scene previous = SceneManager.GetActiveScene();
        Scene test = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        Random.State random = Random.state;
        try
        {
            SceneManager.SetActiveScene(test);
            Random.InitState(4711);
            var host = new GameObject("Silent wheel dust test", typeof(Rigidbody), typeof(CarController), typeof(CarEffects));
            var controller = host.GetComponent<CarController>();
            var settings = host.GetComponent<CarEffects>();
            controller.frontLeftCollider = Wheel(host, new Vector3(-0.8f, 0.4f, 1.2f));
            controller.frontRightCollider = Wheel(host, new Vector3(0.8f, 0.4f, 1.2f));
            controller.rearLeftCollider = Wheel(host, new Vector3(-0.8f, 0.4f, -1.2f));
            controller.rearRightCollider = Wheel(host, new Vector3(0.8f, 0.4f, -1.2f));
            var material = Resources.Load<Material>("Effects/WheelDust");
            Require(material != null, "Wheel dust material missing.");
            float originalSoftness = material.GetFloat("_DepthFadeDistance");
            var effects = new WheelSurfaceEffects(controller, settings);
            var states = (Array)typeof(WheelSurfaceEffects).GetField("wheels", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(effects);
            foreach (object state in states)
            {
                var type = state.GetType();
                var wheel = (WheelCollider)type.GetField("wheel").GetValue(state);
                var dust = (ParticleSystem)type.GetField("dust").GetValue(state);
                var haze = (ParticleSystem)type.GetField("haze").GetValue(state);
                Require(dust != null && haze != null && dust != haze, "Every wheel needs separate contact and wake emitters.");
                Require(dust.main.simulationSpace == ParticleSystemSimulationSpace.World, "Dust must not be attached to the moving car.");
                Require(dust.main.startLifetime.constantMax <= 0.5f, "Contact dust must not become another long trailing cloud.");
                Require(!dust.limitVelocityOverLifetime.multiplyDragByParticleVelocity
                    && !dust.limitVelocityOverLifetime.multiplyDragByParticleSize, "Contact drag must not erase fresh wheel momentum.");
                Require(dust.colorOverLifetime.color.gradient.Evaluate(0).a >= 0.75f, "Fresh wheel dust starts invisible.");
                Require(dust.sizeOverLifetime.size.Evaluate(0) >= 0.8f, "Fresh wheel dust starts too small.");
                Require(haze.colorOverLifetime.color.gradient.Evaluate(0).a == 0, "The wake must retain its soft fade-in.");
                Require(haze.main.startLifetime.constantMin > dust.main.startLifetime.constantMax, "Contact and wake lifetimes were not separated.");
                float contactSoftness = Softness(dust), wakeSoftness = Softness(haze);
                Require(contactSoftness > 0 && contactSoftness <= 0.1f && contactSoftness < wakeSoftness,
                    "Ground softening must not erase contact dust or sharpen the airborne wake.");
                foreach (float speed in new[] { 0f, 35f, 55f })
                {
                    dust.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    Vector3 normal = new Vector3(0.1f, 1, 0.15f).normalized;
                    Vector3 point = wheel.transform.position - Vector3.up * wheel.radius;
                    Vector3 velocity = Vector3.ProjectOnPlane(Vector3.forward * speed, normal);
                    for (int i = 0; i < 16; i++)
                        Invoke(effects, "EmitDust", state, point, normal, velocity, speed == 0 ? 1f : 0f);
                    var particles = new ParticleSystem.Particle[16];
                    Require(dust.GetParticles(particles) == 16, "Wheel-level puffs were not emitted.");
                    float side = Mathf.Sign(wheel.transform.localPosition.x);
                    foreach (var particle in particles)
                    {
                        Vector3 offset = particle.position - point;
                        Require(Vector3.Dot(offset, normal) > 0.15f, "Contact dust spawned inside the ground.");
                        Require(offset.x * side > 0.1f && offset.magnitude < 0.5f, "Dust must spawn beside each tyre, not inside it or far behind it.");
                        Require(particle.startLifetime >= 0.28f && particle.startLifetime <= 0.45f, "Emitted lifetime disagrees with the contact configuration.");
                        Require(particle.startColor.a / 255f >= settings.dustOpacity * 0.8f, "Contact dust emission is too faint.");
                        if (speed > 0)
                            Require(Vector3.Dot(particle.velocity, velocity.normalized) >= velocity.magnitude * 0.6f,
                                "Fresh dust loses wheel momentum and immediately falls behind the car.");
                    }
                    dust.Simulate(0.04f, false, false);
                    Require(dust.GetParticles(particles) == 16, "Fresh wheel dust vanished during its first physics steps.");
                    if (speed > 0)
                        foreach (var particle in particles)
                            Require(Vector3.Dot(particle.position - point, velocity.normalized) >= velocity.magnitude * 0.04f * 0.5f - 0.05f,
                                "Velocity limiting erases wheel momentum immediately after emission: speed=" + speed
                                + ", distance=" + Vector3.Dot(particle.position - point, velocity.normalized)
                                + ", age=" + (particle.startLifetime - particle.remainingLifetime));
                    dust.Simulate(0.6f, false, false);
                    Require(dust.particleCount == 0, "Contact dust outlived its short wheel-level puff.");
                }
                Vector3 contact = wheel.transform.position - Vector3.up * wheel.radius;
                Invoke(effects, "EmitWake", state, contact, Vector3.up, Vector3.forward * 35, 0f);
                haze.Simulate(0.6f, false, false);
                Require(haze.particleCount > 0, "The trailing dust cloud was lost.");
            }
            Require(material.GetFloat("_DepthFadeDistance") == originalSoftness, "Shared dust material was changed.");
            Require(host.GetComponentsInChildren<AudioSource>().Length == 0, "Dust checks must not create audio sources.");
            effects.Stop();
            Debug.Log("WHEEL_DUST_PASS: all four tyre shoulders, visible birth alpha/size, ground softness, cruise/boost momentum, stationary wheelspin, short contact lifetime, separate lingering wake and unchanged shared material checked without Play mode or audio.");
        }
        finally
        {
            Random.state = random;
            EditorSceneManager.CloseScene(test, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    private static WheelCollider Wheel(GameObject host, Vector3 position)
    {
        var wheel = new GameObject("Test Wheel", typeof(WheelCollider));
        wheel.transform.SetParent(host.transform, false);
        wheel.transform.localPosition = position;
        var collider = wheel.GetComponent<WheelCollider>();
        collider.radius = 0.4f;
        return collider;
    }

    private static float Softness(ParticleSystem particles)
    {
        var properties = new MaterialPropertyBlock();
        particles.GetComponent<ParticleSystemRenderer>().GetPropertyBlock(properties);
        return properties.GetFloat("_DepthFadeDistance");
    }

    private static void Invoke(WheelSurfaceEffects effects, string method, params object[] arguments) =>
        typeof(WheelSurfaceEffects).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(effects, arguments);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
