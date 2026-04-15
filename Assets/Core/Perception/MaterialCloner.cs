using UnityEngine;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 메테리얼 복사 시스템 구현체
    /// PropertyBlock을 사용하여 공유 메테리얼의 색상만 변경
    /// </summary>
    public sealed class MaterialCloner : IMaterialCloner
    {
        private readonly Renderer _playerRenderer;
        private Color _originalColor;
        private Color _currentColor;
        private MaterialPropertyBlock _propertyBlock;
        private bool _isBlending;

        // Shader 속성 이름 (Unity 기본 Standard Shader)
        private const string COLOR_PROPERTY = "_Color";

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="playerRenderer">플레이어의 Renderer 컴포넌트</param>
        public MaterialCloner(Renderer playerRenderer)
        {
            _playerRenderer = playerRenderer;
            _propertyBlock = new MaterialPropertyBlock();
            _isBlending = false;

            // 원본 색상 저장
            if (_playerRenderer != null)
            {
                _playerRenderer.GetPropertyBlock(_propertyBlock);
                if (_propertyBlock.HasProperty(COLOR_PROPERTY))
                {
                    _originalColor = _propertyBlock.GetColor(COLOR_PROPERTY);
                }
                else
                {
                    // 기본값 (흰색)
                    _originalColor = Color.white;
                }
                _currentColor = _originalColor;
            }
        }

        /// <summary>
        /// 타겟 오브젝트의 색상 가져옴
        /// </summary>
        public Color GetTargetColor(GameObject target)
        {
            if (target == null) return Color.white;

            Renderer targetRenderer = target.GetComponent<Renderer>();
            if (targetRenderer == null) return Color.white;

            MaterialPropertyBlock targetBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(targetBlock);

            if (targetBlock.HasProperty(COLOR_PROPERTY))
            {
                return targetBlock.GetColor(COLOR_PROPERTY);
            }

            // 메테리얼에서 직접 색상 가져오기
            if (targetRenderer.sharedMaterial.HasProperty(COLOR_PROPERTY))
            {
                return targetRenderer.sharedMaterial.GetColor(COLOR_PROPERTY);
            }

            return Color.white;
        }

        /// <summary>
        /// 플레이어 색상을 타겟으로 보간
        /// </summary>
        public void BlendToTarget(GameObject target, float progress)
        {
            if (target == null || _playerRenderer == null) return;

            Color targetColor = GetTargetColor(target);
            _currentColor = Color.Lerp(_originalColor, targetColor, progress);

            ApplyColor(_currentColor);
            _isBlending = true;
        }

        /// <summary>
        /// 원본 색상으로 복원
        /// </summary>
        public void RestoreOriginalColor()
        {
            _currentColor = _originalColor;
            ApplyColor(_originalColor);
            _isBlending = false;
        }

        /// <summary>
        /// 원본 색상 설정
        /// </summary>
        public void SetOriginalColor(Color originalColor)
        {
            _originalColor = originalColor;
            if (!_isBlending)
            {
                _currentColor = originalColor;
                ApplyColor(_currentColor);
            }
        }

        /// <summary>
        /// 색상 적용
        /// </summary>
        private void ApplyColor(Color color)
        {
            if (_playerRenderer == null) return;

            _playerRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(COLOR_PROPERTY, color);
            _playerRenderer.SetPropertyBlock(_propertyBlock);
        }

        /// <summary>
        /// 현재 색상 확인
        /// </summary>
        public Color CurrentColor => _currentColor;

        /// <summary>
        /// 원본 색상 확인
        /// </summary>
        public Color OriginalColor => _originalColor;
    }
}