using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.AI.Behaviors
{
    /// <summary>
    /// 순찰 행동
    /// 랜덤한 위치를 계속 이동하며 순찰 (멈추지 않음)
    /// X-Z 평면 이동
    /// </summary>
    public sealed class PatrolBehavior : IEnemyAIState
    {
        private readonly IEnemy _enemy;
        private readonly IEnemyMovement _movement;

        // 순찰 설정
        private readonly float _patrolRadius;
        private readonly float _minDistance; // 최소 이동 거리 (이보다 가까우면 새 목표)

        // 상태
        private Vector3 _patrolCenter;
        private Vector3 _currentTarget;
        private bool _isInitialized;

        /// <summary>
        /// 생성자
        /// </summary>
        public PatrolBehavior(
            IEnemy enemy,
            IEnemyMovement movement,
            float patrolRadius = 5f,
            float minDistance = 1f)
        {
            _enemy = enemy;
            _movement = movement;
            _patrolRadius = patrolRadius;
            _minDistance = minDistance;
        }

        public EnemyAIState StateType => EnemyAIState.Patrol;

        public void OnEnter()
        {
            if (!_isInitialized)
            {
                _patrolCenter = _enemy.Position;
                _isInitialized = true;
            }

            PickNewTarget();
        }

        public void OnUpdate(float deltaTime)
        {
            // 현재 위치 (X-Z 평면)
            Vector3 currentPos = new Vector3(_enemy.Position.x, 0f, _enemy.Position.z);
            Vector3 targetPos = new Vector3(_currentTarget.x, 0f, _currentTarget.z);

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
            _movement.Stop();
        }

        public bool CanTransitionTo(EnemyAIState targetState)
        {
            // Patrol → Chase, Search 가능
            return targetState == EnemyAIState.Chase || targetState == EnemyAIState.Search;
        }

        /// <summary>
        /// 새로운 순찰 목표 지점 선택
        /// </summary>
        private void PickNewTarget()
        {
            Vector2 randomDirection = Random.insideUnitCircle;
            float randomDistance = Random.Range(_minDistance, _patrolRadius);
            Vector2 offset = randomDirection * randomDistance;

            _currentTarget = new Vector3(
                _patrolCenter.x + offset.x,
                _enemy.Position.y, // Y축 고정
                _patrolCenter.z + offset.y);
        }

        /// <summary>
        /// 순찰 중심점 재설정 (보스가 이동할 때)
        /// </summary>
        public void ResetPatrolCenter(Vector3 newCenter)
        {
            _patrolCenter = newCenter;
        }
    }
}
