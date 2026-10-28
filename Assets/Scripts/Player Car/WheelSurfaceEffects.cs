using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Contact-driven sand effects, owned by CarEffects. No per-frame allocations.</summary>
public sealed class WheelSurfaceEffects
{
    private sealed class WheelState
    {
        public WheelCollider wheel;
        public ParticleSystem dust;
        public ParticleSystem haze;
        public Collider surface;
        public Material surfaceMaterial;
        public Vector3 point, trackPoint, trackNormal, trackRight;
        public float dustAmount, pendingParticles, pendingHaze, distance;
        public bool grounded, hasTrack;
        public SandDeformationSurface deformableSand;
        public bool checkedDeformation;
    }

    private readonly CarController controller;
    private readonly Rigidbody carBody;
    private readonly CarEffects settings;
    private readonly WheelState[] wheels;
    private SandTireTracks tracks;

    public WheelSurfaceEffects(CarController controller, CarEffects settings)
    {
        this.controller = controller;
        carBody = controller.GetComponent<Rigidbody>();
        this.settings = settings;
        wheels = new[] {
            new WheelState { wheel = controller.frontLeftCollider },
            new WheelState { wheel = controller.frontRightCollider },
            new WheelState { wheel = controller.rearLeftCollider },
            new WheelState { wheel = controller.rearRightCollider }
        };

        // Do not depend on array order or on the artist-authored emitter transforms.
        if (settings.desertSmokeEffects != null)
        {
            foreach (ParticleSystem dust in settings.desertSmokeEffects)
            {
                if (dust == null) continue;
                dust.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                int nearest = -1;
                float nearestDistance = float.MaxValue;
                for (int i = 0; i < wheels.Length; i++)
                {
                    if (wheels[i].wheel == null || wheels[i].dust != null) continue;
                    float distance = (dust.transform.position - wheels[i].wheel.transform.position).sqrMagnitude;
                    if (distance < nearestDistance) { nearestDistance = distance; nearest = i; }
                }
                if (nearest >= 0) wheels[nearest].dust = dust;
            }
        }

        Material material = Resources.Load<Material>("Effects/WheelDust");
        for (int i = 0; i < wheels.Length; i++)
        {
            WheelState state = wheels[i];
            if (state.wheel == null || material == null) continue;
            if (state.dust == null)
            {
                var host = new GameObject("Wheel Dust");
                host.transform.SetParent(controller.transform, false);
                state.dust = host.AddComponent<ParticleSystem>();
            }
            ConfigureDust(state.dust, material);
            if (settings.desertDustWakeEffects != null && i < settings.desertDustWakeEffects.Length)
                state.haze = settings.desertDustWakeEffects[i];
            if (state.haze == null)
            {
                var host = new GameObject("Sand Dust Wake " + state.wheel.name);
                host.transform.SetParent(controller.transform, false);
                state.haze = host.AddComponent<ParticleSystem>();
            }
            ConfigureWake(state.haze, material, settings);
        }
        // Keep auto-created emitters referenced through Unity's Play-mode hot reload,
        // instead of allocating another four particle systems after every recompile.
        settings.desertSmokeEffects = new ParticleSystem[wheels.Length];
        settings.desertDustWakeEffects = new ParticleSystem[wheels.Length];
        for (int i = 0; i < wheels.Length; i++)
        {
            settings.desertSmokeEffects[i] = wheels[i].dust;
            settings.desertDustWakeEffects[i] = wheels[i].haze;
        }
    }

