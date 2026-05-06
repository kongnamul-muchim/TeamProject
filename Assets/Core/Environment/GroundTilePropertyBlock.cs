using UnityEngine;

namespace HideAndInk.Core.Environment
{
    /// <summary>
    /// MaterialPropertyBlock을 사용하여 공유 매터리얼의 개별 속성을 오버라이드합니다.
    /// 각 Ground 오브젝트에 부착하면 Inspector에서 Tiling, Color 등을 개별 제어할 수 있습니다.
    /// 
    /// 장점:
    /// - 매터리얼 인스턴스 생성 없이 개별 속성 제어 (메모리 절약)
    /// - GPU 인스팅싱 호환 (SRP Batcher 지원)
    /// - Inspector에서 직관적으로 값 조정 가능
    /// 
    /// 사용 방법:
    /// 1. Ground 오브젝트에 이 컴포넌트를 Add Component
    /// 2. Inspector에서 Tiling, Color 등 원하는 값으로 조정
    /// 3. 런타임에도 값 변경 가능 (SetPropertyBlock 자동 호출)
    /// </summary>
    [ExecuteInEditMode]
    public class GroundTilePropertyBlock : MonoBehaviour
    {
        [Header("Texture Tiling & Offset")]
        [Tooltip("메인 텍스처 타일링 (X=가로, Y=세로)")]
        [SerializeField] private Vector2 _tiling = new Vector2(10f, 3f);

        [Tooltip("메인 텍스처 오프셋")]
        [SerializeField] private Vector2 _offset = Vector2.zero;

        [Header("Color")]
        [Tooltip("베이스 컬러 (매터리얼 색상 오버라이드)")]
        [SerializeField] private Color _baseColor = Color.white;

        [Header("Surface")]
        [Tooltip("부드러움 (0=거침, 1=매끈)")]
        [SerializeField, Range(0f, 1f)] private float _smoothness = 0f;

        [Tooltip("금속성 (0=비금속, 1=금속)")]
        [SerializeField, Range(0f, 1f)] private float _metallic = 0f;

        private MaterialPropertyBlock _propertyBlock;
        private Renderer _renderer;

        /// <summary>Inspector에서 값 변경 시 자동으로 PropertyBlock 업데이트</summary>
        private void OnValidate()
        {
            ApplyProperties();
        }

        private void Awake()
        {
            ApplyProperties();
        }

        private void Start()
        {
            ApplyProperties();
        }

        /// <summary>
        /// MaterialPropertyBlock을 생성/업데이트하여 Renderer에 적용합니다.
        /// </summary>
        public void ApplyProperties()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<Renderer>();
            }

            if (_renderer == null)
            {
                Debug.LogWarning($"[GroundTilePropertyBlock] '{name}'에 Renderer가 없습니다.");
                return;
            }

            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            // 현재 값 가져오기 (기존 매터리얼 값 유지)
            _renderer.GetPropertyBlock(_propertyBlock);

            // Tiling & Offset 적용 (_BaseMap, _MainTex 모두)
            _propertyBlock.SetVector("_BaseMap_ST", new Vector4(_tiling.x, _tiling.y, _offset.x, _offset.y));
            _propertyBlock.SetVector("_MainTex_ST", new Vector4(_tiling.x, _tiling.y, _offset.x, _offset.y));

            // Color 적용
            _propertyBlock.SetColor("_BaseColor", _baseColor);
            _propertyBlock.SetColor("_Color", _baseColor);

            // Surface 속성 적용
            _propertyBlock.SetFloat("_Smoothness", _smoothness);
            _propertyBlock.SetFloat("_Metallic", _metallic);

            // Renderer에 PropertyBlock 적용
            _renderer.SetPropertyBlock(_propertyBlock);
        }

        // ─── 런타임 API ──────────────────────────────────────────

        /// <summary>런타임에 Tiling 값을 변경합니다.</summary>
        public void SetTiling(Vector2 tiling)
        {
            _tiling = tiling;
            ApplyProperties();
        }

        /// <summary>런타임에 Color를 변경합니다.</summary>
        public void SetColor(Color color)
        {
            _baseColor = color;
            ApplyProperties();
        }

        /// <summary>런타임에 Smoothness를 변경합니다.</summary>
        public void SetSmoothness(float smoothness)
        {
            _smoothness = Mathf.Clamp01(smoothness);
            ApplyProperties();
        }

        /// <summary>런타임에 Metallic을 변경합니다.</summary>
        public void SetMetallic(float metallic)
        {
            _metallic = Mathf.Clamp01(metallic);
            ApplyProperties();
        }

        /// <summary>PropertyBlock을 제거하고 공유 매터리얼 원본 값으로 되돌립니다.</summary>
        public void ResetToSharedMaterial()
        {
            if (_renderer != null)
            {
                _renderer.SetPropertyBlock(null);
            }
        }
    }
}
