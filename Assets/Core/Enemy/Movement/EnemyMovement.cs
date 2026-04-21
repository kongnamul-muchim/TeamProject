using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Enemy.Movement
{
    /// <summary>
    /// Enemy 이동 구현체
    /// 3D 환경 (X-Z 평면 이동, Y축 고정)
    /// 가속도 기반 부드러운 이동
    /// </summary>
    public sealed class EnemyMovement : IEnemyMovement
    {
        private readonly IEnemy _enemy;
        private readonly float _acceleration;
        private readonly float _friction;
        private readonly float _maxSpeed;

        private Vector3 _velocity;
        private Vector3? _targetPosition;
        private bool _isMoving;
        private MoveDirection _direction;
        private float _speed;

        /// <summary>
        /// 현재 속도 벡터 (X-Z 평면)
        /// </summary>
        public Vector3 Velocity => _velocity;

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

            _velocity = Vector3.zero;
            _targetPosition = null;
            _isMoving = false;
            _direction = MoveDirection.Left;
        }

        /// <summary>
        /// 목표 위치로 이동
        /// </summary>
        public void MoveTo(Vector3 targetPosition)
        {
            _targetPosition = targetPosition;
        }

        /// <summary>
        /// 이동 즉시 중지
        /// </summary>
        public void Stop()
        {
            _velocity = Vector3.zero;
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
        /// 목표 지점으로 부드럽게 이동 (X-Z 평면)
        /// </summary>
        private void MoveTowardsTarget(float deltaTime)
        {
            Vector3 currentPos = new Vector3(_enemy.Position.x, 0f, _enemy.Position.z);
            Vector3 targetPos = new Vector3(_targetPosition.Value.x, 0f, _targetPosition.Value.z);
            Vector3 direction = (targetPos - currentPos);
            float distance = direction.magnitude;

            // 목표 지점 도달 판정
            if (distance < 0.5f)
            {
                // Behavior에서 새 목표를 설정할 때까지 현재 속도 유지
                // 갑자기 멈추지 않고 관성으로 계속 이동
                _velocity *= _friction;
                _isMoving = _velocity.sqrMagnitude > 0.01f;
                if (_isMoving)
                {
                    UpdateDirection(_velocity);
                }
                return;
            }

            direction.Normalize();

            // 목표 속도 계산
            Vector3 targetVelocity = direction * _speed;

            // 가속도로 현재 속도→목표 속도 보간 (부드럽게)
            float accelFactor = _acceleration * deltaTime;
            accelFactor = Mathf.Clamp01(accelFactor);
            _velocity = Vector3.Lerp(_velocity, targetVelocity, accelFactor);

            // 최대 속도 제한
            if (_velocity.magnitude > _maxSpeed)
            {
                _velocity = _velocity.normalized * _maxSpeed;
            }

            // Y축 속도 제거 (고정)
            _velocity.y = 0f;

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
                _velocity = Vector3.zero;
                _isMoving = false;
            }
        }

        /// <summary>
        /// 방향 업데이트 (X-Z 평면 기준 2D 방향 매핑)
        /// </summary>
        private void UpdateDirection(Vector3 direction)
        {
            float absX = Mathf.Abs(direction.x);
            float absZ = Mathf.Abs(direction.z);

            if (absX > absZ)
            {
                _direction = direction.x > 0 ? MoveDirection.Right : MoveDirection.Left;
            }
            else if (absZ > absX)
            {
                _direction = direction.z > 0 ? MoveDirection.Up : MoveDirection.Down;
            }
        }
    }
}