    private static void ConfigureDust(ParticleSystem dust, Material material)
    {
        dust.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = dust.main;
        main.playOnAwake = false;
        main.loop = true;
        main.prewarm = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.simulationSpeed = 1;
        main.maxParticles = 256;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.7f);
        main.startSpeed = 0;
        main.startSize3D = false;
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
        main.startRotation3D = false;
        main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.gravityModifier = 0.025f;
        main.startColor = Color.white;
        main.stopAction = ParticleSystemStopAction.None;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        // Emission is sampled along the contact path below, not from the old cone emitters.
        var emission = dust.emission;
        emission.enabled = false;
        var shape = dust.shape; shape.enabled = false;
        var velocity = dust.velocityOverLifetime; velocity.enabled = false;
        var inherit = dust.inheritVelocity; inherit.enabled = false;
        var force = dust.forceOverLifetime; force.enabled = false;
        var speedSize = dust.sizeBySpeed; speedSize.enabled = false;
        var speedColor = dust.colorBySpeed; speedColor.enabled = false;
        var rotation = dust.rotationOverLifetime; rotation.enabled = false;
        var speedRotation = dust.rotationBySpeed; speedRotation.enabled = false;
        var collision = dust.collision; collision.enabled = false;
        var subEmitters = dust.subEmitters; subEmitters.enabled = false;
        var trails = dust.trails; trails.enabled = false;
        var sheet = dust.textureSheetAnimation; sheet.enabled = false;
        var lights = dust.lights; lights.enabled = false;
        var external = dust.externalForces; external.enabled = false;

