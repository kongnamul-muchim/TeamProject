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

            // 콘솔창에서 이 로그가 뜨는지 확인해 주세요!
            Debug.Log("Underwater Pass Executing...");

            CommandBuffer cmd = CommandBufferPool.Get("Underwater Effects");
            
            // 현재 카메라의 컬러 타겟 핸들 가져오기
            RTHandle source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            // 매테리얼 파라미터 업데이트
            settings.material.SetColor("_color", settings.color);
            settings.material.SetFloat("_dis", settings.distance);
            settings.material.SetFloat("_alpha", settings.alpha);
            settings.material.SetFloat("_refraction", settings.refraction);
            settings.material.SetTexture("_NormalMap", settings.normalmap);
            settings.material.SetVector("_normalUV", settings.UV);

            // [변경] 가장 보편적인 Blit 방식으로 교체
            cmd.SetGlobalTexture("_MainTex", source);
            cmd.Blit(source, m_TempTexture, settings.material);
            cmd.Blit(m_TempTexture, source);

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
