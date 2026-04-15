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

        /// <summary>
        /// 현재 속도 벡터
        /// </summary>
        public Vector2 Velocity => _velocity;

        /// <summary>
        /// 이동 중인지 여부
        /// </summary>
        public bool IsMoving => _isMoving;

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="horizontalSpeed">좌우(수평) 이동 속도</param>
        /// <param name="verticalSpeed">상하(수직) 이동 속도</param>
        /// <param name="acceleration">가속도</param>
        /// <param name="friction">마찰력 (0~1, 1이면 즉시 정지)</param>
        public PlayerMovement(
            float horizontalSpeed = 5.0f,
            float verticalSpeed = 4.0f,
            float acceleration = 10.0f,
            float friction = 0.9f)
        {
            _horizontalSpeed = horizontalSpeed;
            _verticalSpeed = verticalSpeed;
            _acceleration = acceleration;
            _friction = friction;

            _velocity = Vector2.zero;
            _inputDirection = Vector2.zero;
            _isMoving = false;
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
            }
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
                return;
            }

            // 대각선 이동 시 벡터 정규화 (속도 유지)
            Vector2 normalizedDirection = _inputDirection.normalized;
            
            // 수평/수직 속도 분리 적용
            float targetHorizontalSpeed = normalizedDirection.x * _horizontalSpeed;
            float targetVerticalSpeed = normalizedDirection.y * _verticalSpeed;
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