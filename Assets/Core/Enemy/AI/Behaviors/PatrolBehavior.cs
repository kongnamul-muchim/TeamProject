using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.AI.Behaviors
{
    /// <summary>
    /// 순찰 행동
    /// X축만 이동 (Z축 고정), 맵 전체 순찰
    /// 방향 전환 쿨타임 없음
    /// </summary>
    public sealed class PatrolBehavior : IEnemyAIState
    {
        private readonly IEnemy _enemy;
        private readonly IEnemyMovement _movement;

        // 순찰 설정
        private readonly float _minDistance; // 목표 지점 도달 판정 거리

        // 상태
        private Vector3 _currentTarget;
        private bool _isInitialized;

        /// <summary>
        /// 생성자
        /// </summary>
        public PatrolBehavior(
            IEnemy enemy,
            IEnemyMovement movement,
            float minDistance = 0.5f)
        {
            _enemy = enemy;
            _movement = movement;
            _minDistance = minDistance;
        }

        public EnemyAIState StateType => EnemyAIState.Patrol;

        public void OnEnter()
        {
            _isInitialized = true;
            PickNewTarget();
        }

        public void OnUpdate(float deltaTime)
        {
            // 현재 위치 (X축만, Z축 고정)
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
            return targetState == EnemyAIState.Chase || targetState == EnemyAIState.Search;
        }

        /// <summary>
        /// 새로운 순찰 목표 지점 선택
        /// X축만 이동, Z축은 현재 위치 유지
        /// 맵 전체 순찰 (제한 없음)
        /// </summary>
        private void PickNewTarget()
        {
            // X축: 랜덤 방향 (-1 또는 1)
            float xDir = Random.value > 0.5f ? 1f : -1f;
            // X축 이동 거리: 5~15 (맵 전체 순찰)
            float xDistance = Random.Range(5f, 15f);

            Vector3 currentPos = _enemy.Position;

            _currentTarget = new Vector3(
                currentPos.x + xDir * xDistance,
                currentPos.y, // Y축 고정
                currentPos.z  // Z축 고정 (Patrol에서는 Z 이동 없음)
            );
        }
    }
}
