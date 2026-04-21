using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.AI.Behaviors
{
    /// <summary>
    /// 추적 행동
    /// Player를 부드럽게 추적 (예측 이동)
    /// Chase 상태에서만 X-Z 평면 이동 (Z축 이동 허용)
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

            // Player 속도 계산 (X-Z 평면)
            Vector3 playerDelta = currentPlayerPos - _lastKnownPlayerPosition;
            playerDelta.y = 0f; // Y축 무시
            Vector3 playerVelocity = playerDelta / deltaTime;
            _lastPlayerVelocity = Vector3.Lerp(_lastPlayerVelocity, playerVelocity, 0.1f);

            // 예측 위치 계산 (X-Z 평면, Chase에서는 Z축 이동 허용)
            Vector3 predictedPosition = currentPlayerPos + (_lastPlayerVelocity * _predictionTime);
            predictedPosition.y = _enemy.Position.y; // Y축 고정

            // 이동
            _movement.MoveTo(predictedPosition);

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
        /// Player가 추적 범위를 벗어났는지 확인
        /// </summary>
        public bool IsPlayerOutOfRange()
        {
            if (_playerTransform == null) return true;

            Vector3 enemyPos = new Vector3(_enemy.Position.x, 0f, _enemy.Position.z);
            Vector3 playerPos = new Vector3(_playerTransform.position.x, 0f, _playerTransform.position.z);
            float distance = Vector3.Distance(enemyPos, playerPos);
            return distance > _loseDistance;
        }
    }
}
