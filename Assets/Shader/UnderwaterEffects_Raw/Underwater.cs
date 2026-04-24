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
        public Color color = Color.cyan;
        [Range(0, 1)] public float alpha = 0.5f;
        [Header("Refraction Settings")]
        public float refraction = 1f;
        public Texture normalmap;
        public Vector4 UV = new Vector4(1, 1, 0.2f, 0.1f);
    }

    public Settings settings = new Settings();

    class Pass : ScriptableRenderPass
    {
        public Settings settings;

        private class PassData
        {
            public Material material;
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

            // 1. 첫 번째 패스: Source -> Temp (쉐이더 적용)
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Underwater Pass", out var passData))
            {
                passData.material = settings.material;
                passData.source = source;

                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(temp, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    data.material.SetColor("_color", settings.color);
                    data.material.SetFloat("_alpha", settings.alpha);
                    data.material.SetFloat("_refraction", settings.refraction);
                    data.material.SetTexture("_NormalMap", settings.normalmap);
                    data.material.SetVector("_normalUV", settings.UV);

                    // 가장 확실한 Blit 호출
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // 2. 두 번째 패스: Temp -> Source (다시 덮어쓰기)
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Underwater Final", out var passData))
            {
                passData.source = temp;
                builder.UseTexture(temp, AccessFlags.Read);
                builder.SetRenderAttachment(source, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) { }
    }

    Pass m_Pass;
    public override void Create() => m_Pass = new Pass { settings = settings, renderPassEvent = settings.renderPassEvent };
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) => renderer.EnqueuePass(m_Pass);
}
