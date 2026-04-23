using UnityEngine;

namespace HideAndInk.ParallaxSystem
{
    /// <summary>
    /// Y축 → Z-Depth 패럴랙스 레이어.
    /// 
    /// 캐릭터가 위아래로 이동할 때, 각 레이어의 깊이 비율(depthRatio)에 따라
    /// 위치와 스케일을 조절하여 2D 게임에서 입체감을 표현한다.
    /// 
    /// ┌─────────────────────────────────────────────────────────┐
    /// │  Orthographic 모드 (직교 카메라)                          │
    /// │                                                         │
    /// │  원리: Y축 이동을 "가상의 Z축"으로 치환                    │
    /// │                                                         │
    /// │  캐릭터가 위로 올라가면:                                   │
    /// │    전경(depthRatio=1) → Y를 더 많이 올림 (가까이 보임)     │
    /// │    원경(depthRatio=0) → Y를 적게 올림  (멀리 보임)        │
    /// │                                                         │
    /// │  공식:                                                   │
    /// │    layerY = originY + (charY - originY) × depthRatio    │
    │    layerScale = originScale × (1 + depthRatio × scaleBoost│
    /// │                                                         │
    /// │  ┌─────────────────────────────────────────────────────┐ │
    /// │  │  Perspective 모드 (원근 카메라)                      │ │
    /// │  │                                                     │ │
    /// │  │  원리: 실제 Z축 값을 이동하여 카메라 투영이 자동 처리    │ │
    /// │  │                                                     │ │
    /// │  │  캐릭터가 위로 올라가면:                               │ │
    /// │  │    전경 → Z를 앞으로 당김 (더 크게 보임)               │ │
    /// │  │    원경 → Z를 뒤로 밂   (더 작게 보임)               │ │
    /// │  │                                                     │ │
    /// │  │  공식:                                               │ │
    /// │  │    layerZ = originZ + (charY - originY) × depthRatio │ │
    /// │  │    × zDepthFactor                                   │ │
    /// │  └─────────────────────────────────────────────────────┘ │
    /// └─────────────────────────────────────────────────────────┘
    /// 
    /// SRP: 깊이에 따른 위치/스케일 계산만 담당
    /// DI: [SerializeField]로 설정 참조
    /// OCP: IDepthParallaxLayer 구현으로 확장 가능
    /// </summary>
    public sealed class DepthParallaxLayer : MonoBehaviour, IDepthParallaxLayer
    {
        // ── 열거형 ──

        /// <summary>깊이 패럴랙스 모드</summary>
        public enum DepthMode
        {
            /// <summary>직교 카메라: Y 위치와 스케일로 깊이감 표현</summary>
            Orthographic,

            /// <summary>원근 카메라: Z 위치로 깊이감 표현 (카메라가 자동 투영)</summary>
            Perspective
        }

        // ── DI ──

        [Header("깊이 비율 (0=원경, 1=전경)")]
        [Range(0f, 1f)]
        [SerializeField] private float depthRatio = 0.5f;

        [Header("깊이 모드")]
        [SerializeField] private DepthMode mode = DepthMode.Orthographic;

        // ── Orthographic 전용 설정 ──

        [Header("Orthographic - Y축 깊이 이동 배율")]
        [SerializeField] private float yDepthFactor = 0.3f;

        [Header("Orthographic - 깊이에 따른 스케일 증가량")]
        [SerializeField] private float scaleBoost = 0.15f;

        // ── Perspective 전용 설정 ──

        [Header("Perspective - Z축 깊이 이동 배율")]
        [SerializeField] private float zDepthFactor = 2f;

        // ── 공통 설정 ──

        [Header("X축에도 깊이 이동 적용 (원근 보정)")]
        [SerializeField] private bool applyXDepth;

        [Header("X축 깊이 이동 배율")]
        [SerializeField] private float xDepthFactor = 0.15f;

        // ── IDepthParallaxLayer 구현 ──

        public float DepthRatio => depthRatio;

        // ── 내부 상태 ──

        private Vector3 _originPos;
        private Vector3 _originScale;
        private bool _isInitialized;
        private bool _isControllerDriven;

        // ── 공개 메서드 ──

        /// <summary>초기 위치와 스케일을 기록한다.</summary>
        public void Initialize()
        {
            _originPos = transform.position;
            _originScale = transform.localScale;
            _isInitialized = true;
        }

        /// <summary>캐릭터 Y 위치 변화에 따라 레이어를 갱신한다.</summary>
        public void ApplyDepthOffset(float characterY, float deltaFromOrigin)
        {
            if (!_isInitialized) return;

            switch (mode)
            {
                case DepthMode.Orthographic:
                    ApplyOrthographic(characterY, deltaFromOrigin);
                    break;

                case DepthMode.Perspective:
                    ApplyPerspective(characterY, deltaFromOrigin);
                    break;
            }
        }

        /// <summary>Controller가 이 레이어를 관리한다고 알린다.</summary>
        public void SetControllerDriven(bool driven)
        {
            _isControllerDriven = driven;
        }

        // ── Unity 라이프사이클 ──

        private void Start()
        {
            Initialize();
        }

        // ── 내부 메서드 ──

        /// <summary>
        /// Orthographic 모드: Y 위치와 스케일로 깊이감 표현
        /// 
        /// 핵심 로직:
        ///   캐릭터가 위로 이동할 때:
        ///   - 전경(depthRatio≈1): Y를 더 많이 올림 → "가까이 올라온" 느낌
        ///   - 원경(depthRatio≈0): Y를 적게 올림 → "멀리서 거의 안 움직임" 느낌
        ///   
        ///   스케일:
        ///   - 전경일수록 약간 커짐 → 가까이 있다는 착시 강화
        ///   - 원경일수록 원래 크기 유지
        /// </summary>
        private void ApplyOrthographic(float characterY, float deltaFromOrigin)
        {
            // Y축: 깊이 비율에 따라 이동량 조절
            float yOffset = deltaFromOrigin * depthRatio * yDepthFactor;
            float newY = _originPos.y + yOffset;

            // X축: 원근 보정 (선택적)
            float newX = _originPos.x;
            if (applyXDepth)
                newX += deltaFromOrigin * depthRatio * xDepthFactor;

            // 스케일: 깊이에 따라 약간 확대/축소
            float scaleMultiplier = 1f + depthRatio * scaleBoost * (deltaFromOrigin * 0.1f);
            // 과도한 스케일 변화 방지
            scaleMultiplier = Mathf.Clamp(scaleMultiplier, 0.8f, 1.3f);

            transform.position = new Vector3(newX, newY, _originPos.z);
            transform.localScale = Vector3.Scale(_originScale, new Vector3(scaleMultiplier, scaleMultiplier, 1f));
        }

        /// <summary>
        /// Perspective 모드: Z 위치로 깊이감 표현
        /// 
        /// 핵심 로직:
        ///   캐릭터가 위로 이동할 때:
        ///   - 전경(depthRatio≈1): Z를 앞으로 당김 → 카메라에 더 가까이 → 더 크게 보임
        ///   - 원경(depthRatio≈0): Z를 뒤로 밂   → 카메라에서 더 멀리 → 더 작게 보임
        ///   
        ///   Perspective 카메라가 자동으로 투영 처리하므로
        ///   스케일 조작이 불필요하다.
        /// </summary>
        private void ApplyPerspective(float characterY, float deltaFromOrigin)
        {
            // Z축: 깊이 비율에 따라 앞뒤 이동
            // 위로 올라가면 전경은 앞으로(+Z), 원경은 뒤로(-Z)
            float zOffset = deltaFromOrigin * depthRatio * zDepthFactor;
            float newZ = _originPos.z + zOffset;

            // X축: 원근 보정 (선택적)
            float newX = _originPos.x;
            if (applyXDepth)
                newX += deltaFromOrigin * depthRatio * xDepthFactor;

            // Y축: 원래 위치 유지 (Perspective는 Z로 처리)
            transform.position = new Vector3(newX, _originPos.y, newZ);

            // 스케일은 Perspective 카메라가 자동 처리
            transform.localScale = _originScale;
        }
    }
}