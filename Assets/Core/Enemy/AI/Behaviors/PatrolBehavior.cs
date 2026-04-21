using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.AI.Behaviors
{
    /// <summary>
    /// 순찰 행동
    /// 랜덤한 위치를 이동하며 순찰
    /// </summary>
    public sealed class PatrolBehavior : IEnemyAIState
    {
        private readonly IEnemy _enemy;
        private readonly IEnemyMovement _movement;

        // 순찰 설정
        private readonly float _patrolRadius;
        private readonly float _waitTimeMin;
        private readonly float _waitTimeMax;

        // 상태
        private Vector3 _patrolCenter;
        private Vector3 _currentTarget;
        private float _waitTimer;
        private bool _isWaiting;
        private bool _isInitialized;

        /// <summary>
        /// 생성자
        /// </summary>
        public PatrolBehavior(
            IEnemy enemy,
            IEnemyMovement movement,
            float patrolRadius = 5f,
            float waitTimeMin = 1f,
            float waitTimeMax = 3f)
        {
            _enemy = enemy;
            _movement = movement;
            _patrolRadius = patrolRadius;
            _waitTimeMin = waitTimeMin;
            _waitTimeMax = waitTimeMax;
        }

        public EnemyAIState StateType => EnemyAIState.Patrol;

        public void OnEnter()
        {
            if (!_isInitialized)
            {
                _patrolCenter = _enemy.Position;
                _isInitialized = true;
            }

            _isWaiting = false;
            PickNewTarget();
        }

        public void OnUpdate(float deltaTime)
        {
            if (_isWaiting)
            {
                _waitTimer -= deltaTime;
                if (_waitTimer <= 0f)
                {
                    _isWaiting = false;
                    PickNewTarget();
                }
                return;
            }

            // 목표 지점에 도달했는지 확인
            float distanceToTarget = Vector2.Distance(
                new Vector2(_enemy.Position.x, _enemy.Position.y),
                new Vector2(_currentTarget.x, _currentTarget.y));

            if (distanceToTarget < 0.3f)
            {
                // 도달 → 대기
                _movement.Stop();
                _isWaiting = true;
                _waitTimer = Random.Range(_waitTimeMin, _waitTimeMax);
            }
            else
            {
                // 이동
                _movement.MoveTo(new Vector2(_currentTarget.x, _currentTarget.y));
            }
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
            float randomDistance = Random.Range(1f, _patrolRadius);
            Vector2 offset = randomDirection * randomDistance;

            _currentTarget = new Vector3(
                _patrolCenter.x + offset.x,
                _patrolCenter.y + offset.y,
                _enemy.Position.z);
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
