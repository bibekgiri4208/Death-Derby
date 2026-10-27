using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>Resolve the whole dust volume once, rather than repeatedly mixing yellow sprites with blue scenery.</summary>
public sealed class SandDustRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader compositeShader;
    private Material composite;
    private DustPass pass;
    private static readonly List<ParticleSystem> Emitters = new List<ParticleSystem>();

    internal static void Register(ParticleSystem emitter)
    {
        if (!Emitters.Contains(emitter)) Emitters.Add(emitter);
    }

    private static bool HasParticles()
    {
        bool found = false;
        for (int i = Emitters.Count - 1; i >= 0; i--)
        {
            var emitter = Emitters[i];
            if (emitter == null) { Emitters.RemoveAt(i); continue; }
            if (emitter.gameObject.activeInHierarchy && emitter.particleCount > 0
                && emitter.GetComponent<ParticleSystemRenderer>().enabled) found = true;
        }
        return found;
    }

    public override void Create()
    {
        CoreUtils.Destroy(composite);
        if (compositeShader == null) return;
        composite = CoreUtils.CreateEngineMaterial(compositeShader);
        pass = new DustPass(composite);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass != null && composite != null && HasParticles()) renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(composite);
    }

    private sealed class DustPass : ScriptableRenderPass
    {
        private static readonly ShaderTagId DustTag = new ShaderTagId("SandDustVolume");
        private static readonly int VolumeId = Shader.PropertyToID("_SandDustVolume");
        private readonly Material material;

        private sealed class AccumulateData { public RendererListHandle renderers; }
        private sealed class CompositeData
        {
            public TextureHandle scene, volume;
            public Material material;
        }

        public DustPass(Material material)
        {
            this.material = material;
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            requiresIntermediateTexture = true;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return;
            var camera = frameData.Get<UniversalCameraData>();
            var rendering = frameData.Get<UniversalRenderingData>();
            var lights = frameData.Get<UniversalLightData>();
            var source = resources.activeColorTexture;
            var volumeDesc = graph.GetTextureDesc(source);
            volumeDesc.name = "Sand Dust Optical Depth";
            volumeDesc.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
            volumeDesc.clearBuffer = true;
            volumeDesc.clearColor = Color.clear;
            var volume = graph.CreateTexture(volumeDesc);
            using (var builder = graph.AddRasterRenderPass<AccumulateData>("Accumulate Sand Dust", out var data))
            {
                var drawing = RenderingUtils.CreateDrawingSettings(DustTag, rendering, camera, lights, SortingCriteria.None);
                var filtering = new FilteringSettings(RenderQueueRange.transparent, camera.camera.cullingMask);
                data.renderers = graph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filtering));
                builder.UseRendererList(data.renderers);
                builder.SetRenderAttachment(volume, 0);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                builder.UseAllGlobalTextures(true);
                builder.SetRenderFunc(static (AccumulateData data, RasterGraphContext context) =>
                    context.cmd.DrawRendererList(data.renderers));
            }
            var destinationDesc = graph.GetTextureDesc(source);
            destinationDesc.name = "Camera Colour With Sand Dust";
            destinationDesc.clearBuffer = false;
            var destination = graph.CreateTexture(destinationDesc);
            using (var builder = graph.AddRasterRenderPass<CompositeData>("Resolve Sand Dust", out var data))
            {
                data.scene = source;
                data.volume = volume;
                data.material = material;
                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(volume, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0);
                builder.SetRenderFunc(static (CompositeData data, RasterGraphContext context) =>
                {
                    data.material.SetTexture(VolumeId, data.volume);
                    Blitter.BlitTexture(context.cmd, data.scene, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }
            resources.cameraColor = destination;
        }
    }
}
