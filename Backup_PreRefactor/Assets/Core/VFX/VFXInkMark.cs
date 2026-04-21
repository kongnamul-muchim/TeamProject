using UnityEngine;

namespace HideAndInk.Core.VFX
{
    /// <summary>
    /// InkMark VFX가 바닥에 깔린 후 서서히 사라지는 컴포넌트
    /// vfx_InkMark_01~04에 부착
    /// </summary>
    public class VFXInkMark : MonoBehaviour
    {
        [Header("Fade 설정")]
        [Tooltip("Fade 시작 전 대기 시간 (초)")]
        [SerializeField] private float delayBeforeFade = 1f;

        [Tooltip("Fade 지속 시간 (초)")]
        [SerializeField] private float fadeDuration = 2f;

        private SpriteRenderer _spriteRenderer;
        private float _elapsedTime;
        private bool _isFading;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            _elapsedTime += Time.deltaTime;

            // 대기 시간 경과 후 Fade 시작
            if (!_isFading && _elapsedTime >= delayBeforeFade)
            {
                _isFading = true;
            }

            // Fade 진행
            if (_isFading && _spriteRenderer != null)
            {
                float fadeProgress = (_elapsedTime - delayBeforeFade) / fadeDuration;
                fadeProgress = Mathf.Clamp01(fadeProgress);

                Color color = _spriteRenderer.color;
                color.a = Mathf.Lerp(1f, 0f, fadeProgress);
                _spriteRenderer.color = color;

                // Fade 완료 시 삭제
                if (fadeProgress >= 1f)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
