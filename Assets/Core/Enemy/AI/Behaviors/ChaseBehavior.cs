using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.AI.Behaviors
{
    /// <summary>
    /// 추적 행동
    /// Player를 부드럽게 추적 (예측 이동)
    /// </summary>
    public sealed class ChaseBehavior : IEnemyAIState
    {
        private readonly IEnemy _enemy;
        private readonly IEnemyMovement _movement;
        private readonly Transform _playerTransform;

        // 추적 설정
        private readonly float _predictionTime;     // 예측 시간 (초)
        private readonly float _loseDistance;       // 추적 실패 거리

        // 상태
        private Vector3 _lastKnownPlayerPosition;
        private Vector3 _lastPlayerVelocity;

        /// <summary>
        /// 마지막으로 Player를 본 위치
        /// </summary>
        public Vector3 LastKnownPosition => _lastKnownPlayerPosition;

        /// <summary>
        /// 생성자
        /// </summary>
        public ChaseBehavior(
            IEnemy enemy,
            IEnemyMovement movement,
            Transform playerTransform,
            float predictionTime = 0.5f,
            float loseDistance = 8f)
        {
            _enemy = enemy;
            _movement = movement;
            _playerTransform = playerTransform;
            _predictionTime = predictionTime;
            _loseDistance = loseDistance;
        }

        public EnemyAIState StateType => EnemyAIState.Chase;

        public void OnEnter()
        {
            if (_playerTransform != null)
            {
                _lastKnownPlayerPosition = _playerTransform.position;
            }
        }

        public void OnUpdate(float deltaTime)
        {
            if (_playerTransform == null) return;

            // Player 현재 위치
            Vector3 currentPlayerPos = _playerTransform.position;

            // Player 속도 계산 (간단한 차분)
            Vector3 playerVelocity = (currentPlayerPos - _lastKnownPlayerPosition) / deltaTime;
            _lastPlayerVelocity = Vector3.Lerp(_lastPlayerVelocity, playerVelocity, 0.1f);

            // 예측 위치 계산
            Vector3 predictedPosition = currentPlayerPos + (_lastPlayerVelocity * _predictionTime);

            // 2D 평면으로 투영
            Vector2 targetPos = new Vector2(predictedPosition.x, predictedPosition.y);

            // 이동
            _movement.MoveTo(targetPos);

            // 시야 방향 업데이트 (Player 방향으로 부드럽게 회전)
            UpdateViewDirection(targetPos);

            // 마지막 위치 갱신
            _lastKnownPlayerPosition = currentPlayerPos;
        }

        public void OnExit()
        {
            _movement.Stop();
        }

        public bool CanTransitionTo(EnemyAIState targetState)
        {
            // Chase → Search, Patrol 가능
            return targetState == EnemyAIState.Search || targetState == EnemyAIState.Patrol;
        }

        /// <summary>
        /// 시야 방향을 목표 지점으로 부드럽게 회전
        /// </summary>
        private void UpdateViewDirection(Vector2 targetPos)
        {
            Vector2 enemyPos = new Vector2(_enemy.Position.x, _enemy.Position.y);
            Vector2 direction = (targetPos - enemyPos).normalized;

            if (direction.sqrMagnitude > 0.01f)
            {
                // 왼쪽/오른쪽 판단
                bool shouldFaceRight = direction.x > 0;
                // Y축 회전으로 처리 (기본 왼쪽 = 0도, 오른쪽 = 180도)
                // 실제 회전은 EnemyAIController에서 처리
            }
        }

        /// <summary>
        /// Player가 추적 범위를 벗어났는지 확인
        /// </summary>
        public bool IsPlayerOutOfRange()
        {
            if (_playerTransform == null) return true;

            float distance = Vector3.Distance(_enemy.Position, _playerTransform.position);
            return distance > _loseDistance;
        }
    }
}
