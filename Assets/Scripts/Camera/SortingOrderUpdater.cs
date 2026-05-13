using UnityEngine;

namespace HideAndInk.ParallaxSystem
{
    /// <summary>
    /// 스프라이트의 Sorting Order를 Y축 위치에 따라 자동 계산한다.
    /// 
    /// ┌──────────────────────────────────────────────────────────┐
    /// │  2D 게임에서 깊이감 표현의 핵심                             │
    /// │                                                          │
    /// │  Unity의 Sorting Order:                                   │
    /// │    값이 클수록 → 화면 앞쪽에 그려짐 (전경)                   │
    /// │    값이 작을수록 → 화면 뒤쪽에 그려짐 (원경)                │
    /// │                                                          │
    /// │  Y축 기반 정렬 규칙:                                       │
    /// │    캐릭터가 아래에 있을수록 → 앞쪽 (큰 Sorting Order)       │
    /// │    캐릭터가 위에 있을수록 → 뒤쪽 (작은 Sorting Order)       │
    /// │                                                          │
    /// │  공식:                                                    │
    /// │    sortingOrder = baseOrder - (Y × precision)             │
    /// │                                                          │
    /// │  ┌─────────┐                                             │
    /// │  │ Y=5  원경│ sortingOrder = 0 - 5×10 = -50              │
    /// │  │ Y=3  중경│ sortingOrder = 0 - 3×10 = -30              │
    /// │  │ Y=1  전경│ sortingOrder = 0 - 1×10 = -10              │
    /// │  │ Y=-1 캐릭터│ sortingOrder = 0 - (-1)×10 = 10           │
    /// │  └─────────┘                                             │
    /// │                                                          │
    /// │  팁: precision 값을 10~100으로 설정하면                    │
    /// │  같은 Y 위치에서도 미세한 차이를 표현할 수 있다.             │
    /// └──────────────────────────────────────────────────────────┘
    /// 
    /// SRP: Sorting Order 계산만 담당
    /// DI: [SerializeField]로 설정 참조
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SortingOrderUpdater : MonoBehaviour
    {
        // ── DI ──

        [Header("DI - 추적할 대상 (미할당 시 자신의 Transform 사용)")]
        [Tooltip("추적 대상 트랜스폼")]
        [SerializeField] private Transform trackingTarget;

        [Header("DI - 정렬 기준 Transform (미할당 시 trackingTarget 사용)")]
        [Tooltip("정렬 기준 트랜스폼")]
        [SerializeField] private Transform sortingReference;

        // ── 설정 ──

        [Header("기본 Sorting Order (Y=0일 때의 값)")]
        [Tooltip("기본 정렬 순서")]
        [SerializeField] private int baseOrder;

        [Header("Y축 정밀도 (클수록 미세한 Y차이도 구분)")]
        [Tooltip("Y축 세분화 정밀도")]
        [SerializeField] private int precision = 10;

        [Header("Y 오프셋 (오브젝트 발 위치 보정, 양수=아래쪽 기준점)")]
        [Tooltip("Y축 오프셋")]
        [SerializeField] private float yOffset;

        [Header("업데이트 모드")]
        [Tooltip("업데이트 모드")]
        [SerializeField] private UpdateMode mode = UpdateMode.LateUpdate;

        [Header("Sorting Layer 이름 (비어있으면 변경 안 함)")]
        [Tooltip("정렬 레이어 이름")]
        [SerializeField] private string sortingLayerName;

        // ── 열거형 ──

        public enum UpdateMode
        {
            LateUpdate,  // 매 프레임 자동 갱신
            OnDemand      // 수동으로만 갱신
        }

        // ── 내부 상태 ──

        private SpriteRenderer _renderer;
        private int _lastOrder = int.MinValue;

        // ── Unity 라이프사이클 ──

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();

            if (trackingTarget == null)
                trackingTarget = transform;

            if (sortingReference == null)
                sortingReference = trackingTarget;
        }

        private void Start()
        {
            ApplySortingOrder();
        }

        private void LateUpdate()
        {
            if (mode == UpdateMode.LateUpdate)
                ApplySortingOrder();
        }

        // ── 공개 메서드 ──

        /// <summary>Sorting Order를 수동으로 갱신한다.</summary>
        public void Refresh()
        {
            ApplySortingOrder();
        }

        /// <summary>추적 대상을 변경한다.</summary>
        public void SetTrackingTarget(Transform newTarget)
        {
            trackingTarget = newTarget;
            if (sortingReference == null)
                sortingReference = newTarget;
        }

        /// <summary>정렬 기준 Transform을 변경한다.</summary>
        public void SetSortingReference(Transform newReference)
        {
            sortingReference = newReference;
        }

        // ── 내부 메서드 ──

        private void ApplySortingOrder()
        {
            if (_renderer == null || sortingReference == null) return;

            // Y 위치에 따른 Sorting Order 계산
            // 아래에 있을수록(작은 Y) 앞에 그려져야 함 → 큰 sortingOrder
            // 위에 있을수록(큰 Y) 뒤에 그려져야 함 → 작은 sortingOrder
            float y = sortingReference.position.y - yOffset;
            int newOrder = baseOrder - Mathf.RoundToInt(y * precision);

            // ★ 배경이 Enemy(order=0)와 겹치지 않도록 상한 제한
            //    모든 배경 오브젝트는 Enemy보다 항상 뒤에 있어야 함
            newOrder = Mathf.Clamp(newOrder, int.MinValue, -1);

            // 변경이 있을 때만 적용 (불필요한 렌더링 최소화)
            if (newOrder != _lastOrder)
            {
                _lastOrder = newOrder;
                _renderer.sortingOrder = newOrder;

                if (!string.IsNullOrEmpty(sortingLayerName))
                    _renderer.sortingLayerName = sortingLayerName;
            }
        }
    }
}