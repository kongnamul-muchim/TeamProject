using UnityEngine;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Player
{
    /// <summary>
    /// 플레이어 이동 구현체
    /// 8방향 이동 + 가감속 지원
    /// 수평/수직 속도 분리
    /// </summary>
    public sealed class PlayerMovement : IPlayerMovement
    {
        private readonly float _horizontalSpeed;
        private readonly float _verticalSpeed;
        private readonly float _acceleration;
        private readonly float _friction;

        private Vector2 _velocity;
        private Vector2 _inputDirection;
        private bool _isMoving;
        private MoveDirection _direction;
        private float _speedMultiplier = 1f;

        /// <summary>
        /// 현재 속도 벡터
        /// </summary>
        public Vector2 Velocity => _velocity;

        /// <summary>
        /// 현재 이동 방향
        /// </summary>
        public MoveDirection Direction => _direction;

        /// <summary>
        /// 이동 중인지 여부
        /// </summary>
        public bool IsMoving => _isMoving;

        /// <summary>
        /// 속도 배율 (1.0 = 기본, 외부 효과로 변경 가능)
        /// </summary>
        public float SpeedMultiplier
        {
            get => _speedMultiplier;
            set => _speedMultiplier = Mathf.Max(0f, value);
        }

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="config">이동 설정 (인스펙터 값이 Config 객체로 주입됨)</param>
        public PlayerMovement(IPlayerMovementConfig config)
        {
            _horizontalSpeed = config.HorizontalSpeed;
            _verticalSpeed = config.VerticalSpeed;
            _acceleration = config.Acceleration;
            _friction = config.Friction;

            _velocity = Vector2.zero;
            _inputDirection = Vector2.zero;
            _isMoving = false;
            _direction = MoveDirection.Down;
        }

        /// <summary>
        /// 이동 입력 처리
        /// </summary>
        /// <param name="direction">방향 벡터</param>
        public void Move(Vector2 direction)
        {
            _inputDirection = direction;

            if (direction != Vector2.zero)
            {
                _isMoving = true;
                UpdateDirection(direction);
            }
        }

        /// <summary>
        /// 방향 업데이트 (0 0 0 기준 직관적 매핑)
        /// </summary>
        private void UpdateDirection(Vector2 direction)
        {
            float absX = Mathf.Abs(direction.x);
            float absY = Mathf.Abs(direction.y);

            if (absX > absY)
            {
                // 좌우 방향
                _direction = direction.x > 0 ? MoveDirection.Right : MoveDirection.Left;
            }
            else if (absY > absX)
            {
                // 상하 방향
                _direction = direction.y > 0 ? MoveDirection.Up : MoveDirection.Down;
            }
            // absX == absY (대각선) 때는 기존 방향 유지
        }

        /// <summary>
        /// 이동 즉시 중지
        /// </summary>
        public void Stop()
        {
            _velocity = Vector2.zero;
            _inputDirection = Vector2.zero;
            _isMoving = false;
        }

        /// <summary>
        /// 매 프레임 호출 (고정 시간 간격)
        /// </summary>
        /// <param name="deltaTime">경과 시간</param>
        public void Update(float deltaTime)
        {
            if (_inputDirection == Vector2.zero)
            {
                ApplyFriction(deltaTime);
                _isMoving = _velocity.sqrMagnitude > 0.01f;
                return;
            }

            // 대각선 이동 시 벡터 정규화 (속도 유지)
            Vector2 normalizedDirection = _inputDirection.normalized;
            
            // 수평/수직 속도 분리 적용 + 속도 배율 반영
            float targetHorizontalSpeed = normalizedDirection.x * _horizontalSpeed * _speedMultiplier;
            float targetVerticalSpeed = normalizedDirection.y * _verticalSpeed * _speedMultiplier;
            Vector2 targetVelocity = new Vector2(targetHorizontalSpeed, targetVerticalSpeed);

            // 가속도로 현재 속도→목표 속도 보간
            _velocity = Vector2.Lerp(_velocity, targetVelocity, _acceleration * deltaTime);

            _isMoving = _velocity.sqrMagnitude > 0.01f;
        }

        /// <summary>
        /// 마찰력 적용
        /// </summary>
        private void ApplyFriction(float deltaTime)
        {
            _velocity *= _friction;

            if (_velocity.sqrMagnitude < 0.01f)
            {
                _velocity = Vector2.zero;
                _isMoving = false;
            }
        }
    }
}