using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;
using HideAndInk.Core.Enemy.Boss.Gimmicks;

namespace HideAndInk.Core.Enemy.AI.Behaviors
{
    /// <summary>
    /// 순찰 행동
    /// X축만 이동 (Z축 고정), Ground 범위 내 순찰
    /// 방향 전환 쿨타임 없음
    /// Ground 경계를 벗어나지 않도록 목표 제한
    /// 보스 기믹이 이동 제어권을 가질 수 있음
    /// </summary>
    public sealed class PatrolBehavior : IEnemyAIState
    {
        private readonly IEnemy _enemy;
        private readonly IEnemyMovement _movement;
        private readonly IEnemyGimmick _gimmick;

        // 순찰 설정
        private readonly float _minDistance; // 목표 지점 도달 판정 거리

        // Ground 경계 (Controller에서 전달)
        private GroundBounds _groundBounds;
        private bool _hasGroundBounds;

        // Player 반경 제한 (Controller에서 전달)
        private Transform _playerTransform;
        private float _patrolRadius = 0f; // 0 = 제한 없음

        // 상태
        private Vector3 _currentTarget;
        private bool _isInitialized;
        private bool _movementOverride; // 기믹이 이동 제어권을 가지고 있는지 (AmbushGimmick 호환)

        /// <summary>
        /// 이동 제어권 설정 (true = 기믹이 제어, false = PatrolBehavior가 제어)
        /// </summary>
        public void SetMovementOverride(bool isOverridden)
        {
            _movementOverride = isOverridden;
        }

        /// <summary>
        /// 생성자
        /// </summary>
        public PatrolBehavior(
            IEnemy enemy,
            IEnemyMovement movement,
            IEnemyGimmick gimmick = null,
            float minDistance = 0.5f)
        {
            _enemy = enemy;
            _movement = movement;
            _gimmick = gimmick;
            _minDistance = minDistance;
        }

        /// <summary>
        /// 현재 Patrol 목표 무효화 → 다음 OnUpdate에서 PickNewTarget 실행
        /// (Ground edge 도달 시 Controller에서 호출)
        /// </summary>
        public void InvalidateTarget()
        {
            _currentTarget = _enemy.Position;
        }

        /// <summary>
        /// Ground 경계 설정 (Controller에서 호출)
        /// </summary>
        public void SetGroundBounds(GroundBounds bounds)
        {
            _groundBounds = bounds;
            _hasGroundBounds = true;
        }

        /// <summary>
        /// Player 기준 순찰 반경 설정 (Controller에서 호출)
        /// 보스가 Player 주변 일정 범위 밖으로 벗어나지 않도록 제한
        /// </summary>
        public void SetPlayerPatrolRadius(Transform player, float radius)
        {
            _playerTransform = player;
            _patrolRadius = Mathf.Max(0f, radius);
        }

        public EnemyAIState StateType => EnemyAIState.Patrol;

        public void OnEnter()
        {
            _isInitialized = true;
            PickNewTarget();
        }

        public void OnUpdate(float deltaTime)
        {
            // 기믹이 이동 제어권을 가지고 있으면 기믹의 목표 사용
            if (_gimmick != null && _gimmick.HasMovementOverride)
            {
                Vector3? gimmickTarget = _gimmick.GetPatrolTarget(_enemy.Position, _hasGroundBounds ? _groundBounds : default);
                if (gimmickTarget.HasValue)
                {
                    _movement.MoveTo(gimmickTarget.Value);
                }
                return;
            }

            // 현재 위치 (X축만, Z축 고정)
            Vector3 currentPos = new Vector3(_enemy.Position.x, 0f, _enemy.Position.z);
            Vector3 targetPos = new Vector3(_currentTarget.x, 0f, _currentTarget.z);

            // 이동 중에도 Player 반경 이탈 시 즉시 새 목표 설정
            if (_playerTransform != null && _patrolRadius > 0f)
            {
                float distToPlayer = Mathf.Abs(_enemy.Position.x - _playerTransform.position.x);
                float targetDistToPlayer = Mathf.Abs(_currentTarget.x - _playerTransform.position.x);
                if (targetDistToPlayer > _patrolRadius || distToPlayer > _patrolRadius * 1.2f)
                {
                    PickNewTarget();
                }
            }

            // 목표 지점에 도달했는지 확인
            float distanceToTarget = Vector3.Distance(currentPos, targetPos);

            if (distanceToTarget < _minDistance)
            {
                // 도달 → 즉시 새 목표 설정 (멈추지 않음)
                PickNewTarget();
            }

            // 계속 이동
            _movement.MoveTo(_currentTarget);
        }

        public void OnExit()
        {
            _movementOverride = false; // 제어권 해제
            _movement.Stop();
        }

        public bool CanTransitionTo(EnemyAIState targetState)
        {
            return targetState == EnemyAIState.Chase || targetState == EnemyAIState.Search;
        }

        /// <summary>
        /// 새로운 순찰 목표 지점 선택
        /// X축만 이동, Z축은 현재 위치 유지
        /// Ground 범위 + Player 반경 내에서만 목표 설정
        /// </summary>
        private void PickNewTarget()
        {
            Vector3 currentPos = _enemy.Position;

            // X축: 랜덤 방향 (-1 또는 1)
            float xDir = Random.value > 0.5f ? 1f : -1f;
            float xDistance = Random.Range(5f, 15f);

            float targetX = currentPos.x + xDir * xDistance;

            // Player 반경 내로 제한 (1순위)
            if (_playerTransform != null && _patrolRadius > 0f)
            {
                float distFromPlayer = Mathf.Abs(targetX - _playerTransform.position.x);
                if (distFromPlayer > _patrolRadius)
                {
                    // Player 방향으로 되돌림
                    float dirToPlayer = _playerTransform.position.x > currentPos.x ? 1f : -1f;
                    targetX = _playerTransform.position.x + dirToPlayer * _patrolRadius * 0.8f;
                }
            }

            // Ground 범위 내로 제한 (2순위)
            if (_hasGroundBounds)
            {
                targetX = _groundBounds.ClampX(targetX);

                if (Mathf.Abs(targetX - currentPos.x) < 1f)
                {
                    xDir = -xDir;
                    targetX = currentPos.x + xDir * xDistance;
                    targetX = _groundBounds.ClampX(targetX);

                    // Player 반경 재적용
                    if (_playerTransform != null && _patrolRadius > 0f)
                    {
                        float distFromPlayer = Mathf.Abs(targetX - _playerTransform.position.x);
                        if (distFromPlayer > _patrolRadius)
                        {
                            float dirToPlayer = _playerTransform.position.x > currentPos.x ? 1f : -1f;
                            targetX = _playerTransform.position.x + dirToPlayer * _patrolRadius * 0.8f;
                        }
                    }
                }
            }

            _currentTarget = new Vector3(
                targetX,
                currentPos.y,
                currentPos.z
            );
        }
    }
}
