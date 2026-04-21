using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.AI.Behaviors
{
    /// <summary>
    /// 탐색 행동
    /// 마지막 Player 위치 기반으로 주변을 계속 수색 (멈추지 않음)
    /// X축만 이동 (Z축 고정), Chase 상태에서만 Z축 이동 허용
    /// </summary>
    public sealed class SearchBehavior : IEnemyAIState
    {
        private readonly IEnemy _enemy;
        private readonly IEnemyMovement _movement;

        // 탐색 설정
        private readonly float _searchDuration;     // 탐색 최대 지속 시간
        private readonly float _searchDistance;     // 탐색 이동 거리

        // 상태
        private Vector3 _lastKnownPosition;
        private Vector3 _currentTarget;
        private Vector3 _currentDirection; // 현재 이동 방향
        private float _searchTimer;
        private float _directionTimer;

        /// <summary>
        /// 탐색 타이머 (0이 되면 Patrol로 복귀)
        /// </summary>
        public float SearchTimer => _searchTimer;

        /// <summary>
        /// 생성자
        /// </summary>
        public SearchBehavior(
            IEnemy enemy,
            IEnemyMovement movement,
            float searchDuration = 5f,
            float searchDistance = 3f,
            float directionChangeCooldown = 1f)
        {
            _enemy = enemy;
            _movement = movement;
            _searchDuration = searchDuration;
            _searchDistance = searchDistance;
            _directionTimer = directionChangeCooldown;
        }

        public EnemyAIState StateType => EnemyAIState.Search;

        /// <summary>
        /// 마지막 위치 설정 (Chase에서 넘어올 때 호출)
        /// </summary>
        public void SetLastKnownPosition(Vector3 position)
        {
            _lastKnownPosition = position;
        }

        public void OnEnter()
        {
            _searchTimer = _searchDuration;
            _directionTimer = 0f; // 진입 시 즉시 이동
            PickNewTarget();
        }

        public void OnUpdate(float deltaTime)
        {
            _searchTimer -= deltaTime;
            _directionTimer -= deltaTime;

            // 탐색 시간 초과 체크
            if (_searchTimer <= 0f)
            {
                return; // 상태 머신에서 Patrol로 전환 처리
            }

            // 현재 위치 (X축만, Z축 고정)
            Vector3 currentPos = new Vector3(_enemy.Position.x, 0f, _enemy.Position.z);
            Vector3 targetPos = new Vector3(_currentTarget.x, 0f, _currentTarget.z);

            float distanceToTarget = Vector3.Distance(currentPos, targetPos);

            if (distanceToTarget < 0.5f)
            {
                // 도달
                if (_directionTimer <= 0f)
                {
                    // 쿨타임 종료 → 새 방향
                    PickNewTarget();
                }
                else
                {
                    // 쿨타임 중 → 현재 방향 유지
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
            // Search → Patrol, Chase 가능
            return targetState == EnemyAIState.Patrol || targetState == EnemyAIState.Chase;
        }

        /// <summary>
        /// 새로운 탐색 목표 지점 선택 (X축만)
        /// </summary>
        private void PickNewTarget()
        {
            // X축: 랜덤 방향
            float xDir = Random.value > 0.5f ? 1f : -1f;
            float xDistance = Random.Range(1f, _searchDistance);

            Vector3 currentPos = _enemy.Position;
            _currentDirection = new Vector3(xDir, 0f, 0f);

            _currentTarget = new Vector3(
                currentPos.x + xDir * xDistance,
                currentPos.y, // Y축 고정
                _lastKnownPosition.z // Z축은 마지막 Player 위치로 고정
            );

            _directionTimer = 1f; // 방향 전환 쿨타임
        }

        /// <summary>
        /// 목표 지점을 현재 방향으로 연장
        /// </summary>
        private void ExtendTarget()
        {
            _currentTarget = _enemy.Position + _currentDirection * _searchDistance;
        }

        /// <summary>
        /// 탐색 시간이 초과되었는지 확인
        /// </summary>
        public bool IsSearchTimeout()
        {
            return _searchTimer <= 0f;
        }
    }
}
