using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Enemy.Movement
{
    /// <summary>
    /// Enemy 이동 구현체
    /// 3D 환경 (X-Z 평면 이동, Y축 고정)
    /// 가속도 기반 부드러운 이동
    /// Ground 검증 기능 포함
    /// </summary>
    public sealed class EnemyMovement : IEnemyMovement
    {
        private readonly IEnemy _enemy;
        private readonly float _acceleration;
        private readonly float _friction;
        private float _maxSpeed; // 돌진 시 동적 변경 가능하도록 readonly 제거

        // Ground 검증 설정
        private readonly LayerMask _groundLayer;
        private readonly float _groundCheckDistance;
        private readonly float _groundCheckRadius;

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
        /// 최대 속도 설정 (돌진 등 임시 속도 증가용)
        /// Speed와 MaxSpeed를 동시에 설정하여 속도 제한 해제
        /// </summary>
        public void SetMaxSpeed(float maxSpeed)
        {
            _maxSpeed = Mathf.Max(0f, maxSpeed);
            // Speed가 기존 MaxSpeed보다 크면 함께 조정
            if (_speed > _maxSpeed)
            {
                _speed = _maxSpeed;
            }
        }

        /// <summary>
        /// 생성자 (DI 주입)
        /// </summary>
        public EnemyMovement(
            IEnemy enemy,
            float speed = 3f,
            float acceleration = 8f,
            float friction = 0.9f,
            float maxSpeed = 5f,
            LayerMask groundLayer = default,
            float groundCheckDistance = 0.5f,
            float groundCheckRadius = 0.3f)
        {
            _enemy = enemy ?? throw new System.ArgumentNullException(nameof(enemy));
            _speed = speed;
            _acceleration = acceleration;
            _friction = friction;
            _maxSpeed = maxSpeed;
            _groundLayer = groundLayer;
            _groundCheckDistance = groundCheckDistance;
            _groundCheckRadius = groundCheckRadius;

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

            // 가속도 기반 선형 속도 변화 (MoveTowards로 일정한 가속)
            _velocity = Vector3.MoveTowards(_velocity, targetVelocity, _acceleration * deltaTime);

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

        #region Ground 검증

        /// <summary>
        /// 지정 위치가 Ground 위에 있는지 확인
        /// </summary>
        public bool IsPositionOnGround(Vector3 position)
        {
            if (_groundLayer == 0) return true; // Ground 레이어 미설정 시 항상 true

            // 해당 위치 아래로 Raycast
            Vector3 checkPoint = new Vector3(position.x, position.y + 0.1f, position.z);
            if (Physics.Raycast(checkPoint, Vector3.down, out RaycastHit hit, 2f, _groundLayer))
            {
                return true;
            }

            // SphereCast로 넓은 영역 체크
            if (Physics.CheckSphere(checkPoint, _groundCheckRadius, _groundLayer))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 이동 방향 앞쪽에 Ground가 있는지 확인
        /// </summary>
        public bool IsGroundAhead()
        {
            if (_groundLayer == 0) return true; // Ground 레이어 미설정 시 항상 true
            if (_velocity.sqrMagnitude < 0.01f) return true; // 정지 상태면 통과

            Vector3 moveDirection = _velocity.normalized;
            Vector3 checkPoint = _enemy.Position + moveDirection * _groundCheckDistance;

            // 아래로 Raycast
            if (Physics.Raycast(checkPoint, Vector3.down, out RaycastHit hit, 2f, _groundLayer))
            {
                return true;
            }

            // SphereCast
            if (Physics.CheckSphere(checkPoint, _groundCheckRadius, _groundLayer))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Ground 경계 스캔 (시작 시 호출)
        /// 현재 위치에서 좌우로 Raycast를 쏴서 Ground 끝 지점 찾기
        /// </summary>
        public GroundBounds ScanGroundBounds(float maxScanDistance = 50f, float scanStep = 1f)
        {
            Vector3 startPos = _enemy.Position;

            // X축 경계 스캔
            float minX = ScanBoundaryX(startPos, -1f, maxScanDistance, scanStep);
            float maxX = ScanBoundaryX(startPos, 1f, maxScanDistance, scanStep);

            // Z축 경계 스캔
            float minZ = ScanBoundaryZ(startPos, -1f, maxScanDistance, scanStep);
            float maxZ = ScanBoundaryZ(startPos, 1f, maxScanDistance, scanStep);

            return new GroundBounds(minX, maxX, minZ, maxZ);
        }

        /// <summary>
        /// X축 경계 스캔 (방향: -1=왼쪽, 1=오른쪽)
        /// 이진 탐색으로 Raycast 횟수 최소화 (~50회 → ~15회)
        /// </summary>
        private float ScanBoundaryX(Vector3 startPos, float direction, float maxDistance, float step)
        {
            if (_groundLayer == 0) return startPos.x + direction * maxDistance;

            // 1단계: 지수 탐색으로 경계 범위 찾기 (1, 2, 4, 8, 16, 32...)
            int lastValidStep = 0;
            int firstInvalidStep = -1;
            int currentStep = 1;

            while (currentStep * step <= maxDistance)
            {
                float checkX = startPos.x + direction * currentStep * step;
                Vector3 checkPoint = new Vector3(checkX, startPos.y + 0.1f, startPos.z);

                if (Physics.Raycast(checkPoint, Vector3.down, out _, 2f, _groundLayer))
                {
                    lastValidStep = currentStep;
                    currentStep *= 2;
                }
                else
                {
                    firstInvalidStep = currentStep;
                    break;
                }
            }

            // 끝까지 Ground가 있으면 최대 거리 반환
            if (firstInvalidStep == -1)
            {
                return startPos.x + direction * maxDistance;
            }

            // 2단계: 이진 탐색으로 정확한 경계 찾기
            int low = lastValidStep;
            int high = firstInvalidStep;

            while (high - low > 1)
            {
                int mid = (low + high) / 2;
                float checkX = startPos.x + direction * mid * step;
                Vector3 checkPoint = new Vector3(checkX, startPos.y + 0.1f, startPos.z);

                if (Physics.Raycast(checkPoint, Vector3.down, out _, 2f, _groundLayer))
                {
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            return startPos.x + direction * low * step;
        }

        /// <summary>
        /// Z축 경계 스캔 (방향: -1=앞쪽, 1=뒤쪽)
        /// 이진 탐색으로 Raycast 횟수 최소화 (~50회 → ~15회)
        /// </summary>
        private float ScanBoundaryZ(Vector3 startPos, float direction, float maxDistance, float step)
        {
            if (_groundLayer == 0) return startPos.z + direction * maxDistance;

            // 1단계: 지수 탐색으로 경계 범위 찾기
            int lastValidStep = 0;
            int firstInvalidStep = -1;
            int currentStep = 1;

            while (currentStep * step <= maxDistance)
            {
                float checkZ = startPos.z + direction * currentStep * step;
                Vector3 checkPoint = new Vector3(startPos.x, startPos.y + 0.1f, checkZ);

                if (Physics.Raycast(checkPoint, Vector3.down, out _, 2f, _groundLayer))
                {
                    lastValidStep = currentStep;
                    currentStep *= 2;
                }
                else
                {
                    firstInvalidStep = currentStep;
                    break;
                }
            }

            // 끝까지 Ground가 있으면 최대 거리 반환
            if (firstInvalidStep == -1)
            {
                return startPos.z + direction * maxDistance;
            }

            // 2단계: 이진 탐색으로 정확한 경계 찾기
            int low = lastValidStep;
            int high = firstInvalidStep;

            while (high - low > 1)
            {
                int mid = (low + high) / 2;
                float checkZ = startPos.z + direction * mid * step;
                Vector3 checkPoint = new Vector3(startPos.x, startPos.y + 0.1f, checkZ);

                if (Physics.Raycast(checkPoint, Vector3.down, out _, 2f, _groundLayer))
                {
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            return startPos.z + direction * low * step;
        }

        #endregion
    }
}
