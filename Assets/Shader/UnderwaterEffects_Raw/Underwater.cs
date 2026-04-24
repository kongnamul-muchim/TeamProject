using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class Underwater : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material material;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        [Header("Fog Settings")]
        public Color color = Color.red;
        public float distance = 10f;
        [Range(0, 1)] public float alpha = 1f;
        [Header("Refraction Settings")]
        public float refraction = 1f;
        public Texture normalmap;
        public Vector4 UV = new Vector4(1, 1, 0.2f, 0.1f);
    }

    public Settings settings = new Settings();

    class Pass : ScriptableRenderPass
    {
        public Settings settings;
        private RTHandle m_TempTexture;

        public Pass()
        {
            ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
        }

        private class PassData
        {
            public Material material;
            public Color color;
            public float alpha;
            public float refraction;
            public Texture normalmap;
            public Vector4 UV;
            public TextureHandle source;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (settings.material == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid()) return;

            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            TextureHandle temp = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_UnderwaterTemp", false);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Underwater Effects", out var passData))
            {
                passData.material = settings.material;
                passData.color = settings.color;
                passData.alpha = settings.alpha;
                passData.refraction = settings.refraction;
                passData.normalmap = settings.normalmap;
                passData.UV = settings.UV;
                passData.source = source;

                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(temp, 0, AccessFlags.Write);
                builder.AllowPassOptimization(false);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    data.material.SetColor("_color", data.color);
                    data.material.SetFloat("_alpha", data.alpha);
                    data.material.SetFloat("_refraction", data.refraction);
                    data.material.SetTexture("_NormalMap", data.normalmap);
                    data.material.SetVector("_normalUV", data.UV);

                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Underwater Copy Back", out var passData))
            {
                passData.source = temp;
                builder.UseTexture(temp, AccessFlags.Read);
                builder.SetRenderAttachment(source, 0, AccessFlags.Write);
                builder.AllowPassOptimization(false);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) { }

        public void Cleanup() => m_TempTexture?.Release();
    }

    Pass m_Pass;

    public override void Create()
    {
        m_Pass = new Pass { settings = settings, renderPassEvent = settings.renderPassEvent };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_Pass);
    }

    protected override void Dispose(bool disposing) => m_Pass?.Cleanup();
}
