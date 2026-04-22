using UnityEngine;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;
using HideAndInk.Core.Player;

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
        [SerializeField] private float horizontalSpeed = 5.0f;
        [SerializeField] private float verticalSpeed = 4.0f;
        [SerializeField] private float acceleration = 10.0f;
        [SerializeField] private float friction = 0.9f;

        [Header("입력 설정")]
        [SerializeField] private string horizontalAxis = "Horizontal";
        [SerializeField] private string verticalAxis = "Vertical";

        [Header("로거 (DI)")]
        [SerializeField] private MovementLogger movementLogger;

        private IPlayerMovement _playerMovement;
        private Rigidbody _rigidbody;
        private Vector2 _moveInput;
        private bool _isMovementLocked;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();

            // Rigidbody 설정
            _rigidbody.useGravity = true;
            _rigidbody.isKinematic = false;
            _rigidbody.freezeRotation = true;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            // DI 컨테이너에서 해결하거나 직접 생성
            if (GameManager.Container != null && GameManager.Container.IsRegistered<IPlayerMovement>())
            {
                _playerMovement = GameManager.Container.Resolve<IPlayerMovement>();
            }
            else
            {
                _playerMovement = new PlayerMovement(
                    horizontalSpeed,
                    verticalSpeed,
                    acceleration,
                    friction);
            }
        }

        private void Start()
        {
            // 로거 초기화 (MovementLogger가 파일 I/O 담당)
            movementLogger?.Initialize();
        }

        private void Update()
        {
            // Unity 기본 Input으로 이동 입력 처리 (카메라 반대편이므로 반전)
            float h = -Input.GetAxisRaw(horizontalAxis);
            float v = -Input.GetAxisRaw(verticalAxis);
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
        [SerializeField] private LayerMask wallLayer;

        private void OnDestroy()
        {
            // MovementLogger 리소스 해제는 MovementLogger 자체에서 관리
            (movementLogger as System.IDisposable)?.Dispose();
        }
    }
}