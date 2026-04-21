using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.AI.Behaviors
{
    /// <summary>
    /// 탐색 행동
    /// 마지막 Player 위치 기반으로 주변을 계속 수색 (멈추지 않음)
    /// X-Z 평면 이동
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
        private float _minDistance; // 최소 이동 거리

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
            int searchPoints = 4,
            float minDistance = 0.5f)
        {
            _enemy = enemy;
            _movement = movement;
            _searchRadius = searchRadius;
            _searchDuration = searchDuration;
            _searchPoints = searchPoints;
            _minDistance = minDistance;
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
            MoveToNextPoint();
        }

        public void OnUpdate(float deltaTime)
        {
            _searchTimer -= deltaTime;

            // 탐색 시간 초과 체크
            if (_searchTimer <= 0f)
            {
                return; // 상태 머신에서 Patrol로 전환 처리
            }

            // 현재 탐색 포인트로 계속 이동
            if (_searchPointsArray == null || _searchPointsArray.Length == 0)
                return;

            Vector3 currentPos = new Vector3(_enemy.Position.x, 0f, _enemy.Position.z);
            Vector3 targetPoint = _searchPointsArray[_currentPointIndex];
            Vector3 targetPos = new Vector3(targetPoint.x, 0f, targetPoint.z);

            float distanceToTarget = Vector3.Distance(currentPos, targetPos);

            if (distanceToTarget < _minDistance)
            {
                // 도달 → 다음 포인트로 즉시 이동 (멈추지 않음)
                _currentPointIndex++;
                if (_currentPointIndex >= _searchPointsArray.Length)
                {
                    _currentPointIndex = 0; // 반복
                    // 포인트 재생성 (새로운 패턴)
                    GenerateSearchPoints();
                }
                MoveToNextPoint();
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
                float z = Mathf.Sin(angle) * _searchRadius;

                _searchPointsArray[i] = new Vector3(
                    _lastKnownPosition.x + x,
                    _lastKnownPosition.y, // Y축 고정
                    _lastKnownPosition.z + z);
            }
        }

        /// <summary>
        /// 다음 포인트로 이동
        /// </summary>
        private void MoveToNextPoint()
        {
            if (_searchPointsArray == null || _searchPointsArray.Length == 0) return;
            _movement.MoveTo(_searchPointsArray[_currentPointIndex]);
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
