using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;
using HideAndInk.Core.Enemy.Movement;

namespace HideAndInk.Core.Enemy.Normal
{
    /// <summary>
    /// 일반 몬스터 컨트롤러
    /// 단순 이동 + Player 접촉 시 보스에게 위치 알림
    /// X-Z 평면 이동
    /// </summary>
    public class NormalEnemyController : EnemyAIController
    {
        public override EnemyType Type => EnemyType.Normal;

        [Header("일반 몬스터 설정")]
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private float moveInterval = 2f;
        [SerializeField] private float moveDistance = 2f;
        [SerializeField] private float idleTime = 1f;

        // 상태
        private float _stateTimer;
        private Vector3 _targetPosition;
        private bool _isMoving;
        private bool _isAlerted;

        protected override void InitializeMovement()
        {
            _movement = new EnemyMovement(
                enemy: this,
                speed: moveSpeed,
                acceleration: 5f,
                friction: 0.8f,
                maxSpeed: moveSpeed,
                groundLayer: groundLayer,
                groundCheckDistance: groundCheckDistance,
                groundCheckRadius: groundCheckRadius);
        }

        protected override void UpdateAI(float deltaTime)
        {
            _stateTimer -= deltaTime;

            if (_stateTimer <= 0f)
            {
                if (_isMoving)
                {
                    // 이동 완료 → 대기
                    _movement.Stop();
                    _isMoving = false;
                    _stateTimer = idleTime;
                }
                else
                {
                    // 대기 완료 → 이동
                    PickNewTarget();
                    _isMoving = true;
                    _stateTimer = moveInterval;
                }
            }

            if (_isMoving)
            {
                _movement.MoveTo(_targetPosition);
            }
        }

        /// <summary>
        /// 새로운 이동 목표 지점 선택 (X-Z 평면)
        /// Ground 범위 내에서만 목표 설정
        /// </summary>
        private void PickNewTarget()
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            Vector3 currentPos = transform.position;
            Vector3 target = new Vector3(
                currentPos.x + randomDirection.x * moveDistance,
                currentPos.y, // Y축 고정
                currentPos.z + randomDirection.y * moveDistance);

            // Ground 범위 내로 제한
            _targetPosition = ClampToGroundBounds(target);
        }

        /// <summary>
        /// Player 접촉 감지 (3D)
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (_isAlerted) return;

            // Player Layer 체크
            if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
            {
                AlertBoss(other.transform.position);
            }
        }

        /// <summary>
        /// 보스에게 Player 위치 알림
        /// </summary>
        private void AlertBoss(Vector3 playerPosition)
        {
            _isAlerted = true;

            // 보스 찾기
            var boss = FindObjectOfType<Boss.BossEnemyController>();
            if (boss != null)
            {
                // 보스의 AI 상태를 Chase로 전환하고 위치 전달
                boss.AlertPlayerPosition(playerPosition);
            }

            // 알림 후 일정 시간 후 재활성화
            Invoke(nameof(ResetAlert), 3f);
        }

        /// <summary>
        /// 알림 상태 초기화
        /// </summary>
        private void ResetAlert()
        {
            _isAlerted = false;
        }
    }
}
