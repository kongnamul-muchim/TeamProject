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

        // --- Render Graph (Modern URP) ---
        private class PassData
        {
            public Material material;
            public Color color;
            public float distance;
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
            TextureHandle depth = resourceData.activeDepthTexture;

            // 소스 텍스처가 유효하지 않으면 실행 안 함
            if (!source.IsValid()) return;

            // 카메라 타겟과 동일한 설정으로 임시 텍스처 생성
            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0; // 컬러만 담을 것이므로 0
            TextureHandle temp = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_UnderwaterTemp", false);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Underwater Effects", out var passData))
            {
                passData.material = settings.material;
                passData.color = settings.color;
                passData.distance = settings.distance;
                passData.alpha = settings.alpha;
                passData.refraction = settings.refraction;
                passData.normalmap = settings.normalmap;
                passData.UV = settings.UV;
                passData.source = source;

                // 텍스처 사용권 획득
                builder.UseTexture(source, AccessFlags.Read);
                if (depth.IsValid()) builder.UseTexture(depth, AccessFlags.Read);
                
                // 출력 타겟을 임시 텍스처(temp)로 설정
                builder.SetRenderAttachment(temp, 0, AccessFlags.Write);
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    // 매테리얼 파라미터 실시간 동기화
                    data.material.SetColor("_color", data.color);
                    data.material.SetFloat("_dis", data.distance);
                    data.material.SetFloat("_alpha", data.alpha);
                    data.material.SetFloat("_refraction", data.refraction);
                    data.material.SetTexture("_NormalMap", data.normalmap);
                    data.material.SetVector("_normalUV", data.UV);

                    // 화면(source)에 쉐이더를 먹여서 temp에 그림
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // [핵심] 효과가 적용된 temp 텍스처를 이후 단계의 활성 컬러 텍스처로 지정 (Chaining)
            resourceData.activeColorTexture = temp;
        }

        // --- Compatibility Mode (Older URP) ---
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (settings.material == null) return;
            CommandBuffer cmd = CommandBufferPool.Get("Underwater Effects");
            RTHandle source = renderingData.cameraData.renderer.cameraColorTargetHandle;
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref m_TempTexture, desc, name: "_UnderwaterTemp");

            settings.material.SetColor("_color", settings.color);
            settings.material.SetFloat("_dis", settings.distance);
            settings.material.SetFloat("_alpha", settings.alpha);
            settings.material.SetFloat("_refraction", settings.refraction);
            settings.material.SetTexture("_NormalMap", settings.normalmap);
            settings.material.SetVector("_normalUV", settings.UV);

            Blitter.BlitCameraTexture(cmd, source, m_TempTexture, settings.material, 0);
            Blitter.BlitCameraTexture(cmd, m_TempTexture, source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

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
