using UnityEngine;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;
using HideAndInk.Core.Player;
using System;
using System.IO;

namespace HideAndInk.Player
{
    /// <summary>
    /// 플레이어 이동 시스템 Unity 어댑터
    /// Unity 기본 Input 시스템 + Rigidbody 사용
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

        private IPlayerMovement _playerMovement;
        private bool _ignoreWallCollision; // 의태 중 벽 충돌 무시
        private Rigidbody _rigidbody;
        private Vector2 _moveInput;
        private bool _isMovementLocked; // 의태 중 이동 잠금

        // 이동 로그 관련
        private StreamWriter _moveLogWriter;
        private bool _isMoveLogInitialized;

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
            InitializeMoveLog();
        }

        /// <summary>
        /// 이동 로그 파일 초기화
        /// </summary>
        private void InitializeMoveLog()
        {
            if (_isMoveLogInitialized) return;

            string dateString = DateTime.Now.ToString("yyyy-MM-dd");
            string projectPath = Application.dataPath.Replace("/Assets", "");
            string logFolder = Path.Combine(projectPath, "Logs", dateString);

            if (!Directory.Exists(logFolder))
            {
                Directory.CreateDirectory(logFolder);
            }

            string filePath = Path.Combine(logFolder, "MOVE.md");
            _moveLogWriter = new StreamWriter(filePath, false);
            _moveLogWriter.WriteLine("# MOVE Log\n");
            _moveLogWriter.WriteLine("---");
            _moveLogWriter.AutoFlush = true;

            _isMoveLogInitialized = true;
            Debug.Log($"[PlayerMovementAdapter] Move log initialized: {filePath}");
        }

        /// <summary>
        /// 이동 좌표 로그 기록
        /// </summary>
        private void LogMove(string message)
        {
            if (_moveLogWriter == null) return;

            string timeString = DateTime.Now.ToString("HH:mm:ss.fff");
            string logEntry = $"## {timeString}\n{message}";
            _moveLogWriter.WriteLine(logEntry);
        }

        private void Update()
        {
            // Unity 기본 Input으로 이동 입력 처리
            float h = Input.GetAxisRaw(horizontalAxis);
            float v = Input.GetAxisRaw(verticalAxis);
            _moveInput = new Vector2(h, v);

            // 이동 입력 전달
            _playerMovement.Move(_moveInput);

            // 이동 시스템 업데이트
            if (_playerMovement is PlayerMovement movement)
            {
                movement.Update(Time.deltaTime);
            }
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

            // 이동 좌표 로그 기록
            Vector3 pos = transform.position;
            LogMove($"Pos: ({pos.x:F3}, {pos.y:F3}, {pos.z:F3}) | Velocity: ({velocity.x:F3}, {velocity.y:F3}) | Input: ({_moveInput.x:F3}, {_moveInput.y:F3})");
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
        /// 이동 잠금 설정 (의태系统中使用)
        /// </summary>
        /// <param name="locked">잠금 여부</param>
        public void SetMovementLocked(bool locked)
        {
            _isMovementLocked = locked;
        }

        /// <summary>
        /// 벽 충돌 무시 설정 (의태系统中使用)
        /// </summary>
        /// <param name="ignore">무시 여부</param>
        public void SetIgnoreWallCollision(bool ignore)
        {
            _ignoreWallCollision = ignore;
        }

        // 충돌 감지용 레이어
        [SerializeField] private LayerMask wallLayer;

        private void OnDestroy()
        {
            if (_moveLogWriter != null)
            {
                _moveLogWriter.Close();
                _moveLogWriter.Dispose();
            }
        }

        private void OnApplicationQuit()
        {
            if (_moveLogWriter != null)
            {
                _moveLogWriter.Close();
                _moveLogWriter.Dispose();
            }
        }
    }
}