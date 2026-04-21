using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Enemy.Movement
{
    /// <summary>
    /// Enemy 이동 구현체
    /// 가속도/마찰력 기반 부드러운 이동
    /// PlayerMovement와 유사한 구조
    /// </summary>
    public sealed class EnemyMovement : IEnemyMovement
    {
        private readonly IEnemy _enemy;
        private readonly float _acceleration;
        private readonly float _friction;
        private readonly float _maxSpeed;

        private Vector2 _velocity;
        private Vector2? _targetPosition;
        private bool _isMoving;
        private MoveDirection _direction;
        private float _speed;

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
        /// 이동 속도 (인스펙터 설정 가능)
        /// </summary>
        public float Speed
        {
            get => _speed;
            set => _speed = Mathf.Max(0f, value);
        }

        /// <summary>
        /// 생성자 (DI 주입)
        /// </summary>
        /// <param name="enemy">Enemy 인터페이스 (위치 참조용)</param>
        /// <param name="speed">기본 이동 속도</param>
        /// <param name="acceleration">가속도</param>
        /// <param name="friction">마찰력 (0~1, 1이면 즉시 정지)</param>
        /// <param name="maxSpeed">최대 속도</param>
        public EnemyMovement(
            IEnemy enemy,
            float speed = 3f,
            float acceleration = 8f,
            float friction = 0.9f,
            float maxSpeed = 5f)
        {
            _enemy = enemy ?? throw new System.ArgumentNullException(nameof(enemy));
            _speed = speed;
            _acceleration = acceleration;
            _friction = friction;
            _maxSpeed = maxSpeed;

            _velocity = Vector2.zero;
            _targetPosition = null;
            _isMoving = false;
            _direction = MoveDirection.Left;
        }

        /// <summary>
        /// 목표 위치로 이동
        /// </summary>
        public void MoveTo(Vector2 targetPosition)
        {
            _targetPosition = targetPosition;
        }

        /// <summary>
        /// 이동 즉시 중지
        /// </summary>
        public void Stop()
        {
            _velocity = Vector2.zero;
            _targetPosition = null;
            _isMoving = false;
        }

        /// <summary>
        /// 상태 업데이트 (매 프레임 호출)
        /// </summary>
        public void Update(float deltaTime)
        {
            if (_targetPosition.HasValue)
            {
                MoveTowardsTarget(deltaTime);
            }
            else
            {
                ApplyFriction(deltaTime);
            }
        }

        /// <summary>
        /// 목표 지점으로 부드럽게 이동
        /// </summary>
        private void MoveTowardsTarget(float deltaTime)
        {
            Vector2 currentPos = new Vector2(_enemy.Position.x, _enemy.Position.y);
            Vector2 direction = (_targetPosition.Value - currentPos).normalized;

            // 목표 속도 계산
            Vector2 targetVelocity = direction * _speed;

            // 가속도로 현재 속도→목표 속도 보간
            _velocity = Vector2.Lerp(_velocity, targetVelocity, _acceleration * deltaTime);

            // 최대 속도 제한
            if (_velocity.magnitude > _maxSpeed)
            {
                _velocity = _velocity.normalized * _maxSpeed;
            }

            _isMoving = _velocity.sqrMagnitude > 0.01f;

            if (_isMoving)
            {
                UpdateDirection(_velocity);
            }
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

        /// <summary>
        /// 방향 업데이트
        /// </summary>
        private void UpdateDirection(Vector2 direction)
        {
            float absX = Mathf.Abs(direction.x);
            float absY = Mathf.Abs(direction.y);

            if (absX > absY)
            {
                _direction = direction.x > 0 ? MoveDirection.Right : MoveDirection.Left;
            }
            else if (absY > absX)
            {
                _direction = direction.y > 0 ? MoveDirection.Up : MoveDirection.Down;
            }
        }
    }
}
