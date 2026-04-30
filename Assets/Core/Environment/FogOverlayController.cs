using UnityEngine;
using UnityEngine.UI;

namespace HideAndInk.Core.Environment
{
    /// <summary>
    /// 2D 안개 오버레이 셰이더를 제어하는 컨트롤러.
    /// Canvas 아래 RawImage에 부착하여 사용합니다.
    /// 
    /// 사용 방법:
    /// 1. Canvas 아래에 RawImage 생성 (화면 전체 덮기)
    /// 2. RawImage에 Custom/FogOverlay 셰이더가 적용된 머티리얼 할당
    /// 3. 이 스크립트를 RawImage가 있는 GameObject에 부착
    /// 4. Noise Texture에 Perlin/Simplex 노이즈 텍스처 할당
    /// 
    /// 인스펙터 설정:
    ///   Density: 안개 밀도 (0=투명, 1=불투명)
    ///   Speed: 안개 이동 속도/방향 (X, Y)
    ///   Fog Color: 안개 색상
    /// </summary>
    public class FogOverlayController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("안개가 적용된 RawImage (미할당 시 자동 탐색)")]
        [SerializeField] private RawImage _rawImage;

        [Header("Fog Settings")]
        [Tooltip("안개 밀도 (0=투명, 1=불투명)")]
        [SerializeField] private float _density = 0.25f;

        [Tooltip("안개 이동 속도/방향")]
        [SerializeField] private Vector2 _speed = new Vector2(0.02f, 0.01f);

        [Tooltip("안개 색상")]
        [SerializeField] private Color _fogColor = new Color(0.7f, 0.7f, 0.75f, 1f);

        [Tooltip("노이즈 텍스처 (Perlin/Simplex 노이즈 권장)")]
        [SerializeField] private Texture2D _noiseTexture;

        [Tooltip("노이즈 타일링 크기 (클수록 노이즈가 더 넓게 퍼짐)")]
        [SerializeField] private Vector2 _noiseTiling = new Vector2(1f, 1f);

        [Header("Shader Properties")]
        [SerializeField] private string _densityProperty = "_Density";
        [SerializeField] private string _speedProperty = "_Speed";
        [SerializeField] private string _fogColorProperty = "_FogColor";

        private Material _material;

        /// <summary>안개 밀도 (0=투명, 1=불투명)</summary>
        public float Density
        {
            get => _density;
            set
            {
                _density = Mathf.Clamp01(value);
                if (_material != null)
                    _material.SetFloat(_densityProperty, _density);
            }
        }

        /// <summary>안개 이동 속도/방향</summary>
        public Vector2 Speed
        {
            get => _speed;
            set
            {
                _speed = value;
                if (_material != null)
                    _material.SetVector(_speedProperty, _speed);
            }
        }

        /// <summary>안개 색상</summary>
        public Color FogColor
        {
            get => _fogColor;
            set
            {
                _fogColor = value;
                if (_material != null)
                    _material.SetColor(_fogColorProperty, _fogColor);
            }
        }

        private void Awake()
        {
            if (_rawImage == null)
            {
                _rawImage = GetComponent<RawImage>();
            }

            if (_rawImage != null)
            {
                _material = _rawImage.material;
            }

            ApplySettings();
        }

        /// <summary>
        /// 셰이더 프로퍼티를 현재 인스펙터 값으로 동기화합니다.
        /// </summary>
        public void ApplySettings()
        {
            if (_material == null) return;

            _material.SetFloat(_densityProperty, _density);
            _material.SetVector(_speedProperty, _speed);
            _material.SetColor(_fogColorProperty, _fogColor);

            if (_noiseTexture != null)
            {
                _material.SetTexture("_NoiseTexture", _noiseTexture);
            }

            // 노이즈 타일링 적용
            _material.SetVector("_NoiseTexture_ST", new Vector4(_noiseTiling.x, _noiseTiling.y, 0, 0));
        }

        /// <summary>
        /// 안개를 서서히 나타나게 합니다.
        /// </summary>
        /// <param name="targetDensity">목표 밀도</param>
        /// <param name="duration">변화 시간 (초)</param>
        public System.Collections.IEnumerator FadeIn(float targetDensity = 0.25f, float duration = 2f)
        {
            float startDensity = _density;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Density = Mathf.Lerp(startDensity, targetDensity, t);
                yield return null;
            }

            Density = targetDensity;
        }

        /// <summary>
        /// 안개를 서서히 사라지게 합니다.
        /// </summary>
        /// <param name="duration">변화 시간 (초)</param>
        public System.Collections.IEnumerator FadeOut(float duration = 2f)
        {
            float startDensity = _density;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Density = Mathf.Lerp(startDensity, 0f, t);
                yield return null;
            }

            Density = 0f;
        }
    }
}