        var color = dust.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.1f),
                new GradientAlphaKey(0.55f, 0.55f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        var size = dust.sizeOverLifetime;
        size.enabled = true;
        size.separateAxes = false;
        size.size = new ParticleSystem.MinMaxCurve(1,
            new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(0.25f, 1.5f), new Keyframe(1, 3.6f)));
        var drag = dust.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.separateAxes = false;
        drag.limit = 4f;
        drag.dampen = 0.3f;
        drag.drag = 1.2f;
        var noise = dust.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.separateAxes = true;
        noise.strengthX = 0.22f;
        noise.strengthY = 0.09f;
        noise.strengthZ = 0.22f;
        noise.frequency = 0.45f;
        noise.scrollSpeed = 0.2f;
        noise.damping = true;
        noise.octaveCount = 1;
        noise.remapEnabled = false;
        noise.positionAmount = 1;
        noise.rotationAmount = 0;
        noise.sizeAmount = 0;

        var renderer = dust.GetComponent<ParticleSystemRenderer>();
        renderer.enabled = true;
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.minParticleSize = 0;
        renderer.maxParticleSize = 0.2f;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV });
        var properties = new MaterialPropertyBlock();
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        properties.SetFloat("_SoftParticlesEnabled", pipeline != null && pipeline.supportsCameraDepthTexture ? 1 : 0);
        renderer.SetPropertyBlock(properties);
        SandDustRendererFeature.Register(dust);
        dust.Play(false);
    }

    private static void ConfigureWake(ParticleSystem haze, Material material, CarEffects settings)
    {
        ConfigureDust(haze, material);
        var main = haze.main;
        main.maxParticles = 224;
        main.startLifetime = new ParticleSystem.MinMaxCurve(settings.dustWakeLifetime * 0.75f, settings.dustWakeLifetime * 1.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(settings.dustWakeSize * 0.8f, settings.dustWakeSize * 1.2f);
        main.gravityModifier = -0.008f;
        haze.GetComponent<ParticleSystemRenderer>().maxParticleSize = 0.5f;
        var drag = haze.limitVelocityOverLifetime;
        drag.drag = 0.65f;
        var noise = haze.noise;
        noise.strengthX = 0.45f;
        noise.strengthY = 0.16f;
        noise.strengthZ = 0.45f;
        noise.frequency = 0.2f;
        noise.scrollSpeed = 0.11f;
        var wind = haze.forceOverLifetime;
        wind.enabled = true;
        wind.space = ParticleSystemSimulationSpace.World;
        wind.x = 0.16f;
        wind.y = 0;
        wind.z = 0.07f;
        var size = haze.sizeOverLifetime;
        size.size = new ParticleSystem.MinMaxCurve(1,
            new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(0.3f, 1.6f), new Keyframe(1, 3.8f)));
        var color = haze.colorOverLifetime;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.08f),
                new GradientAlphaKey(0.7f, 0.45f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
    }

    public void Tick(float deltaTime)
    {
        Rigidbody body = carBody;
        if (body == null) return;
        for (int i = 0; i < wheels.Length; i++)
        {
            WheelState state = wheels[i];
            if (state.wheel == null || !state.wheel.GetGroundHit(out WheelHit hit)
                || !(hit.collider is TerrainCollider) || hit.normal.y < 0.35f)
            {
                BreakContact(state);
                continue;
            }

            Vector3 contactVelocity = Vector3.ProjectOnPlane(body.GetPointVelocity(hit.point), hit.normal);
            float speed = contactVelocity.magnitude;
            float travelled = state.grounded ? Vector3.Distance(state.point, hit.point) : 0;
            // Respawns, teleports and surface changes must not draw long ribbons or particle streaks.
            if (state.surface != hit.collider || travelled > 3.5f) { BreakContact(state); travelled = 0; }
            float slip = Mathf.Clamp01(Mathf.Max(Mathf.Abs(hit.sidewaysSlip), Mathf.Abs(hit.forwardSlip)) * 1.5f);
            UpdateSandPalette(state, hit.collider);
            if (controller.IsBurningOut && i >= 2) slip = 1;
            // Build a readable trail at ordinary driving speeds, not only near top speed.
            float dustCruisingSpeed = Mathf.Max(settings.minSmokeSpeed + 1, Mathf.Min(18, controller.maxForwardSpeed));
            float rolling = Mathf.InverseLerp(settings.minSmokeSpeed, dustCruisingSpeed, speed);
            float target = Mathf.Clamp01(Mathf.Sqrt(rolling) * 0.85f + slip * 0.8f);
            if (speed < settings.minSmokeSpeed && slip < 0.15f) target = 0;
            state.dustAmount = Mathf.Lerp(state.dustAmount, target, 1 - Mathf.Exp(-settings.emissionSmoothing * deltaTime));
            float axleWeight = i < 2 ? 0.55f : 1;
            state.pendingParticles += state.dustAmount * axleWeight *
                (Mathf.Max(0, settings.maxSmokeEmissionRate) * deltaTime + Mathf.Max(0, settings.maxSmokeDistanceRate) * travelled);
            int count = Mathf.Min(4, Mathf.FloorToInt(state.pendingParticles));
            state.pendingParticles -= Mathf.Floor(state.pendingParticles);
            for (int particle = 0; particle < count; particle++)
            {
                Vector3 point = state.grounded ? Vector3.Lerp(state.point, hit.point, (particle + 1f) / count) : hit.point;
                EmitDust(state, point, hit.normal, contactVelocity, slip);
            }
            // Fine particles hang in the wake; coarse sand remains close to the tyre.
            // Separate bounded systems keep this a continuous plume, not a row of opaque balls.
            float wakeAxleWeight = i < 2 ? 0.45f : 1;
            state.pendingHaze += state.dustAmount * wakeAxleWeight * Mathf.Max(0, settings.dustWakeDensity) *
                (Mathf.Max(0, settings.maxSmokeEmissionRate) * 0.45f * deltaTime + Mathf.Max(0, settings.maxSmokeDistanceRate) * 0.7f * travelled);
            int hazeCount = Mathf.Min(3, Mathf.FloorToInt(state.pendingHaze));
            state.pendingHaze -= Mathf.Floor(state.pendingHaze);
            for (int particle = 0; particle < hazeCount; particle++)
            {
                Vector3 point = state.grounded ? Vector3.Lerp(state.point, hit.point, (particle + 1f) / hazeCount) : hit.point;
                EmitWake(state, point, hit.normal, contactVelocity, slip);
            }
            bool deformed = settings.enableTireTracks && settings.enableSandDeformation
                && UpdateDeformation(state, hit, slip, body.mass, speed);
            if (!deformed && settings.enableTireTracks && speed > 0.35f)
                UpdateTrack(state, hit, slip, body.mass);
            else state.hasTrack = false;
            state.point = hit.point;
            state.surface = hit.collider;
            state.grounded = true;
        }
    }

    private void EmitDust(WheelState state, Vector3 point, Vector3 normal, Vector3 velocity, float slip)
    {
        if (state.dust == null) return;
        if (!state.dust.isPlaying) state.dust.Play(false);
        Vector3 tangent = Vector3.ProjectOnPlane(Random.insideUnitSphere, normal);
        Vector3 wake = velocity * Random.Range(0.04f, 0.1f);
        float speedSpread = Mathf.Lerp(1, 1.35f, Mathf.InverseLerp(8, 35, velocity.magnitude));
        float kick = Mathf.Max(0, settings.maxSmokeParticleSpeed) * (0.25f + slip * 0.6f);
        var emit = new ParticleSystem.EmitParams {
            position = point + normal * 0.12f + tangent * 0.08f,
            velocity = wake + tangent * kick + normal * Random.Range(0.45f, 0.8f),
            startSize = Random.Range(0.4f, 0.7f) * speedSpread * (1 + slip * 0.3f),
            startLifetime = Random.Range(1f, 1.7f),
            startColor = new Color(1, 1, 1, settings.dustOpacity * Random.Range(0.65f, 0.9f)),
            applyShapeToPosition = false
        };
        state.dust.Emit(emit, 1);
    }

    private void EmitWake(WheelState state, Vector3 point, Vector3 normal, Vector3 velocity, float slip)
    {
        if (state.haze == null) return;
        if (!state.haze.isPlaying) state.haze.Play(false);
        float side = Mathf.Sign(Vector3.Dot(state.wheel.transform.position - controller.transform.position, controller.transform.right));
        Vector3 outward = Vector3.ProjectOnPlane(controller.transform.right * side, normal).normalized;
        Vector3 spread = Vector3.ProjectOnPlane(Random.insideUnitSphere, normal);
        // A faster car stirs a wider, taller turbulent wake. Without this the same
        // particles just get spaced farther apart and dust appears thinner at speed.
        float turbulence = Mathf.InverseLerp(3, 35, velocity.magnitude);
        float wakeSpread = Mathf.Lerp(1, 1.85f, turbulence);
        var emit = new ParticleSystem.EmitParams {
            position = point + normal * Random.Range(0.2f, 0.35f) + spread * 0.18f,
            velocity = velocity * Random.Range(0.1f, 0.18f) + outward * Random.Range(0.25f, 0.65f) * wakeSpread
                + spread * (0.25f + slip * 0.65f) + normal * Random.Range(0.5f, 0.9f) * Mathf.Lerp(1, 1.4f, turbulence),
            startSize = settings.dustWakeSize * Random.Range(0.8f, 1.2f) * wakeSpread * (1 + slip * 0.25f),
            startLifetime = settings.dustWakeLifetime * Random.Range(0.75f, 1.15f),
            startColor = new Color(1, 1, 1, settings.dustWakeOpacity * Random.Range(0.7f, 1)),
            applyShapeToPosition = false
        };
        state.haze.Emit(emit, 1);
    }

    private bool UpdateDeformation(WheelState state, WheelHit hit, float slip, float mass, float speed)
    {
        if (!state.checkedDeformation)
        {
            state.deformableSand = SandDeformationSurface.GetOrCreate(hit.collider as TerrainCollider);
            state.checkedDeformation = true;
        }
        if (state.deformableSand == null || !state.deformableSand.isActiveAndEnabled) return false;
        // Resting wheels do not keep digging. Wheelspin at a standstill still moves sand.
        if (speed < 0.25f && slip < 0.15f) return true;
        float load = Mathf.Clamp01(hit.force / Mathf.Max(1, mass * Physics.gravity.magnitude * 0.25f));
        if (load < 0.02f) return true;
        Vector3 right = Vector3.ProjectOnPlane(hit.sidewaysDir, hit.normal).normalized;
        if (right.sqrMagnitude < 0.5f) return true;
        Vector3 from = state.grounded ? state.point : hit.point;
        Vector3 movement = hit.point - from;
        if (movement.sqrMagnitude > 0.0001f)
        {
            Vector3 pathRight = Vector3.Cross(hit.normal, movement.normalized);
            if (Vector3.Dot(pathRight, right) < 0) pathRight = -pathRight;
            right = Vector3.Slerp(right, pathRight, slip).normalized;
        }
        float pressure = Mathf.Sqrt(load) * Mathf.Lerp(0.5f, 1, slip);
        state.deformableSand.DeformSegment(from, hit.point, right,
            settings.tireTrackWidth * (1 + slip * 0.3f),
            settings.sandRutDepth * pressure, settings.sandPileHeight * pressure,
            Mathf.Clamp(hit.sidewaysSlip, -1, 1));
        return true;
    }

    private void UpdateTrack(WheelState state, WheelHit hit, float slip, float mass)
    {
        Vector3 right = Vector3.ProjectOnPlane(hit.sidewaysDir, hit.normal).normalized;
        if (right.sqrMagnitude < 0.5f) return;
        float length = Vector3.Distance(state.trackPoint, hit.point);
        if (state.hasTrack && length >= 0.28f && length < 3.5f && Vector3.Dot(state.trackNormal, hit.normal) > 0.75f)
        {
            Vector3 pathRight = Vector3.Cross(hit.normal, (hit.point - state.trackPoint).normalized);
            if (Vector3.Dot(pathRight, right) < 0) pathRight = -pathRight;
            // A sideways slide scuffs across the path; it must not collapse into a zero-width ribbon.
            right = Vector3.Slerp(right, pathRight, slip).normalized;
            if (tracks == null) tracks = SandTireTracks.GetOrCreate();
            float load = Mathf.Clamp01(hit.force / Mathf.Max(1, mass * Physics.gravity.magnitude * 0.25f));
            float opacity = settings.tireTrackStrength * Mathf.Lerp(0.4f, 0.65f + slip * 0.35f, load);
            if (Vector3.Dot(right, state.trackRight) < 0) right = -right;
            tracks?.AddSegment(state.trackPoint, hit.point, state.trackNormal, hit.normal, state.trackRight, right,
                settings.tireTrackWidth * (1 + slip * 0.3f), state.distance, length, opacity, slip);
            state.distance += length;
        }
        else if (state.hasTrack && length < 0.28f) return;
        state.trackPoint = hit.point;
        state.trackNormal = hit.normal;
        state.trackRight = right;
        state.hasTrack = true;
    }

    private static void UpdateSandPalette(WheelState state, Collider collider)
    {
        if (state.surface == collider && state.surfaceMaterial != null) return;
        var terrain = collider.GetComponent<Terrain>();
        var material = terrain != null ? terrain.materialTemplate : null;
        if (material == null) return;
        state.surfaceMaterial = material;
        bool procedural = material.shader.name == "Death Derby/Training Sand";
        Color baseColour = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor")
            : material.HasProperty("_Color") ? material.GetColor("_Color") : new Color(0.78f, 0.55f, 0.29f);
        Color lowColour = procedural ? material.GetColor("_LowColor") : baseColour;
        // Material.GetColor already returns the stored shader value. Copy it verbatim:
        // converting it to linear again would turn the dust much darker than its source.
        var properties = new MaterialPropertyBlock();
        foreach (var dust in new[] { state.dust, state.haze })
        {
            if (dust == null) continue;
            var renderer = dust.GetComponent<ParticleSystemRenderer>();
            renderer.GetPropertyBlock(properties);
            properties.SetVector("_SandBaseColor", baseColour);
            properties.SetVector("_SandLowColor", lowColour);
            properties.SetFloat("_ProceduralSand", procedural ? 1 : 0);
            properties.SetFloat("_SandPatchScale", procedural ? material.GetFloat("_PatchScale") : 32);
            properties.SetFloat("_SandSmoothness", material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") : 0.08f);
            renderer.SetPropertyBlock(properties);
        }
    }

    private static void BreakContact(WheelState state)
    {
        state.grounded = false;
        state.hasTrack = false;
        state.surface = null;
        state.dustAmount = 0;
        state.pendingParticles = 0;
        state.pendingHaze = 0;
        state.distance = 0;
        state.deformableSand = null;
        state.checkedDeformation = false;
    }

    public void ResetContacts()
    {
        foreach (WheelState state in wheels) BreakContact(state);
    }

    public void Stop()
    {
        ResetContacts();
        foreach (WheelState state in wheels)
        {
            if (state.dust != null) state.dust.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            if (state.haze != null) state.haze.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
