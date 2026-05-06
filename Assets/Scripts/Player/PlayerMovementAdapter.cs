using UnityEngine;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Player;
using HideAndInk.Scripts.Save;

namespace HideAndInk.Player
{
    /// <summary>
    /// 플레이어 이동 시스템 Unity 어댑터
    /// Unity 기본 Input 시스템 + Rigidbody 사용
    /// SRP 준수: 이동 로직 + 입력 처리만 담당, 로깅은 MovementLogger에 위임
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PlayerMovementAdapter : MonoBehaviour
    {
        [Header("이동 속도 설정")]
        [Tooltip("수평 이동 속도")]
        [SerializeField] private float horizontalSpeed = 5.0f;
        [Tooltip("수직 이동 속도")]
        [SerializeField] private float verticalSpeed = 4.0f;
        [Tooltip("이동 가속도")]
        [SerializeField] private float acceleration = 10.0f;
        [Tooltip("이동 마찰 계수")]
        [SerializeField] private float friction = 0.9f;

        [Header("입력 설정")]
        [Tooltip("수평 입력 축 이름")]
        [SerializeField] private string horizontalAxis = "Horizontal";
        [Tooltip("수직 입력 축 이름")]
        [SerializeField] private string verticalAxis = "Vertical";

        [Header("로거 (DI)")]
        [Tooltip("이동 로거 참조")]
        [SerializeField] private MovementLogger movementLogger;

        [Header("대시 연동")]
        [Tooltip("PlayerInk 참조 (대시 속도/무적 연동)")]
        [SerializeField] private PlayerInk playerInk;
        [Tooltip("PlayerLives 참조 (대시 무적 연동)")]
        [SerializeField] private PlayerLives playerLives;

        private IPlayerMovement _playerMovement;
        private Rigidbody _rigidbody;
        private Vector2 _moveInput;
        private bool _isMovementLocked;

        private void Awake()
        {
            // CharacterRegistry에 자신 등록 (Find/태그 하드코딩 제거)
            CharacterRegistry.RegisterPlayer(transform);

            _rigidbody = GetComponent<Rigidbody>();

            // Rigidbody 설정
            _rigidbody.useGravity = true;
            _rigidbody.isKinematic = false;
            _rigidbody.freezeRotation = true;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            // Config 객체 생성 (인스펙터 값 보존)
            var config = new PlayerMovementConfig(horizontalSpeed, verticalSpeed, acceleration, friction);

            // IPlayerMovement 직접 생성 (DI 컨테이너 등록 대신 Config를 직접 주입)
            _playerMovement = new PlayerMovement(config);

            // PlayerInk 자동 탐색 (같은 오브젝트)
            if (playerInk == null)
                playerInk = GetComponent<PlayerInk>();
            if (playerLives == null)
                playerLives = GetComponent<PlayerLives>();
        }

        private void Start()
        {
            // 로거 초기화 (MovementLogger가 파일 I/O 담당)
            movementLogger?.Initialize();

            // 대시 이벤트 구독
            if (playerInk != null)
            {
                playerInk.OnDashStarted += OnDashStarted;
                playerInk.OnDashEnded += OnDashEnded;
            }
        }

        private void OnDestroy()
        {
            // MovementLogger 리소스 해제는 MovementLogger 자체에서 관리
            (movementLogger as System.IDisposable)?.Dispose();

            // 대시 이벤트 구독 해제
            if (playerInk != null)
            {
                playerInk.OnDashStarted -= OnDashStarted;
                playerInk.OnDashEnded -= OnDashEnded;
            }
        }

        private void Update()
        {
            // Unity 기본 Input으로 이동 입력 처리 (0 0 0 기준 직관적 매핑)
            float h = Input.GetAxisRaw(horizontalAxis);
            float v = Input.GetAxisRaw(verticalAxis);
            _moveInput = new Vector2(h, v);

            // 이동 입력 전달
            _playerMovement.Move(_moveInput);

            // 이동 시스템 업데이트
            _playerMovement?.Update(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            // 이동 잠금 상태면 속도 0
            if (_isMovementLocked)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                return;
            }

            // FixedUpdate에서 velocity로 이동 적용 (물리 엔진과 동기화)
            Vector2 velocity = _playerMovement.Velocity;
            Vector3 moveDirection = new Vector3(velocity.x, 0f, velocity.y);

            _rigidbody.linearVelocity = moveDirection;

            // 이동 좌표 로그 기록 (MovementLogger에 위임)
            Vector3 pos = transform.position;
            movementLogger?.Log($"Pos: ({pos.x:F3}, {pos.y:F3}, {pos.z:F3}) | Velocity: ({velocity.x:F3}, {velocity.y:F3}) | Input: ({_moveInput.x:F3}, {_moveInput.y:F3})");
        }

        /// <summary>
        /// 대시 시작 이벤트 핸들러
        /// </summary>
        private void OnDashStarted(float duration, float speedBoost)
        {
            // 속도 추가 보정 적용
            if (_playerMovement != null)
                _playerMovement.SpeedBoost = speedBoost;

            // 무적 설정
            if (playerLives != null)
                playerLives.SetInvincible(duration);
        }

        /// <summary>
        /// 대시 종료 이벤트 핸들러
        /// </summary>
        private void OnDashEnded()
        {
            // 속도 추가 보정 제거
            if (_playerMovement != null)
                _playerMovement.SpeedBoost = 0f;
        }

        /// <summary>
        /// 현재 이동 상태 확인
        /// </summary>
        public bool IsMoving => _playerMovement?.IsMoving ?? false;

        /// <summary>
        /// 현재 이동 방향
        /// </summary>
        public MoveDirection Direction => _playerMovement?.Direction ?? MoveDirection.Down;

        /// <summary>
        /// 현재 속도 확인
        /// </summary>
        public Vector2 CurrentVelocity => _playerMovement?.Velocity ?? Vector2.zero;

        /// <summary>
        /// Rigidbody 참조 (의태 시스템에서 사용)
        /// </summary>
        public Rigidbody Rigidbody => _rigidbody;

        /// <summary>
        /// 속도 배율 설정 (성게 둔화 등 외부 효과)
        /// </summary>
        /// <param name="multiplier">1.0 = 기본, 0.5 = 50% 느림</param>
        public void SetSpeedMultiplier(float multiplier)
        {
            if (_playerMovement != null)
                _playerMovement.SpeedMultiplier = multiplier;
        }

        /// <summary>
        /// 이동 잠금 설정 (의태 시스템에서 사용)
        /// </summary>
        /// <param name="locked">잠금 여부</param>
        public void SetMovementLocked(bool locked)
        {
            _isMovementLocked = locked;
        }

        /// <summary>
        /// 벽 충돌 무시 설정 (의태 시스템에서 사용)
        /// </summary>
        /// <param name="ignore">무시 여부</param>
        public void SetIgnoreWallCollision(bool ignore)
        {
            int playerLayer = gameObject.layer;

            // wallLayer 미설정 시 아무 동작 안 함
            if (wallLayer.value == 0) return;

            // wallLayer에 포함된 모든 레이어에 대해 충돌 무시/복원
            for (int i = 0; i < 32; i++)
            {
                if ((wallLayer & (1 << i)) != 0)
                {
                    Physics.IgnoreLayerCollision(playerLayer, i, ignore);
                }
            }
        }

        // 충돌 감지용 레이어
        [Tooltip("벽 레이어 마스크 (설정 안 하면 충돌 무시/복원 미동작)")]
        [SerializeField] private LayerMask wallLayer;
    }
}
