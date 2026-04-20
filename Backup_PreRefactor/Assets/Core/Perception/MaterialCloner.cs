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

        // Material 관리
        private Material _defaultMaterial;
        private Material _octopusMaterial;
        private bool _isUsingOctopusMaterial;

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
            _isUsingOctopusMaterial = false;

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

            // 기본 Material 저장
            if (_spriteRenderer != null)
            {
                _defaultMaterial = _spriteRenderer.material;
            }
            else if (_renderer != null)
            {
                _defaultMaterial = _renderer.material;
            }

            // Octopus Material 로드 (Resources에서)
            _octopusMaterial = Resources.Load<Material>("Materials/Octopus");
            Debug.Log($"[MaterialCloner] Awake: _defaultMaterial={_defaultMaterial?.name ?? "null"}, _octopusMaterial={_octopusMaterial?.name ?? "null"}, path=Materials/Octopus");
        }

        /// <summary>
        /// 타겟 오브젝트의 색상 가져옴
        /// CamouflageTarget 컴포넌트 우선 확인 → 없으면 기존 방식
        /// </summary>
        public Color GetTargetColor(GameObject target)
        {
            if (target == null) return Color.white;

            // 1순위: CamouflageTarget 컴포넌트 확인 (ScriptableObject 기반 색상)
            CamouflageTarget camoTarget = target.GetComponent<CamouflageTarget>();
            if (camoTarget != null)
            {
                Color dataColor = camoTarget.GetCamouflageColor();
                Debug.Log($"[MaterialCloner] GetTargetColor from CamouflageTarget: {dataColor}");
                return dataColor;
            }

            // 2순위: 기존 방식 (Renderer 색상)
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

        /// <summary>
        /// Octopus Material로 전환 (의태 시 사용)
        /// </summary>
        public void ApplyOctopusMaterial()
        {
            Debug.Log($"[MaterialCloner] ApplyOctopusMaterial called. _octopusMaterial is null: {_octopusMaterial == null}, _spriteRenderer is null: {_spriteRenderer == null}, _renderer is null: {_renderer == null}");
            if (_octopusMaterial == null) return;

            if (_spriteRenderer != null)
            {
                _spriteRenderer.material = _octopusMaterial;
                _isUsingOctopusMaterial = true;
                Debug.Log("[MaterialCloner] Applied to SpriteRenderer");
            }
            else if (_renderer != null)
            {
                _renderer.material = _octopusMaterial;
                _isUsingOctopusMaterial = true;
                Debug.Log("[MaterialCloner] Applied to Renderer");
            }
        }

        /// <summary>
        /// Default Material로 복원 (의태 해제 시 사용)
        /// </summary>
        public void RestoreDefaultMaterial()
        {
            if (_defaultMaterial == null) return;

            if (_spriteRenderer != null)
            {
                _spriteRenderer.material = _defaultMaterial;
                _isUsingOctopusMaterial = false;
            }
            else if (_renderer != null)
            {
                _renderer.material = _defaultMaterial;
                _isUsingOctopusMaterial = false;
            }
        }

        /// <summary>
        /// Octopus Material 사용 중인지 확인
        /// </summary>
        public bool IsUsingOctopusMaterial => _isUsingOctopusMaterial;

        /// <summary>
        /// OriginalRate 설정 (의태 강도 조절)
        /// </summary>
        /// <param name="rate">0.0 ~ 1.0</param>
        public void SetOriginalRate(float rate)
        {
            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat("_OriginalRate", rate);
                _renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        /// <summary>
        /// 현재 Material의 색상 가져오기 (shader 프로퍼티에서)
        /// </summary>
        public Color GetCurrentMaterialColor()
        {
            if (_renderer != null)
            {
                // PropertyBlock에서 _Color 가져오기
                _renderer.GetPropertyBlock(_propertyBlock);
                if (_propertyBlock.HasProperty(COLOR_PROPERTY))
                {
                    Color c = _propertyBlock.GetColor(COLOR_PROPERTY);
                    Debug.Log($"[MaterialCloner] GetCurrentMaterialColor from PropertyBlock: {c}");
                    return c;
                }
                
                // 또는 material에서 직접 가져오기
                if (_renderer.sharedMaterial != null && 
                    _renderer.sharedMaterial.HasProperty(COLOR_PROPERTY))
                {
                    Color c = _renderer.sharedMaterial.GetColor(COLOR_PROPERTY);
                    Debug.Log($"[MaterialCloner] GetCurrentMaterialColor from sharedMaterial: {c}");
                    return c;
                }
                
                // SpriteRenderer면 color 프로퍼티 사용
                if (_spriteRenderer != null)
                {
                    Color c = _spriteRenderer.color;
                    Debug.Log($"[MaterialCloner] GetCurrentMaterialColor from SpriteRenderer.color: {c}");
                    return c;
                }
            }
            Debug.Log("[MaterialCloner] GetCurrentMaterialColor returning white (no renderer)");
            return Color.white;
        }

        /// <summary>
        /// 타겟 오브젝트의 Material을 ZWrite가 켜진 Material로 교체 (2D OutlineHidden용)
        /// 의태 해제 시 RestoreTargetMaterial()로 복원 필요
        /// </summary>
        private Material _originalTargetMaterial;

        public void EnableTargetZWrite(GameObject target)
        {
            if (target == null) return;

            Renderer targetRenderer = target.GetComponent<Renderer>();
            if (targetRenderer == null) return;

            // 원본 Material 저장
            _originalTargetMaterial = targetRenderer.sharedMaterial;

            // ZWrite가 켜진 Material로 교체
            Material zWriteMatTemplate = Resources.Load<Material>("Materials/Sprite-ZWrite");
            if (zWriteMatTemplate != null)
            {
                // 인스턴스 생성 (공유 머티리얼 수정 방지)
                Material zWriteMatInstance = new Material(zWriteMatTemplate);
                
                // 원본 텍스처 복사 (SpriteRenderer에서 직접 가져오기)
                Texture tex = null;
                if (targetRenderer is SpriteRenderer sr && sr.sprite != null)
                {
                    tex = sr.sprite.texture;
                }
                else if (targetRenderer.sharedMaterial != null)
                {
                    tex = targetRenderer.sharedMaterial.mainTexture;
                }

                if (tex != null)
                {
                    zWriteMatInstance.mainTexture = tex;
                    Debug.Log($"[MaterialCloner] Texture copied to ZWrite material: {tex.name}");
                }
                else
                {
                    Debug.LogWarning($"[MaterialCloner] No texture found on {target.name}!");
                }
                
                targetRenderer.material = zWriteMatInstance;
                Debug.Log($"[MaterialCloner] Applied ZWrite material to {target.name}");
            }
            else
            {
                Debug.LogWarning("[MaterialCloner] Sprite-ZWrite material not found in Resources/Materials/");
            }
        }

        /// <summary>
        /// 타겟 오브젝트의 Material을 원래대로 복원
        /// </summary>
        public void RestoreTargetMaterial(GameObject target)
        {
            if (target == null || _originalTargetMaterial == null) return;

            Renderer targetRenderer = target.GetComponent<Renderer>();
            if (targetRenderer == null) return;

            targetRenderer.material = _originalTargetMaterial;
            _originalTargetMaterial = null;
            Debug.Log($"[MaterialCloner] Restored original material to {target.name}");
        }
    }
}