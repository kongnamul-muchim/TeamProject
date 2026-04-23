using UnityEngine;

namespace HideAndInk.ParallaxSystem
{
    /// <summary>
    /// 배경 Sprite를 카메라 뷰포트에 꽉 차게 자동 스케일링
    /// SRP: 스케일 맞춤만 담당, DI: [SerializeField]로 카메라 참조
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BackgroundAutoFit : MonoBehaviour
    {
        [Header("DI - 기준 카메라 (미할당 시 Camera.main 사용)")]
        [SerializeField] private Camera targetCamera;

        [Header("피팅 모드")]
        [SerializeField] private FitMode fitMode = FitMode.Fill;

        [Header("여백 오프셋 (비율)")]
        [SerializeField] private float paddingX;
        [SerializeField] private float paddingY;

        private SpriteRenderer _spriteRenderer;
        private float _cachedOrthoSize;
        private float _cachedAspect;

        public enum FitMode
        {
            Fill,   // 화면에 꽉 채움 (잘릴 수 있음)
            Fit     // 화면 안에 모두 표시 (여백 생길 수 있음)
        }

        private void Start()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();

            if (targetCamera == null)
                targetCamera = Camera.main;

            ApplyFit();
        }

        private void LateUpdate()
        {
            if (targetCamera == null || !_spriteRenderer.sprite) return;

            float orthoSize = targetCamera.orthographicSize;
            float aspect = targetCamera.aspect;

            if (Mathf.Abs(orthoSize - _cachedOrthoSize) > 0.01f ||
                Mathf.Abs(aspect - _cachedAspect) > 0.01f)
            {
                ApplyFit();
            }
        }

        private void ApplyFit()
        {
            if (targetCamera == null || _spriteRenderer.sprite == null) return;

            _cachedOrthoSize = targetCamera.orthographicSize;
            _cachedAspect = targetCamera.aspect;

            Sprite sprite = _spriteRenderer.sprite;
            float spriteW = sprite.bounds.size.x;
            float spriteH = sprite.bounds.size.y;

            float camH = _cachedOrthoSize * 2f;
            float camW = camH * _cachedAspect;

            float scaleX = (camW + paddingX) / spriteW;
            float scaleY = (camH + paddingY) / spriteH;

            float scale = fitMode == FitMode.Fill
                ? Mathf.Max(scaleX, scaleY)
                : Mathf.Min(scaleX, scaleY);

            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
