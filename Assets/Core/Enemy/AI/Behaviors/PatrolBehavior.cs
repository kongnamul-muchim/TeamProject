using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.AI.Behaviors
{
    /// <summary>
    /// 순찰 행동
    /// 방향 전환 쿨타임 적용: 쿨타임 중에는 현재 방향 유지, 쿨타임 종료 후 새 방향 선택
    /// X축 중심 이동 (Z축은 약간의 변동만)
    /// </summary>
    public sealed class PatrolBehavior : IEnemyAIState
    {
        private readonly IEnemy _enemy;
        private readonly IEnemyMovement _movement;

        // 순찰 설정
        private readonly float _patrolRadius;
        private readonly float _directionChangeCooldown;
        private readonly float _targetDistance; // 한 번에 이동할 거리

        // 상태
        private Vector3 _patrolCenter;
        private Vector3 _currentTarget;
        private Vector3 _currentDirection; // 현재 이동 방향
        private bool _isInitialized;
        private float _directionTimer; // 방향 전환 쿨타임 타이머

        /// <summary>
        /// 생성자
        /// </summary>
        public PatrolBehavior(
            IEnemy enemy,
            IEnemyMovement movement,
            float patrolRadius = 5f,
            float directionChangeCooldown = 1.5f,
            float targetDistance = 3f)
        {
            _enemy = enemy;
            _movement = movement;
            _patrolRadius = patrolRadius;
            _directionChangeCooldown = directionChangeCooldown;
            _targetDistance = targetDistance;
        }

        public EnemyAIState StateType => EnemyAIState.Patrol;

        public void OnEnter()
        {
            if (!_isInitialized)
            {
                _patrolCenter = _enemy.Position;
                _isInitialized = true;
            }

            _directionTimer = 0f; // 진입 시 즉시 이동 시작
            PickNewTarget();
        }

        public void OnUpdate(float deltaTime)
        {
            // 쿨타임 감소
            _directionTimer -= deltaTime;

            // 현재 위치 (X-Z 평면)
            Vector3 currentPos = new Vector3(_enemy.Position.x, 0f, _enemy.Position.z);
            Vector3 targetPos = new Vector3(_currentTarget.x, 0f, _currentTarget.z);

            // 목표 지점에 도달했는지 확인
            float distanceToTarget = Vector3.Distance(currentPos, targetPos);

            if (distanceToTarget < 0.5f)
            {
                // 목표 지점 도달
                if (_directionTimer <= 0f)
                {
                    // 쿨타임 종료 → 새 방향 선택
                    PickNewTarget();
                }
                else
                {
                    // 쿨타임 중 → 현재 방향으로 계속 이동 (목표 지점 연장)
                    ExtendTarget();
                }
            }

            // 계속 이동
            _movement.MoveTo(_currentTarget);
        }

        public void OnExit()
        {
            _movement.Stop();
        }

        public bool CanTransitionTo(EnemyAIState targetState)
        {
            return targetState == EnemyAIState.Chase || targetState == EnemyAIState.Search;
        }

        /// <summary>
        /// 새로운 순찰 목표 지점 선택 (새 방향)
        /// X축 중심 이동 (Z축은 약간의 변동만)
        /// </summary>
        private void PickNewTarget()
        {
            // X축: 주요 이동 방향 (-1 ~ 1)
            float xMove = Random.Range(-1f, 1f);
            // Z축: 약간의 변동만 (-0.2 ~ 0.2)
            float zMove = Random.Range(-0.2f, 0.2f);

            _currentDirection = new Vector3(xMove, 0f, zMove).normalized;

            Vector3 currentPos = _enemy.Position;
            _currentTarget = currentPos + _currentDirection * _targetDistance;

            // 순찰 반경 벗어나면 반대 방향으로
            float distFromCenter = Vector2.Distance(
                new Vector2(currentPos.x, currentPos.z),
                new Vector2(_patrolCenter.x, _patrolCenter.z));

            if (distFromCenter > _patrolRadius)
            {
                // 중심 방향으로 되돌아가기
                Vector3 toCenter = (_patrolCenter - currentPos).normalized;
                _currentDirection = new Vector3(toCenter.x, 0f, toCenter.z * 0.3f).normalized;
                _currentTarget = currentPos + _currentDirection * _targetDistance;
            }

            // 방향 전환 쿨타임 설정
            _directionTimer = _directionChangeCooldown;
        }

        /// <summary>
        /// 목표 지점을 현재 방향으로 연장 (쿨타임 중 계속 이동)
        /// </summary>
        private void ExtendTarget()
        {
            _currentTarget = _enemy.Position + _currentDirection * _targetDistance;
        }

        /// <summary>
        /// 순찰 중심점 재설정
        /// </summary>
        public void ResetPatrolCenter(Vector3 newCenter)
        {
            _patrolCenter = newCenter;
        }
    }
}
