using UnityEngine;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Player.Visual
{
    /// <summary>
    /// 플레이어 애니메이션 브릿지 (아티스트 파트 전용)
    /// 기존 코어 스크립트를 수정하지 않고 이동 정보를 애니메이터에 전달합니다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerVisualBridge : MonoBehaviour
    {
        [Header("연결 컴포넌트 (자동 할당)")]
        [Tooltip("플레이어 이동 컨트롤러")]
        [SerializeField] private PlayerMovementAdapter movementAdapter;
        [Tooltip("PlayerInk 참조 (대시 애니메이션 연동)")]
        [SerializeField] private PlayerInk playerInk;
        private Animator _animator;
        private SpriteRenderer _spriteRenderer;
        private Material _material;

        [Header("마스크 시트 설정")]
        [Tooltip("마스크 스프라이트 배열")]
        [SerializeField] private Sprite[] maskSprites; // 마스크 시트 슬라이스 파일들을 여기에 드래그 앤 드롭
        private int _lastSpriteIndex = -1;

        [Header("애니메이터 파라미터 이름")]
        [Tooltip("X 이동 애니메이션 파라미터명")]
        [SerializeField] private string moveXParam = "MoveX";
        [Tooltip("Y 이동 애니메이션 파라미터명")]
        [SerializeField] private string moveYParam = "MoveY";
        [Tooltip("이동 중 애니메이션 파라미터명")]
        [SerializeField] private string isMovingParam = "isMoving";
        [Tooltip("대시 중 애니메이션 파라미터명 (isEscaping 활용)")]
        [SerializeField] private string isDashingParam = "isEscaping";

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            
            // 'Visual' 자식 오브젝트에서 SpriteRenderer를 먼저 찾습니다. (Scale 0.2 이슈 해결)
            Transform visualTransform = transform.Find("Visual");
            if (visualTransform != null)
            {
                _spriteRenderer = visualTransform.GetComponent<SpriteRenderer>();
            }
            else
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }
            
            if (_spriteRenderer != null)
            {
                _material = _spriteRenderer.material;
            }

            if (movementAdapter == null)
            {
                movementAdapter = GetComponentInParent<PlayerMovementAdapter>() ?? GetComponent<PlayerMovementAdapter>();
            }

            if (playerInk == null)
            {
                playerInk = GetComponentInParent<PlayerInk>() ?? GetComponent<PlayerInk>();
            }
        }

        private void Start()
        {
            // 대시 이벤트 구독
            if (playerInk != null)
            {
                playerInk.OnDashStarted += OnDashStarted;
                playerInk.OnDashEnded += OnDashEnded;
            }
        }

        private void OnDestroy()
        {
            if (playerInk != null)
            {
                playerInk.OnDashStarted -= OnDashStarted;
                playerInk.OnDashEnded -= OnDashEnded;
            }
        }

        private void Update()
        {
            if (movementAdapter == null || _animator == null) return;

            // 1. 이동 정보 가져오기
            Vector2 velocity = movementAdapter.CurrentVelocity;
            bool isMoving = movementAdapter.IsMoving;
            MoveDirection direction = movementAdapter.Direction;

            // 2. 애니메이터 파라미터 업데이트
            UpdateAnimatorParameters(velocity, isMoving, direction);
        }

        private void LateUpdate()
        {
            // 3. 마스크 시트 동기화 로직
            SyncMaskWithMainSprite();
        }

        /// <summary>
        /// 대시 시작 시 애니메이션 전환
        /// </summary>
        private void OnDashStarted(float duration, float speedBoost)
        {
            if (_animator != null)
                _animator.SetBool(isDashingParam, true);
        }

        /// <summary>
        /// 대시 종료 시 애니메이션 복귀
        /// </summary>
        private void OnDashEnded()
        {
            if (_animator != null)
                _animator.SetBool(isDashingParam, false);
        }

        /// <summary>
        /// 마스크 스프라이트의 텍스처를 진행 중인 머티리얼에 동기화하여 적용합니다.
        /// </summary>
        private void SyncMaskWithMainSprite()
        {
            if (_spriteRenderer == null || maskSprites == null || maskSprites.Length == 0) return;

            // 의태 시 MaterialCloner가 _spriteRenderer.material을 교체하므로 항상 활성화된 현재 매테리얼을 가져옵니다!
            Material activeMat = _spriteRenderer.material;

            if (maskSprites[0] != null && activeMat != null && activeMat.HasProperty("_ColorPart"))
            {
                Texture currentMaskTexture = activeMat.GetTexture("_ColorPart");
                Texture targetMaskTexture = maskSprites[0].texture;

                if (currentMaskTexture != targetMaskTexture)
                {
                    activeMat.SetTexture("_ColorPart", targetMaskTexture);
                }
            }
        }

        private void UpdateAnimatorParameters(Vector2 velocity, bool isMoving, MoveDirection direction)
        {
            _animator.SetBool(isMovingParam, isMoving);

            if (isMoving)
            {
                // 이동 중일 때 속도 방향 전달 (0 0 0 기준 직관적 매핑)
                _animator.SetFloat(moveXParam, velocity.normalized.x);
                _animator.SetFloat(moveYParam, velocity.normalized.y);
            }
            else
            {
                // 정지 시 마지막 방향 유지하기 위한 로직 (0 0 0 기준 직관적 매핑)
                switch (direction)
                {
                case MoveDirection.Up:    _animator.SetFloat(moveYParam, 1);  _animator.SetFloat(moveXParam, 0); break;
                case MoveDirection.Down:  _animator.SetFloat(moveYParam, -1); _animator.SetFloat(moveXParam, 0); break;
                case MoveDirection.Left:  _animator.SetFloat(moveXParam, -1); _animator.SetFloat(moveYParam, 0); break;
                case MoveDirection.Right: _animator.SetFloat(moveXParam, 1);  _animator.SetFloat(moveYParam, 0); break;
                }
            }
        }
    }
}
