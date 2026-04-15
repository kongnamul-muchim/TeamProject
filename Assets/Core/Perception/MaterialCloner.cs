using UnityEngine;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 메테리얼 복사 시스템 구현체
    /// SpriteRenderer와 일반 Renderer 둘 다 지원
    /// </summary>
    public sealed class MaterialCloner : IMaterialCloner
    {
        private Renderer _renderer;
        private SpriteRenderer _spriteRenderer;
        private Color _originalColor;
        private Color _currentColor;
        private MaterialPropertyBlock _propertyBlock;
        private bool _isBlending;

        // Shader 속성 이름 (Standard Shader)
        private const string COLOR_PROPERTY = "_Color";

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="playerRenderer">플레이어의 Renderer 컴포넌트</param>
        public MaterialCloner(Renderer playerRenderer)
        {
            _renderer = playerRenderer;
            _spriteRenderer = playerRenderer as SpriteRenderer;
            _propertyBlock = new MaterialPropertyBlock();
            _isBlending = false;

            // 원본 색상 저장
            if (_spriteRenderer != null)
            {
                // SpriteRenderer는 color 프로퍼티로 접근
                _originalColor = _spriteRenderer.color;
            }
            else if (_renderer != null)
            {
                // 일반 Renderer는 PropertyBlock으로 접근
                _renderer.GetPropertyBlock(_propertyBlock);
                if (_propertyBlock.HasProperty(COLOR_PROPERTY))
                {
                    _originalColor = _propertyBlock.GetColor(COLOR_PROPERTY);
                }
                else
                {
                    _originalColor = Color.white;
                }
            }
            else
            {
                _originalColor = Color.white;
            }
            
            _currentColor = _originalColor;
        }

        /// <summary>
        /// 타겟 오브젝트의 색상 가져옴
        /// </summary>
        public Color GetTargetColor(GameObject target)
        {
            if (target == null) return Color.white;

            Renderer targetRenderer = target.GetComponent<Renderer>();
            if (targetRenderer == null) return Color.white;

            // SpriteRenderer 체크
            SpriteRenderer targetSprite = targetRenderer as SpriteRenderer;
            if (targetSprite != null)
            {
                return targetSprite.color;
            }

            // 일반 Renderer
            MaterialPropertyBlock targetBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(targetBlock);

            if (targetBlock.HasProperty(COLOR_PROPERTY))
            {
                return targetBlock.GetColor(COLOR_PROPERTY);
            }

            // 메테리얼에서 직접 색상 가져오기
            if (targetRenderer.sharedMaterial != null && 
                targetRenderer.sharedMaterial.HasProperty(COLOR_PROPERTY))
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
            if (target == null) return;

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
        /// 원본 색상으로 천천히 복원 (보간)
        /// </summary>
        /// <param name="progress">보간 진행도 (0~1)</param>
        public void BlendToOriginal(float progress)
        {
            _currentColor = Color.Lerp(_currentColor, _originalColor, progress);
            ApplyColor(_currentColor);
            _isBlending = false; // 원래 색으로 복원 중이므로 blen
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
            if (_spriteRenderer != null)
            {
                // SpriteRenderer는 color 프로퍼티로 직접 설정
                _spriteRenderer.color = color;
            }
            else if (_renderer != null)
            {
                // 일반 Renderer는 PropertyBlock 사용
                _renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(COLOR_PROPERTY, color);
                _renderer.SetPropertyBlock(_propertyBlock);
            }
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