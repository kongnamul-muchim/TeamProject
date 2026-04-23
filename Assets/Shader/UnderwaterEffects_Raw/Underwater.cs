using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
            // 최신 URP 필수 입력 선언
            ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // 임시 텍스처 생성 (카메라와 동일한 해상도)
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref m_TempTexture, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_UnderwaterTemp");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (settings.material == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("Underwater Effects");
            
            // 최신 URP에서는 renderer에서 직접 타겟 핸들을 가져옵니다.
            RTHandle source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            // 매테리얼 변수 강제 업데이트
            settings.material.SetColor("_color", settings.color);
            settings.material.SetFloat("_dis", settings.distance);
            settings.material.SetFloat("_alpha", settings.alpha);
            settings.material.SetFloat("_refraction", settings.refraction);
            settings.material.SetTexture("_NormalMap", settings.normalmap);
            settings.material.SetVector("_normalUV", settings.UV);

            // [핵심] Blitter를 이용한 화면 복사 및 쉐이더 적용
            // 1. 카메라 화면(source)을 임시 텍스처(m_TempTexture)로 옮기면서 수중 쉐이더 적용
            Blitter.BlitCameraTexture(cmd, source, m_TempTexture, settings.material, 0);
            
            // 2. 쉐이더가 적용된 임시 텍스처를 다시 카메라 화면(source)으로 덮어쓰기
            Blitter.BlitCameraTexture(cmd, m_TempTexture, source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Cleanup()
        {
            m_TempTexture?.Release();
        }
    }

    Pass m_Pass;

    public override void Create()
    {
        m_Pass = new Pass();
        m_Pass.settings = settings;
        m_Pass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // 렌더링 패스 등록
        renderer.EnqueuePass(m_Pass);
    }

    protected override void Dispose(bool disposing)
    {
        m_Pass?.Cleanup();
    }
}
