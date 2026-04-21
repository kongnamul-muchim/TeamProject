using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.AI.Behaviors
{
    /// <summary>
    /// 탐색 행동
    /// 마지막 Player 위치 기반으로 주변을 수색
    /// </summary>
    public sealed class SearchBehavior : IEnemyAIState
    {
        private readonly IEnemy _enemy;
        private readonly IEnemyMovement _movement;

        // 탐색 설정
        private readonly float _searchRadius;       // 탐색 반경
        private readonly float _searchDuration;     // 탐색 최대 지속 시간
        private readonly int _searchPoints;         // 탐색 포인트 수

        // 상태
        private Vector3 _lastKnownPosition;
        private Vector3[] _searchPointsArray;
        private int _currentPointIndex;
        private float _searchTimer;
        private bool _isInitialized;

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
            float searchRadius = 3f,
            float searchDuration = 5f,
            int searchPoints = 4)
        {
            _enemy = enemy;
            _movement = movement;
            _searchRadius = searchRadius;
            _searchDuration = searchDuration;
            _searchPoints = searchPoints;
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
            _currentPointIndex = 0;
            GenerateSearchPoints();
        }

        public void OnUpdate(float deltaTime)
        {
            _searchTimer -= deltaTime;

            // 탐색 시간 초과 체크
            if (_searchTimer <= 0f)
            {
                _movement.Stop();
                return;
            }

            // 현재 탐색 포인트로 이동
            if (_searchPointsArray == null || _searchPointsArray.Length == 0)
                return;

            Vector3 targetPoint = _searchPointsArray[_currentPointIndex];
            Vector2 targetPos = new Vector2(targetPoint.x, targetPoint.y);

            float distanceToTarget = Vector2.Distance(
                new Vector2(_enemy.Position.x, _enemy.Position.y),
                targetPos);

            if (distanceToTarget < 0.3f)
            {
                // 다음 포인트로
                _currentPointIndex++;
                if (_currentPointIndex >= _searchPointsArray.Length)
                {
                    _currentPointIndex = 0; // 반복
                }
            }
            else
            {
                _movement.MoveTo(targetPos);
            }
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
        /// 탐색 포인트 생성 (원형 패턴)
        /// </summary>
        private void GenerateSearchPoints()
        {
            _searchPointsArray = new Vector3[_searchPoints];
            float angleStep = 360f / _searchPoints;

            for (int i = 0; i < _searchPoints; i++)
            {
                float angle = angleStep * i * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle) * _searchRadius;
                float y = Mathf.Sin(angle) * _searchRadius;

                _searchPointsArray[i] = new Vector3(
                    _lastKnownPosition.x + x,
                    _lastKnownPosition.y + y,
                    _lastKnownPosition.z);
            }
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
